# NinetyNine — Architecture Blueprint

Single source of truth for the Blazor + MongoDB rewrite. All implementation agents must read and follow this document.

## 1. Vision

A web-based scorekeeper for the pool game Ninety-Nine (P&B RPI rules, see [../README.md](../README.md)). Users log in with a third-party provider, pick a display name, and record frame-by-frame scores in a UI that visually mirrors the official P&B score card they're already familiar with. Stats, leaderboards, and game history are available per player. Deployed to an Azure Linux VM via GitHub Actions; local development uses Docker Compose for dev-prod parity.

## 2. Locked Decisions

| Area | Choice |
|---|---|
| Target framework | .NET 8 |
| UI framework | Blazor Web App (Server interactive render mode) |
| Database (prod) | MongoDB Atlas free tier |
| Database (dev) | MongoDB 7 container with named volume |
| ORM/Driver | `MongoDB.Driver` 3.x, no EF Core |
| Authentication | External OAuth/OIDC only; cookie auth post-login; no passwords |
| v1 OAuth providers | Google only (Discord/Telegram deferred) |
| Avatar storage | GridFS (in the same Mongo database) |
| Image processing | `SixLabors.ImageSharp` |
| Testing | xUnit + Testcontainers (real Mongo) + bUnit (Blazor components) |
| Container registry | GitHub Container Registry (GHCR) |
| Compute target | Azure Linux VM running Docker Compose |
| Secrets | GitHub Actions secrets → `.env` on deploy; Key Vault deferred |
| Observability (v1) | Structured stdout logs collected via `docker logs`; App Insights deferred |
| TLS/domain | HTTP only on VM hostname for v1; Caddy/Let's Encrypt when domain is acquired |
| Multi-player | Single-player `Game` aggregate v1; `Match` aggregate deferred |
| DisplayName | Unique across the site |
| Privacy model | Per-field Public/Private visibility flags on `Player` |
| Game behavior | Lives on the `Game` entity for v1 (extractable later) |

## 3. Target Project Structure

```
NinetyNine/
├── .github/
│   └── workflows/
│       ├── ci.yml                  # build, test, lint on PR + push
│       └── deploy.yml              # build & push images to GHCR, SSH deploy to VM
├── .env.example                    # committed template; .env itself is gitignored
├── deploy.sh                       # local one-shot stand-up / teardown utility
├── docker-compose.yml              # prod (Azure VM): web app container only, connects to Atlas
├── docker-compose.dev.yml          # dev: web app + local mongo + mongo-express
├── Dockerfile                      # multi-stage: sdk-build → runtime
├── NinetyNine.sln
├── src/
│   ├── NinetyNine.Model/           # pure domain entities, no external deps
│   ├── NinetyNine.Repository/      # MongoDB.Driver, BSON class maps, GridFS, repositories
│   ├── NinetyNine.Services/        # domain services: scoring, stats, player, venue, avatar
│   └── NinetyNine.Web/             # Blazor Web App (hosts UI + auth endpoints)
├── tests/
│   ├── NinetyNine.Model.Tests/
│   ├── NinetyNine.Repository.Tests/  # integration: testcontainers + real mongo
│   ├── NinetyNine.Services.Tests/
│   └── NinetyNine.Web.Tests/         # bUnit
└── docs/
    ├── architecture.md             # THIS FILE
    ├── local-dev.md
    ├── deployment.md
    └── (existing P&B reference material)
```

### Project dependency graph

```
Model  ← Repository ← Services ← Web
                              ↖─── Repository
```

No circular references. `Model` has zero external dependencies (no BSON attributes, no MongoDB types).

## 4. Domain Model (`NinetyNine.Model`)

All entities use `System.Text.Json` for serialization. No `INotifyPropertyChanged` (Avalonia artifact, dropped). No attributes beyond `System.ComponentModel.DataAnnotations` for validation.

### 4.1 `Player`

```csharp
public class Player
{
    public Guid PlayerId { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = "";

    // Optional, user-supplied, user-controlled visibility
    public string? EmailAddress { get; set; }
    public string? PhoneNumber { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }

    public ProfileVisibility Visibility { get; set; } = new();
    public AvatarRef? Avatar { get; set; }

    public List<LinkedIdentity> LinkedIdentities { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ProfileVisibility
{
    public bool EmailAddress { get; set; } = false;
    public bool PhoneNumber { get; set; } = false;
    public bool RealName { get; set; } = false;   // controls First/Middle/Last together
    public bool Avatar { get; set; } = true;      // default visible
}

public class LinkedIdentity
{
    public string Provider { get; set; } = "";        // "Google"
    public string ProviderUserId { get; set; } = "";  // OIDC sub claim
    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;
}

public class AvatarRef
{
    public string StorageKey { get; set; } = "";   // GridFS ObjectId string
    public string ContentType { get; set; } = "";
    public int WidthPx { get; set; }
    public int HeightPx { get; set; }
    public long SizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
```

### 4.2 `Venue`

```csharp
public class Venue
{
    public Guid VenueId { get; set; } = Guid.NewGuid();
    public bool Private { get; set; }
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
}
```

