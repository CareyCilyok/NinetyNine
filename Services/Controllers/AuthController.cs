/// Copyright (c) 2020-2022
///
/// Permission is hereby granted, free of charge, to any person obtaining a copy
/// of this software and associated documentation files (the "Software"), to deal
/// in the Software without restriction, including without limitation the rights
/// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
/// copies of the Software, and to permit persons to whom the Software is
/// furnished to do so, subject to the following conditions:
///
/// The above copyright notice and this permission notice shall be included in all
/// copies or substantial portions of the Software.
///
/// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
/// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
/// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
/// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
/// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
/// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
/// SOFTWARE.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NinetyNine.Model;
using Services.Services;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace Services.Controllers;

[ApiController]
[ApiVersion("0.0")]
[Route("api/{v:apiVersion}/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IJwtService _jwtService;
    private readonly NinetyNineContext _context;
    private readonly ILogger<AuthController> _logger;

    // Thread-safe refresh token storage (in production, use Redis or database)
    private static readonly ConcurrentDictionary<string, RefreshTokenData> _refreshTokens = new();

    public AuthController(IJwtService jwtService, NinetyNineContext context, ILogger<AuthController> logger)
    {
        _jwtService = jwtService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Register a new user account
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponse("Validation failed", ModelState));
        }

        _logger.LogInformation("Registration attempt for email: {Email}", request.Email);

        // Check if user already exists
        var existingPlayer = await _context.Players
            .FirstOrDefaultAsync(p => p.EmailAddress == request.Email);

        if (existingPlayer != null)
        {
            _logger.LogWarning("Registration failed - email already exists: {Email}", request.Email);
            return Conflict(new ErrorResponse("An account with this email already exists"));
        }

        // Create password hash with salt
        var salt = GenerateSalt();
        var passwordHash = HashPassword(request.Password, salt);

        var player = new Player
        {
            PlayerId = Guid.NewGuid(),
            EmailAddress = request.Email,
            Username = request.Username ?? request.Email.Split('@')[0],
            FirstName = request.FirstName ?? "",
            LastName = request.LastName ?? "",
            PasswordHash = passwordHash,
            PasswordSalt = salt,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Players.Add(player);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User registered successfully: {PlayerId}", player.PlayerId);

        // Generate tokens for immediate login
        var roles = new[] { "User" };
        var accessToken = _jwtService.GenerateToken(player.PlayerId, request.Email, roles);
        var refreshToken = _jwtService.GenerateRefreshToken();

        StoreRefreshToken(refreshToken, player.PlayerId);

        return CreatedAtAction(nameof(ValidateToken), new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = 3600,
            TokenType = "Bearer",
            UserId = player.PlayerId
        });
    }

    /// <summary>
    /// Login with email and password
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponse("Validation failed", ModelState));
        }

        _logger.LogInformation("Login attempt for email: {Email}", request.Email);

        // Find user by email
        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.EmailAddress == request.Email);

        if (player == null)
        {
            _logger.LogWarning("Login failed - user not found: {Email}", request.Email);
            // Use generic message to prevent email enumeration
            return Unauthorized(new ErrorResponse("Invalid email or password"));
        }

        if (!player.IsActive)
        {
            _logger.LogWarning("Login failed - account inactive: {PlayerId}", player.PlayerId);
            return Unauthorized(new ErrorResponse("Account is inactive"));
        }

        // Verify password
        var passwordHash = HashPassword(request.Password, player.PasswordSalt);
        if (passwordHash != player.PasswordHash)
        {
            _logger.LogWarning("Login failed - invalid password for: {PlayerId}", player.PlayerId);
            return Unauthorized(new ErrorResponse("Invalid email or password"));
        }

        // Update last login time
        player.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var roles = new[] { "User" };
        var accessToken = _jwtService.GenerateToken(player.PlayerId, request.Email, roles);
        var refreshToken = _jwtService.GenerateRefreshToken();

        StoreRefreshToken(refreshToken, player.PlayerId);

        _logger.LogInformation("User logged in successfully: {PlayerId}", player.PlayerId);

        return Ok(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = 3600,
            TokenType = "Bearer",
            UserId = player.PlayerId
        });
    }

    /// <summary>
    /// Refresh an expired access token
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest(new ErrorResponse("Refresh token is required"));
        }

        // Validate refresh token
        if (!_refreshTokens.TryGetValue(request.RefreshToken, out var tokenData))
        {
            _logger.LogWarning("Refresh token not found");
            return Unauthorized(new ErrorResponse("Invalid refresh token"));
        }

        if (tokenData.Expiry < DateTime.UtcNow)
        {
            _refreshTokens.TryRemove(request.RefreshToken, out _);
            _logger.LogWarning("Refresh token expired for user: {UserId}", tokenData.UserId);
            return Unauthorized(new ErrorResponse("Refresh token expired"));
        }

        // Get user from database
        var player = await _context.Players.FindAsync(tokenData.UserId);
        if (player == null || !player.IsActive)
        {
            _refreshTokens.TryRemove(request.RefreshToken, out _);
            return Unauthorized(new ErrorResponse("User not found or inactive"));
        }

        var roles = new[] { "User" };
        var accessToken = _jwtService.GenerateToken(player.PlayerId, player.EmailAddress, roles);
        var newRefreshToken = _jwtService.GenerateRefreshToken();

        // Rotate refresh token (remove old, add new)
        _refreshTokens.TryRemove(request.RefreshToken, out _);
        StoreRefreshToken(newRefreshToken, player.PlayerId);

        _logger.LogInformation("Token refreshed for user: {PlayerId}", player.PlayerId);

        return Ok(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresIn = 3600,
            TokenType = "Bearer",
            UserId = player.PlayerId
        });
    }

    /// <summary>
    /// Logout and invalidate refresh token
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public ActionResult Logout([FromBody] LogoutRequest? request)
    {
        if (!string.IsNullOrEmpty(request?.RefreshToken))
        {
            _refreshTokens.TryRemove(request.RefreshToken, out _);
        }

        var userId = _jwtService.GetUserIdFromToken(
            Request.Headers["Authorization"].ToString().Replace("Bearer ", ""));

        _logger.LogInformation("User logged out: {UserId}", userId);

        return Ok(new { Message = "Logged out successfully" });
    }

    /// <summary>
    /// Validate current access token
    /// </summary>
    [HttpPost("validate")]
    [Authorize]
    public ActionResult ValidateToken()
    {
        var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        var userId = _jwtService.GetUserIdFromToken(token);

        return Ok(new { Valid = true, UserId = userId });
    }

    /// <summary>
    /// Change password for authenticated user
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponse("Validation failed", ModelState));
        }

        var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        var userId = _jwtService.GetUserIdFromToken(token);

        if (userId == null)
        {
            return Unauthorized(new ErrorResponse("Invalid token"));
        }

        var player = await _context.Players.FindAsync(Guid.Parse(userId));
        if (player == null)
        {
            return NotFound(new ErrorResponse("User not found"));
        }

        // Verify current password
        var currentHash = HashPassword(request.CurrentPassword, player.PasswordSalt);
        if (currentHash != player.PasswordHash)
        {
            _logger.LogWarning("Password change failed - wrong current password: {PlayerId}", player.PlayerId);
            return BadRequest(new ErrorResponse("Current password is incorrect"));
        }

        // Set new password
        var newSalt = GenerateSalt();
        player.PasswordHash = HashPassword(request.NewPassword, newSalt);
        player.PasswordSalt = newSalt;

        await _context.SaveChangesAsync();

        // Invalidate all refresh tokens for this user (force re-login)
        var tokensToRemove = _refreshTokens.Where(t => t.Value.UserId == player.PlayerId).Select(t => t.Key).ToList();
        foreach (var tokenKey in tokensToRemove)
        {
            _refreshTokens.TryRemove(tokenKey, out _);
        }

        _logger.LogInformation("Password changed successfully: {PlayerId}", player.PlayerId);

        return Ok(new { Message = "Password changed successfully" });
    }

    #region Private Methods

    private static string GenerateSalt()
    {
        var saltBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(saltBytes);
        return Convert.ToBase64String(saltBytes);
    }

    private static string HashPassword(string password, string salt)
    {
        using var sha256 = SHA256.Create();
        var saltedPassword = password + salt;
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(saltedPassword));
        return Convert.ToBase64String(hashBytes);
    }

    private void StoreRefreshToken(string token, Guid userId)
    {
        var expiry = DateTime.UtcNow.AddDays(7);
        _refreshTokens[token] = new RefreshTokenData(token, userId, expiry);

        // Cleanup expired tokens periodically
        CleanupExpiredTokens();
    }

    private void CleanupExpiredTokens()
    {
        var now = DateTime.UtcNow;
        var expiredTokens = _refreshTokens
            .Where(t => t.Value.Expiry < now)
            .Select(t => t.Key)
            .ToList();

        foreach (var token in expiredTokens)
        {
            _refreshTokens.TryRemove(token, out _);
        }
    }

    #endregion
}

#region Request/Response Models

public class RegisterRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    public string Password { get; set; } = string.Empty;

    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}

public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;
}

public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public class LogoutRequest
{
    public string? RefreshToken { get; set; }
}

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    public string NewPassword { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public string TokenType { get; set; } = string.Empty;
    public Guid UserId { get; set; }
}

// Kept for backwards compatibility
public class LoginResponse : AuthResponse { }

public class ErrorResponse
{
    public string Message { get; set; }
    public object? Details { get; set; }

    public ErrorResponse(string message, object? details = null)
    {
        Message = message;
        Details = details;
    }
}

public record RefreshTokenData(string Token, Guid UserId, DateTime Expiry);

#endregion
