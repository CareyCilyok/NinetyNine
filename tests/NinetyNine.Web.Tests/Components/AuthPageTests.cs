using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NinetyNine.Model;
using NinetyNine.Repository.Repositories;
using NinetyNine.Services.Auth;
using NinetyNine.Web.Components.Pages;

namespace NinetyNine.Web.Tests.Components;

/// <summary>
/// bUnit tests for the auth page components:
/// Register, Login, VerifyEmail, ForgotPassword, ResendVerification, ResetPassword.
/// Focuses on rendering, initial state, and branch conditions.
/// Form-submit flows that require HttpContext cookie sign-in (Login) are intentionally
/// out of scope — those require an HTTP pipeline and are covered by integration tests.
/// [SupplyParameterFromQuery] parameters are supplied via NavigationManager navigation
/// (bUnit requirement — they cannot be passed through the parameter builder).
/// </summary>
public class AuthPageTests : TestContext
{
    // ─── Stub services ────────────────────────────────────────────────────────

    private sealed class StubAuthService : IAuthService
    {
        public int VerifyCallCount { get; private set; }
        public string? LastVerifyToken { get; private set; }

        public Queue<AuthResult<Player>> RegisterResponses { get; } = new();
        public Queue<AuthResult<Player>> LoginResponses { get; } = new();
        public Queue<AuthResult> VerifyResponses { get; } = new();
        public Queue<AuthResult> ForgotResponses { get; } = new();
        public Queue<AuthResult> ResetResponses { get; } = new();
        public Queue<AuthResult> ResendResponses { get; } = new();

        public Task<AuthResult<Player>> RegisterAsync(
            string email, string displayName, string password, string confirmPassword,
            string verifyUrlBase, CancellationToken ct = default)
            => Task.FromResult(RegisterResponses.TryDequeue(out var r)
                ? r : AuthResult<Player>.Fail("no response queued"));

        public Task<AuthResult<Player>> LoginAsync(
            string email, string password, CancellationToken ct = default)
            => Task.FromResult(LoginResponses.TryDequeue(out var r)
                ? r : AuthResult<Player>.Fail("no response queued"));

        public Task<AuthResult> VerifyEmailAsync(string token, CancellationToken ct = default)
        {
            VerifyCallCount++;
            LastVerifyToken = token;
            return Task.FromResult(VerifyResponses.TryDequeue(out var r) ? r : AuthResult.Ok());
        }

        public Task<AuthResult> ForgotPasswordAsync(
            string email, string resetUrlBase, CancellationToken ct = default)
            => Task.FromResult(ForgotResponses.TryDequeue(out var r) ? r : AuthResult.Ok());

        public Task<AuthResult> ResetPasswordAsync(
            string token, string newPassword, string confirmPassword, CancellationToken ct = default)
            => Task.FromResult(ResetResponses.TryDequeue(out var r) ? r : AuthResult.Ok());

        public Task<AuthResult> ResendVerificationAsync(
            string email, string verifyUrlBase, CancellationToken ct = default)
            => Task.FromResult(ResendResponses.TryDequeue(out var r) ? r : AuthResult.Ok());
    }

