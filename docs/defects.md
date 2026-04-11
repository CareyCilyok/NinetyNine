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
**Status**: Open
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
**Status**: Open — **blocks all Blazor SSR form submissions**, including email/password login, registration, forgot-password, venue edit, profile edit, and new game.
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
**Status**: Open
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

## DEF-005 — New Game: venue dropdown styled unreadably / "corrupted" in the native popup

**Discovered**: 2026-04-11 during Wave 7 manual smoke testing (section 6)
**Status**: Open — **needs screenshot confirmation from tester to finalize diagnosis**
**Severity**: Medium — venue is still selectable by keyboard and the first post-DEF-003-fix submission will still accept the chosen value, but the picker is visually unusable in dark mode.
**Owner**: frontend

### Symptom

Tester reports the venue dropdown on `/games/new` appears "corrupted". Exact visual symptom not yet documented (screenshot pending).

### Most likely root cause (unconfirmed without screenshot)

[New.razor.css:56-61](src/NinetyNine.Web/Components/Pages/Games/New.razor.css#L56-L61) styles the `<select>` with `background-color: var(--nn-bg-tertiary); color: var(--nn-text-primary);` (dark background, light text). Chromium and Firefox apply these styles to the closed `<select>` control, but **the native popup list of options uses OS-native rendering**, not the CSS from the `<select>` rule. On a dark OS theme the popup may render dark-on-dark (illegible); on a light OS theme the contrast flips unexpectedly when the select is hovered.

The rendered HTML itself is well-formed — two seeded venues render correctly as `<option>` elements with real `Guid` values — so this is not a data or Razor-template bug:

```html
<select id="venue" name="_model.VenueId" class="form-select nn-new-page__select valid">
  <option value="">-- Select a venue --</option>
  <option value="7f768c81-...">Home Table — 42 Corner Pocket Ln</option>
  <option value="b36c71f7-...">Summerville Billiards — 123 Rail Ave, Summerville SC</option>
</select>
```

### Alternate hypotheses to check against the screenshot

1. **Native popup contrast inversion** (above) — most likely.
2. **`<option>` text truncation** if the dropdown is width-constrained by the form card at narrow viewports — [New.razor.css:17](src/NinetyNine.Web/Components/Pages/Games/New.razor.css#L17) sets `max-width: 36rem`.
3. **Em-dash rendering** — source uses `\u2014` (proper em dash), output renders as `&#x2014;`. Should look fine but a font without an em-dash glyph could show a `.notdef` box.
4. **CSS-isolation scope markers on `<option>` elements** (`b-yflyr2nfxz`) — cosmetic only, not a rendering bug.

### Fix options (for hypothesis 1)

1. Drop the custom dark background on `<select>` and rely on `color-scheme: dark` from the document root, which tells the browser to render the native popup in dark mode using OS-dark colors. [Program.cs:143](src/NinetyNine.Web/Program.cs#L143) does not currently include `color-scheme` — confirm the theme layer declares it.
2. Replace the `<select>` with a custom popover component (significant scope, rejected unless other dropdowns in the app share this pain).

### Action required

Attach a screenshot of the "corrupted" dropdown to this defect before attempting a fix. Without a visual, the diagnosis above is a best guess.

### Investigation summary (2026-04-11, while fixing DEF-002/003/004/006)

Checked the following and found nothing that a speculative fix would clearly improve:

- `color-scheme: dark` **is** declared on `:root` in [wwwroot/css/app.css:42](src/NinetyNine.Web/wwwroot/css/app.css#L42) and [wwwroot/css/theme.css:197](src/NinetyNine.Web/wwwroot/css/theme.css#L197), so hypothesis 1 (native popup contrast inversion from missing `color-scheme`) is ruled out — browsers should already render the popup in dark mode.
- The seeded venue documents are clean (`Home Table`, `Summerville Billiards`, valid addresses), so it isn't a data defect.
- Rendered `<option>` HTML is well-formed; no stray attributes that would break the control.
- No CSS rule anywhere in `wwwroot/css/` or `Components/` explicitly targets `option {…}` — there's no custom style hiding or corrupting the option elements.

**Deferred pending screenshot.** This defect is not blocking any smoke-test step because the venue is still selectable via keyboard (Tab to the select, arrow keys to pick, Enter to confirm) and the underlying form submission works (verified end-to-end during DEF-003 and DEF-004 fixes). Reopen with the screenshot when available, or dismiss as "not reproducible" if the tester can no longer see the original symptom after the DEF-003/004 rebuilds.

---

## DEF-006 — Leaderboard renders literal `entry.DisplayName` / `entry.PlayerId` / `entry.AvatarUrl` strings in the desktop table

**Discovered**: 2026-04-11 during Wave 7 manual smoke testing (section 10 — Statistics)
**Status**: Open — **trivial fix**
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
