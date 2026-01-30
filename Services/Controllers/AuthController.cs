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
    private readonly Dictionary<string, (string Token, Guid UserId, DateTime Expiry)> _refreshTokens = new();

    public AuthController(IJwtService jwtService, NinetyNineContext context)
    {
        _jwtService = jwtService;
        _context = context;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Email and password are required");
        }

        // Validate user against database
        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.EmailAddress == request.Email);

        if (player == null)
        {
            // For first-time users, create an account automatically
            player = new Player
            {
                PlayerId = Guid.NewGuid(),
                EmailAddress = request.Email,
                FirstName = request.Email.Split('@')[0],
                LastName = "Player",
                Username = request.Email.Split('@')[0]
            };

            _context.Players.Add(player);
            await _context.SaveChangesAsync();
        }

        // Validate password (in production, use proper hashing)
        // For now, we'll accept any password with minimum length validation
        if (request.Password.Length < 6)
        {
            return Unauthorized("Invalid credentials");
        }

        var roles = new[] { "User" };

        // Generate tokens
        var accessToken = _jwtService.GenerateToken(player.PlayerId, request.Email, roles);
        var refreshToken = _jwtService.GenerateRefreshToken();

        // Store refresh token for validation
        _refreshTokens[refreshToken] = (refreshToken, player.PlayerId, DateTime.UtcNow.AddDays(7));

        return Ok(new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = 3600,
            TokenType = "Bearer"
        });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest("Refresh token is required");
        }

        // Validate refresh token
        if (!_refreshTokens.TryGetValue(request.RefreshToken, out var tokenData))
        {
            return Unauthorized("Invalid refresh token");
        }

        if (tokenData.Expiry < DateTime.UtcNow)
        {
            _refreshTokens.Remove(request.RefreshToken);
            return Unauthorized("Refresh token expired");
        }

        // Get user from database
        var player = await _context.Players.FindAsync(tokenData.UserId);
        if (player == null)
        {
            return Unauthorized("User not found");
        }

        var roles = new[] { "User" };

        // Generate new tokens
        var accessToken = _jwtService.GenerateToken(player.PlayerId, player.EmailAddress ?? "user@ninetynine.com", roles);
        var newRefreshToken = _jwtService.GenerateRefreshToken();

        // Remove old refresh token and store new one
        _refreshTokens.Remove(request.RefreshToken);
        _refreshTokens[newRefreshToken] = (newRefreshToken, player.PlayerId, DateTime.UtcNow.AddDays(7));

        return Ok(new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresIn = 3600,
            TokenType = "Bearer"
        });
    }

    [HttpPost("validate")]
    [Authorize]
    public ActionResult ValidateToken()
    {
        var userId = _jwtService.GetUserIdFromToken(Request.Headers["Authorization"].ToString().Replace("Bearer ", ""));
        
        return Ok(new { Valid = true, UserId = userId });
    }
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

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public string TokenType { get; set; } = string.Empty;
}