    private sealed class StubPlayerRepository : IPlayerRepository
    {
        public Task<Player?> GetByEmailAsync(string email, CancellationToken ct = default)
            => Task.FromResult<Player?>(null);
        public Task<Player?> GetByEmailVerificationTokenAsync(string token, CancellationToken ct = default)
            => Task.FromResult<Player?>(null);
        public Task<Player?> GetByPasswordResetTokenAsync(string token, CancellationToken ct = default)
            => Task.FromResult<Player?>(null);
        public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
            => Task.FromResult(false);
        public Task<Player?> GetByIdAsync(Guid playerId, CancellationToken ct = default)
            => Task.FromResult<Player?>(null);
        public Task<Player?> GetByDisplayNameAsync(string displayName, CancellationToken ct = default)
            => Task.FromResult<Player?>(null);
        public Task<bool> DisplayNameExistsAsync(string displayName, CancellationToken ct = default)
            => Task.FromResult(false);
        public Task<IReadOnlyList<Player>> SearchAsync(string query, int limit, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Player>>(Array.Empty<Player>());
        public Task CreateAsync(Player player, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Player player, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid playerId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "NinetyNine.Web";
        public string WebRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private sealed class StubHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    // ─── Helper: register minimum services for auth pages ─────────────────────

    private StubAuthService SetupAuthServices(
        bool mockEnabled = false,
        string environment = "Production")
    {
        var stub = new StubAuthService();
        Services.AddSingleton<IAuthService>(stub);
        Services.AddSingleton<IPlayerRepository>(new StubPlayerRepository());
        Services.AddSingleton<IHttpContextAccessor>(new StubHttpContextAccessor());

        var env = new StubWebHostEnvironment { EnvironmentName = environment };
        Services.AddSingleton<IWebHostEnvironment>(env);

        var configValues = new Dictionary<string, string?>
        {
            ["Auth:Mock:Enabled"] = mockEnabled ? "true" : "false"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        Services.AddSingleton<IConfiguration>(config);

        Services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>),
            typeof(NullLogger<>));

        return stub;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Register.razor
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Register_RendersEmailAndPasswordFields()
    {
        SetupAuthServices();

        var cut = RenderComponent<Register>();

        cut.Find("#email").Should().NotBeNull("email field must be present");
        cut.Find("#display-name").Should().NotBeNull("display-name field must be present");
        cut.Find("#password").Should().NotBeNull("password field must be present");
        cut.Find("#confirm-password").Should().NotBeNull("confirm-password field must be present");
    }

    [Fact]
    public void Register_SubmitButtonIsPresent()
    {
        SetupAuthServices();

        var cut = RenderComponent<Register>();

        var submit = cut.Find("button[type='submit']");
        submit.Should().NotBeNull();
        submit.TextContent.Trim().Should().Contain("Create account");
    }

    [Fact]
    public void Register_AlreadyHaveAnAccountLink_PointsToLogin()
    {
        SetupAuthServices();

        var cut = RenderComponent<Register>();

        var link = cut.FindAll("a").FirstOrDefault(a => a.GetAttribute("href") == "/login");
        link.Should().NotBeNull("'Already have an account?' link pointing to /login must be rendered");
    }

    [Fact]
    public void Register_NoErrorOrSuccessMessageOnInitialRender()
    {
        SetupAuthServices();

        var cut = RenderComponent<Register>();

        cut.FindAll(".alert-danger").Should().BeEmpty("no errors on initial render");
        cut.FindAll(".alert-success").Should().BeEmpty("no success on initial render");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Login.razor
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Login_RendersEmailAndPasswordFields()
    {
        SetupAuthServices();

        var cut = RenderComponent<Login>();

        cut.Find("#email").Should().NotBeNull("email field must be present");
        cut.Find("#password").Should().NotBeNull("password field must be present");
    }

    [Fact]
    public void Login_ForgotPasswordAndResendVerificationLinksPresent()
    {
        SetupAuthServices();

        var cut = RenderComponent<Login>();

        var anchors = cut.FindAll("a").Select(a => a.GetAttribute("href")).ToList();
        anchors.Should().Contain("/forgot-password", "forgot-password link must be present");
        anchors.Should().Contain("/resend-verification", "resend-verification link must be present");
    }

    [Fact]
    public void Login_MockPickerSection_IsHidden_WhenMockDisabled()
    {
        SetupAuthServices(mockEnabled: false, environment: "Production");

        var cut = RenderComponent<Login>();

        cut.FindAll(".nn-auth-mock").Should().BeEmpty(
            "mock auth section must not appear when IsMockEnabled is false");
    }

    [Fact]
    public void Login_MockPickerSection_IsRendered_WhenMockEnabled()
    {
        SetupAuthServices(mockEnabled: true, environment: "Development");

        var cut = RenderComponent<Login>();

        cut.Find(".nn-auth-mock").Should().NotBeNull(
            "mock auth section must appear when IsMockEnabled is true");
    }

    [Fact]
    public void Login_NoErrorMessageOnInitialRender()
    {
        SetupAuthServices();

        var cut = RenderComponent<Login>();

        cut.FindAll(".alert-danger").Should().BeEmpty("no error alert on initial render");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // VerifyEmail.razor
    // [SupplyParameterFromQuery] Token is supplied via NavigationManager.NavigateTo
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void VerifyEmail_MissingToken_RendersErrorMessage()
    {
        var stub = new StubAuthService();
        Services.AddSingleton<IAuthService>(stub);
        Services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>),
            typeof(NullLogger<>));

        // Navigate to /verify-email with no token query param
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("http://localhost/verify-email");

        var cut = RenderComponent<VerifyEmail>();

        cut.Find(".alert-danger").Should().NotBeNull("missing token must render an error alert");
        cut.FindAll("a").Any(a =>
            a.GetAttribute("href")?.Contains("resend-verification") == true)
            .Should().BeTrue("a 'request new link' anchor must be present");
    }

    [Fact]
    public void VerifyEmail_WithToken_CallsVerifyEmailAsync()
    {
        var stub = new StubAuthService();
        stub.VerifyResponses.Enqueue(AuthResult.Ok());
        Services.AddSingleton<IAuthService>(stub);
        Services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>),
            typeof(NullLogger<>));

        // Supply the token via query string navigation (required for SupplyParameterFromQuery)
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("http://localhost/verify-email?Token=abc123verifytoken");

        var cut = RenderComponent<VerifyEmail>();

        stub.VerifyCallCount.Should().Be(1, "VerifyEmailAsync must be called once");
        stub.LastVerifyToken.Should().Be("abc123verifytoken");
    }

