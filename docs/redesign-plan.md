# NinetyNine Redesign Plan — Email/Password Auth + Dark-First UI

**Document version:** 1.0  
**Date:** 2026-04-10  
**Status:** Awaiting Wave 1 execution  
**Orchestrator:** main Claude (spawns specialist agents per wave)

---

## 1. Vision

The NinetyNine Blazor Web App is undergoing a dual transformation: **functional** and **visual**.

**A. Functional Redesign — Email/Password Authentication**
Replace third-party OAuth (Google) with a classic, industry-standard email/password authentication system. This removes external identity provider dependencies, simplifies the user flow, and gives the application full control over the identity store. New users register with an email address, password, and display name; they receive a verification email before the account is active. Existing sessions use cookie-based authentication. Dev mode preserves a mock sign-in bypass for rapid UX prototyping without email loops. The change requires extending the `Player` model with password hashing, email verification state, and account lockout fields; removing the `LinkedIdentity` list; and implementing a secure registration, login, password reset, and email verification flow end-to-end.

**B. Visual Redesign — Dark-First UI**
Redesign the entire Blazor UI around a dark theme as the primary, default color scheme (not a toggle afterthought). Light mode remains available as secondary. Anchor the visual identity in the pool-hall atmosphere: deep neutral backgrounds (charcoal, not pure black), pool-accent greens/teals as the brand color, warm gold accents for active/highlight states. Layout follows a persistent left-side navigation pattern (loosely inspired by the archived Avalonia `NavigationView` but with modern, distinctive styling—not a pixel-for-pixel replica). The current Bootstrap 5 defaults and cream-paper scorecard aesthetic are completely replaced. Scoring grids and all 17+ pages adopt the new dark-themed, accessible, responsive component system using Phosphor icons and the committed billiards photography as decorative elements.

---

## 2. Sequencing Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           REDESIGN DEPENDENCY GRAPH                          │
└─────────────────────────────────────────────────────────────────────────────┘

WAVE 1: Foundation (parallel, no upstream dependencies)
  ├─ WP-01: Player Model Auth Fields        ──────┐
  ├─ WP-02: Email Sender Interface + Impls ──────┤
  └─ WP-03: Base CSS Theme (dark-first)   ──────┤
                                                  │
                ┌─────────────────────────────────┘
                │
WAVE 2: Auth Services & Early Layout (dependent on WP-01, WP-02)
  ├─ WP-04: Auth Repository Updates       ──────┐
  ├─ WP-05: Auth Services (register/login/etc) ─┤
  ├─ WP-06: MainLayout + NavMenu Redesign      ──┤
  └─ WP-07: Auth Page Scaffolds (no logic yet) ─┤
                                                  │
                ┌─────────────────────────────────┘
                │
WAVE 3: Auth Page Implementation (dependent on WP-05, WP-07)
  ├─ WP-08: Register & Login Pages   ────────┐
  ├─ WP-09: Verify Email & Password Reset   ──┤
  ├─ WP-10: Auth Tests (model/repo/services) ─┤
  └─ WP-11: Dev Mock Auth Update ────────────┤
                                             │
                ┌────────────────────────────┘
                │
WAVE 4: Scoring Grid & Game Pages (parallel, some dependent on WP-06)
  ├─ WP-12: ScoreCardGrid Redesign    ────────┐
  ├─ WP-13: Game Pages (New/Play/List/Details) ─┤
  ├─ WP-14: Shared Components Redesign    ────┤
  └─ WP-15: Game Pages Dark Theme Styling  ───┤
                                             │
                ┌────────────────────────────┘
                │
WAVE 5: Profile, Venue, Stats Pages (parallel)
  ├─ WP-16: Player Pages (Profile/EditProfile)  ─┐
  ├─ WP-17: Venue Pages (List/Edit)          ────┤
  ├─ WP-18: Stats Pages (Leaderboard/MyStats) ──┤
  └─ WP-19: Home Page Redesign              ────┤
                                             │
                ┌────────────────────────────┘
                │
WAVE 6: Integration & Polish (parallel, all WPs upstream)
  ├─ WP-20: Web Component Tests (bUnit) ─────┐
  ├─ WP-21: Accessibility Audit (WCAG 2.2 AA) ─┤
  ├─ WP-22: Cross-browser Smoke Test      ────┤
  └─ WP-23: Final Build & Integration Pass  ──┤