### 4.3 `Game` and `Frame`

```csharp
public enum GameState { NotStarted, InProgress, Completed, Paused }

public enum TableSize { Unknown = 0, SixFoot = 6, SevenFoot = 7, NineFoot = 9, TenFoot = 10 }

public class Game
{
    public Guid GameId { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public Guid VenueId { get; set; }
    public DateTime WhenPlayed { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public TableSize TableSize { get; set; } = TableSize.Unknown;
    public GameState GameState { get; set; } = GameState.NotStarted;
    public int CurrentFrameNumber { get; set; } = 1;
    public List<Frame> Frames { get; set; } = new(9);
    public string? Notes { get; set; }

    // Computed (BsonIgnore in class map)
    public int TotalScore => Frames.Where(f => f.IsCompleted).Sum(f => f.FrameScore);
    public int RunningTotal => Frames.LastOrDefault(f => f.IsCompleted)?.RunningTotal ?? 0;
    public bool IsInProgress => GameState == GameState.InProgress;
    public bool IsCompleted => GameState == GameState.Completed;
    public int CompletedFrames => Frames.Count(f => f.IsCompleted);
    public Frame? CurrentFrame => Frames.FirstOrDefault(f => f.IsActive);
    public double AverageScore => CompletedFrames > 0 ? (double)TotalScore / CompletedFrames : 0;
    public Frame? BestFrame => Frames.Where(f => f.IsCompleted).OrderByDescending(f => f.FrameScore).FirstOrDefault();
    public int PerfectFrames => Frames.Count(f => f.IsPerfectFrame);
    public bool IsPerfectGame => IsCompleted && TotalScore == 99;

    // Behavior (v1: on entity)
    public void InitializeFrames();
    public bool AdvanceToNextFrame();
    public void CompleteCurrentFrame(int breakBonus, int ballCount, string? notes = null);
    public bool ValidateGame();
}

public class Frame
{
    public Guid FrameId { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public int FrameNumber { get; set; }          // 1-9
    public int BreakBonus { get; set; }           // 0 or 1
    public int BallCount { get; set; }            // 0-10 (9-ball counts as 2)
    public int RunningTotal { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }

    // Computed
    public int FrameScore => BreakBonus + BallCount;
    public bool IsValidScore => FrameScore <= 11;
    public bool IsPerfectFrame => FrameScore == 11;

    public bool ValidateFrame();
    public void CompleteFrame(int previousRunningTotal = 0);
    public void ResetFrame();
}
```

**Invariants enforced by behavior**:
- `BreakBonus` ∈ {0, 1}
- `BallCount` ∈ [0, 10]
- `FrameScore` ≤ 11
- `FrameNumber` ∈ [1, 9]
- Exactly 9 frames per game
- Only one `IsActive` frame at a time

## 5. MongoDB Strategy (`NinetyNine.Repository`)

### 5.1 Collections

| Collection | Document | Notes |
|---|---|---|
| `players` | `Player` | Unique index on `displayName`; unique compound index on `linkedIdentities.provider + linkedIdentities.providerUserId` |
| `venues` | `Venue` | Index on `name` |
| `games` | `Game` | **Frames embedded as array inside the Game document.** Indexes on `playerId`, `whenPlayed desc`, `gameState` |

GridFS uses default buckets: `fs.files` and `fs.chunks`. All avatars stored there.

### 5.2 BSON Configuration

All BSON class maps registered **externally** in `NinetyNine.Repository/BsonConfiguration.cs` so the `Model` project stays dependency-free. Registration is idempotent and called once at startup from `Web/Program.cs`.

Required configuration:
- **Guid representation**: `GuidRepresentation.Standard` globally
- **Guids serialize as strings** via `[BsonRepresentation(BsonType.String)]` (set via class map, not attributes) for readability in `mongo` shell
- **Enums serialize as strings** (`GameState`, `TableSize`) via convention pack
- **`BsonIgnore` on computed properties**: `TotalScore`, `RunningTotal`, `IsInProgress`, `IsCompleted`, `CompletedFrames`, `CurrentFrame`, `AverageScore`, `BestFrame`, `PerfectFrames`, `IsPerfectGame` on `Game`; `FrameScore`, `IsValidScore`, `IsPerfectFrame` on `Frame`
- **`_id` mapping**: `PlayerId`/`VenueId`/`GameId`/`FrameId` mapped to `_id` via `SetIdMember`
- **Camel-case field names** via `CamelCaseElementNameConvention`

### 5.3 Settings + DI

```csharp
public class MongoDbSettings
{
    public string ConnectionString { get; set; } = "";
    public string DatabaseName { get; set; } = "NinetyNine";
}
```

Registered via options pattern in `Web/Program.cs`:
```csharp
services.Configure<MongoDbSettings>(config.GetSection("MongoDb"));
services.AddSingleton<IMongoClient>(sp =>
    new MongoClient(sp.GetRequiredService<IOptions<MongoDbSettings>>().Value.ConnectionString));
services.AddScoped<INinetyNineDbContext, NinetyNineDbContext>();
services.AddScoped<IPlayerRepository, PlayerRepository>();
services.AddScoped<IVenueRepository, VenueRepository>();
services.AddScoped<IGameRepository, GameRepository>();
services.AddScoped<IAvatarStore, GridFsAvatarStore>();
```

