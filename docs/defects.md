# Defects log

<!-- markdownlint-disable MD024 -->
<!-- Duplicate section headings (### Symptom, ### Root cause, ### Fix options) -->
<!-- are intentional: each defect entry reuses the same subsection vocabulary. -->

Tracks bugs discovered during development. Entries are added as issues are found and kept after fixes land so future work can reference them.

---

## DEF-001 — Seeder idempotency short-circuits data migrations

**Discovered**: 2026-04-11 during Wave 4 post-integration testing
**Status**: Partially fixed in commit `cfb053d` (heal pass for `passwordHash` and `emailAddress`)
**Severity**: Medium — blocked email/password login end-to-end testing until healed
**Owner**: backend

### Symptom

After Wave 3 (WP-11) added password hashing to the seeded dev test players, the three test players (`carey`, `george`, `carey_b`) in an existing MongoDB volume still had `passwordHash: ''` (empty string) and `emailAddress: ''` in their documents. Email/password login against any of them failed silently with "Invalid email or password." (from the auth service's no-enumeration generic error). Only the mock picker (which bypasses password validation) worked.

### Root cause

`DataSeeder.SeedAsync` uses a single-check idempotency guard:

```csharp
var existing = await playerRepository.GetByDisplayNameAsync(
    IDataSeeder.TestPlayerDisplayNames[0], ct);
if (existing is not null) return;  // short-circuit
```

When the `carey` player already exists (from an earlier seed run), the method returns early. This means any schema-level changes to seeded entities shipped in later WPs — new required fields, hashed credentials, visibility defaults — don't reach the existing records. The seeder is "idempotent for creation" but "non-reconciling for updates".

### Partial fix (cfb053d)

Added a `HealExistingTestPlayersAsync` pass that runs **before** the idempotency check. For each known test display name, if `PasswordHash` or `EmailAddress` is empty, it backfills them and saves. The heal itself is idempotent (zero-ops after the first successful run).

This covers the specific case we hit but does not address the underlying pattern.

### Remaining work (not yet scheduled)

The seeder should be refactored from "create once, skip forever" to **reconcile every startup**. Concretely:

1. For each known test player display name, either create or update the player document so that all fields match the seeded template (not just the ones currently in the heal pass).
2. Same for venues and games if we extend their schema in the future.
3. Keep idempotency: running the seeder twice on a fresh DB must not duplicate records (current state: ok, because we check existence before create).
4. Keep startup fast: if nothing changes, no writes should hit MongoDB (current state: the heal pass short-circuits on non-empty fields, so no write per unchanged record).
5. Consider a `SchemaVersion` field on seeded records so the seeder can cheaply detect when a reconcile is needed rather than field-by-field comparison.

### Why this isn't yet fully fixed

The heal-pass solution is tactical: it patches the fields we know are broken (`passwordHash`, `emailAddress`) and unblocks dev testing. A full reconcile rewrite is a small-to-medium work package and should land in a dedicated defect-fix WP, probably after the remaining redesign waves complete so it doesn't compete with UI work for ownership boundaries.

### Related risks surfaced during investigation

During the investigation two other bugs were found and fixed in commit `ef2a3b9`:

- **NavigationException swallowed in `HandleSubmitAsync`** — `New.razor` wrapped `Navigation.NavigateTo` inside `try/catch (Exception)`, catching the Blazor-internal `NavigationException` that signals the redirect. Fixed by moving `NavigateTo` outside the catch. Audit confirmed this was the only occurrence in the codebase (Login/Register/etc. use `try/finally` which is safe).
- **Data Protection key ring ephemeral across Docker rebuilds** — every `./deploy.sh rebuild` regenerated the DP key ring, invalidating all browser cookies (auth + antiforgery). Fixed by persisting `/var/ninetynine/keys` on a named volume, `chown $APP_UID` in the Dockerfile, and `AddDataProtection().PersistKeysToFileSystem(...)` in `Program.cs`.

These three issues surfaced together because the DP-keys issue caused the original antiforgery failure, which masked the New.razor NavigationException swallow, which in turn masked the seeder idempotency bug. Each layer had to be stripped away to find the underlying data defect.

---

## DEF-002 — `/players/me` is not a registered route (profile 404)

**Discovered**: 2026-04-11 during Wave 7 manual smoke testing (section 8 — Profile and Editing)
**Status**: Fixed (Sprint 3 S3.6, commit 4588897)
**Severity**: High — "View profile" in the user menu is unreachable for all authenticated users
**Owner**: frontend

### Symptom

Clicking **User menu → View profile** (or **Profile** in the primary sidebar) navigates to `/players/me` and renders the 404 "Not found" page.

### Root cause

[Profile.razor:1](src/NinetyNine.Web/Components/Pages/Players/Profile.razor#L1) declares a single route:

```razor
@page "/players/{PlayerId:guid}"
```

No route is registered for the literal `/players/me` path. However, both [UserMenu.razor:42](src/NinetyNine.Web/Components/Layout/UserMenu.razor#L42) and [NavMenu.razor:86](src/NinetyNine.Web/Components/Layout/NavMenu.razor#L86) link to `players/me`. Because `me` does not parse as a `Guid`, the router falls through to the not-found fallback.

Interestingly, [EditProfile.razor:1](src/NinetyNine.Web/Components/Pages/Players/EditProfile.razor#L1) **does** use the literal `@page "/players/me/edit"`, so edit works; only the profile view is broken.

### Fix options

1. Add a second `@page` directive to `Profile.razor`: `@page "/players/me"` and resolve the current `PlayerId` from `HttpContextAccessor` / `ClaimNames.PlayerId` when `PlayerId` is `null` in the route match. Preferred: consistent with `EditProfile.razor`.
2. Alternatively, change the nav links to resolve the signed-in player's `Guid` at render time and link to `/players/{id}`. Rejected: leaks identifiers into nav markup and forces a claim lookup in every layout render.

### Related risks

- None of the `Profile.razor` route-matching changes should affect the existing `/players/{guid}` route — the new `/players/me` route must be declared in a way that does not create ambiguity for `Guid` path segments (the router treats literal segments as higher-priority than typed constraints, so `/players/me` will match the literal route and `/players/{guid}` will continue to match real ids).

---

## DEF-003 — New Game form: duplicate `__RequestVerificationToken` causes 400 antiforgery failure on every submit

**Discovered**: 2026-04-11 during Wave 7 manual smoke testing (section 6 — Start and Play a Game)
**Status**: Fixed (pre-Sprint 0, explicit `<AntiforgeryToken />` removed from all forms)
**Severity**: **Critical** — no form in the application that uses `EditForm method="post"` can successfully post while DP keys are valid, because the rendered form always contains two `__RequestVerificationToken` hidden inputs.
**Owner**: backend / framework

### Symptom

Clicking **Start Game** on `/games/new` returns HTTP 400 with the message:

> A valid antiforgery token was not provided with the request. Add an antiforgery token, or disable antiforgery validation for this endpoint.

Reproduced deterministically via direct `curl` posts — see "Evidence" below.

### Root cause

Every Blazor SSR page in the app that uses an `EditForm` with `method="post"` renders **two identical** `<input type="hidden" name="__RequestVerificationToken">` elements in the final HTML. When the browser submits the form, both values are posted and end up in `HttpRequest.Form["__RequestVerificationToken"]` as a `StringValues` collection with two entries. ASP.NET Core's `DefaultAntiforgery.ValidateRequestAsync` implicitly coerces the multi-valued field to a single `string` (by calling `.ToString()` on `StringValues`), which joins the values with a comma — `"<tok>,<tok>"`. That joined blob is not a valid token, so validation fails with 400.

The duplicate token is emitted because:

1. [New.razor:38](src/NinetyNine.Web/Components/Pages/Games/New.razor#L38) (and every other form page in the codebase) contains an explicit `<AntiforgeryToken />` element.
2. In .NET 8 Blazor SSR, when an `EditForm` has `method="post"` **and** a `FormName` (which the `[SupplyParameterFromForm]` pattern requires), the framework's form mapping machinery injects a second `__RequestVerificationToken` hidden input automatically.

The result is two identical hidden fields with the same value — one from `<AntiforgeryToken />` and one from the framework's automatic injection.

### Evidence

Reproduced in-container without a browser. First, fetch `/games/new` to get the token and cookie:

```bash
curl -b cookies.txt -c cookies.txt http://localhost:8080/games/new -o newgame.html
grep -c '__RequestVerificationToken' newgame.html   # → 2
```

Then POST **with a single token** — succeeds:

```bash
curl -b cookies.txt -X POST http://localhost:8080/games/new \
  --data-urlencode "_handler=new-game" \
  --data-urlencode "__RequestVerificationToken=$TOKEN" \
  --data-urlencode "_model.VenueId=..." \
  --data-urlencode "_model.TableSize=SevenFoot"
# → HTTP 302 Location: /games/{guid}/play
```

POST **with two identical tokens** (what the browser actually sends) — fails:

```bash
curl -b cookies.txt -X POST http://localhost:8080/games/new \
  --data-urlencode "_handler=new-game" \
  --data-urlencode "__RequestVerificationToken=$TOKEN" \
  --data-urlencode "__RequestVerificationToken=$TOKEN" \
  --data-urlencode "_model.VenueId=..." \
  --data-urlencode "_model.TableSize=SevenFoot"
# → HTTP 400
```

Server access log confirms every browser POST to `/games/new` returns 400 in ~9 ms (well before any handler runs — antiforgery short-circuits the pipeline).

### Blast radius

This is not a `/games/new`–specific bug. Every page that follows the same pattern is affected:

- [Login.razor:40](src/NinetyNine.Web/Components/Pages/Login.razor#L40) — email/password sign-in
- [Register.razor:35](src/NinetyNine.Web/Components/Pages/Register.razor#L35) — account creation
- [ForgotPassword.razor:26](src/NinetyNine.Web/Components/Pages/ForgotPassword.razor#L26) — password reset request
- [ResendVerification.razor:26](src/NinetyNine.Web/Components/Pages/ResendVerification.razor#L26) — resend email verification
- [ResetPassword.razor:42](src/NinetyNine.Web/Components/Pages/ResetPassword.razor#L42) — new password submission
- [EditProfile.razor:118](src/NinetyNine.Web/Components/Pages/Players/EditProfile.razor#L118) — profile update
- [Venues/Edit.razor:47,140](src/NinetyNine.Web/Components/Pages/Venues/Edit.razor#L47) — venue add / edit
- [UserMenu.razor:60](src/NinetyNine.Web/Components/Layout/UserMenu.razor#L60) — sign-out POST
- [New.razor:38](src/NinetyNine.Web/Components/Pages/Games/New.razor#L38) — start new game

All of these will 400 on submit. The mock-sign-in picker worked only because it's a raw GET (`/mock/signin-as?displayName=…`) with no form POST — this is why the bug was not caught by prior ad-hoc testing.

### Fix options

1. **Remove the explicit `<AntiforgeryToken />`** from every form and rely on the framework's automatic injection. Smallest diff. Recommended.
2. Remove the automatic injection instead. Not available — the framework emits it unconditionally for `EditForm method="post"` with a `FormName`.
3. Custom antiforgery options that deduplicate multi-valued form fields before validation. Rejected — overrides framework internals and will break on upgrades.

A follow-up work package should add a `SmokeTests.cs` HTTP harness (see note in `docs/smoke-test-checklist.md` §390–398) that asserts exactly one `__RequestVerificationToken` hidden input per form, so this regression is caught in CI.

### Why DEF-001's "DP keys" fix didn't catch this

DEF-001 diagnosed that `./deploy.sh rebuild` regenerated DP keys and invalidated cookies. That fix made antiforgery tokens *decryptable* across rebuilds, but it did not address the duplicate-token bug. The prior symptoms ("could not decrypt") were replaced by the current symptoms ("not a valid token") — the latter has been present since the form pages were first written but was masked by the former.

---

## DEF-004 — New Game: table size picker is non-interactive (page renders as static SSR)

**Discovered**: 2026-04-11 during Wave 7 manual smoke testing (section 6)
**Status**: Fixed (pre-Sprint 0, replaced with native radio buttons)
**Severity**: High — users cannot change the table size from the default `7 ft`; there is no workaround short of re-coding the form.
**Owner**: frontend

### Symptom

On `/games/new`, clicking any of the table size buttons (6 ft / 7 ft / 9 ft / 10 ft) has no effect. The "7 ft" selection (the default) never updates regardless of which button is clicked.

### Root cause

[New.razor](src/NinetyNine.Web/Components/Pages/Games/New.razor) has no `@rendermode` directive, so the page is rendered as **static SSR**. The `<TableSizePicker>` component at [TableSizePicker.razor:16-17](src/NinetyNine.Web/Components/Shared/TableSizePicker.razor#L16-L17) uses `@onclick="() => OnChange(s)"` and `@onkeydown="..."` event handlers to mutate its selected value. Those handlers only execute inside an active Blazor circuit — i.e., under `@rendermode InteractiveServer` (or `InteractiveAuto`). In static SSR, the rendered HTML is a plain `<button type="button">` with no client-side JavaScript wiring, so clicks are silent no-ops.

By contrast, [Play.razor:3](src/NinetyNine.Web/Components/Pages/Games/Play.razor#L3) *does* declare `@rendermode InteractiveServer`, which is why frame-score interactions work on the play page but not on new-game.

### Interaction with DEF-003

DEF-003 (duplicate antiforgery token) masked this bug during the initial report: the form never successfully POSTed, so the "table size won't change" symptom appeared even more broken than it is. Even after DEF-003 is fixed, the form will always submit `TableSize = SevenFoot` regardless of what the user clicks, because the picker cannot update the bound value.

### Fix options

1. **Declare `@rendermode InteractiveServer` on `New.razor`** and switch the form's model binding from `[SupplyParameterFromForm]` to a circuit-managed model. Keeps the existing `TableSizePicker` as-is. Recommended.
2. Replace the `<TableSizePicker>` with a native `<input type="radio">` group that binds through `InputRadioGroup` inside the `EditForm`. Works in static SSR without a circuit. Lower-diff but loses the segmented-button UX.
3. Serialize the table size into a hidden `<input type="hidden" name="_model.TableSize">` and update it via a form-submit-button-per-size approach. Rejected — worst UX.

---

## DEF-005 — New Game: venue dropdown chevron SVG tiles horizontally across the control

**Discovered**: 2026-04-11 during Wave 7 manual smoke testing (section 6)
**Status**: **Fixed** — root cause confirmed from screenshot and resolved.
**Severity**: Medium — venue was still selectable via keyboard but the chevron row looked like the control was broken.
**Owner**: frontend

### Symptom

On `/games/new`, the venue `<select>` rendered with a **row of seven-ish tiled chevron carets** stretching across the right two-thirds of the control instead of a single right-aligned chevron. The placeholder text ("-- Select a venue --") was visually overlapped by the leftmost chevron, making the label look garbled.

### Root cause (confirmed from screenshot)

[wwwroot/css/app.css:256-259](src/NinetyNine.Web/wwwroot/css/app.css#L256-L259) set `background-image` for `.form-select` (a custom teal-grey chevron SVG) but did **not** set any of the companion properties that control how a background image is sized and positioned:

- `background-repeat: no-repeat`
- `background-position: right 0.75rem center`
- `background-size: 16px 12px`
- `appearance: none` (so the browser's own native chevron is suppressed)
- `padding-right: 2.25rem` (so option text doesn't run into the chevron)

In most Blazor project templates these properties arrive via Bootstrap's own `.form-select` rules — but this project does **not** load Bootstrap CSS (verified: `grep -rn "bootstrap"` in `wwwroot/css/` returns no results). The only `.form-select` rules are the ones in `app.css`. With only `background-image` set, `background-repeat` defaults to `repeat`, which tiles the 16×12 chevron SVG across the full width of the control in both directions — producing the "corrupted" visual the tester captured.

The rendered HTML itself is well-formed — two seeded venues render correctly as `<option>` elements with real `Guid` values — so this is not a data or Razor-template bug:

```html
<select id="venue" name="_model.VenueId" class="form-select nn-new-page__select valid">
  <option value="">-- Select a venue --</option>
  <option value="7f768c81-...">Home Table — 42 Corner Pocket Ln</option>
  <option value="b36c71f7-...">Summerville Billiards — 123 Rail Ave, Summerville SC</option>
</select>
```

### Fix

Add the missing background-* companion properties to the `.form-select` rule in `app.css`. Also set `appearance: none` to suppress the browser's native chevron (which would otherwise render next to the custom one on some platforms) and `padding-right: 2.25rem` so option labels never collide with the chevron.

### Why the earlier deferral missed this

The initial triage checked `color-scheme`, seed data, and the rendered option HTML and concluded the rendered output was well-formed. It was — the `<select>` element itself is fine. The bug was entirely in the **background-image tiling** of a CSS rule that applied to `.form-select`, which is only visible when looking at the control *visually*. Static HTML inspection could not have caught this.

### Why this wasn't caught earlier in development

Prior to Wave 5 (dark redesign), the only `<select>` in the app was on a page that used Bootstrap's default chevron, so `background-image` was never overridden. The custom dark-mode chevron was added as a single line in Wave 5 without the supporting properties, which is why the bug only surfaced during the first smoke test that exercised `/games/new` under the new theme. A bUnit test would not catch this — it's purely a rendered-CSS bug.

---

## DEF-006 — Leaderboard renders literal `entry.DisplayName` / `entry.PlayerId` / `entry.AvatarUrl` strings in the desktop table

**Discovered**: 2026-04-11 during Wave 7 manual smoke testing (section 10 — Statistics)
**Status**: Fixed (pre-Sprint 0, added missing `@` prefix on component parameters)
**Severity**: High — leaderboard is the feature page for section 10; desktop rendering is broken for all rows.
**Owner**: frontend

### Symptom

The desktop leaderboard table's **Player** column shows the literal text `entry.DisplayName` (and links go to `/players/entry.PlayerId`, avatars attempt to load `entry.AvatarUrl` as an image src). Every row in the table displays the same broken strings instead of per-player data.

### Root cause

[Leaderboard.razor:92-95](src/NinetyNine.Web/Components/Pages/Stats/Leaderboard.razor#L92-L95):

```razor
<PlayerBadge PlayerId="entry.PlayerId"
             DisplayName="entry.DisplayName"
             AvatarUrl="entry.AvatarUrl"
             AvatarSizePx="24" />
```

The component parameters are assigned **quoted string literals**, not Razor expressions. To bind a C# expression to a component parameter in Razor, the value must be prefixed with `@` (or use `@(...)`):

```razor
<PlayerBadge PlayerId="@entry.PlayerId"
             DisplayName="@entry.DisplayName"
             AvatarUrl="@entry.AvatarUrl"
             AvatarSizePx="24" />
```

`AvatarSizePx="24"` is correct *only* because Razor auto-parses numeric literals for `int`/`double`-typed parameters; string- and `Guid`-typed parameters silently receive the literal text.

The mobile-card rendering at [Leaderboard.razor:131-140](src/NinetyNine.Web/Components/Pages/Stats/Leaderboard.razor#L131-L140) correctly uses `@entry.DisplayName` and `@entry.PlayerId` — only the desktop table is affected.

### Fix

Add the missing `@` prefix on the three expression parameters on lines 92–94 of `Leaderboard.razor`. One-line change per attribute. No other call sites in the codebase exhibit the same pattern — this is localized to the desktop table block.

### Why this wasn't caught

No bUnit component test exercises the rendered output of `Leaderboard.razor`'s desktop branch; the existing Wave 6 component tests check structural rendering but not the specific props passed to `PlayerBadge`. A follow-up test could assert that `PlayerBadge` receives non-literal string props by rendering with seed data and grepping the DOM for the literal substring `entry.`.

---

## DEF-007 — `.form-control` inputs do not fill their container (missing `width: 100%`)

**Discovered**: 2026-04-11 during Wave 7 manual smoke testing of the Edit Profile page (section 8)
**Status**: Fixed
**Severity**: Medium — visible layout breakage on every form; name-row on Edit Profile looked mis-aligned because First/Middle/Last were left-anchored in three `1fr` grid cells.
**Owner**: frontend

### Symptom

On `/players/me/edit`, the three name inputs (First name, Middle name, Last name) rendered at their intrinsic ~20ch default width and sat left-aligned inside the three-column grid cells, producing visibly uneven spacing and a wide gap between the Middle and Last name fields.

### Root cause

[wwwroot/css/app.css:189-216](src/NinetyNine.Web/wwwroot/css/app.css#L189-L216) styles `.form-control` (and sibling selectors) with background, border, padding, and focus rules — **but declares no `width`**. In the standard Blazor project template the missing property comes from Bootstrap's own `.form-control { width: 100%; }` rule. This project does not load Bootstrap CSS, so inputs fell back to their browser-default intrinsic size.

Inside [EditProfile.razor.css:146-151](src/NinetyNine.Web/Components/Pages/Players/EditProfile.razor.css#L146-L151) the name row is `display: grid; grid-template-columns: repeat(3, 1fr)`, so each cell is 1/3 of the container. But because the inputs didn't fill their cells, they appeared huddled on the left of each cell, leaving empty gaps on the right and making the whole row look broken.

### Fix

Add `display: block; width: 100%; box-sizing: border-box;` to the shared form-input rule in `app.css`. This restores the Bootstrap-equivalent behavior and applies consistently to every form in the app (login, register, forgot/reset password, venue edit, profile edit, new game), not just Edit Profile. The rule targets only `.form-control`, `.form-select`, `textarea`, and typed text inputs — checkboxes, radios, and `input[type="hidden"]` are excluded by the selector list, so their layout is untouched.

### Blast radius of the fix

Positive: every form input on every page is now the same width as its container, matching the designs.
Negative risk: minimal — any previously-narrow intentional inline input would now stretch. A grep of the codebase shows no intentional inline `.form-control` usage; every input is inside a field wrapper that expects full width.

---

## DEF-008 — Seeder forces `Visibility.RealName = true` on every seeded test player

**Discovered**: 2026-04-11 during Wave 7 manual smoke testing of the Edit Profile page (section 8)
**Status**: Fixed
**Severity**: Medium — privacy default contradicts the user's stated intent; visible as a toggle starting in the ON/PUBLIC position.
**Owner**: backend

### Symptom

On `/players/me/edit`, the **"Show real name publicly"** toggle rendered in the ON position (labeled PUBLIC) for every seeded test player. The tester reported this should default to PRIVATE (OFF) because the upcoming Friends/Communities features will add finer-grained audience controls and the safe default for every new sharing dimension is the most-private option.

### Root cause

Two layers contributed:

1. **Model default is correct**: [Player.cs:97](src/NinetyNine.Model/Player.cs#L97) declares `public bool RealName { get; set; } = false;` — so any newly-registered user already lands in the desired state.
2. **Seeder overrides the default**: [DataSeeder.cs:158-162](src/NinetyNine.Services/DataSeeder.cs#L158-L162) constructed each seeded test player with `Visibility = new ProfileVisibility { RealName = true, Avatar = true }`, deliberately opting the three dev test players into publishing their real name. This was carried forward from an earlier wave when the test data was expected to exercise the real-name-visible branch of the public profile page.

Once the override wrote `realName: true` into Mongo, DEF-001's "create once, skip forever" idempotency meant subsequent seed runs never reconciled the value — the field stayed true even after the `new ProfileVisibility` expression's literal was changed.

### Fix

1. Drop `RealName = true` from the seeder literal; the `ProfileVisibility` default (`false`) now applies.
2. Extend the DEF-001 heal pass in `HealExistingTestPlayersAsync` to reset `Visibility.RealName` from `true` to `false` for the three known seeded display names (`carey`, `george`, `carey_b`). The heal pass is scoped by display name, so it can never touch a real user account. It also remains idempotent: once reset, subsequent runs see `RealName == false` and skip the write.

### Related: Friends/Communities privacy defaults

The user introduced the Friends and Communities features in the same session. A project memory has been saved capturing the rule: "any new sharing dimension should default to the most-private option so the eventual multi-tier audience model (Private / Friends / Communities / Public) has safe defaults." Future visibility toggles should follow this convention.

---

## DEF-009 — Skip-to-main-content link is permanently visible on screen

**Discovered**: 2026-04-12 during Sprint 6 live verification
**Status**: Resolved in Sprint 7 — commit `5783b96` ("Fix DEF-009: skip-to-main-content link permanently visible"), 2026-04-13
**Severity**: Medium — accessibility link is visually distracting; should only appear on keyboard focus
**Owner**: frontend

### Symptom

The "Skip to main content" link added in Sprint 6 S6.3 (commit `b1ded58`) is permanently visible on the left side of the website instead of being hidden off-screen until the user presses Tab.

### Root cause

[MainLayout.razor.css:19-31](src/NinetyNine.Web/Components/Layout/MainLayout.razor.css#L19-L31) uses `position: absolute; top: -100%` to hide the skip link. This approach is unreliable because:

1. The percentage `top: -100%` resolves relative to the containing block's height, not the viewport. If the containing block (`nn-layout`) has an explicit height or is the initial containing block, the resolved value may not push the element fully off-screen.
2. Blazor scoped CSS may introduce specificity or selector-matching issues that prevent the unfocused styles from applying correctly.

The standard accessible-hiding pattern uses `clip: rect(0, 0, 0, 0)` with `width: 1px; height: 1px; overflow: hidden` (the "sr-only" pattern), which reliably hides the element regardless of ancestor positioning context.

### Fix

Replace the `top: -100%` approach with the `clip-rect` visually-hidden pattern. On `:focus`, reset all clip/overflow properties and position the link at the top of the viewport with `position: fixed`.

### Resolution

Applied in commit `5783b96` in Sprint 7. [MainLayout.razor.css](src/NinetyNine.Web/Components/Layout/MainLayout.razor.css) now hides the skip link via the `clip: rect(0,0,0,0)` / `width: 1px; height: 1px; overflow: hidden` pattern and reveals it via `:focus` / `:focus-visible` pseudo-class. This also closes accessibility-audit F-D4 (WCAG 2.4.1 Bypass Blocks).

---

## DEF-010 — MailKit 4.15.1 has a moderate-severity advisory (GHSA-9j88-vvj5-vhgr)

**Discovered**: 2026-04-19 during UX-020 smoke test dry run (post-Sprint 10 baseline capture)
**Status**: Open — scheduled for v0.1.8 polish sprint
**Severity**: Medium — supply chain advisory, not a runtime exploit path in current app usage
**Owner**: backend / dependencies

### Symptom

`dotnet build NinetyNine.sln` now fails on [src/NinetyNine.Web/NinetyNine.Web.csproj](src/NinetyNine.Web/NinetyNine.Web.csproj) (which sets `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` at line 7). NuGet restore emits `NU1902: Package 'MailKit' 4.15.1 has a known moderate severity vulnerability`, which escalates to an error on the Web project. Test project [tests/NinetyNine.Web.Tests/NinetyNine.Web.Tests.csproj](tests/NinetyNine.Web.Tests/NinetyNine.Web.Tests.csproj) emits the same NU1902 as a warning (no `TreatWarningsAsErrors`).

The advisory ([GHSA-9j88-vvj5-vhgr](https://github.com/advisories/GHSA-9j88-vvj5-vhgr)) was published after the Sprint 10 close; at v0.1.7 tag time the build was clean.

### Impact

`dotnet build NinetyNine.sln -warnaserror` fails until the package is bumped or the warning is suppressed. Workaround for local development: `dotnet build NinetyNine.sln -p:NuGetAudit=false`.

### Fix options

1. **Preferred**: upgrade `MailKit` PackageReference in [NinetyNine.Web.csproj](src/NinetyNine.Web/NinetyNine.Web.csproj#L13) to the advisory's fixed version (verify with `dotnet list package --outdated` and check the advisory's "Patched versions" field). Apply the same bump to [tests/NinetyNine.Web.Tests/NinetyNine.Web.Tests.csproj](tests/NinetyNine.Web.Tests/NinetyNine.Web.Tests.csproj) if the dependency is also declared there.
2. **Acceptable**: add `<NuGetAuditSuppress Include="https://github.com/advisories/GHSA-9j88-vvj5-vhgr" />` to a `<ItemGroup>` in the Web csproj *only if* investigation shows the vulnerable code path is unreachable in the current deployment (the app only uses MailKit for SMTP send; check the advisory for the affected API surface). Document the suppression's justification inline.
3. **Not acceptable**: disable `TreatWarningsAsErrors` globally to make the problem go away.

### Related

Updates the "0 warnings/errors" claim in [docs/redesign-completion-report.md](docs/redesign-completion-report.md) — that claim was accurate at v0.1.7 but is now stale because of the newly-published advisory.