```

**Key dependencies:**
- **WP-01 → WP-04, WP-05, WP-08, WP-09, WP-10** — Player model changes block all auth work.
- **WP-02 → WP-05, WP-08** — Email sender required before registration/reset flows work.
- **WP-03 → WP-06, WP-12, WP-14, WP-15, WP-16, WP-17, WP-18, WP-19** — Base theme tokens and component classes must exist before individual pages consume them.
- **WP-04, WP-05 → WP-08, WP-09, WP-10, WP-11** — Auth repository and services are upstream of all auth page implementations.
- **WP-06 → WP-13, WP-16, WP-17, WP-18, WP-19** — MainLayout and NavMenu redesign is used by all pages.
- **WP-07 → WP-08, WP-09, WP-11** — Auth page scaffolds must exist before logic implementation.
- **WP-10 → WP-20** — Auth tests may inform or be required for component tests.

---

## 3. Work Packages

### WP-01: Player Model Auth Fields

**WP-ID:** WP-01-player-model-auth-fields  
**Owner agent:** `backend-developer` (or `voltagent-core-dev:backend-developer`)  
**Depends on:** none  

**Files owned:**
- `src/NinetyNine.Model/Player.cs` (expand with new auth fields, remove LinkedIdentity list)
- `src/NinetyNine.Model/NinetyNine.Model.csproj` (no package changes needed)

**Files read-only:**
- `docs/architecture.md §7.2` (reference specification)

**Deliverables:**
1. Add to `Player` class:
   - `EmailAddress: string` — required, unique, case-insensitive storage
   - `PasswordHash: string` — PBKDF2 hash from `PasswordHasher<Player>`
   - `EmailVerified: bool` — initially false
   - `EmailVerificationToken: string?` — nullable, 32-byte URL-safe base64
   - `EmailVerificationTokenExpiresAt: DateTime?` — nullable
   - `PasswordResetToken: string?` — nullable
   - `PasswordResetTokenExpiresAt: DateTime?` — nullable
   - `LastLoginAt: DateTime?` — nullable
   - `FailedLoginAttempts: int` — default 0
   - `LockedOutUntil: DateTime?` — nullable
2. Remove `LinkedIdentity LinkedIdentities` list (and the `LinkedIdentity` class definition if no other code references it; check via codebase search first).
3. Update XML documentation to explain the new fields and invariants (e.g., "EmailAddress must be unique and is stored lowercased in MongoDB").
4. No behavior methods on `Player` — this is a pure data model.

**Acceptance criteria:**
- `dotnet build NinetyNine.sln` succeeds with no errors or warnings.
- `src/NinetyNine.Model/Player.cs` compiles cleanly.
- All 9 new fields have XML doc comments.
- `LinkedIdentity` class removed and no compilation errors elsewhere (confirm via search for "LinkedIdentity" in codebase).
- Model is serializable via `System.Text.Json` (existing tests will verify).

**Out of scope:**
- Hashing algorithms — that's deferred to `PasswordHasher<T>` in WP-05.
- MongoDB indexes — those are defined in WP-04 (Repository).
- Password validation rules — those are in WP-05 (Services).

**Estimated effort:** S (small, ~1 hour)

**Risks:**
1. **Breaking existing serialization** — if any existing code has hardcoded assumptions about Player fields, the changes break it. **Mitigation:** run the full test suite to catch any serialization mismatches (WP-10 will address this).
2. **LinkedIdentity removal cascades** — if ExternalLoginHandler or other auth code still references `LinkedIdentity`, it won't compile. **Mitigation:** do a codebase grep for "LinkedIdentity" and fix references as part of this WP.

---

### WP-02: Email Sender Interface & Implementations

**WP-ID:** WP-02-email-sender  
**Owner agent:** `backend-developer` (or `backend-api-security:backend-security-coder`)  
**Depends on:** none  

**Files owned:**
- `src/NinetyNine.Web/Auth/EmailSender/IEmailSender.cs` (new file)
- `src/NinetyNine.Web/Auth/EmailSender/MailKitEmailSender.cs` (new file)
- `src/NinetyNine.Web/Auth/EmailSender/ConsoleEmailSender.cs` (new file)
- `src/NinetyNine.Web/Auth/EmailSender/MockEmailSender.cs` (new file)
- `src/NinetyNine.Web/Auth/EmailSettings.cs` (new file, configuration model)

**Files read-only:**
- `docs/architecture.md §7.8` (reference specification)
- `src/NinetyNine.Web/Program.cs` (will read to understand DI patterns; WP-05 or a dedicated agent will register these at DI time)

**Deliverables:**
1. **`IEmailSender` interface** (signature per §7.8):
   ```csharp
   public interface IEmailSender
   {
       Task SendVerificationAsync(string toEmail, string displayName, string verifyUrl, CancellationToken ct);
       Task SendPasswordResetAsync(string toEmail, string displayName, string resetUrl, CancellationToken ct);
   }
   ```
2. **`MailKitEmailSender`** — production implementation using MailKit.Net.Smtp:
   - Read SMTP settings from `EmailSettings` (host, port, username, password, from-address).
   - Implement both methods with professional HTML email templates (include pool-themed footer or logo ref).
   - Use TLS/SSL appropriately.
   - Catch MailKit exceptions and re-throw as application exceptions with user-friendly messages.
   - Include XML doc comments explaining configuration.
3. **`ConsoleEmailSender`** — dev fallback:
   - Log email body, to-address, subject to console (ILogger).
   - Include the full verification/reset URL in the log for easy click-through during development.
   - No SMTP calls.
4. **`MockEmailSender`** — test implementation:
   - Store sent emails in an in-memory list (`public List<(string ToEmail, string Subject, string Body)> SentEmails`).
   - Used in integration tests to assert "verification email was sent with correct token".
5. **`EmailSettings` config class**:
   ```csharp
   public class EmailSettings
   {
       public string Provider { get; set; } = "Console"; // "MailKit", "Console", or "Mock"
       public string SmtpHost { get; set; } = "";
       public int SmtpPort { get; set; } = 587;
       public string SmtpUsername { get; set; } = "";
       public string SmtpPassword { get; set; } = "";
       public string FromAddress { get; set; } = "";
       public string FromDisplayName { get; set; } = "NinetyNine";
   }
   ```
6. Add `MailKit` NuGet package to `NinetyNine.Web.csproj` (no version lock; use latest stable).

**Acceptance criteria:**
- `dotnet build NinetyNine.sln` succeeds.
- All four implementations exist and compile.
- No external web requests (except SMTP in MailKit).
- MailKitEmailSender has comprehensive error handling.
- ConsoleEmailSender logs to ILogger (testable via container logs in local dev).
- MockEmailSender is used by integration tests (WP-10 will verify this).
- XML doc comments on all public members.

**Out of scope:**
- DI registration — that's WP-05.
- Email template design — keep templates simple HTML with pool branding notes inline (future design pass can polish).
- Retry logic — MailKit handles that; this WP does not add exponential backoff.

**Estimated effort:** M (medium, ~2 hours)

**Risks:**
1. **MailKit version compatibility** — if a future version breaks TLS negotiation, SMTP credentials won't work. **Mitigation:** pin MailKit to a known-stable version and test against production SMTP creds in a separate task.
2. **Email template HTML injection** — if displayName or verifyUrl is unsanitized, an attacker could inject HTML/script. **Mitigation:** use `System.Net.WebUtility.HtmlEncode()` on all user-supplied values in email templates.

---

### WP-03: Base CSS Theme (Dark-First)

**WP-ID:** WP-03-css-theme-dark-first  
**Owner agent:** `ui-designer` (or `voltagent-core-dev:frontend-developer` with design oversight)  
**Depends on:** none  

**Files owned:**
- `src/NinetyNine.Web/wwwroot/css/theme.css` (rewrite from scratch)
- `src/NinetyNine.Web/wwwroot/css/app.css` (rewrite base layout, grid, forms)
- `src/NinetyNine.Web/wwwroot/css/scorecard.css` (preserve for now, will be updated in WP-12)
- `src/NinetyNine.Web/Components/App.razor` (ensure dark theme is default, confirm `data-bs-theme` setup)

**Files read-only:**
- `docs/design-assets-canvas.md` (color palette inspiration, Phosphor icon guidance)
- `docs/project-design-direction.md` (dark theme primary, pool-hall aesthetic, no P&B paper replication)
- `src/NinetyNine.Web/wwwroot/img/billiards/ATTRIBUTION.md` (reference for decorative photo locations)

**Deliverables:**
1. **`theme.css` — New dark-first CSS custom properties:**
   - **Neutral backgrounds:** `--nn-bg-primary` (deepest, ~#1a1a1a), `--nn-bg-secondary` (#2a2a2a), `--nn-bg-tertiary` (#3a3a3a) — avoid pure black.
   - **Pool accent colors:** `--nn-accent-teal` (~#1db584), `--nn-accent-green` (~#0f9b6e) — for active states, highlights, brand elements.
   - **Warm accent:** `--nn-accent-gold` (~#d4a574) — for hover, focus, active states.
   - **Text colors:** `--nn-text-primary` (#e8e8e8), `--nn-text-secondary` (#b0b0b0), `--nn-text-tertiary` (#808080).
   - **Semantic colors:** `--nn-success` (#4ade80), `--nn-danger` (#f87171), `--nn-warning` (#facc15), `--nn-info` (#38bdf8).
   - **Component tokens:** `--nn-surface-*`, `--nn-border-*`, `--nn-shadow-*` for consistent elevation and depth.
   - All custom properties use a consistent `--nn-*` namespace to avoid Bootstrap conflicts.
2. **`app.css` — Base layout & form styling:**
   - Body: `background-color: var(--nn-bg-primary)`, `color: var(--nn-text-primary)`.
   - Remove all Bootstrap light-mode defaults (reset `data-bs-body-bg`, `data-bs-body-color`, etc.).
   - Define `:focus-visible` outlines using `--nn-accent-teal` with 3px offset for keyboard navigation.
   - Form inputs: dark backgrounds, teal borders on focus, light text.
   - Buttons: secondary button as `--nn-bg-secondary` with `--nn-text-primary`; primary button as `--nn-accent-teal` with dark text.
   - Form labels: `--nn-text-primary` with explicit `<label>` tags (no placeholder-only).
   - Reduce-motion: `@media (prefers-reduced-motion: reduce)` disables transitions/animations.
3. **`scorecard.css` — Update (minimal for now):**
   - Replace `--sc-paper` and `--sc-ink` tokens with references to theme.css tokens.
   - Keep the grid structure but update colors to match dark theme (WP-12 will do the full redesign).
4. **`App.razor` — Ensure dark-mode default:**
   - Verify `data-bs-theme="dark"` is set by default (not just in dark-mode toggle).
   - Confirm no Bootstrap light-mode colors are hardcoded inline.

**Acceptance criteria:**
- `dotnet build NinetyNine.sln` succeeds.
- All CSS files are valid (no syntax errors).
- `:focus-visible` outline is visible on all interactive elements (keyboard Tab navigation test).
- Contrast ratios meet WCAG 2.2 AA: `--nn-text-primary` on `--nn-bg-primary` ≥ 4.5:1 (use a contrast checker tool).
- Light mode is still available via a toggle (WP-06 will implement the toggle UI, this WP just ensures the CSS variables exist).
- No hardcoded Bootstrap colors (all Bootstrap vars are overridden via `--bs-*` custom properties).
- Animations/transitions are smooth and respect `prefers-reduced-motion`.

**Out of scope:**
- Component-specific scoped CSS (each `.razor.css` file is owned by the agent who rewrites that page).
- Figma mockups — this is purely CSS implementation.
- Light-mode toggle implementation (that's UI in WP-06).

**Estimated effort:** M (medium, ~2-3 hours)

**Risks:**
1. **Bootstrap 5 color override conflicts** — if some Bootstrap components are not overridden correctly, they'll still show light-mode colors. **Mitigation:** test every component type (buttons, forms, alerts, cards) in the browser DevTools before committing.
2. **Phosphor icon visibility** — if the icon stroke weight is too thin on the dark background, they're hard to see. **Mitigation:** test Phosphor regular (42 icons) in the actual dark theme during design.

---

### WP-04: Auth Repository Updates

**WP-ID:** WP-04-auth-repository  
**Owner agent:** `backend-developer` (or `voltagent-core-dev:backend-developer`)  
**Depends on:** WP-01 (Player model changes)  

**Files owned:**
- `src/NinetyNine.Repository/BsonConfiguration.cs` (add indexes for email/verification/reset tokens)
- `src/NinetyNine.Repository/PlayerRepository.cs` (add new methods)
- `src/NinetyNine.Repository/NinetyNine.Repository.csproj` (no new packages)

**Files read-only:**
- `src/NinetyNine.Model/Player.cs` (WP-01 has already defined new fields)
- `docs/architecture.md §5.2` (BSON configuration reference)
- `docs/architecture.md §7.2` (auth field specifications)

**Deliverables:**
1. **Update `BsonConfiguration.cs`:**
   - Add unique index on `players.emailAddress` (lowercased at write time: `Builders<Player>.IndexKeys.Ascending(p => p.EmailAddress.ToLower())`).
   - Add sparse index on `players.emailVerificationToken` (for fast lookups during `/verify-email`).
   - Add sparse index on `players.passwordResetToken` (for fast lookups during `/reset-password`).
   - Ensure `emailAddress` is treated as case-insensitive in queries (convert to lowercase).
2. **Add new methods to `IPlayerRepository`:**
   ```csharp
   Task<Player?> GetByEmailAsync(string email, CancellationToken ct = default);
   Task<Player?> GetByEmailVerificationTokenAsync(string token, CancellationToken ct = default);
   Task<Player?> GetByPasswordResetTokenAsync(string token, CancellationToken ct = default);
   Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
   ```
3. **Implement the new methods in `PlayerRepository.cs`:**
   - `GetByEmailAsync`: query lowercased email (case-insensitive), return null if not found.
   - `GetByEmailVerificationTokenAsync`: query by exact token (no case conversion).
   - `GetByPasswordResetTokenAsync`: query by exact token.
   - `EmailExistsAsync`: return true/false (used for "is email already registered" checks without user enumeration).
4. **Preserve existing methods** — do not change `GetByIdAsync`, `GetByDisplayNameAsync`, etc.; they're used by existing code.

**Acceptance criteria:**
- `dotnet build NinetyNine.sln` succeeds.
- All new methods compile and have XML doc comments.
- Indexes are created correctly (integration test in WP-10 will verify).
- Case-insensitive email lookups work as expected (test in WP-10).
- No breaking changes to existing repository methods.

**Out of scope:**
- Password validation — that's Services (WP-05).
- Email sending — that's the Email Sender (WP-02).
- Removing the OAuth-related index on `linkedIdentities.provider` — that's deferred to a cleanup pass if the LinkedIdentity class is still referenced elsewhere.

**Estimated effort:** S (small, ~1.5 hours)

**Risks:**
1. **Case-insensitive index performance** — lowercasing on every query could be slow at scale. **Mitigation:** this is standard practice for email indexes; if performance becomes an issue, MongoDB can be configured with a case-insensitive collation at collection creation time (deferred for v1).

---

### WP-05: Auth Services (Register/Login/Verify/Reset)

**WP-ID:** WP-05-auth-services  
**Owner agent:** `backend-developer` or `backend-api-security:backend-security-coder`  
**Depends on:** WP-01 (Player model), WP-02 (Email Sender), WP-04 (Repository)  

**Files owned:**
- `src/NinetyNine.Services/Auth/IAuthService.cs` (new file, interface)
- `src/NinetyNine.Services/Auth/AuthService.cs` (new file, implementation)
- `src/NinetyNine.Services/Auth/PasswordValidator.cs` (new file, password rules)
- `src/NinetyNine.Web/Auth/AuthEndpoints.cs` (rewrite to use new AuthService instead of OAuth)
- `src/NinetyNine.Web/Program.cs` (update DI: remove Google auth, add email sender + auth service registration)

**Files read-only:**
- `docs/architecture.md §7.3–7.6` (auth flow specifications)
- `src/NinetyNine.Services/IPlayerService.cs` (confirm interface, may need updates)

**Deliverables:**
1. **`IAuthService` interface:**
   ```csharp
   public interface IAuthService
   {
       Task<(Player player, string errorMessage?)> RegisterAsync(
           string email, string displayName, string password, string confirmPassword,
           CancellationToken ct = default);
       Task<(Player? player, string errorMessage?)> LoginAsync(
           string email, string password, CancellationToken ct = default);
       Task<(bool success, string errorMessage?)> VerifyEmailAsync(
           string token, CancellationToken ct = default);
       Task<(bool success, string errorMessage?)> ResendVerificationAsync(
           string email, CancellationToken ct = default);
       Task<(bool success, string errorMessage?)> ForgotPasswordAsync(
           string email, CancellationToken ct = default);
       Task<(bool success, string errorMessage?)> ResetPasswordAsync(
           string token, string newPassword, string confirmPassword,
           CancellationToken ct = default);
   }
   ```
2. **`PasswordValidator` class:**
   - Validate: ≥ 10 chars, ≥ 1 uppercase, ≥ 1 lowercase, ≥ 1 digit, ≥ 1 symbol (`!@#$%^&*`).
   - Return a list of validation errors (e.g., `["Password must be at least 10 characters", "Password must contain an uppercase letter"]`).