### 5.4 Repository contracts (authoritative signatures)

```csharp
public interface INinetyNineDbContext
{
    IMongoCollection<Player> Players { get; }
    IMongoCollection<Venue> Venues { get; }
    IMongoCollection<Game> Games { get; }
    IMongoDatabase Database { get; }
}

public interface IPlayerRepository
{
    Task<Player?> GetByIdAsync(Guid playerId, CancellationToken ct = default);
    Task<Player?> GetByDisplayNameAsync(string displayName, CancellationToken ct = default);
    Task<Player?> GetByLinkedIdentityAsync(string provider, string providerUserId, CancellationToken ct = default);
    Task<bool> DisplayNameExistsAsync(string displayName, CancellationToken ct = default);
    Task<IReadOnlyList<Player>> SearchAsync(string query, int limit, CancellationToken ct = default);
    Task CreateAsync(Player player, CancellationToken ct = default);
    Task UpdateAsync(Player player, CancellationToken ct = default);
    Task DeleteAsync(Guid playerId, CancellationToken ct = default);
}

public interface IVenueRepository
{
    Task<Venue?> GetByIdAsync(Guid venueId, CancellationToken ct = default);
    Task<IReadOnlyList<Venue>> GetAllAsync(bool includePrivate, CancellationToken ct = default);
    Task CreateAsync(Venue venue, CancellationToken ct = default);
    Task UpdateAsync(Venue venue, CancellationToken ct = default);
    Task DeleteAsync(Guid venueId, CancellationToken ct = default);
}

public interface IGameRepository
{
    Task<Game?> GetByIdAsync(Guid gameId, CancellationToken ct = default);
    Task<IReadOnlyList<Game>> GetByPlayerAsync(Guid playerId, int skip, int limit, CancellationToken ct = default);
    Task<IReadOnlyList<Game>> GetRecentAsync(int limit, CancellationToken ct = default);
    Task<IReadOnlyList<Game>> GetCompletedByPlayerAsync(Guid playerId, CancellationToken ct = default);
    Task<Game?> GetActiveForPlayerAsync(Guid playerId, CancellationToken ct = default);
    Task CreateAsync(Game game, CancellationToken ct = default);
    Task UpdateAsync(Game game, CancellationToken ct = default);
    Task DeleteAsync(Guid gameId, CancellationToken ct = default);
}

public interface IAvatarStore
{
    Task<string> UploadAsync(Stream content, string contentType, string filename, CancellationToken ct = default);
    Task<(Stream content, string contentType)?> DownloadAsync(string storageKey, CancellationToken ct = default);
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}
```

## 6. Service Layer (`NinetyNine.Services`)

Thin orchestration over repositories + domain logic.

```csharp
public interface IGameService
{
    Task<Game> StartNewGameAsync(Guid playerId, Guid venueId, TableSize tableSize, CancellationToken ct = default);
    Task<Game> RecordFrameAsync(Guid gameId, int frameNumber, int breakBonus, int ballCount, string? notes, CancellationToken ct = default);
    Task<Game> ResetFrameAsync(Guid gameId, int frameNumber, CancellationToken ct = default);
    Task<Game> CompleteGameAsync(Guid gameId, CancellationToken ct = default);
    Task<Game?> GetGameAsync(Guid gameId, CancellationToken ct = default);
}

public interface IPlayerService
{
    Task<Player> RegisterAsync(string displayName, string provider, string providerUserId, CancellationToken ct = default);
    Task<Player?> LoginAsync(string provider, string providerUserId, CancellationToken ct = default);
    Task<Player> UpdateProfileAsync(Guid playerId, PlayerProfileUpdate update, CancellationToken ct = default);
    Task<bool> IsDisplayNameAvailableAsync(string displayName, CancellationToken ct = default);
    Task SetAvatarAsync(Guid playerId, Stream imageContent, string contentType, CancellationToken ct = default);
    Task RemoveAvatarAsync(Guid playerId, CancellationToken ct = default);
}

public interface IVenueService
{
    Task<Venue> CreateAsync(Venue venue, CancellationToken ct = default);
    Task<Venue?> GetAsync(Guid venueId, CancellationToken ct = default);
    Task<IReadOnlyList<Venue>> ListAsync(bool includePrivate, CancellationToken ct = default);
    Task<Venue> UpdateAsync(Venue venue, CancellationToken ct = default);
    Task DeleteAsync(Guid venueId, CancellationToken ct = default);
}

public interface IStatisticsService
{
    Task<PlayerStats> GetPlayerStatsAsync(Guid playerId, CancellationToken ct = default);
    Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(int limit, CancellationToken ct = default);
    Task<IReadOnlyList<Game>> GetBestGamesAsync(Guid playerId, int limit, CancellationToken ct = default);
}

public record PlayerProfileUpdate(
    string? DisplayName,
    string? EmailAddress,
    string? PhoneNumber,
    string? FirstName,
    string? MiddleName,
    string? LastName,
    ProfileVisibility? Visibility);

public record PlayerStats(
    Guid PlayerId,
    int GamesPlayed,
    int GamesCompleted,
    double AverageScore,
    int BestScore,
    int PerfectGames,
    int PerfectFrames,
    DateTime? LastPlayed);

public record LeaderboardEntry(
    Guid PlayerId,
    string DisplayName,
    string? AvatarUrl,
    int GamesPlayed,
    double AverageScore,
    int BestScore);

public class AvatarService
{
    // Validates content-type, resizes to 512x512 max via ImageSharp,
    // uploads to IAvatarStore, updates Player.Avatar
}
```