    [Fact]
    public void VerifyEmail_OnSuccess_RendersSuccessAlert()
    {
        var stub = new StubAuthService();
        stub.VerifyResponses.Enqueue(AuthResult.Ok());
        Services.AddSingleton<IAuthService>(stub);
        Services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>),
            typeof(NullLogger<>));

        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("http://localhost/verify-email?Token=valid-token-xyz");

        var cut = RenderComponent<VerifyEmail>();

        cut.Find(".alert-success").Should().NotBeNull("success alert must appear after successful verification");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ForgotPassword.razor
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ForgotPassword_RendersEmailFieldAndSubmitButton()
    {
        var stub = new StubAuthService();
        Services.AddSingleton<IAuthService>(stub);
        Services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>),
            typeof(NullLogger<>));

        var cut = RenderComponent<ForgotPassword>();

        cut.Find("#email").Should().NotBeNull("email field must be present");
        cut.Find("button[type='submit']").Should().NotBeNull("submit button must be present");
    }

    [Fact]
    public void ForgotPassword_FormIsVisibleOnInitialRender()
    {
        var stub = new StubAuthService();
        Services.AddSingleton<IAuthService>(stub);
        Services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>),
            typeof(NullLogger<>));

        var cut = RenderComponent<ForgotPassword>();

        // Before submit, the EditForm should be present
        cut.FindAll("form").Should().NotBeEmpty("form must be visible before submission");
        cut.FindAll(".alert-success").Should().BeEmpty("no success message before submission");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ResendVerification.razor
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ResendVerification_RendersEmailFieldOnInitialRender()
    {
        var stub = new StubAuthService();
        Services.AddSingleton<IAuthService>(stub);
        Services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>),
            typeof(NullLogger<>));

        var cut = RenderComponent<ResendVerification>();

        cut.Find("#email").Should().NotBeNull("email field must be present");
        cut.FindAll(".alert-success").Should().BeEmpty("no success message before submission");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ResetPassword.razor
    // [SupplyParameterFromQuery] Token is supplied via NavigationManager.NavigateTo
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ResetPassword_MissingToken_RendersErrorWithForgotPasswordLink()
    {
        var stub = new StubAuthService();
        Services.AddSingleton<IAuthService>(stub);
        Services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>),
            typeof(NullLogger<>));

        // Navigate without a token query param
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("http://localhost/reset-password");

        var cut = RenderComponent<ResetPassword>();

        cut.Find(".alert-danger").Should().NotBeNull("error alert must be shown when token is missing");

        var forgotLink = cut.FindAll("a")
            .FirstOrDefault(a => a.GetAttribute("href")?.Contains("forgot-password") == true);
        forgotLink.Should().NotBeNull("link to /forgot-password must be shown when token is missing");
    }

    [Fact]
    public void ResetPassword_WithToken_RendersPasswordFieldsAndSubmitButton()
    {
        var stub = new StubAuthService();
        Services.AddSingleton<IAuthService>(stub);
        Services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>),
            typeof(NullLogger<>));

        // Supply the reset token via query string navigation
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("http://localhost/reset-password?Token=valid-reset-token-abc");

        var cut = RenderComponent<ResetPassword>();

        cut.Find("#new-password").Should().NotBeNull("new-password field must be present");
        cut.Find("#confirm-password").Should().NotBeNull("confirm-password field must be present");
        cut.Find("button[type='submit']").Should().NotBeNull("submit button must be present");
    }
}