3. **`AuthService` implementation:**
   - **RegisterAsync:**
     - Validate email well-formed (use `System.ComponentModel.DataAnnotations.EmailAddressAttribute`).
     - Check if email/display name already taken (case-insensitive email).
     - Validate password + confirm match.
     - Use `PasswordHasher<Player>.HashPassword(player, password)` to hash.
     - Create Player with `EmailVerified = false`, generate `EmailVerificationToken` (32 bytes, base64url-encode), set expiry to now+24h.
     - Save to repository.
     - Call `IEmailSender.SendVerificationAsync(email, displayName, verifyUrl)` — verifyUrl is `/verify-email?token={token}`.
     - Return the player (no error, as the caller will show "check your email").
     - If validation fails, return error message (no user enumeration — don't say "email already exists", say "Email or display name invalid").
   - **LoginAsync:**
     - Load Player by email (case-insensitive).
     - If not found, email not verified, or account locked out (LockedOutUntil > now), return generic "Invalid credentials" (no enumeration).
     - Use `PasswordHasher<Player>.VerifyHashedPassword(player, player.PasswordHash, password)` to verify.
     - If `VerificationResult.Failed`, increment `FailedLoginAttempts`; at 5, set `LockedOutUntil = now + 15 minutes`. Save and return "Invalid credentials".
     - If `VerificationResult.Success`, reset `FailedLoginAttempts = 0`, set `LastLoginAt = now`, clear `LockedOutUntil`, save, return player.
   - **VerifyEmailAsync:**
     - Load Player by verification token.
     - If not found or token expired (now > expiresAt), return error.
     - Set `EmailVerified = true`, clear token + expiry, save.
     - Return success.
   - **ResendVerificationAsync:**
     - Always return "If email exists, a link has been sent" (no enumeration).
     - Load Player by email (case-insensitive); if not found or already verified, stop.
     - Generate new token + expiry (now + 24h).
     - Call `IEmailSender.SendVerificationAsync(...)`.
     - Save.
   - **ForgotPasswordAsync:**
     - Always return "If email exists, a reset link has been sent" (no enumeration).
     - Load Player by email; if not found, stop.
     - Generate `PasswordResetToken` (32 bytes), set expiry to now + 1h.
     - Call `IEmailSender.SendPasswordResetAsync(email, displayName, resetUrl)` — resetUrl is `/reset-password?token={token}`.
     - Save.
   - **ResetPasswordAsync:**
     - Load Player by reset token; if not found or expired, return error.
     - Validate new password + confirm match.
     - Hash the new password.
     - Update `PasswordHash`, clear token + expiry, save.
     - Return success.
4. **`AuthEndpoints.cs` rewrite:**
   - Remove `/signin-google` (OAuth callback).
   - Rewrite `/register` to POST to the new AuthService and handle success/error.
   - Rewrite `/login` to POST email/password to AuthService.
   - Add `/logout` to clear the auth cookie.
   - Update `/verify-email` to call `VerifyEmailAsync(token)`.
   - Add `/forgot-password` POST endpoint.
   - Add `/reset-password` POST endpoint.
   - Add `/resend-verification` POST endpoint.
   - All endpoints must be rate-limited (10 req/min per IP — already configured in Program.cs, use the `[RequireRateLimitPolicy("auth")]` attribute).
   - All POST endpoints use `[ValidateAntiForgeryToken]`.
5. **Update `Program.cs`:**
   - Remove `.AddGoogle(...)` from authentication setup.
   - Remove `using Microsoft.AspNetCore.Authentication.Google;`.
   - Add `services.AddScoped<IAuthService, AuthService>();`.
   - Add `services.Configure<EmailSettings>(config.GetSection("Email"));` (if not already there).
   - Add `services.AddScoped<IEmailSender>()` registration with a factory that picks implementation based on config (MailKit in prod, Console in dev with no config, Mock in tests).
6. **Token generation helper:**
   - Centralize token generation: `string GenerateSecureToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace("+", "-").Replace("/", "_");` (URL-safe base64).

**Acceptance criteria:**
- `dotnet build NinetyNine.sln` succeeds.
- All new classes and interfaces compile with XML doc comments.
- No Google/OAuth references remain in Program.cs or AuthEndpoints.
- `PasswordValidator` enforces all 5 rules.
- All flows (register, login, verify, reset, resend) return consistent error messages (no enumeration).
- Token expiry is correctly enforced.
- Account lockout on 5 failed attempts, clears after 15 minutes.
- Email sending is called at the right moments (registration, forgot-password, resend).

**Out of scope:**
- Rate limiting enforcement (that's Program.cs/middleware, already configured).
- HTML email template design (WP-02 handles that).
- Two-factor authentication (v1 not needed).

**Estimated effort:** L (large, ~4-5 hours)

**Risks:**
1. **Timing-based user enumeration** — even with constant-time password hashing, the difference between "email not found" (fast query) and "email found but wrong password" (hash computation) can leak info. **Mitigation:** always perform a hash computation even if email not found (compute on a dummy password, discard result). This is done in the above spec.
2. **Token collision** — if two users are issued the same verification token, the second one wins. **Mitigation:** use 32 bytes of `RandomNumberGenerator` (cryptographically secure); collision probability is negligible.

---

### WP-06: MainLayout + NavMenu + UserMenu Redesign (Dark Theme)

**WP-ID:** WP-06-layout-components-redesign  
**Owner agent:** `ui-designer` (with frontend dev pairing if needed)  
**Depends on:** WP-03 (Base CSS theme)  

**Files owned:**
- `src/NinetyNine.Web/Components/Layout/MainLayout.razor` (rewrite)
- `src/NinetyNine.Web/Components/Layout/MainLayout.razor.css` (new scoped styles)
- `src/NinetyNine.Web/Components/Layout/NavMenu.razor` (rewrite)
- `src/NinetyNine.Web/Components/Layout/NavMenu.razor.css` (new scoped styles)
- `src/NinetyNine.Web/Components/Layout/UserMenu.razor` (rewrite)
- `src/NinetyNine.Web/Components/Layout/UserMenu.razor.css` (new scoped styles)
- `src/NinetyNine.Web/Components/Layout/LoginDisplay.razor` (update if needed)

**Files read-only:**
- `docs/architecture.md §8.2–8.4` (layout pattern and component specs)
- `src/NinetyNine.Web/wwwroot/css/theme.css` (use theme tokens)
- `src/NinetyNine.Web/wwwroot/icons/phosphor/regular/` (icon references)

**Deliverables:**
1. **`MainLayout.razor` — Top-level layout:**
   - Left sidebar (80-120px wide, persistent): NinetyNine logo at top, navigation links (Home, New Game, History, Stats, Venues, Profile).
   - Main content area: takes remaining width, full height.
   - Footer: subtle, single line of copyright.
   - Responsive: on mobile (<768px), sidebar collapses to an off-canvas drawer or hamburger menu.
   - Dark theme: sidebar uses `--nn-bg-secondary`, main uses `--nn-bg-primary`.
   - Link styling: inactive links use `--nn-text-secondary`, active use `--nn-accent-teal` with an underline or left border.
   - UserMenu integrated into sidebar (user avatar + name + dropdown: Profile / Sign Out).
2. **`NavMenu.razor` — Navigation component:**
   - List of nav links: `<NavLink href="/" Match="NavLinkMatch.All">Home</NavLink>`, etc.
   - Each link includes an icon (from Phosphor: house, plus, list, chart-bar, map-pin, user).
   - Active link styling via Blazor's built-in `active` class (customize in scoped CSS).
3. **`UserMenu.razor` — User profile dropdown:**
   - Displays user avatar (circular, 40px) + display name.
   - On click, shows a dropdown: "View Profile", "Edit Profile", "Settings", "Sign Out".
   - If not logged in, shows "Sign In" link.
   - Avatar falls back to initials if no image.
4. **`LoginDisplay.razor` — Update if exists:**
   - Show "Sign In / Register" button if not authenticated.
   - Show user menu if authenticated.
   - No changes needed if UserMenu already handles both cases.
5. **Scoped CSS for each component:**
   - MainLayout.razor.css: flex layout, responsive sidebar collapse, dark theme colors.
   - NavMenu.razor.css: link styling, icons, active state.
   - UserMenu.razor.css: dropdown menu, avatar styling, focus-visible.

**Acceptance criteria:**
- `dotnet build NinetyNine.sln` succeeds.
- Layout renders without errors.
- Left sidebar is visible on desktop (≥768px).
- Sidebar collapses on mobile (<768px).
- Active navigation link is visually distinct (teal, underline, or border).
- UserMenu dropdown appears/disappears on click.
- All Phosphor icons render correctly.
- Keyboard navigation: Tab through links, Enter activates, Escape closes dropdowns.
- `:focus-visible` outlines are visible.
- Contrast ratios meet WCAG 2.2 AA.

**Out of scope:**
- Animation/transition polish (can be added later).
- Accessibility testing beyond keyboard nav + focus (WP-21 will do full audit).
- Page content — this WP is layout only.

**Estimated effort:** M (medium, ~2-3 hours)

**Risks:**
1. **Responsive sidebar complexity** — off-canvas drawer on mobile is tricky without JS. **Mitigation:** use Blazor's built-in modal/offcanvas component or simple CSS `transform: translateX(-100%)` + click handler to toggle.
2. **Icon alignment** — if Phosphor icons are not perfectly centered, they'll look misaligned in the nav. **Mitigation:** test icon sizing and use `display: flex; align-items: center; gap: 0.5rem;` for consistency.

---

### WP-07: Auth Page Scaffolds (Empty Pages)

**WP-ID:** WP-07-auth-page-scaffolds  
**Owner agent:** `frontend-developer` (or `voltagent-core-dev:frontend-developer`)  
**Depends on:** WP-06 (Layout), none yet for logic (WP-05 will provide services)  

**Files owned:**
- `src/NinetyNine.Web/Components/Pages/Register.razor` (rewrite as empty form)
- `src/NinetyNine.Web/Components/Pages/Login.razor` (rewrite as empty form)
- `src/NinetyNine.Web/Components/Pages/VerifyEmail.razor` (new file, empty)
- `src/NinetyNine.Web/Components/Pages/ForgotPassword.razor` (new file, empty)
- `src/NinetyNine.Web/Components/Pages/ResetPassword.razor` (new file, empty)
- `src/NinetyNine.Web/Components/Pages/ResendVerification.razor` (new file, empty)

**Files read-only:**
- `docs/architecture.md §7.4–7.7` (flow specifications)
- `src/NinetyNine.Web/Components/Layout/MainLayout.razor` (WP-06 already created)
- `src/NinetyNine.Web/wwwroot/css/theme.css` (use theme tokens)

**Deliverables:**
1. **Each page scaffolds:**
   - `@page` directive with correct route (`/register`, `/login`, `/verify-email`, `/forgot-password`, `/reset-password`, `/resend-verification`).
   - `@layout MainLayout` directive (use new dark layout).
   - Empty form structure (no logic yet):
     - **Register:** Email, DisplayName, Password, ConfirmPassword fields + Submit button.
     - **Login:** Email, Password fields + Submit button + "Forgot password?" link.
     - **VerifyEmail:** Hidden token parameter, message "Verifying..." placeholder.
     - **ForgotPassword:** Email field + Submit button + message placeholder.
     - **ResetPassword:** Token parameter (hidden or in URL), NewPassword, ConfirmPassword fields + Submit button.
     - **ResendVerification:** Email field + Submit button + message placeholder.
   - Each form has explicit `<label>` tags, no placeholder-only fields.
   - Submit buttons styled with theme (dark background, teal on hover).
   - Error messages placeholder (`@if (errorMessage) { ... }`).
   - Success messages placeholder.
2. **`@code` block stubs:**
   - Inject `IAuthService` and `IEmailSender` (ready for WP-08 to fill in).
   - Declare variables for form state (email, password, errorMessage, isLoading, etc.).
   - Empty event handlers (OnSubmitAsync, etc.).
3. **Routing:** ensure pages are registered in `Routes.razor` (should be automatic via `@page` directive).

**Acceptance criteria:**
- `dotnet build NinetyNine.sln` succeeds.
- All 6 pages exist and compile.
- Pages are accessible via their routes (no 404).
- Form fields are visible and labeled.
- No compile errors from missing methods (they're stubbed).
- Basic CSS from theme.css is applied (dark background).

**Out of scope:**
- Logic implementation (that's WP-08, WP-09).
- Email sending (that's WP-05).
- Rate limiting (that's WP-05).

**Estimated effort:** S (small, ~1-2 hours)

**Risks:**
1. **Route conflicts** — if `/login` is also used by Google OAuth (old code), there might be a conflict. **Mitigation:** confirm old OAuth routes are removed in WP-05.

---

### WP-08: Register & Login Pages (Full Implementation)

**WP-ID:** WP-08-register-login-pages  
**Owner agent:** `frontend-developer` (or `voltagent-core-dev:frontend-developer`)  
**Depends on:** WP-05 (AuthService), WP-07 (Page scaffolds)  

**Files owned:**
- `src/NinetyNine.Web/Components/Pages/Register.razor` (fill in logic)
- `src/NinetyNine.Web/Components/Pages/Register.razor.css` (new scoped styles)
- `src/NinetyNine.Web/Components/Pages/Login.razor` (fill in logic)
- `src/NinetyNine.Web/Components/Pages/Login.razor.css` (new scoped styles)

**Files read-only:**
- `src/NinetyNine.Services/Auth/IAuthService.cs` (use interface)
- `docs/architecture.md §7.4–7.5` (flow specs)
- `src/NinetyNine.Web/wwwroot/css/theme.css` (theme tokens)

**Deliverables:**
1. **Register.razor:**
   - `@code` block implementation:
     - Inject `IAuthService`, `NavigationManager`, `ILogger`.
     - Bind form fields: `emailInput`, `displayNameInput`, `passwordInput`, `confirmPasswordInput`.
     - `OnSubmitAsync`: call `authService.RegisterAsync(email, displayName, password, confirmPassword)`.
     - If success: show message "Check your email to verify your account" + link to `/login` after 3 seconds.
     - If error: display error message in a red alert.
     - Disable submit button while loading.
     - Show validation errors as the user types (optional, nice-to-have for UX).
   - Form validation on client-side (email format, password length) + server-side (already in AuthService).
   - No submit button spam: `disabled="@isLoading"`.
   - Password confirmation visual feedback (show/hide password toggle).
2. **Login.razor:**
   - `@code` block implementation:
     - Inject `IAuthService`, `NavigationManager`, `AuthenticationStateProvider`.
     - Bind form fields: `emailInput`, `passwordInput`.
     - `OnSubmitAsync`: call `authService.LoginAsync(email, password)`.
     - If success: refresh auth state, redirect to return URL or `/`.
     - If error: display "Invalid email or password" (generic).
     - If account locked: display "Too many failed attempts. Try again in 15 minutes."
     - Disable submit while loading.
     - Include "Forgot password?" link to `/forgot-password`.
     - Include "Register" link to `/register`.
   - After successful login, the auth cookie is set by the service; Blazor auto-refreshes auth state via `AuthenticationStateProvider.NotifyAuthenticationStateChangedAsync()`.
3. **Scoped CSS:**
   - Dark theme form styling (use `--nn-*` tokens).
   - Error alert in red/danger color.
   - Button: `--nn-accent-teal` background, dark text, hover state.
   - Input focus: teal border + outline.

**Acceptance criteria:**
- `dotnet build NinetyNine.sln` succeeds.
- Register page: can enter email/display name/password, submit calls AuthService.
- Login page: can enter email/password, submit calls AuthService.
- On successful login, user is redirected to `/` and auth cookie is set.
- On successful register, user sees "Check your email" message.
- Error messages are displayed for validation failures.
- Form is disabled while loading (prevent double-submit).
- `:focus-visible` on all form fields.

**Out of scope:**
- Social login buttons (OAuth removed, not readded).
- "Remember me" checkbox (v1 uses 30-day sliding expiration).
- Custom password strength meter (optional nice-to-have).

**Estimated effort:** M (medium, ~2-3 hours)

**Risks:**
1. **Auth state refresh timing** — if Blazor doesn't immediately refresh auth state after login, user might be redirected but still appear logged-out. **Mitigation:** use `AuthenticationStateProvider.NotifyAuthenticationStateChangedAsync()` explicitly after setting cookie.
2. **Redirect loop** — if `/login` redirects authenticated users to `/`, and `/` is empty (no auth required), the user might get stuck. **Mitigation:** only redirect if the user was unauthenticated before login.

---

### WP-09: Email Verify & Password Reset Pages

**WP-ID:** WP-09-verify-reset-pages  
**Owner agent:** `frontend-developer` (or `voltagent-core-dev:frontend-developer`)  
**Depends on:** WP-05 (AuthService), WP-07 (Page scaffolds)  

**Files owned:**
- `src/NinetyNine.Web/Components/Pages/VerifyEmail.razor` (fill in logic)
- `src/NinetyNine.Web/Components/Pages/VerifyEmail.razor.css` (new scoped styles)
- `src/NinetyNine.Web/Components/Pages/ForgotPassword.razor` (fill in logic)
- `src/NinetyNine.Web/Components/Pages/ForgotPassword.razor.css` (new scoped styles)
- `src/NinetyNine.Web/Components/Pages/ResetPassword.razor` (fill in logic)
- `src/NinetyNine.Web/Components/Pages/ResetPassword.razor.css` (new scoped styles)
- `src/NinetyNine.Web/Components/Pages/ResendVerification.razor` (fill in logic)
- `src/NinetyNine.Web/Components/Pages/ResendVerification.razor.css` (new scoped styles)

**Files read-only:**
- `src/NinetyNine.Services/Auth/IAuthService.cs`
- `docs/architecture.md §7.4–7.7`

**Deliverables:**
1. **VerifyEmail.razor:**
   - Extract token from query string: `@page "/verify-email"` + `[SupplyParameterFromQuery] string? token { get; set; }`.
   - On render (`OnInitializedAsync`): call `authService.VerifyEmailAsync(token)`.
   - If success: show "Email verified! Redirecting to login..." + redirect to `/login` after 2 seconds.
   - If error (invalid or expired token): show "Verification failed. [Resend link](/resend-verification)." link.
   - Loading state: show "Verifying your email..." spinner.
2. **ForgotPassword.razor:**
   - Form with single Email field.
   - `OnSubmitAsync`: call `authService.ForgotPasswordAsync(email)`.
   - Always show success message (no enumeration): "If an account exists with that email, a reset link has been sent."
   - Clear form + disable button for 30 seconds (prevents spam).
   - Include "Back to login" link.
3. **ResetPassword.razor:**
   - Extract token from query string: `@page "/reset-password"` + `[SupplyParameterFromQuery] string? token`.
   - On render: validate token is present.
   - Form with NewPassword + ConfirmPassword fields.
   - `OnSubmitAsync`: call `authService.ResetPasswordAsync(token, newPassword, confirmPassword)`.
   - If success: show "Password reset successful. [Go to login](/login)." + redirect after 2 seconds.
   - If error (invalid/expired token): show "Reset link is invalid or expired. [Request a new one](/forgot-password)."
4. **ResendVerification.razor:**
   - Form with Email field.
   - `OnSubmitAsync`: call `authService.ResendVerificationAsync(email)`.
   - Always show success message (no enumeration): "If an unverified account exists, a verification link has been sent."
   - Disable submit for 30 seconds (rate limit on client).
   - Include "Back to login" link.
5. **Scoped CSS:** dark theme consistent with WP-08 (alerts, buttons, form styling).

**Acceptance criteria:**
- `dotnet build NinetyNine.sln` succeeds.
- VerifyEmail: token is validated, success/error messages shown.
- ForgotPassword: form visible, submit calls service, generic success message always shown.
- ResetPassword: token extracted, form visible, password update works, redirect on success.
- ResendVerification: email form, generic success, submit disabled during cooldown.
- All pages use dark theme tokens.
- `:focus-visible` on all inputs.

**Out of scope:**
- Email template customization (WP-02 owns that).
- Rate limiting on server (WP-05 owns that; client-side cooldown is UI only).

**Estimated effort:** M (medium, ~2-3 hours)

**Risks:**
1. **Token parsing from URL** — if the token contains special characters (e.g., `=` in base64), URL decoding might fail. **Mitigation:** use `WebUtility.UrlDecode()` on the token parameter before passing to AuthService.
2. **Redirect timing** — if redirect happens before user sees the message, they won't know what happened. **Mitigation:** show message for 2+ seconds before redirecting.

---

### WP-10: Auth Tests (Model / Repo / Services)

**WP-ID:** WP-10-auth-tests  
**Owner agent:** `voltagent-qa-sec:test-automator` (or `backend-developer` with test focus)  
**Depends on:** WP-01 (Model), WP-04 (Repo), WP-05 (Services)  

**Files owned:**
- `tests/NinetyNine.Model.Tests/PlayerAuthTests.cs` (new file)
- `tests/NinetyNine.Repository.Tests/PlayerAuthRepositoryTests.cs` (new file)
- `tests/NinetyNine.Services.Tests/AuthServiceTests.cs` (new file)
- `tests/NinetyNine.Services.Tests/PasswordValidatorTests.cs` (new file)

**Files read-only:**
- `src/NinetyNine.Model/Player.cs` (WP-01)
- `src/NinetyNine.Repository/PlayerRepository.cs` (WP-04)
- `src/NinetyNine.Services/Auth/AuthService.cs` (WP-05)
- `docs/architecture.md §12` (testing strategy)

**Deliverables:**
1. **`PlayerAuthTests.cs` — Unit tests for Player model:**
   - EmailAddress field exists and is serializable.
   - PasswordHash field exists.
   - EmailVerified default is false.
   - Token fields (EmailVerificationToken, PasswordResetToken) are nullable.
   - Expiry fields are nullable DateTime.
   - FailedLoginAttempts default is 0.
   - LockedOutUntil is nullable.
   - JSON serialization round-trip with all auth fields.
2. **`PlayerAuthRepositoryTests.cs` — Integration tests using Testcontainers.MongoDB:**
   - `GetByEmailAsync`: case-insensitive lookup works (email stored lowercase).
   - `GetByEmailAsync` returns null for non-existent email.
   - `EmailExistsAsync` returns true/false correctly.
   - `GetByEmailVerificationTokenAsync` finds player by token.
   - `GetByPasswordResetTokenAsync` finds player by reset token.
   - Unique index on email prevents duplicate emails.
   - Unique index on displayName (existing test, should still pass).
   - Sparse indexes on verification/reset tokens allow multiple null values.
3. **`AuthServiceTests.cs` — Integration tests (uses testcontainers Mongo + real repos):**
   - **Register flow:**
     - Valid registration creates Player with hashed password, unverified email.
     - Duplicate email rejected.
     - Duplicate displayName rejected.
     - Invalid email rejected.
     - Weak password rejected (< 10 chars, missing uppercase, etc.).
     - Password and confirm mismatch rejected.
     - `IEmailSender.SendVerificationAsync` is called with correct params.
   - **Login flow:**
     - Valid email + password returns Player.
     - Invalid password increments FailedLoginAttempts.
     - 5 failed attempts lock account (LockedOutUntil set).
     - Locked account cannot login even with correct password.
     - Successful login resets FailedLoginAttempts, sets LastLoginAt.
     - Unverified email cannot login ("Invalid credentials", no enumeration).
     - Non-existent email returns "Invalid credentials" (no enumeration, timing constant).
   - **Email verification:**
     - Valid token marks EmailVerified = true, clears token.
     - Invalid token returns error.
     - Expired token returns error.
   - **Password reset:**
     - Valid reset token allows password update.
     - Invalid/expired reset token rejected.
     - New password is hashed correctly.
   - **Resend verification:**
     - Unverified account gets new token + email sent.
     - Already-verified account gets no email (silent success).
     - Non-existent email gets no email (silent success).
   - **Forgot password:**
     - Existing email gets reset token + email.
     - Non-existent email: silent success (no enumeration).
4. **`PasswordValidatorTests.cs` — Unit tests for password rules:**
   - Password < 10 chars rejected.
   - Password ≥ 10 chars without uppercase rejected.
   - Password without lowercase rejected.
   - Password without digit rejected.
   - Password without symbol rejected.
   - Valid password (10+ chars, all 4 categories) accepted.
   - Returns list of error messages for all failures.

**Acceptance criteria:**
- `dotnet build NinetyNine.sln` succeeds.
- All new tests compile and run: `dotnet test tests/NinetyNine.Model.Tests tests/NinetyNine.Repository.Tests tests/NinetyNine.Services.Tests`.
- Test count increases: expect ~40-50 new tests across 4 files.
- All prior 186 tests still pass (no regressions).
- Coverage on auth code ≥ 80%.
- Testcontainers MongoDB spins up correctly for integration tests.

**Out of scope:**
- Component tests (bUnit) for Razor pages — that's WP-20.
- End-to-end flow testing (that's smoke tests in WP-22).

**Estimated effort:** M (medium, ~3 hours)

**Risks:**
1. **Testcontainers startup time** — if Docker is slow, integration tests take 2-3 minutes to start. **Mitigation:** parallelize test execution (`dotnet test --maxcpucount:4`).

---

### WP-11: Dev Mock Auth Update

**WP-ID:** WP-11-dev-mock-auth-update  
**Owner agent:** `backend-developer` (or `frontend-developer`)  
**Depends on:** WP-01 (Player model with new fields), WP-05 (AuthService available)  

**Files owned:**
- `src/NinetyNine.Web/Auth/MockAuthEndpoints.cs` (update existing)
- `src/NinetyNine.Repository/DataSeeder.cs` (update if exists, or create)

**Files read-only:**
- `src/NinetyNine.Model/Player.cs` (has new auth fields)
- `docs/architecture.md §7.9` (dev mock auth spec)

**Deliverables:**
1. **`MockAuthEndpoints.cs` — Update `/mock/signin-as` endpoint:**
   - Endpoint remains guarded by `Auth:Mock:Enabled && IsDevelopment()`.
   - Takes a `displayName` query param (e.g., `/mock/signin-as?displayName=carey`).
   - Loads the Player by displayName from the database.
   - Issues an auth cookie directly (bypasses password/verification).
   - Redirects to return URL or `/`.
   - Used for rapid UX prototyping during dev without email loops.
2. **Data seeder — ensure test players are set up:**
   - Seed three test players: `carey`, `george`, `carey_b`.
   - Each has `EmailVerified = true` (so they can login with real email/password flow too).
   - Each has a known dev password hashed: `Test1234!` (hashed via `PasswordHasher<Player>`).
   - `carey` and `carey_b` both have FirstName = "Carey" (satisfies the `_b` naming note from design direction).
   - If seeding fails (e.g., DB not ready), log a warning but don't crash.

**Acceptance criteria:**
- `dotnet build NinetyNine.sln` succeeds.
- `/mock/signin-as?displayName=carey` in dev mode issues auth cookie + redirects.
- Test players can login with email (e.g., `carey@test.local`) and password `Test1234!`.
- Test players do not get seeded in Production (check `Environment.IsDevelopment()`).
- Seeding is idempotent (running it twice doesn't create duplicates).

**Out of scope:**
- Email verification for dev players (they're already verified).
- Password reset flows (not needed for dev, mock auth bypasses all that).

**Estimated effort:** S (small, ~1 hour)

**Risks:**
1. **Duplicate seeding** — if the seeder runs multiple times, test players get duplicated. **Mitigation:** check if player exists before creating (use `GetByDisplayNameAsync`).

---

### WP-12: ScoreCardGrid Redesign (Dark Theme)

**WP-ID:** WP-12-scorecard-redesign  
**Owner agent:** `ui-designer` (or `frontend-developer` with design input)  
**Depends on:** WP-03 (CSS theme)  

**Files owned:**
- `src/NinetyNine.Web/Components/Shared/ScoreCardGrid.razor` (rewrite)
- `src/NinetyNine.Web/Components/Shared/ScoreCardGrid.razor.css` (rewrite)
- `src/NinetyNine.Web/Components/Shared/FrameCell.razor` (update styling)
- `src/NinetyNine.Web/Components/Shared/FrameCell.razor.css` (rewrite)
- `src/NinetyNine.Web/wwwroot/css/scorecard.css` (consolidate into scoped CSS or remove)

**Files read-only:**
- `docs/architecture.md §8.5–8.6` (scorecard requirements)
- `src/NinetyNine.Web/wwwroot/css/theme.css` (theme tokens)

**Deliverables:**
1. **`ScoreCardGrid.razor` — Full redesign (functional unchanged, visual only):**
   - 9-column grid, one per frame (Frame 1–9).
   - Each column is a `FrameCell` component displaying break bonus / ball count / running total.
   - Active frame highlighted with teal border or background.
   - Completed frames visually distinct (locked, maybe with a checkmark icon).
   - Empty frames neutral.
   - Below the grid: current game score, average frame score, status indicator.
   - Responsive on mobile (<768px): show single focused frame with a frame-picker strip below (allow swipe or tap to navigate frames).
2. **`FrameCell.razor` — Updated styling:**
   - 3-row vertical stack: Break Bonus / Ball Count / Running Total.
   - Dark background (use `--nn-bg-secondary` or `--nn-bg-tertiary`).
   - Monospace font for numerals (e.g., `'JetBrains Mono', 'Fira Code', ui-monospace`).
   - Active frame: teal border, light background.
   - Completed frame: slightly darker background, subtle lock icon (optional).
   - Hover state: slight elevation/shadow.
   - ARIA labels for accessibility: "Frame 3, Break Bonus 1, Ball Count 7, Running Total 18".
3. **`ScoreCardGrid.razor.css` — Dark theme grid:**
   - 9-column grid layout (`display: grid; grid-template-columns: repeat(9, 1fr)`).
   - Gap between frames (0.5rem).
   - Active frame: `border: 2px solid var(--nn-accent-teal)` + highlight background.
   - Completed frame: `opacity: 0.8` or slightly darker background.
   - Mobile responsive: hide most frames, show only current + adjacent, frame picker strip below.
4. **`FrameCell.razor.css` — Dark theme cell:**
   - Each row (Bonus / Count / Total) in a small box.
   - Dark background, light text.
   - Monospace font for numbers.
   - Focus-visible outline for keyboard navigation.

**Acceptance criteria:**
- `dotnet build NinetyNine.sln` succeeds.
- ScoreCardGrid displays 9 frames in a grid.
- Each FrameCell shows 3 values (break bonus / count / total).
- Active frame is highlighted.
- Completed frames are visually distinct.
- Responsive on mobile: single frame focus + picker.
- ARIA labels are present (test with screen reader or inspect HTML).
- `:focus-visible` on all interactive elements.
- Monospace font used for numerals.

**Out of scope:**
- Editability (WP-13 will handle Play page with input).
- Animation on frame completion (nice-to-have, deferred).

**Estimated effort:** M (medium, ~2 hours)

**Risks:**
1. **Grid layout stability** — if a frame is missing or has extra content, grid alignment breaks. **Mitigation:** ensure exactly 9 frames always, and FrameCell has consistent height.

---

### WP-13: Game Pages (New / Play / List / Details)

**WP-ID:** WP-13-game-pages  
**Owner agent:** `frontend-developer` (or `ui-designer`)  
**Depends on:** WP-06 (MainLayout), WP-12 (ScoreCardGrid design)  

**Files owned:**
- `src/NinetyNine.Web/Components/Pages/Games/New.razor` (rewrite)
- `src/NinetyNine.Web/Components/Pages/Games/New.razor.css` (new)
- `src/NinetyNine.Web/Components/Pages/Games/Play.razor` (rewrite)
- `src/NinetyNine.Web/Components/Pages/Games/Play.razor.css` (new)
- `src/NinetyNine.Web/Components/Pages/Games/List.razor` (rewrite)
- `src/NinetyNine.Web/Components/Pages/Games/List.razor.css` (new)
- `src/NinetyNine.Web/Components/Pages/Games/Details.razor` (rewrite)
- `src/NinetyNine.Web/Components/Pages/Games/Details.razor.css` (new)

**Files read-only:**
- `docs/architecture.md §8.3–8.5` (page specs)
- `src/NinetyNine.Web/Components/Shared/ScoreCardGrid.razor` (WP-12)
- `src/NinetyNine.Web/wwwroot/css/theme.css` (theme tokens)

**Deliverables:**
1. **Games/New.razor — Start new game:**
   - Form: Venue picker (dropdown), Table Size picker (6ft / 7ft / 9ft / 10ft).
   - Submit button: "Start Game" → creates Game via `IGameService.StartNewGameAsync`.
   - On success: redirect to `/games/{id}/play`.
   - Error handling: display validation errors.
2. **Games/Play.razor — Live scoring (main game page):**
   - Requires `[Authorize]`.
   - Displays: Player name, Venue, Table size, Current frame number.
   - Shows the ScoreCardGrid (read-only or editable).
   - For each frame, a modal or sidebar to enter: Break Bonus (0-1), Ball Count (0-10).
   - Submit frame: `IGameService.RecordFrameAsync`.
   - On last frame (9), show "Complete Game" button instead of "Next Frame".
   - Handles in-progress state: can exit and resume later.
3. **Games/List.razor — Game history:**
   - Paginated list of the user's games (completed + in-progress).
   - For each game: date, venue, final score, status (completed/in-progress).
   - Click to view details.
   - Filter/sort options (optional for v1).
4. **Games/Details.razor — View a completed game:**
   - Displays the full ScoreCardGrid (read-only).
   - Game metadata: date played, venue, table size, final score, duration.
   - Notes (if any).
   - Back button to `/games`.

**Acceptance criteria:**
- `dotnet build NinetyNine.sln` succeeds.
- `/games/new` shows venue + table size picker.
- Submitting creates a Game and redirects to Play page.
- `/games/{id}/play` displays the live scoring grid.
- Frames can be entered + recorded.
- `/games` lists the user's games.
- `/games/{id}` shows game details (read-only).
- All pages use dark theme.
- Responsive on mobile.

**Out of scope:**
- Multi-player games (v1 is single-player only).
- Export/print (deferred).

**Estimated effort:** L (large, ~4 hours)

**Risks:**
1. **State management** — if the page is refreshed mid-game, can the game be resumed? **Mitigation:** load the in-progress game from the database on page load.

---

### WP-14: Shared Components Redesign (Dark Theme)

**WP-ID:** WP-14-shared-components-redesign  
**Owner agent:** `ui-designer` (or `frontend-developer`)  
**Depends on:** WP-03 (CSS theme)  

**Files owned:**
- `src/NinetyNine.Web/Components/Shared/AvatarImage.razor` (update)
- `src/NinetyNine.Web/Components/Shared/AvatarImage.razor.css` (new)
- `src/NinetyNine.Web/Components/Shared/InitialsAvatar.razor` (update)
- `src/NinetyNine.Web/Components/Shared/InitialsAvatar.razor.css` (new)
- `src/NinetyNine.Web/Components/Shared/PlayerBadge.razor` (update)
- `src/NinetyNine.Web/Components/Shared/PlayerBadge.razor.css` (new)
- `src/NinetyNine.Web/Components/Shared/VisibilityToggle.razor` (update)
- `src/NinetyNine.Web/Components/Shared/VisibilityToggle.razor.css` (new)
- `src/NinetyNine.Web/Components/Shared/FrameInputDialog.razor` (update)
- `src/NinetyNine.Web/Components/Shared/FrameInputDialog.razor.css` (new)
- `src/NinetyNine.Web/Components/Shared/TableSizePicker.razor` (update)
- `src/NinetyNine.Web/Components/Shared/TableSizePicker.razor.css` (new)

**Files read-only:**
- `src/NinetyNine.Web/wwwroot/css/theme.css` (use theme tokens)

**Deliverables:**
1. **For each component:**
   - Update styling to use dark theme tokens (backgrounds, text colors, borders).
   - Add `:focus-visible` outlines for keyboard nav.
   - Ensure contrast ratios ≥ 4.5:1.
   - Responsive design (mobile-friendly).
   - ARIA labels where applicable.
2. **Specific updates:**
   - **AvatarImage / InitialsAvatar:** circular avatars with dark border, fallback initials in pool-accent teal background.
   - **PlayerBadge:** avatar + name + link, dark background.
   - **VisibilityToggle:** toggle switch (public/private), dark theme (teal when active).
   - **FrameInputDialog:** modal with inputs for Break Bonus (0/1 radio) + Ball Count (0–10 spinner or input). Dark background, submit/cancel buttons.
   - **TableSizePicker:** radio buttons or button group for 6ft / 7ft / 9ft / 10ft. Dark theme, teal highlight for selected.

**Acceptance criteria:**
- `dotnet build NinetyNine.sln` succeeds.
- All components render with dark theme colors.
- `:focus-visible` outlines visible on all interactive elements.
- Contrast ratios verified (use automated tool or manual inspection).
- Mobile responsive (<768px).

**Out of scope:**
- Animation/transitions (can be added later).

**Estimated effort:** M (medium, ~2 hours)

**Risks:**
1. **Component inconsistency** — if each component uses different color tokens, the UI looks fragmented. **Mitigation:** create a component style guide or checklist before implementing.

---

### WP-15: Game Pages Dark Theme Styling

**WP-ID:** WP-15-game-pages-styling  
**Owner agent:** `ui-designer`  
**Depends on:** WP-13 (Game page components exist), WP-03 (theme tokens)  

**Files owned:**
- Scoped CSS for all game pages (New.razor.css, Play.razor.css, List.razor.css, Details.razor.css)

**Files read-only:**
- `src/NinetyNine.Web/wwwroot/css/theme.css` (theme tokens)

**Deliverables:**
1. Apply dark theme tokens to all game page scoped CSS.
2. Ensure consistent button, form, and card styling across pages.
3. Verify responsive layout on mobile.

**Acceptance criteria:**
- All game pages render with dark theme colors.
- No light-theme colors visible.
- Buttons are styled consistently (teal primary, secondary, etc.).
- Forms have proper label + input styling.

**Estimated effort:** S (small, ~1 hour)

---

### WP-16: Player Pages (Profile / EditProfile)

**WP-ID:** WP-16-player-pages  
**Owner agent:** `frontend-developer` (or `ui-designer`)  
**Depends on:** WP-06 (MainLayout), WP-03 (theme)  

**Files owned:**
- `src/NinetyNine.Web/Components/Pages/Players/Profile.razor` (rewrite)
- `src/NinetyNine.Web/Components/Pages/Players/Profile.razor.css` (new)
- `src/NinetyNine.Web/Components/Pages/Players/EditProfile.razor` (rewrite)
- `src/NinetyNine.Web/Components/Pages/Players/EditProfile.razor.css` (new)

**Files read-only:**
- `docs/architecture.md §8.3, §8.5` (page specs)

**Deliverables:**
1. **Profile.razor — Public or personal profile view:**
   - Display user's avatar, display name, real name (if public), email (if public), phone (if public).
   - Show user's stats: games played, best score, average score.
   - Show recent games.
   - If viewing own profile: "Edit Profile" button.
   - If viewing another's profile: read-only view respecting `ProfileVisibility` flags.
2. **EditProfile.razor — Edit own profile:**
   - Requires `[Authorize]`.
   - Form fields: Display Name, Email, Phone, FirstName, MiddleName, LastName.
   - Visibility toggles for each PII field.
   - Avatar upload: `<InputFile>` for image, image preview, delete button.
   - Submit button: save changes.

**Acceptance criteria:**
- `dotnet build NinetyNine.sln` succeeds.
- Profile page displays user info respecting visibility flags.
- EditProfile page allows editing + saving.
- Avatar upload works (stores in GridFS).
- Dark theme applied.

**Estimated effort:** M (medium, ~2 hours)

---

### WP-17: Venue Pages (List / Edit)

**WP-ID:** WP-17-venue-pages  
**Owner agent:** `frontend-developer` (or `ui-designer`)  
**Depends on:** WP-06 (MainLayout), WP-03 (theme)  

**Files owned:**
- `src/NinetyNine.Web/Components/Pages/Venues/List.razor` (rewrite)
- `src/NinetyNine.Web/Components/Pages/Venues/List.razor.css` (new)
- `src/NinetyNine.Web/Components/Pages/Venues/Edit.razor` (rewrite)
- `src/NinetyNine.Web/Components/Pages/Venues/Edit.razor.css` (new)

**Files read-only:**
- `docs/architecture.md §8.3, §8.5` (page specs)

**Deliverables:**
1. **Venues/List.razor:**
   - List of venues (public + user-created private ones).
   - For each: name, address, phone.
   - Add venue button (if authenticated).
   - Click to edit (if owner).
2. **Venues/Edit.razor:**
   - Form: name, address, phone, private toggle.
   - Create new or edit existing.
   - Save + delete buttons.

**Acceptance criteria:**
- `dotnet build NinetyNine.sln` succeeds.
- Venues list displays correctly.
- Edit form works for create + update.
- Dark theme applied.

**Estimated effort:** S (small, ~1.5 hours)

---

### WP-18: Stats Pages (Leaderboard / MyStats)

**WP-ID:** WP-18-stats-pages  
**Owner agent:** `frontend-developer` (or `ui-designer`)  
**Depends on:** WP-06 (MainLayout), WP-03 (theme)  

**Files owned:**
- `src/NinetyNine.Web/Components/Pages/Stats/Leaderboard.razor` (rewrite)
- `src/NinetyNine.Web/Components/Pages/Stats/Leaderboard.razor.css` (new)
- `src/NinetyNine.Web/Components/Pages/Stats/MyStats.razor` (rewrite)
- `src/NinetyNine.Web/Components/Pages/Stats/MyStats.razor.css` (new)

**Files read-only:**
- `docs/architecture.md §8.3, §8.5` (page specs)

**Deliverables:**
1. **Leaderboard.razor — Global leaderboard:**
   - Ranked table: position, player avatar/name, games played, average score, best score.
   - Sorted by average score descending.
   - Paginated (10–20 per page).
2. **MyStats.razor — Personal stats:**
   - Requires `[Authorize]`.
   - Summary: total games, completed games, average score, best score, perfect games, perfect frames.
   - Chart/graph of score progression (optional for v1).
   - Recent games list.

**Acceptance criteria:**
- `dotnet build NinetyNine.sln` succeeds.
- Leaderboard displays top players.
- MyStats shows personal stats.
- Dark theme applied.

**Estimated effort:** M (medium, ~2 hours)

---

### WP-19: Home Page Redesign

**WP-ID:** WP-19-home-page-redesign  
**Owner agent:** `ui-designer` (or `frontend-developer`)  
**Depends on:** WP-06 (MainLayout), WP-03 (theme)  

**Files owned:**
- `src/NinetyNine.Web/Components/Pages/Home.razor` (rewrite)
- `src/NinetyNine.Web/Components/Pages/Home.razor.css` (new)

**Files read-only:**
- `docs/architecture.md §8.2–8.5` (visual direction)
- `src/NinetyNine.Web/wwwroot/img/billiards/ATTRIBUTION.md` (hero image options)

**Deliverables:**
1. **Home.razor — Landing page:**
   - Hero section: full-width background image (e.g., `empty-dark-table.jpg` from billiards photos), overlay with "Welcome to NinetyNine" title + CTA button.
   - If authenticated: quick links to New Game, View History, Check Stats.
   - If not authenticated: Sign In / Register buttons.
   - Brief game rules or feature highlights (optional).
   - Light/dark theme toggle (optional for v1, can be in UserMenu instead).

**Acceptance criteria:**
- `dotnet build NinetyNine.sln` succeeds.
- Hero image displays properly.
- CTA buttons are prominent.
- Dark theme applied.
- Responsive on mobile.

**Estimated effort:** S (small, ~1.5 hours)

---

### WP-20: Web Component Tests (bUnit)

**WP-ID:** WP-20-web-component-tests  
**Owner agent:** `voltagent-qa-sec:test-automator`  
**Depends on:** WP-08, WP-09, WP-13, WP-14, WP-16–WP-19 (pages implemented)  

**Files owned:**
- `tests/NinetyNine.Web.Tests/Components/AuthPageTests.cs` (new)
- `tests/NinetyNine.Web.Tests/Components/ScoreCardGridTests.cs` (new)
- `tests/NinetyNine.Web.Tests/Components/SharedComponentTests.cs` (new)

**Files read-only:**
- All `.razor` component files from WPs 06–19

**Deliverables:**
1. **AuthPageTests.cs:**
   - Register page: form renders, submit button calls service.
   - Login page: form renders, submit validates credentials.
   - Email verify page: token parameter extracted, success message shown.
   - Forgot/reset pages: forms render, messages shown.
2. **ScoreCardGridTests.cs:**
   - Grid renders 9 frames.
   - Active frame highlighted.
   - Completed frame visually distinct.
3. **SharedComponentTests.cs:**
   - AvatarImage renders avatar or fallback initials.
   - PlayerBadge renders name + avatar.
   - VisibilityToggle toggles public/private state.
   - FrameInputDialog opens + closes.

**Acceptance criteria:**
- `dotnet test tests/NinetyNine.Web.Tests` all pass.
- Test count increases by 20–30 new component tests.
- No regressions in existing tests.
- Coverage on component logic ≥ 75%.

**Estimated effort:** M (medium, ~2 hours)

---

### WP-21: Accessibility Audit (WCAG 2.2 AA)

**WP-ID:** WP-21-accessibility-audit  
**Owner agent:** `voltagent-qa-sec:security-auditor` (or `ui-designer` with accessibility focus)  
**Depends on:** All visual WPs (WP-03, WP-06, WP-12–WP-19)  

**Files owned:**
- `docs/accessibility-audit.md` (new, findings + fixes)
- Fixes to `.razor` and `.css` files as needed

**Files read-only:**
- All component + page files

**Deliverables:**
1. **Manual accessibility review:**
   - Keyboard navigation: Tab through all pages, enter activates buttons/links, Esc closes modals.
   - Screen reader test: use NVDA (Windows) or VoiceOver (Mac) to verify ARIA labels + semantic HTML.
   - Color contrast: use WebAIM contrast checker on all text.
   - Focus visible: `:focus-visible` outlines present on all interactive elements.
   - Form labels: all `<input>` fields have explicit `<label>` elements.
   - Touch targets: all buttons ≥ 44×44px.
   - Reduced-motion: animations disabled when `prefers-reduced-motion: reduce`.
2. **Fixes:**
   - Add missing ARIA labels to ScoreCardGrid cells.
   - Fix any inputs missing labels.
   - Increase button size if < 44px.
   - Update colors if contrast < 4.5:1.
   - Ensure focus outlines are 3px + 2px offset.
3. **Report in `docs/accessibility-audit.md`:**
   - Checklist of WCAG 2.2 AA criteria (12–15 items).
   - Status (pass/fail) for each.
   - Fixes applied.

**Acceptance criteria:**
- All WCAG 2.2 AA criteria pass.
- Keyboard navigation works on all pages.
- Screen reader reads page correctly.
- Contrast ratios verified.
- Focus visible on all interactive elements.
- Touch targets ≥ 44px.

**Estimated effort:** M (medium, ~2 hours)

---

### WP-22: Cross-Browser Smoke Test

**WP-ID:** WP-22-cross-browser-smoke-test  
**Owner agent:** `voltagent-qa-sec:test-automator`  
**Depends on:** All WPs (full app ready)  

**Files owned:**
- `tests/NinetyNine.Web.Tests/SmokeTests.cs` (new, optional)
- `docs/smoke-test-checklist.md` (new)

**Files read-only:**
- Entire app

**Deliverables:**
1. **Manual smoke test (documented in checklist):**
   - **Chrome/Chromium:** Home, Register, Login, Play Game, View Leaderboard on desktop + mobile.
   - **Firefox:** same flow.
   - **Safari (Mac):** same flow.
   - Check that:
     - Pages load without 404.
     - Forms submit correctly.
     - Dark theme is default.
     - Icons render.
     - Responsive layout works on mobile.
     - No JS errors in DevTools console.
2. **Test data:** use dev mock auth to sign in as `carey`, create/play a game, view stats.

**Acceptance criteria:**
- All flows work in Chrome, Firefox, Safari.
- No errors in console.
- Dark theme is the default (not light).
- Mobile layout responsive.
- Billiards hero image loads (if included).

**Estimated effort:** S (small, ~1 hour for manual testing, can be skipped if budget tight)

---

### WP-23: Final Build & Integration Pass

**WP-ID:** WP-23-final-build-integration  
**Owner agent:** `backend-developer` (or orchestrator)  
**Depends on:** All other WPs  

**Files owned:**
- Integration coordination (no single file owner)
- `docs/redesign-completion-report.md` (summary of changes)

**Files read-only:**
- All project files (verification only)

**Deliverables:**
1. **Full build:** `dotnet build NinetyNine.sln -warnaserror` succeeds.
2. **All tests:** `dotnet test` — all 186+ prior tests + all new auth/web tests pass.
3. **No regressions:** compare test count before/after (should be +40–50 tests, 0 regressions).
4. **Docker build:** `docker build -t ninetynine-web .` succeeds.
5. **Quick smoke:** run locally, navigate to `/`, login with dev mock auth, play a game, verify dark theme.
6. **Completion report:** document all changes, new files, deleted files, breaking changes (none expected).

**Acceptance criteria:**
- `dotnet build NinetyNine.sln -warnaserror` succeeds (clean build, no warnings).
- All tests pass: `dotnet test` (186+ passing, 0 failing).
- Docker builds without errors.
- Smoke test succeeds: app runs, pages load, dark theme is default.
- All 23 WPs are complete (no TODOs, no stubs).

**Estimated effort:** S (small, ~1 hour for final checks)

---

## 4. Execution Waves

### Wave 1: Foundation (Parallel, ~6 hours total)

**Rationale:** These three packages have no upstream dependencies. They establish the foundation for all subsequent work: the data model, email infrastructure, and visual design tokens.

**Packages:**
- WP-01: Player Model Auth Fields (S, ~1h)
- WP-02: Email Sender Interface & Impls (M, ~2h)
- WP-03: Base CSS Theme (M, ~2.5h)

**Integration checkpoint:** After Wave 1 merges, the codebase should build cleanly, and the theme tokens should be ready for consumption. No code should be broken.

---

### Wave 2: Auth Backend + Early Layout (Parallel, ~8 hours total)

**Rationale:** These packages depend on Wave 1 but can run in parallel. They implement the auth services and backend, plus redesign the layout components that all subsequent pages will use.

**Packages:**
- WP-04: Auth Repository Updates (S, ~1.5h)
- WP-05: Auth Services (L, ~4.5h)
- WP-06: MainLayout + NavMenu Redesign (M, ~2.5h)
- WP-07: Auth Page Scaffolds (S, ~1.5h)

**Integration checkpoint:** After Wave 2 merges, the auth backend is fully functional, repository indexes are in place, and all layout components are ready. Auth pages exist but have no logic yet (blocked on WP-05, which is also in this wave — stagger if parallelism causes conflicts).

---

### Wave 3: Auth Page Implementation + Tests (Parallel, ~7 hours total)

**Rationale:** These packages implement the auth pages and test the auth pipeline end-to-end. WP-10 can run in parallel with WP-08/WP-09 if needed.

**Packages:**
- WP-08: Register & Login Pages (M, ~2.5h)
- WP-09: Email Verify & Password Reset Pages (M, ~2.5h)
- WP-10: Auth Tests (M, ~3h)
- WP-11: Dev Mock Auth Update (S, ~1h)

**Integration checkpoint:** After Wave 3 merges, the full auth flow is complete and tested. Users can register, verify, login, reset passwords. Dev mock auth still works for UX prototyping.

---

### Wave 4: Scoring Grid + Game Pages (Parallel, ~7 hours total)

**Rationale:** ScoreCardGrid design (WP-12) is foundational for all game pages. Pages can be built in parallel.

**Packages:**
- WP-12: ScoreCardGrid Redesign (M, ~2h)
- WP-13: Game Pages (L, ~4h)
- WP-14: Shared Components Redesign (M, ~2h)
- WP-15: Game Pages Dark Theme Styling (S, ~1h)

**Integration checkpoint:** After Wave 4 merges, users can create games, enter scores, and view game history. Scoring grid is visually redesigned and responsive.

---

### Wave 5: Profile, Venue, Stats + Home (Parallel, ~7 hours total)

**Rationale:** These pages are independent of each other and mostly UI/display logic. Can run fully in parallel.

**Packages:**
- WP-16: Player Pages (M, ~2h)
- WP-17: Venue Pages (S, ~1.5h)
- WP-18: Stats Pages (M, ~2h)
- WP-19: Home Page Redesign (S, ~1.5h)

**Integration checkpoint:** After Wave 5 merges, all core pages are complete and styled with the dark theme.

---

### Wave 6: Testing + Polish (Parallel, ~5 hours total)

**Rationale:** Component tests, accessibility audit, and smoke tests can run in parallel once all pages exist.

**Packages:**
- WP-20: Web Component Tests (M, ~2h)
- WP-21: Accessibility Audit (M, ~2h)
- WP-22: Cross-Browser Smoke Test (S, ~1h)
- WP-23: Final Build & Integration Pass (S, ~1h)

**Integration checkpoint:** Final checkpoint. All tests pass, all pages accessible, no regressions, build clean.

---

## 5. Integration Protocol

### Between-Wave Coordination

1. **Wave completion criteria:**
   - All packages in the wave compile cleanly (`dotnet build NinetyNine.sln -warnaserror` succeeds).
   - All tests run (both prior tests and new tests); no regressions.
   - No `TODO` or stub code left in the wave.

2. **Merge into `master`:**
   - After each wave passes its criteria, all packages are merged into `master`.
   - Each package has a dedicated commit or PR (agents do not force-push; maintain linear history).
   - Commit message: `<WP-ID>: <short description>` (e.g., `WP-01: Add email/password auth fields to Player model`).

3. **CI/CD gate:**
   - Before a wave can be merged, the orchestrator runs:
     - `dotnet build NinetyNine.sln -warnaserror`
     - `dotnet test`
     - Verify no test regressions (count prior tests, count after, difference should be positive or zero)
   - If the build fails, the wave is **blocked**. The responsible agent fixes the issue and re-submits.

4. **Known files at wave boundaries:**
   - **Wave 1 → Wave 2 trigger:** WP-01, WP-02, WP-03 all merged, `Player.cs` has new fields, theme.css exists, email senders are registered.
   - **Wave 2 → Wave 3 trigger:** Auth services and repositories are in place; pages can now consume them.
   - **Wave 3 → Wave 4 trigger:** Auth pages are complete; game pages can reference the authenticated Player context.
   - **Wave 4 → Wave 5 trigger:** ScoreCardGrid is finalized; profile/stats/venue pages can reference it or integrate with game data.
   - **Wave 5 → Wave 6 trigger:** All pages are built; testing and polish can proceed.

### Escalation Path

- **If a package fails to build or test:** The responsible agent flags the orchestrator immediately. The orchestrator:
  1. Identifies the blocker (missing interface, broken reference, test failure).
  2. Determines which WP introduced the issue.
  3. Either: (a) asks the responsible agent to fix it, or (b) if it's a cross-boundary issue, escalates to the orchestrator to coordinate with multiple agents.
  4. Once fixed, the package is re-submitted and re-tested.

- **If a package breaks prior tests:** This is a regression. The agent must either:
  1. Fix the code to not break the prior test, OR
  2. Update the prior test with a clear rationale (e.g., "LoginPath changed from /login to /auth/login").
  3. Document the change in the PR.

- **If a package is blocked waiting for another package:** The blocked package is deferred to the next wave or sub-wave. The orchestrator ensures the dependency chain is honored.

---

## 6. Risk Register

| # | Risk | Severity | Likelihood | Mitigation |
|---|---|---|---|---|
| 1 | **LinkedIdentity removal cascades to unexpected places.** The `LinkedIdentity` class is used in OAuth handlers, ExternalLoginHandler, and possibly serialized data in the database. Removing it without checking all references causes compile errors or runtime failures. | M | M | Before WP-01 merges, do a codebase grep for `LinkedIdentity` to find all references. Update all references (ExternalLoginHandler, any deserialization code, tests) in WP-01 or WP-05. If data in MongoDB contains LinkedIdentity arrays, the BSON mapper must be updated to ignore them (set `BsonIgnore` attribute). |
| 2 | **CSS variable name collisions with Bootstrap.** Bootstrap 5 uses `--bs-*` custom properties extensively. If WP-03 accidentally uses the same names or doesn't override Bootstrap defaults correctly, some components stay light-themed even in dark mode. | M | M | WP-03 must explicitly test every Bootstrap component (button, form-control, alert, card, dropdown, modal) in both light and dark themes using the browser DevTools. Create a simple test page (`/dev/theme-test`) that renders all Bootstrap components and verify colors. |
| 3 | **Email sender integration fails in production.** MailKit requires correct SMTP credentials, TLS negotiation, and proper error handling. If the SMTP configuration in production is wrong, verification emails won't send, breaking the auth flow. | H | M | WP-02 must include comprehensive error handling (catch `SmtpCommandException`, `SmtpProtocolException`, `SslHandshakeException`) and re-throw with user-friendly messages. Create a test SMTP server (use a free service like Mailtrap or a local mock) to verify the flow end-to-end before production deploy. Document the SMTP configuration required in `docs/deployment.md`. |
| 4 | **Auth service timing attacks leak user enumeration.** Even with constant-time password hashing, the difference between "email not found" (fast query) and "email found but wrong password" (hash computation) can leak info. An attacker can measure response times to determine if an email is registered. | M | M | WP-05 must perform a dummy hash computation even if the email is not found (compute on a fixed dummy password, discard result). This ensures the response time is constant regardless of whether the email exists. Use `PasswordHasher<Player>.VerifyHashedPassword(dummyPlayer, "", "dummy")` on the error path. |
| 5 | **Responsive design breaks on specific device sizes or between WPs.** If one WP uses `768px` as the mobile breakpoint and another uses `640px`, the layout is inconsistent. Mobile users experience jumps or misalignment. | M | L | Define the responsive breakpoints globally in `theme.css` as CSS custom properties: `--nn-breakpoint-mobile: 640px`, `--nn-breakpoint-tablet: 768px`, `--nn-breakpoint-desktop: 1024px`. All WPs must use these tokens in their media queries. Create a mobile test checklist (WP-22) that verifies consistency across pages. |

---

## 7. Definition of Done for the Redesign

The redesign is **complete** when all of the following are true:

### Functional (Auth Refactor)

- [ ] Player model has all 9 new auth fields (EmailAddress, PasswordHash, EmailVerified, tokens, lockout, LastLoginAt, FailedLoginAttempts).
- [ ] LinkedIdentity list is removed and no references remain in the codebase.
- [ ] All six auth flows work end-to-end: register, login, verify email, forgot password, reset password, resend verification.
- [ ] Email sending is integrated: MailKitEmailSender (prod), ConsoleEmailSender (dev), MockEmailSender (tests).
- [ ] Dev mock auth (`/mock/signin-as`) still works; test players have EmailVerified = true and password = "Test1234!".
- [ ] All auth endpoints are rate-limited (10 req/min per IP).
- [ ] No user enumeration in auth flows (generic error messages, constant-time operations).
- [ ] Account lockout after 5 failed attempts; clears after 15 minutes.
- [ ] Password validation: ≥ 10 chars, uppercase, lowercase, digit, symbol.

### Visual (Dark-First UI Redesign)

- [ ] Dark theme is the DEFAULT (not a toggle). Light mode is secondary.
- [ ] Base theme tokens defined in `theme.css`: `--nn-bg-primary`, `--nn-bg-secondary`, `--nn-accent-teal`, `--nn-accent-gold`, `--nn-text-*`, semantic colors.
- [ ] All 17+ pages updated to dark theme: Home, Login, Register, VerifyEmail, ForgotPassword, ResetPassword, ResendVerification, Games/New, Games/Play, Games/List, Games/Details, Players/Profile, Players/EditProfile, Venues/List, Venues/Edit, Stats/Leaderboard, Stats/MyStats.
- [ ] MainLayout + NavMenu redesigned: left-side persistent navigation, responsive collapse on mobile.
- [ ] ScoreCardGrid redesigned: 9-column grid, dark theme, responsive single-frame view on mobile, ARIA labels.
- [ ] All shared components (Avatar, PlayerBadge, VisibilityToggle, FrameCell, etc.) styled with dark theme.
- [ ] Phosphor icons (42 regular + 9 fill) integrated; all icon references use the correct SVG paths.
- [ ] Billiards photography (6 photos from Pexels) in place for hero backgrounds / empty states (attribution in `img/billiards/ATTRIBUTION.md`).
- [ ] No pool-specific icon gaps left unfilled (if needed, document in `icons/README.md` as v2 future work).

### Quality & Testing

- [ ] **Build:** `dotnet build NinetyNine.sln -warnaserror` succeeds (clean, no warnings).
- [ ] **Tests:** All 186+ prior tests still pass (zero regressions).
- [ ] **New tests:** +40–50 new tests in Model, Repository, Services, Web layers; all passing.
  - Model tests: Player auth fields, serialization.
  - Repo tests: email indexes, case-insensitive lookups, token queries.
  - Services tests: register/login/verify/reset flows, lockout, no enumeration.
  - Web tests: auth pages, scorecard grid, shared components.
- [ ] **Accessibility:** WCAG 2.2 AA compliant.
  - Keyboard navigation works on all pages.
  - `:focus-visible` outlines present on all interactive elements.
  - Contrast ratios ≥ 4.5:1 for text on backgrounds.
  - Form labels explicit (no placeholder-only).
  - Touch targets ≥ 44×44px.
  - ARIA labels on scorecard cells.
  - Reduced-motion respected.
- [ ] **Responsive:** All pages functional and styled correctly on mobile (<768px), tablet (768–1024px), and desktop (>1024px).
- [ ] **Cross-browser:** Smoke test passes on Chrome, Firefox, Safari (manual or automated).

### Documentation & Housekeeping

- [ ] `docs/redesign-plan.md` completed (this document).
- [ ] `docs/accessibility-audit.md` completed with findings and fixes.
- [ ] `docs/deployment.md` updated with email/SMTP configuration notes.
- [ ] No TODOs or FIXMEs left in code (or all TODOs have a clear WP reference and post-v1 justification).
- [ ] All new files have appropriate XML doc comments (C# classes, methods, properties).
- [ ] `.env.example` updated to include Email configuration settings (SMTP host, port, from-address).
- [ ] `archive/pre-blazor-rewrite` branch untouched.

### Final Smoke Test

- [ ] Application runs locally via `dotnet run` or Docker Compose.
- [ ] Home page loads at `/` with dark theme.
- [ ] User can register at `/register` (form visible, submit works).
- [ ] User can login at `/login` (form visible, submit works with valid credentials).
- [ ] Authenticated user can navigate to `/games/new`, create a game, and play it.
- [ ] ScoreCardGrid displays 9 frames correctly, responsive on mobile.
- [ ] User can view `/stats` leaderboard and personal stats.
- [ ] User can edit profile at `/players/me`.
- [ ] Dev mock auth works: `/mock/signin-as?displayName=carey` signs in as test user.
- [ ] No JavaScript errors in browser console.
- [ ] No broken images or missing icons.

---

## 8. Immediate Next Steps for the Orchestrator

### First Actions (Day 1)

1. **Spawn Wave 1 agents in parallel:**
   - **Agent 1 (Backend Developer):** WP-01 (Player Model Auth Fields)
     - Prompt: "Expand the Player model with email/password auth fields as specified in architecture.md §7.2. Remove LinkedIdentity list. Ensure model serializes cleanly. Build must pass with no warnings. Do a codebase grep for 'LinkedIdentity' first and include findings in your report."
   - **Agent 2 (Backend Developer or API Security specialist):** WP-02 (Email Sender)
     - Prompt: "Implement IEmailSender interface with three implementations (MailKit, Console, Mock). Create EmailSettings config class. Add MailKit NuGet package. All implementations must compile and have XML docs. No external web requests except SMTP. Build must pass."
   - **Agent 3 (UI Designer or Frontend Developer):** WP-03 (CSS Theme)
     - Prompt: "Rewrite theme.css from scratch with dark-first palette: deep neutral backgrounds (not pure black), pool-accent teals/greens, warm gold accents. Define CSS custom properties (--nn-bg-*, --nn-text-*, --nn-accent-*). Update app.css and scorecard.css to use new tokens. Ensure contrast ratios ≥ 4.5:1 (WCAG AA). Test with actual components (buttons, forms, inputs). Build must pass."

2. **Run integration build after all three agents complete (or in parallel every 30min):**
   - `dotnet build NinetyNine.sln -warnaserror`
   - `dotnet test` (should not increase test count yet, all prior tests must still pass)
   - If build fails, **immediately** identify which agent's code caused it and escalate.

3. **Prepare Wave 2 agents (do not spawn yet):**
   - Identify the four agents who will handle WP-04, WP-05, WP-06, WP-07.
   - Share the Wave 1 completion state (merged code, passing build) with them once Wave 1 is done.

### Wave 2 Trigger (Once Wave 1 is Merged)

4. **Spawn Wave 2 agents in parallel:**
   - **Agent 4 (Backend Developer):** WP-04 (Auth Repository Updates)
     - Prompt: "Update BsonConfiguration.cs to add unique index on lowercase emailAddress, sparse indexes on verification/reset tokens. Add new methods to IPlayerRepository (GetByEmailAsync, GetByEmailVerificationTokenAsync, GetByPasswordResetTokenAsync, EmailExistsAsync). Implement in PlayerRepository. Ensure case-insensitive email queries. Build must pass, tests will verify in WP-10."
   - **Agent 5 (Backend Developer or API Security specialist):** WP-05 (Auth Services)
     - Prompt: "Implement IAuthService with register, login, verify, reset, resend flows. Include PasswordValidator class (≥10 chars, uppercase, lowercase, digit, symbol). Rewrite AuthEndpoints.cs to remove Google OAuth and add new email/password endpoints. Update Program.cs to register auth services + email sender. No OAuth references remain. All flows must prevent user enumeration (constant-time operations, generic errors). Build must pass."
   - **Agent 6 (UI Designer):** WP-06 (MainLayout + NavMenu Redesign)
     - Prompt: "Redesign MainLayout.razor with left-side persistent navigation (80–120px wide). NavMenu.razor with nav links + Phosphor icons (house, plus, list, chart-bar, map-pin, user). UserMenu.razor with avatar + dropdown. Responsive: sidebar collapses on mobile (<768px). Dark theme using tokens from theme.css. Add scoped CSS for each component. Active link styling with teal accent. Build must pass, pages will reference this layout."
   - **Agent 7 (Frontend Developer):** WP-07 (Auth Page Scaffolds)
     - Prompt: "Create empty page scaffolds for /register, /login, /verify-email, /forgot-password, /reset-password, /resend-verification. Each has form structure (labels + inputs) and empty @code block (inject IAuthService, declare variables). No logic yet. All pages use dark theme via MainLayout. Build must pass."

### Rhythm Going Forward

- After each wave merges, commit a summary to the repo (or update this plan with completion timestamp).
- Monitor build status continuously (orchestrator should be notified if any agent's code fails the build).
- Do not move to the next wave until the current wave is 100% merged and tested.

---

## 9. Summary of Key Decisions

### Architecture

- **Player model:** 9 new auth fields, `LinkedIdentity` list removed.
- **Email sending:** Three implementations (MailKit/Console/Mock), configurable per environment.
- **Auth flows:** All six flows (register, login, verify, forgot, reset, resend) follow the same error-message pattern (generic, no enumeration).
- **Password hashing:** `PasswordHasher<T>` from `Microsoft.AspNetCore.Identity` (PBKDF2, default iteration count).
- **Account lockout:** 5 failed attempts → 15-minute lockout.
- **Dev mode:** `/mock/signin-as` bypasses email/password, test players have known credentials.

### Visual Design

- **Dark theme is primary:** All design decisions made dark-first, light mode secondary.
- **Color palette:** deep neutrals, pool-accent teals/greens, warm gold accents.
- **Icons:** Phosphor (42 regular + 9 fill), pool-specific icons deferred or sourced from Commons if available.
- **Photography:** 6 billiards photos from Pexels (no attribution required).
- **Layout:** left-side persistent navigation (loosely inspired by Avalonia NavigationView, not a replica).
- **Accessibility:** WCAG 2.2 AA compliant (contrast, keyboard nav, ARIA labels, focus-visible, ≥44px touch targets).

### Testing

- **Model tests:** Player auth fields, serialization.
- **Repo tests:** email indexes, case-insensitive lookups, token queries.
- **Services tests:** auth flows, lockout, no enumeration, password validation.
- **Web tests (bUnit):** auth pages, scorecard grid, shared components.
- **All prior 186 tests must still pass** (zero regressions).

---

## 10. Files Referenced in This Plan

**New files to be created:**
- `src/NinetyNine.Web/Auth/EmailSender/IEmailSender.cs`
- `src/NinetyNine.Web/Auth/EmailSender/MailKitEmailSender.cs`
- `src/NinetyNine.Web/Auth/EmailSender/ConsoleEmailSender.cs`
- `src/NinetyNine.Web/Auth/EmailSender/MockEmailSender.cs`
- `src/NinetyNine.Web/Auth/EmailSettings.cs`
- `src/NinetyNine.Services/Auth/IAuthService.cs`
- `src/NinetyNine.Services/Auth/AuthService.cs`
- `src/NinetyNine.Services/Auth/PasswordValidator.cs`
- `src/NinetyNine.Web/Components/Pages/VerifyEmail.razor`
- `src/NinetyNine.Web/Components/Pages/ForgotPassword.razor`
- `src/NinetyNine.Web/Components/Pages/ResetPassword.razor`
- `src/NinetyNine.Web/Components/Pages/ResendVerification.razor`
- (+ all `.razor.css` scoped CSS files for each page/component)
- `tests/NinetyNine.Model.Tests/PlayerAuthTests.cs`
- `tests/NinetyNine.Repository.Tests/PlayerAuthRepositoryTests.cs`
- `tests/NinetyNine.Services.Tests/AuthServiceTests.cs`
- `tests/NinetyNine.Services.Tests/PasswordValidatorTests.cs`
- `docs/accessibility-audit.md`
- `docs/smoke-test-checklist.md`
- `docs/redesign-completion-report.md`

**Files to be significantly modified:**
- `src/NinetyNine.Model/Player.cs` (add auth fields, remove LinkedIdentity)
- `src/NinetyNine.Web/wwwroot/css/theme.css` (rewrite, dark-first)
- `src/NinetyNine.Web/wwwroot/css/app.css` (rewrite, dark-first)
- `src/NinetyNine.Web/wwwroot/css/scorecard.css` (update with new tokens)
- `src/NinetyNine.Web/Components/Layout/MainLayout.razor` (redesign)
- `src/NinetyNine.Web/Components/Layout/NavMenu.razor` (redesign)
- `src/NinetyNine.Web/Components/Layout/UserMenu.razor` (redesign)
- `src/NinetyNine.Web/Components/Pages/Register.razor` (rewrite)
- `src/NinetyNine.Web/Components/Pages/Login.razor` (rewrite)
- `src/NinetyNine.Web/Components/Pages/Home.razor` (redesign)
- All other pages in `Components/Pages/` (dark theme update)
- All shared components in `Components/Shared/` (dark theme update)
- `src/NinetyNine.Web/Auth/AuthEndpoints.cs` (remove Google, add email/password)
- `src/NinetyNine.Web/Auth/MockAuthEndpoints.cs` (update for new auth fields)
- `src/NinetyNine.Web/Program.cs` (remove Google auth, register auth services)
- `src/NinetyNine.Repository/BsonConfiguration.cs` (add email indexes)
- `src/NinetyNine.Repository/PlayerRepository.cs` (add email-related methods)
- `src/NinetyNine.Web/NinetyNine.Web.csproj` (add MailKit package)

**Files to remain unchanged (safety net):**
- `archive/pre-blazor-rewrite` branch (locked, not touched)
- All core domain logic files (Game, Frame, Venue domain in `Model/`)
- All Repository interfaces and implementations except PlayerRepository
- All Service interfaces and implementations except Auth-related additions
- CI/CD workflows (unchanged, will inherit the new test counts)

---

**End of Redesign Plan**