Validation rules enforced at the service boundary:
- DisplayName: 2-32 chars, `[a-zA-Z0-9_-]`, unique
- Avatar upload: `image/png`, `image/jpeg`, or `image/webp`; max 2 MB pre-resize; resized to ≤512×512
- Frame scores: delegated to `Frame.ValidateFrame()`
- Only the game's owner can record frames (enforced in controller/page, PlayerId from auth cookie)

## 7. Authentication (`NinetyNine.Web/Auth/`)

**Third-party OAuth has been REMOVED.** The app uses classic email + password authentication with mandatory email verification. No Google, Discord, Telegram, or other external identity providers. User decision: cleaner UX, no external dependencies, full control over the identity store.

### 7.1 Packages

- `Microsoft.AspNetCore.Authentication.Cookies` (framework, cookie session)
- `Microsoft.AspNetCore.Identity` — used only for `PasswordHasher<T>` (PBKDF2, sensible defaults). The full Identity store / `UserManager` / `IdentityDbContext` are **not** used — we keep our own `Player` document in MongoDB and hash passwords ourselves.
- `MailKit` (or equivalent) — for SMTP email delivery. Configurable via `Email:*` settings.

Packages **removed** from the previous design:

- `Microsoft.AspNetCore.Authentication.Google`
- `AspNet.Security.OAuth.Discord` (never actually added)
- Telegram handler (never actually added)

### 7.2 Revised `Player` model fields

Additions required to support email/password auth:

```csharp
public class Player
{
    // ... existing fields (PlayerId, DisplayName, FirstName/MiddleName/LastName,
    //     PhoneNumber, Visibility, Avatar, CreatedAt) remain ...

    // CHANGED: EmailAddress is now REQUIRED and UNIQUE (was optional)
    public string EmailAddress { get; set; } = "";

    // NEW: password hash (PBKDF2 via Microsoft.AspNetCore.Identity.PasswordHasher)
    public string PasswordHash { get; set; } = "";

    // NEW: email verification state
    public bool EmailVerified { get; set; }
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationTokenExpiresAt { get; set; }

    // NEW: password reset state
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiresAt { get; set; }

    // NEW: activity tracking
    public DateTime? LastLoginAt { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedOutUntil { get; set; }

    // REMOVED: List<LinkedIdentity> LinkedIdentities
}
```

Indexes required on the `players` collection:

- Unique on `emailAddress` (lowercased at write time)
- Unique on `displayName`
- Sparse index on `emailVerificationToken` (lookups during verification)
- Sparse index on `passwordResetToken` (lookups during reset)

### 7.3 Configuration (Program.cs)

```csharp
services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Cookie.Name = "NinetyNine.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

services.AddScoped<IPasswordHasher<Player>, PasswordHasher<Player>>();
services.AddScoped<IEmailSender, MailKitEmailSender>();
services.Configure<EmailSettings>(config.GetSection("Email"));
```

### 7.4 Registration Flow

1. User fills `/register`: `EmailAddress`, `DisplayName`, `Password`, `ConfirmPassword`.
2. Server validates:
   - Email well-formed, not already taken (case-insensitive)
   - DisplayName 2–32 chars, `[a-zA-Z0-9_-]`, not taken
   - Password ≥ 10 chars, at least one of each: lowercase, uppercase, digit, symbol (configurable)
   - ConfirmPassword matches
3. `IPlayerService.RegisterAsync` creates the `Player` with `PasswordHash`, `EmailVerified = false`, a random `EmailVerificationToken` (32 bytes, URL-safe base64), and `EmailVerificationTokenExpiresAt = now + 24h`.
4. `IEmailSender.SendVerificationAsync` delivers an email with a link to `/verify-email?token={token}`.
5. User is shown a "Check your email" page. No session cookie is issued yet.
6. User clicks the email link → `/verify-email` validates the token, marks `EmailVerified = true`, clears the token, issues the auth cookie, redirects to `/`.

### 7.5 Login Flow

1. User fills `/login`: `EmailAddress`, `Password`.
2. Server loads the `Player` by email (case-insensitive).
3. If not found OR email not verified OR account locked out → generic "Invalid credentials" error (no user enumeration).
4. `PasswordHasher.VerifyHashedPassword` → if mismatch, increment `FailedLoginAttempts`; at 5 attempts lock for 15 minutes.
5. If match → reset `FailedLoginAttempts`, set `LastLoginAt`, issue the auth cookie with `PlayerId`/`DisplayName`/optional `avatar_url` claims, redirect to return URL or `/`.

### 7.6 Password Reset Flow

1. User fills `/forgot-password`: `EmailAddress`.
2. Server always returns "If that email exists, a reset link has been sent" (no enumeration).
3. If the email exists: generate `PasswordResetToken` (32 bytes URL-safe), store with `PasswordResetTokenExpiresAt = now + 1h`, send email with `/reset-password?token={token}`.
4. User clicks the link → `/reset-password` validates token, prompts for new password + confirmation, updates `PasswordHash`, clears the reset token, redirects to `/login`.

### 7.7 Verification Re-send

`/resend-verification` endpoint: accepts email, always returns generic success, re-emails the verification link if a matching unverified account exists. Rate-limited to 1 request per email per 5 minutes.

### 7.8 Email Sender

```csharp
public interface IEmailSender
{
    Task SendVerificationAsync(string toEmail, string displayName, string verifyUrl, CancellationToken ct);
    Task SendPasswordResetAsync(string toEmail, string displayName, string resetUrl, CancellationToken ct);
}
```

Implementations:

- **`MailKitEmailSender`** — production SMTP via MailKit, configured with SMTP host/port/username/password/from-address from `Email:*` settings.
- **`ConsoleEmailSender`** — dev-mode fallback that logs the email body and verification link to the console instead of sending. Registered when `Email:Provider == "Console"` or no SMTP config is present.
- **`MockEmailSender`** — test-mode implementation that stores sent emails in an in-memory list for assertion in integration tests.

### 7.9 Dev-mode mock login

The existing `/mock/signin-as` endpoint stays for UX prototyping. It bypasses the password/verification flow and issues a cookie directly for a seeded test player. Guarded by `Auth:Mock:Enabled` + `Environment.IsDevelopment()`. Seeded test players have `EmailVerified = true` and a known dev password (`Test1234!`) so the real email/password login also works against them.

### 7.10 Claims

Standard claims after login (unchanged):

- `ClaimTypes.NameIdentifier` = `PlayerId.ToString()`
- `ClaimTypes.Name` = `DisplayName`
- `ClaimNames.PlayerId` = `PlayerId.ToString()` (custom)
- `ClaimNames.AvatarUrl` = `/api/avatars/{playerId}` if avatar set

### 7.11 Authorization

- All game/profile/stats pages require authentication (`[Authorize]`)
- Public: landing page, leaderboard, public profile views, `/login`, `/register`, `/forgot-password`, `/reset-password`, `/verify-email`, `/resend-verification`
- Players can only modify their own data (enforced via `PlayerId` claim vs. resource ownership)

### 7.12 Rate limiting

The existing `auth` rate limit window (10 req/min per IP) applies to `/login`, `/register`, `/forgot-password`, `/resend-verification`, `/verify-email`, `/reset-password`.

## 8. Blazor UI (`NinetyNine.Web`)

### 8.1 Render mode

Blazor Web App template, **Server interactive** render mode globally. Interactive auto / WASM deferred — server keeps database access simple (direct repo injection, no separate API hop for the UI).

### 8.2 Visual direction

**The P&B score card photos in `docs/signal-*.jpeg` are historical artifacts from the 1990s, NOT the visual design target.** They're useful only as a reminder of the functional layout (9 frames × break-bonus/ball-count/running-total). Do not replicate their cream-paper background, italic serif "99" marks, diamond logo, or any other 90s print-on-paper aesthetic.

**Dark theme is the primary/default color scheme.** Light mode is a secondary toggle, not the default. All design decisions — palette, contrast, elevation — should be made dark-first.

**Layout pattern**: loosely inspired by the archived Avalonia implementation's `FluentAvalonia NavigationView` — a persistent left side navigation (Home / New Game / History / Stats / Venues / Profile) plus a main content pane. This is a rough starting guide, not a replication target. The original Avalonia was functional scaffolding with stock FluentTheme colors and inline HTML4 named colors — do NOT copy its styling.

**Scoring grid requirements** (functional, not visual):

- 9-column grid, one column per frame
- Each frame cell is a 3-row vertical stack: Break Bonus / Ball Count / Running Total
- Active frame highlighted
- Completed frames visibly "locked in"
- Empty frames neutral
- Mobile responsive: collapses to a single-frame-focused view below 768px with a frame-picker strip

**Future asset work** (post-v1, requires explicit web search permission from the user):

- Numbered pool ball graphics (1–15) for future scoring and UI features
- Free icon packs (candidates TBD — Lucide, Tabler, Heroicons, Phosphor all worth considering)
- Free pool/billiard imagery (backgrounds, decorative elements)
- **Do not canvas web asset sources until the user grants search access explicitly.**

**Salvaged from archive** (`archive/pre-blazor-rewrite`):

- 18 FontAwesome-style SVGs live in `src/NinetyNine.Web/wwwroot/icons/` (building, chart-bar, circle, cloud-upload-alt, cog, globe-americas, id-card, key, map-marked-alt, map-marker-alt, map-marker, pied-piper, smoking-ban, smoking, user-friends, user, users, warehouse — all solid-style, `viewBox="0 0 32 32"`, single-color path fills).
- `Icons.axaml` geometry paths preserved in `docs/_salvaged/Icons.axaml` for future reuse if specific Material/Fluent/FontAwesome/BoxIcons/VSImageLib paths are needed. Each entry is an Avalonia `GeometryDrawing` with a `Geometry="M..."` attribute whose value is SVG-compatible and can be pasted directly into a `<path d="..."/>`.

### 8.3 Pages / Routes

```
/                           Home (landing)
/login                      Provider picker
/register                   New-user profile form
/logout                     Sign out
/games                      My game history (paginated)
/games/new                  Start new game (venue + table size picker)
/games/{id}/play            Live scoring (the score card layout)
/games/{id}                 Completed game detail
/players/{id}               Public profile (honors visibility flags)
/players/me                 Edit my profile + avatar + visibility
/venues                     Venue list
/venues/new                 Add venue
/venues/{id}/edit           Edit venue
/stats                      Global leaderboard
/stats/me                   My personal stats
/api/avatars/{playerId}     Avatar image endpoint (GridFS-backed)
```

### 8.4 Layout components

- `MainLayout.razor` — top navbar (brand, nav links, user menu with avatar), main content, footer
- `NavMenu.razor` — Home / New Game / History / Leaderboard / Venues
- `UserMenu.razor` — avatar, display name, Profile / Sign out
- `LoginDisplay.razor` — shows Login button when unauthenticated

### 8.5 Design system

- **Styling**: Bootstrap 5 baseline (bundled with Blazor Web App template) extended with a custom dark-first theme in `wwwroot/css/theme.css`, `app.css`, `scorecard.css`. The current implementation is placeholder quality and due for a dedicated redesign pass.
- **Primary theme: dark.** Palette to be refined in the design pass — anchor around a deep neutral background (not pure black), pool-accent greens/teals as brand color, warm accent for active/highlight states. Light mode is a secondary toggle.
- **Typography**: system font stack for body text; monospace for score cells and numerals (`'JetBrains Mono', 'Fira Code', ui-monospace, monospace`).
- **Icons**: 18 SVGs salvaged from the Avalonia archive in `wwwroot/icons/`. Augment with a modern icon pack (Lucide/Tabler/Heroicons/Phosphor — TBD) once web search is authorized.
- **No heavy component library** (MudBlazor/Radzen) in v1. If the custom styling proves too expensive to maintain, MudBlazor is the upgrade path (its dark theme is strong).
- **Dark mode**: CSS custom properties + `data-bs-theme="dark"` by default, with a user toggle stored in cookie. Light mode variables kept but not the default.

### 8.6 Key components (reusable)

- `ScoreCardGrid.razor` — the full P&B grid for a single game, read or edit mode
- `FrameCell.razor` — single frame (Break/Ball/Running)
- `FrameInputDialog.razor` — modal for entering frame scores on touch devices
- `AvatarImage.razor` — renders `AvatarRef` with fallback to initials avatar
- `PlayerBadge.razor` — name + avatar + link to profile
- `VisibilityToggle.razor` — public/private switch bound to `ProfileVisibility` field

### 8.7 Accessibility (WCAG 2.2 AA targets)

- All interactive elements keyboard accessible
- Frame cells have ARIA labels (`Frame 3, Break Bonus 1, Ball Count 7, Running Total 18`)
- Color is not the sole signal for active/completed state — also shape/border
- Focus-visible outlines
- Contrast ratio ≥ 4.5:1 for text
- Form fields have explicit labels, not just placeholders
- `InputFile` for avatar has a descriptive label and error announcements
- Screen reader announcements when frame is completed / game advances

## 9. Docker + Local Development

### 9.1 `Dockerfile` (multi-stage)

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY NinetyNine.sln .
COPY src/NinetyNine.Model/NinetyNine.Model.csproj src/NinetyNine.Model/
COPY src/NinetyNine.Repository/NinetyNine.Repository.csproj src/NinetyNine.Repository/
COPY src/NinetyNine.Services/NinetyNine.Services.csproj src/NinetyNine.Services/
COPY src/NinetyNine.Web/NinetyNine.Web.csproj src/NinetyNine.Web/
RUN dotnet restore src/NinetyNine.Web/NinetyNine.Web.csproj
COPY . .
RUN dotnet publish src/NinetyNine.Web/NinetyNine.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "NinetyNine.Web.dll"]
```

### 9.2 `docker-compose.yml` (prod, Azure VM)

```yaml
services:
  web:
    image: ghcr.io/${GITHUB_REPOSITORY}:${IMAGE_TAG:-latest}
    restart: unless-stopped
    ports:
      - "80:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      MongoDb__ConnectionString: ${MONGO_CONNECTION_STRING}
      MongoDb__DatabaseName: ${MONGO_DATABASE_NAME:-NinetyNine}
      Auth__Google__ClientId: ${GOOGLE_CLIENT_ID}
      Auth__Google__ClientSecret: ${GOOGLE_CLIENT_SECRET}
    logging:
      driver: json-file
      options:
        max-size: "10m"
        max-file: "5"
```

### 9.3 `docker-compose.dev.yml` (local)

```yaml
services:
  web:
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "8080:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      MongoDb__ConnectionString: mongodb://root:devpassword@mongo:27017/?authSource=admin
      MongoDb__DatabaseName: NinetyNine
      Auth__Google__ClientId: ${GOOGLE_CLIENT_ID:-dev-noop}
      Auth__Google__ClientSecret: ${GOOGLE_CLIENT_SECRET:-dev-noop}
    depends_on:
      mongo:
        condition: service_healthy

  mongo:
    image: mongo:7
    restart: unless-stopped
    environment:
      MONGO_INITDB_ROOT_USERNAME: root
      MONGO_INITDB_ROOT_PASSWORD: devpassword
    volumes:
      - mongo_data:/data/db
    ports:
      - "27017:27017"
    healthcheck:
      test: ["CMD", "mongosh", "--quiet", "--eval", "db.adminCommand('ping').ok"]
      interval: 5s
      timeout: 5s
      retries: 10

  mongo-express:
    image: mongo-express:latest
    restart: unless-stopped
    environment:
      ME_CONFIG_MONGODB_ADMINUSERNAME: root
      ME_CONFIG_MONGODB_ADMINPASSWORD: devpassword
      ME_CONFIG_MONGODB_SERVER: mongo
      ME_CONFIG_BASICAUTH: "false"
    ports:
      - "8081:8081"
    depends_on:
      mongo:
        condition: service_healthy

volumes:
  mongo_data:
```

### 9.4 `deploy.sh` (local utility)

```bash
#!/usr/bin/env bash
# Local stand-up / teardown utility for NinetyNine.
# Usage: ./deploy.sh [up|down|rebuild|logs|seed|clean]

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

COMPOSE="docker compose -f docker-compose.dev.yml"
cmd="${1:-up}"

case "$cmd" in
  up)
    [ -f .env ] || cp .env.example .env
    $COMPOSE up -d --build
    echo "Web:          http://localhost:8080"
    echo "Mongo Express: http://localhost:8081"
    ;;
  down)
    $COMPOSE down
    ;;
  rebuild)
    $COMPOSE build --no-cache web
    $COMPOSE up -d
    ;;
  logs)
    $COMPOSE logs -f "${2:-web}"
    ;;
  seed)
    $COMPOSE exec web dotnet NinetyNine.Web.dll --seed
    ;;
  clean)
    $COMPOSE down -v
    echo "Volumes removed. Next 'up' starts fresh."
    ;;
  *)
    echo "Usage: $0 [up|down|rebuild|logs|seed|clean]"
    exit 1
    ;;
esac
```

### 9.5 `.env.example`

```bash
# Google OAuth (create at https://console.cloud.google.com/apis/credentials)
GOOGLE_CLIENT_ID=
GOOGLE_CLIENT_SECRET=

# MongoDB (local dev uses the compose mongo service; prod uses Atlas)
MONGO_CONNECTION_STRING=mongodb://root:devpassword@mongo:27017/?authSource=admin
MONGO_DATABASE_NAME=NinetyNine

# Image tag for prod compose (set by CI to SHA)
IMAGE_TAG=latest
```

## 10. Azure Deployment

### 10.1 Target topology

```
[GitHub]                              [Azure]
   │                                     │
   │ push to master                      │
   ▼                                     │
[Actions CI]──build──▶[GHCR]             │
   │                    │                │
   │ on success         │                │
   ▼                    │                │
[Actions Deploy]─SSH──▶ │   [Linux VM]    │
                        └──▶ docker login │
                             docker pull  │
                             compose up -d│
                                   │      │
                                   └──▶ [MongoDB Atlas]
```

### 10.2 VM prerequisites (documented in `docs/deployment.md`)

- Ubuntu 22.04 LTS
- Docker Engine + Compose v2
- `docker` group access for deploy user
- Inbound firewall: 22 (SSH), 80 (HTTP)
- `/opt/ninetynine/` directory with `docker-compose.yml` + `.env` (managed by deploy workflow)

### 10.3 Secrets (GitHub Actions → repo settings → secrets)

| Secret | Purpose |
|---|---|
| `AZURE_VM_HOST` | VM hostname or IP |
| `AZURE_VM_USER` | SSH user |
| `AZURE_VM_SSH_KEY` | Private key for SSH deploy |
| `MONGO_CONNECTION_STRING` | Atlas connection string |
| `GOOGLE_CLIENT_ID` | Google OAuth client ID |
| `GOOGLE_CLIENT_SECRET` | Google OAuth client secret |
| `GHCR_TOKEN` | PAT or auto `GITHUB_TOKEN` for GHCR push |

## 11. CI/CD (`.github/workflows/`)

### 11.1 `ci.yml` — PR / push validation

- Trigger: `push` and `pull_request` to any branch
- Jobs:
  1. `build-test`: checkout, setup .NET 8, restore, build, test (xUnit + testcontainers)
  2. `lint`: `dotnet format --verify-no-changes`
  3. `docker-build`: verify `Dockerfile` builds (no push)

### 11.2 `deploy.yml` — push to master deploys

- Trigger: `push` to `master`
- Jobs:
  1. `build-and-push`: build image, tag `ghcr.io/{owner}/{repo}:${{ github.sha }}` and `:latest`, push
  2. `deploy`: SSH to VM, `docker login ghcr.io`, `IMAGE_TAG=${{ github.sha }} docker compose pull && docker compose up -d --remove-orphans`, health check
  3. `smoke-test`: curl `http://${VM_HOST}/healthz` and assert 200

## 12. Testing Strategy

### 12.1 Unit tests (`NinetyNine.Model.Tests`)

Pure, no external deps. Covers:
- `Frame.ValidateFrame` boundary cases (0, 11, 12, negative, frame number 0/10)
- `Frame.CompleteFrame` running total math
- `Game.InitializeFrames` produces 9 frames, frame 1 active
- `Game.CompleteCurrentFrame` advances correctly
- `Game.CompleteCurrentFrame` on frame 9 marks game completed
- Scoring edge cases: scratch on break (9-ball still counts), perfect frame, perfect game
- `ValidateGame` catches malformed state

### 12.2 Integration tests (`NinetyNine.Repository.Tests`)

Uses `Testcontainers.MongoDb` to spin up real Mongo per test class. No mocks.
- Each repository tested against real driver
- BSON round-trip: serialize → deserialize → equal
- Guid representation renders as string in Mongo
- Index creation verified
- GridFS upload/download/delete

### 12.3 Service tests (`NinetyNine.Services.Tests`)

- Uses testcontainers Mongo through real repositories
- Scoring flow: start → record 9 frames → game completed
- Player registration: new provider/sub creates Player; existing provider/sub returns same Player
- DisplayName uniqueness enforced
- Avatar upload: invalid content-type rejected; valid image resized and stored

### 12.4 Component tests (`NinetyNine.Web.Tests`)

Uses `bUnit`:
- `ScoreCardGrid` renders 9 frames correctly
- `FrameCell` active/completed/empty states
- `AvatarImage` falls back to initials when no avatar
- `VisibilityToggle` two-way binding

### 12.5 Quality gates

- All tests green
- `dotnet format --verify-no-changes` passes
- Code coverage ≥ 70% on Model and Services (enforced via `coverlet` + threshold)
- No `Warning` level build output (treat warnings as errors in Release)

## 13. Security Requirements

- OAuth cookie: `HttpOnly`, `SameSite=Lax`, `Secure` when TLS added
- Anti-forgery tokens on all form submissions (Blazor handles by default)
- Avatar uploads: content-type whitelist, size cap, server-side resize (strips EXIF)
- Log lines never include: access tokens, cookie values, OAuth secrets, PII
- DisplayName uniqueness enforced at DB index level (defense in depth over service check)
- Rate limiting on `/login` and `/register` (ASP.NET rate limiting middleware, 10 req/min/IP)
- CSP header set to `default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline';`
- No inline scripts in Blazor templates (use component binding)

## 14. Out of Scope (v1)

- Multi-player `Match` aggregate
- Live multi-user game sessions (SignalR)
- Tournament mode / brackets
- Export to PDF/CSV
- Mobile native apps
- Discord/Telegram auth
- Application Insights / distributed tracing
- TLS / custom domain
- Automated backups (manual Atlas snapshots for v1)
- Admin panel (user management handled via Atlas directly)

## 15. Agent Ownership Matrix

For the builder team. Each agent owns specific files; **no cross-writes** outside their zone.

| Zone | Owner Agent | Files |
|---|---|---|
| Solution + Model | backend-architect | `NinetyNine.sln`, `src/NinetyNine.Model/**` |
| Repository | backend-architect | `src/NinetyNine.Repository/**` |
| Services | backend-architect | `src/NinetyNine.Services/**` |
| Blazor Web + Auth | frontend-developer | `src/NinetyNine.Web/**` (except styling) |
| UI Design / CSS | ui-designer | `src/NinetyNine.Web/wwwroot/css/**`, component `.razor.css` files |
| Docker + deploy.sh | docker-expert | `Dockerfile`, `docker-compose.yml`, `docker-compose.dev.yml`, `deploy.sh`, `.env.example` |
| CI/CD | deployment-engineer | `.github/workflows/**` |
| Azure notes | azure-infra-engineer | `docs/deployment.md` |
| Tests | test-automator | `tests/**` |
| Security review | backend-security-coder | Review pass on auth, upload, cookies (no code authorship) |

Integration is orchestrated by the coordinator (main Claude). Contracts in this document are authoritative — if an agent disagrees, they flag it back, they do not silently change course.
