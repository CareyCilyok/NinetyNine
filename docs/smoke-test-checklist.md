# NinetyNine Smoke Test Checklist

<!-- markdownlint-disable MD024 MD040 MD060 -->
<!-- MD024: checklists naturally repeat subsection names like "Prerequisites". -->
<!-- MD040/MD060: cosmetic — code fences and table column alignment. -->

Executed against: __________  (local / staging / prod)
Date: __________
Tester: __________
Browser: __________  (Chrome X / Firefox X / Safari X)
Result: __________  (PASS / FAIL / BLOCKED)

---

## Prerequisites

- Stack is running locally (`./deploy.sh up`) OR deployed to the Azure VM.
- `GET /healthz` returns HTTP 200 before proceeding.
- **Local only:** `.env` exists (auto-created from `.env.example` on first run); `Auth:Mock:Enabled` is `true` in `appsettings.Development.json`.
- **Staging / prod:** real Google OAuth credentials are configured; mock auth is disabled.
- Browser DevTools console is open throughout the run.
- Network tab is open with "Preserve log" enabled.

---

## Test Data

| Item | Value |
|---|---|
| Seeded players | `carey` / `george` / `carey_b` |
| Dev password (all three) | `Test1234!a` |
| Email template | `{displayName}@example.local` (e.g., `carey@example.local`) |
| Mock sign-in endpoint | `GET /mock/signin-as?displayName={name}` |
| New-account mock flow | `GET /mock/signin-new` |
| Seeded venues | Home Table (private), Summerville Billiards (public) |
| QA test email | `qatest@example.local` |
| QA test display name | `qatest` |

---

## Steps

### 1. Unauthenticated Landing

- [ ] Navigate to `http://localhost:8080` (or the deployed URL).
- [ ] Verify: the page title in the browser tab reads "NinetyNine — Pool Scorekeeper".
- [ ] Verify: background is dark charcoal, not white — dark theme is the default.
- [ ] Verify: a hero photograph of a pool table loads and is visible (no broken image icon).
- [ ] Verify: the "NinetyNine" wordmark is visible inside the hero overlay.
- [ ] Verify: the tagline "Score the classic P&B pool game..." is visible.
- [ ] Verify: a "Sign in" button is present and links to `/login`.
- [ ] Verify: a "Create account" button is present and links to `/register`.
- [ ] Verify: the authenticated quick-links grid ("Jump back in") is NOT visible.
- [ ] Verify: the feature highlights section ("Built for the table") renders with three feature articles.
- [ ] Verify: the "How NinetyNine scoring works" collapsible `<details>` is present and collapsed by default.
- [ ] Click the collapsible summary — verify the rules list expands.
- [ ] Verify: no JavaScript errors appear in the DevTools console.
- [ ] Verify: no 4xx or 5xx responses appear in the Network tab for static assets (CSS, JS, images, icons).

---

### 2. Sign In via Dev Mock Picker (Local Dev)

- [ ] Click "Sign in" from the hero CTA or navigate directly to `/login`.
- [ ] Verify: the page title reads "Sign in — NinetyNine".
- [ ] Verify: an email/password form is visible with labelled "Email address" and "Password" fields.
- [ ] Verify: links for "Forgot password?" and "Resend verification email" are present.
- [ ] Verify: the development-only mock auth section is visible (yellow warning banner + "Sign in as a test player" heading).
- [ ] Verify: the mock picker lists exactly three players: `carey`, `george`, `carey_b`.
- [ ] Click "Sign in" next to `carey`.
- [ ] Verify: the browser is redirected to `/` (home page).
- [ ] Verify: the hero now shows "Welcome back, carey." (authenticated greeting).
- [ ] Verify: the "Sign in" / "Create account" CTAs have been replaced by "Start new game" / "View history".
- [ ] Verify: the quick-links grid ("Jump back in") is now visible with four tiles.
- [ ] Verify: the sidebar / nav menu shows the user's display name or avatar, not a bare "Sign in" link.
- [ ] Verify: no JavaScript errors in the DevTools console.

---

### 3. Sign In via Email and Password (All Environments)

- [ ] Sign out first (User menu > "Sign out") if currently authenticated.
- [ ] Navigate to `/login`.
- [ ] Enter email `carey@example.local` and password `Test1234!a`.
- [ ] Click "Sign in".
- [ ] Verify: the server returns a redirect (302 or 303) and sets the `NinetyNine.Auth` cookie.
- [ ] Verify: the browser lands on `/` as an authenticated user (same checks as step 2 above).
- [ ] Navigate back to `/login` — verify: the auth cookie is still set (not cleared on navigation).
- [ ] Repeat the sign-in test with invalid credentials (`carey@example.local` / `wrongpassword`).
- [ ] Verify: the form remains on `/login` and shows "Invalid email or password." error.
- [ ] Verify: the error alert has `role="alert"` (screen-reader accessible).

---

### 4. Register a New Account

- [ ] Sign out if currently authenticated.
- [ ] Navigate to `/register`.
- [ ] Verify: the page title reads "Create account — NinetyNine".
- [ ] Verify: four labelled form fields are visible: Email address, Display name, Password, Confirm password.
- [ ] Verify: the password hint text reads "At least 10 characters with uppercase, lowercase, digit, and symbol."
- [ ] Verify: the display name hint reads "2–32 characters. Letters, numbers, _ and - only."
- [ ] Fill the form: Email = `qatest@example.local`, Display name = `qatest`, Password = `Test1234!a`, Confirm password = `Test1234!a`.
- [ ] Click "Create account".
- [ ] Verify: the form disappears and a green success alert appears: "Account created. Check your email to verify your account before signing in."
- [ ] Verify: a "Back to sign in" link appears inside the success alert.
- [ ] Open a terminal and run `./deploy.sh logs web` — verify: the `ConsoleEmailSender` has logged a line containing a `/verify-email?token=` URL.
- [ ] Copy the full `/verify-email?token=...` URL from the container log.
- [ ] Paste the URL into the browser (or open a new tab).
- [ ] Verify: the page shows a success message confirming email verification.
- [ ] Navigate to `/login` and sign in as `qatest@example.local` / `Test1234!a`.
- [ ] Verify: successful sign-in, redirected to home as `qatest`.
- [ ] **Negative test:** attempt to register a second account with the same email `qatest@example.local`.
- [ ] Verify: the server returns an error message indicating the email is already taken.
- [ ] **Negative test:** attempt to register with display name `carey` (already taken).
- [ ] Verify: the server returns an error message indicating the display name is already taken.

---

### 5. Forgot Password Flow

- [ ] Sign out and navigate to `/forgot-password`.
- [ ] Verify: the page title reads "Forgot password — NinetyNine" (or similar).
- [ ] Verify: a single email field with label is present.
- [ ] Enter `carey@example.local` and submit.
- [ ] Verify: the form shows "If that email exists, a reset link has been sent." (no user enumeration).
- [ ] Check container logs (`./deploy.sh logs web`) — verify: a `/reset-password?token=...` URL is logged.
- [ ] Copy the token URL and navigate to it.
- [ ] Verify: a form appears with "New password" and "Confirm password" fields.
- [ ] Enter `NewPass1234!b` in both fields and submit.
- [ ] Verify: redirected to `/login` with a success message.
- [ ] Sign in with `carey@example.local` / `NewPass1234!b` — verify success.
- [ ] (Restore original password: sign in, navigate to profile edit, or re-run `./deploy.sh seed`.)
- [ ] **Negative test:** attempt the reset-password URL a second time (token should be consumed).
- [ ] Verify: an "invalid or expired token" error is shown.

---

### 6. Start and Play a Game

*Prerequisite: signed in as `carey`.*

- [ ] Navigate to `/games/new` (or click "Start new game" from nav).
- [ ] Verify: the page title reads "New Game — NinetyNine".
- [ ] Verify: a venue dropdown and table-size picker are present.
- [ ] Verify: "Home Table" and "Summerville Billiards" appear in the venue dropdown.
- [ ] Select "Home Table" and choose a table size (e.g., 7 foot).
- [ ] Click "Start game".
- [ ] Verify: the browser redirects to `/games/{id}/play` where `{id}` is a valid GUID.
- [ ] Verify: the page title updates to "Frame 1 / 9 — NinetyNine".
- [ ] Verify: the score card grid renders with 9 frame columns.
- [ ] Verify: frame 1 has the active state (highlighted border or accent color).
- [ ] Verify: frames 2–9 show placeholder dashes, not zeros.
- [ ] Click or tap the active frame 1 cell.
- [ ] Verify: the `FrameInputDialog` opens with fields for Break Bonus and Ball Count.
- [ ] Enter Break Bonus = `1`, Ball Count = `5`.
- [ ] Click "Submit" (or equivalent confirm button).
- [ ] Verify: frame 1 now shows Break Bonus = 1, Ball Count = 5, Running Total = 6.
- [ ] Verify: frame 2 is now the active frame.
- [ ] Verify: no JavaScript errors in the DevTools console.
- [ ] Continue entering scores for frames 2–8 (any valid values, e.g., Break Bonus 0, Ball Count 7 each).
- [ ] For frame 9, enter Break Bonus = `1`, Ball Count = `9` (Running Total should reach the game total).
- [ ] Submit frame 9.
- [ ] Verify: a "Game Complete!" celebration state appears with the final score and a "/ 99" denominator.
- [ ] Verify: "View details" and "New game" action buttons are present.
- [ ] Verify: no JavaScript errors throughout the game session.

---

### 7. View Game History and Details

*Prerequisite: at least one completed game exists.*

- [ ] Navigate to `/games` (History link in nav).
- [ ] Verify: the page title reads "Game History — NinetyNine" (or similar).
- [ ] Verify: the completed game from step 6 appears in the list.
- [ ] Verify: the list entry shows the venue name, date, and final score.
- [ ] Click the completed game entry.
- [ ] Verify: the browser navigates to `/games/{id}` (details page).
- [ ] Verify: the score card grid renders in read-only (View) mode — no "Score frame" buttons.
- [ ] Verify: all 9 frames are filled with their recorded scores.
- [ ] Verify: a stats summary card shows the final score, perfect frames count, and average ball count.
- [ ] Verify: a "Back to history" or equivalent navigation link is present.
- [ ] Verify: no JavaScript errors on the details page.

---

### 8. Profile and Editing

*Prerequisite: signed in as `carey`.*

- [ ] Open the user menu (avatar or display name in the nav).
- [ ] Verify: the user menu shows the display name "carey" and options including "View profile" and "Sign out".
- [ ] Click "View profile".
- [ ] Verify: navigated to `/players/me` or a profile page.
- [ ] Verify: the profile shows the avatar (or initials fallback), display name, and a stats summary.
- [ ] Verify: recent games list is present (even if empty).
- [ ] Click "Edit profile".
- [ ] Verify: navigated to `/players/me/edit` (or edit profile page).
- [ ] Verify: the edit form has fields for display name, email, first name, last name, and visibility toggles.
- [ ] Change the display name from `carey` to `carey_x`.
- [ ] Toggle at least one visibility flag (e.g., toggle "Real name" visibility).
- [ ] Click "Save".
- [ ] Verify: redirected back to the profile page.
- [ ] Verify: the profile page shows the updated display name `carey_x`.
- [ ] Verify: the user menu also reflects `carey_x`.
- [ ] Edit the profile again and revert the display name to `carey` before proceeding.

---

### 9. Venues

*Prerequisite: signed in as `carey`.*

- [ ] Navigate to `/venues`.
- [ ] Verify: the venue list shows at least the two seeded venues: "Home Table" and "Summerville Billiards".
- [ ] Verify: each venue card displays the name and an edit/manage option.
- [ ] Click "Add venue" (or navigate to `/venues/new`).
- [ ] Verify: a form with fields for Name, Address, and IsPublic toggle is visible.
- [ ] Fill in: Name = `QA Test Venue`, Address = `123 Test St`.
- [ ] Submit the form.
- [ ] Verify: redirected to `/venues` and "QA Test Venue" appears in the list.
- [ ] Click "QA Test Venue" or its edit link.
- [ ] Verify: navigated to `/venues/{id}/edit`.
- [ ] Change the address to `456 Updated Ave`.
- [ ] Save.
- [ ] Verify: redirected back to the list or venue detail with the updated address.
- [ ] Initiate deletion of "QA Test Venue" (click delete or trash icon).
- [ ] Verify: a confirmation dialog or warning appears before deletion proceeds.
- [ ] Confirm the deletion.
- [ ] Verify: "QA Test Venue" is no longer in the venue list.
- [ ] Verify: no JavaScript errors throughout the venue flow.

---

### 10. Statistics

*Prerequisite: signed in as `carey`, at least one completed game exists.*

- [ ] Navigate to `/stats`.
- [ ] Verify: the page title reads "Leaderboard — NinetyNine" (or similar).
- [ ] Verify: a ranked list of players appears, sorted by average score or total games.
- [ ] Verify: `carey` appears in the leaderboard.
- [ ] Verify: each row shows the player's display name and at least one numeric stat.
- [ ] Navigate to `/stats/me`.
- [ ] Verify: personal stats page renders with summary cards (e.g., Games Played, Average Score, Best Game, Perfect Frames).
- [ ] Verify: a "Best games" or "Recent games" table or list is present.
- [ ] Verify: no JavaScript errors on either stats page.

---

### 11. Responsive Mobile Layout

*Use DevTools device emulation. Suggested device: iPhone SE (375 × 667).*

- [ ] Open DevTools, enable device toolbar, set to 375 × 667 (or closest preset).
- [ ] Navigate to `/` (home page).
- [ ] Verify: the sidebar or top nav collapses — a hamburger or menu button is visible.
- [ ] Verify: the hero photograph is still visible and not cropped in a jarring way.
- [ ] Verify: the "Sign in" and "Create account" CTAs are visible and not cut off.
- [ ] Tap the hamburger / menu button.
- [ ] Verify: the off-canvas or overlay nav opens with all nav links visible.
- [ ] Navigate to `/games/{id}/play` (a live or just-started game).
- [ ] Verify: the score card switches to a single-frame-focused view with a picker strip for frame navigation (or scrolls horizontally with frames visible).
- [ ] Verify: the "Score frame" button is large enough to tap comfortably (target appears at least 44 × 44 px, visually).
- [ ] Navigate to `/register`.
- [ ] Verify: the registration form fields stack vertically (not side-by-side).
- [ ] Verify: form labels are above their inputs, not inline.
- [ ] Verify: the "Create account" submit button spans the full width of the form card.
- [ ] Navigate to `/venues/new`.
- [ ] Verify: the add-venue form also stacks correctly.
- [ ] Verify: no horizontal scroll bar appears on any of the tested pages.
- [ ] Verify: no JavaScript errors during mobile emulation.

---

### 12. Light Mode Toggle

- [ ] Sign in as any user.
- [ ] Open the user menu.
- [ ] If a "Light mode" or "Theme" toggle is present:
  - [ ] Click the toggle to switch to light mode.
  - [ ] Verify: the page background changes to a light color (near white or light grey).
  - [ ] Verify: text remains legible (dark text on light background, contrast maintained).
  - [ ] Verify: icons remain visible on the light background.
  - [ ] Navigate to another page and verify: the light-mode preference persists (cookie-backed).
  - [ ] Switch back to dark mode.
  - [ ] Verify: dark theme is restored.
- [ ] If no toggle exists yet (feature not yet implemented):
  - [ ] Mark this section as `SKIPPED — light mode toggle not yet implemented`.

---

### 13. Sign Out

- [ ] Sign in as `carey` (if not already).
- [ ] Open the user menu.
- [ ] Click "Sign out".
- [ ] Verify: the browser is redirected to `/` (home page) or `/login`.
- [ ] Verify: the `NinetyNine.Auth` cookie is cleared (check Application > Cookies in DevTools).
- [ ] Verify: the hero shows the unauthenticated CTAs ("Sign in" and "Create account") — NOT the authenticated state.
- [ ] Verify: the quick-links grid ("Jump back in") is no longer visible.
- [ ] Attempt to navigate directly to `/games` (protected route).
- [ ] Verify: redirected to `/login` (not a blank page or a 403/500 error).
- [ ] Attempt to navigate directly to `/games/new`.
- [ ] Verify: redirected to `/login`.
- [ ] Attempt to navigate directly to `/venues` (if also protected).
- [ ] Verify: redirected to `/login` (or shows a public view if the route is public).
- [ ] Verify: no JavaScript errors during the sign-out flow.

---

### 14. Dev Theme Test Page (Local Dev Only)

- [ ] Ensure the stack is running in Development mode (`ASPNETCORE_ENVIRONMENT=Development`).
- [ ] Navigate to `/dev/theme-test`.
- [ ] Verify: the page loads without error and the title reads "Dev — Theme Test — NinetyNine".
- [ ] Verify: the "Development only" pill badge is visible in the page header.
- [ ] **Typography section:** heading levels h1–h6, body copy, links, strong/em/code, and monospace numerals all render.
- [ ] **Buttons section:** Primary, Secondary, Accent, Danger, Ghost, and Disabled buttons are visible; small, medium, and large size variants render.
- [ ] **Form controls section:** text input, email input, select, password (with invalid state), textarea, checkboxes, radio buttons, and the dark-mode switch all render.
- [ ] **Alerts section:** Info, Success, Warning, and Danger alert variants are all visible.
- [ ] **Cards section:** plain card, accent card (with hover style), and empty-state card all render; the empty-state icon loads.
- [ ] **Tables section:** the striped leaderboard sample table renders with all three seeded player rows.
- [ ] **Badges and pills section:** five badge variants and three pill variants render.
- [ ] **Phosphor icon gallery (section 8):** verify that exactly 42 icon cells are rendered in the grid.
- [ ] Verify: none of the 42 icons show a broken-image placeholder — all SVG files load successfully.
- [ ] **Hero image section (section 9):** the billiards photograph renders inside the aspect-ratio container with overlay text.
- [ ] **Color token swatches (section 10):** all 14 swatch cells are visible; each swatch background is a distinct color (not all the same).
- [ ] Verify: the page is scrollable to the bottom with no layout overflow issues.
- [ ] Verify: no JavaScript errors on the theme test page.
- [ ] **Prod guard:** on a staging or production environment, navigate to `/dev/theme-test`.
  - [ ] Verify: the page renders "Not found" — the test harness is hidden in non-Development environments.

---

### 15. Health Check Endpoint

- [ ] Navigate to `/healthz` (or `curl -s http://localhost:8080/healthz`).
- [ ] Verify: the response is HTTP 200.
- [ ] Verify: the response body is `Healthy` (or a JSON health report).
- [ ] Verify: the MongoDB health check component is listed as healthy (if JSON format).
- [ ] **Negative test:** stop the MongoDB container (`docker compose -f docker-compose.dev.yml stop mongo`), then re-request `/healthz`.
- [ ] Verify: the response is HTTP 503 (Unhealthy) or similar degraded status.
- [ ] Restart MongoDB (`docker compose -f docker-compose.dev.yml start mongo`) and confirm `/healthz` returns 200 again.

---

### 16. Friends + Communities Foundation Migration (Sprint 0)

*Post-Sprint-0 data-layer verification. No UI changes ship in Sprint 0 — this section verifies the heal pass, new collections, and indexes without touching the browser. Remove or expand this section once Sprint 1 lands the `/friends` page (§17) and Sprint 2 lands `/communities` (§18).*

#### Prerequisites

- Stack running via `./deploy.sh up`.
- Fresh container logs available (`./deploy.sh logs web`).

#### 16.1 — Visibility heal pass fired on first-after-S0 startup

- [ ] Run `./deploy.sh logs web | grep "Migrated visibility"`.
- [ ] Verify: one "Migrated visibility for player X" line per seeded player (`carey`, `george`, `carey_b`) on the first startup after S0.5 lands.
- [ ] Verify: a "Profile visibility heal: migrated N player(s) to SchemaVersion 2." summary line follows.

#### 16.2 — Second startup is a no-op (idempotency)

- [ ] `docker compose -f docker-compose.dev.yml restart web`.
- [ ] `./deploy.sh logs web | grep -E "Migrated visibility|Profile visibility heal"` (limited to the most recent startup).
- [ ] Verify: no new "Migrated visibility" lines.
- [ ] Verify: the only DataSeeder message is "Seed skipped — test players already exist." (no trailing count of heal/migration/venue additions).

#### 16.3 — Audience enum persisted on every player

- [ ] Run:

  ```bash
  docker exec ninetynine-mongo-1 mongosh \
    "mongodb://root:devpassword@localhost:27017/NinetyNine?authSource=admin" \
    --quiet --eval 'db.players.find({}, {displayName:1, schemaVersion:1, visibility:1}).toArray()'
  ```

- [ ] Verify: every player document has `schemaVersion: 2`.
- [ ] Verify: every player's `visibility` embeds `emailAudience`, `phoneAudience`, `realNameAudience`, `avatarAudience` fields.
- [ ] Verify: the locked migration map holds — `emailAudience`, `phoneAudience`, `realNameAudience` are all `"Private"` for the seeded players (they had `false` bool flags); `avatarAudience` is `"Public"`.
- [ ] Verify: legacy bool fields (`emailAddress`, `phoneNumber`, `realName`, `avatar`) are still present for backward-compat reads (removed in Sprint 3).

#### 16.4 — Four new collections exist with every index

- [ ] Run:

  ```bash
  docker exec ninetynine-mongo-1 mongosh \
    "mongodb://root:devpassword@localhost:27017/NinetyNine?authSource=admin" \
    --quiet --eval '
      ["friendships","friend_requests","communities","community_members"].forEach(c => {
        const idxs = db.getCollection(c).getIndexes();
        print(c + ": " + idxs.length + " indexes");
      });'
  ```

- [ ] Verify: `friendships` reports 4 indexes (including `ux_friendships_playerIdsKey` UNIQUE).
- [ ] Verify: `friend_requests` reports 4 indexes (including `ux_friend_requests_pending_pair` UNIQUE PARTIAL).
- [ ] Verify: `communities` reports 6 indexes (including `ux_communities_name_ci` UNIQUE with collation and `ux_communities_slug` UNIQUE).
- [ ] Verify: `community_members` reports 4 indexes (including `ux_community_members_player_community` UNIQUE).
- [ ] Verify: `venues.getIndexes()` includes `idx_venues_communityId` (sparse).

#### 16.5 — No visible UI change

- [ ] Navigate to `http://localhost:8080/` and sign in as any seeded player via the mock picker.
- [ ] Verify: every existing smoke-test section (§1–§15) still passes exactly as before. No new nav items, no new pages, no visual regressions. Sprint 0 is pure data layer.

---

### 17. Friends end-to-end (Sprint 1)

*First sprint that ships user-facing UI for Friends. Exercises the full send-accept-decline-cancel lifecycle plus the sidebar badge, the Home card, and the seeded pre-befriended state.*

#### Prerequisites

- Stack running via `./deploy.sh up`.
- Signed in as `carey` via the mock picker (`http://localhost:8080/mock/signin-as?displayName=carey`).

#### 17.1 — Seeded pre-befriended friends

- [ ] Navigate to `/friends` (or tap the sidebar **Friends** link).
- [ ] Verify: the Friends tab is active by default.
- [ ] Verify: the friends list shows **carey_b** and **george**, sorted alphabetically. Each row has an avatar (initials), display name, and a "Remove" button.
- [ ] Verify: the tab badge next to "Friends" shows `2`.
- [ ] Verify: the sidebar **Friends** nav link shows no pending-request badge (count is 0).

#### 17.2 — Send a friend request from the Find tab

- [ ] Open a second browser window (private / different profile) and sign in as **george** via `http://localhost:8080/mock/signin-as?displayName=george`. *(Needed because we don't yet have a fourth seeded player to friend fresh.)*
- [ ] Return to the `carey` window. First **remove** `george` from Friends (so we can re-friend via the UI). Confirm the Friends list drops to just `carey_b`.
- [ ] Click **Find friends** tab.
- [ ] Type `george` into the search box and click **Search**.
- [ ] Verify: one result card shows `george` with the appropriate chip. Since you just unfriended, the relationship should read `None` and the action button should be **Send request**.
- [ ] Click **Send request**.
- [ ] Verify: the browser redirects to `/friends?tab=requests&flash=sent` with the success banner "Friend request sent."
- [ ] Verify: the **Outgoing** section lists george with a **Cancel** button.

#### 17.3 — Accept the request from the other account

- [ ] Switch to the `george` window.
- [ ] Verify: the **Home** page renders a prominent **Friend requests** card above the "Jump back in" grid with "1 pending" and a preview row showing `carey`.
- [ ] Verify: the sidebar **Friends** link shows a teal badge with the number `1`.
- [ ] Click **View all requests**.
- [ ] Verify: redirected to `/friends?tab=requests`, and the **Incoming** section lists carey with Accept / Decline buttons.
- [ ] Click **Accept**.
- [ ] Verify: redirect to `/friends?tab=friends&flash=accepted` with "Friend request accepted."
- [ ] Verify: carey now appears in george's Friends list.

#### 17.4 — Cancel and decline error paths

- [ ] In the `carey` window, navigate back to `/friends?tab=find`, search for `george` again. Verify the chip now reads **Friends**.
- [ ] Click **Remove** on george in the Friends tab to unfriend.
- [ ] Search `george` again and click **Send request**.
- [ ] Switch to the `george` window, navigate to `/friends?tab=requests`, click **Decline** on the incoming request.
- [ ] Verify: the Incoming section becomes empty and the flash reads "Friend request declined."
- [ ] Switch back to the `carey` window, search `george`, try **Send request** a second time.
- [ ] Verify: the page shows the inline error "This player declined a recent friend request. You can try again later." (the 90-day `FriendRequestCooldown`).

#### 17.5 — Rate-limit message

- [ ] Sign into 11 non-existent target accounts would require data setup — skip unless you want to validate the `10-outbound-pending` cap. *(Unit test `Max10PendingOutbound_BlocksEleventh` covers this path automatically.)*

#### 17.6 — No visible regression

- [ ] Navigate through `/games`, `/stats`, `/venues`, `/players/me` — verify none of the Sprint 0 / existing pages have layout regressions from the new sidebar item or cascading badge.

---

### 18. Communities end-to-end (Sprint 2)

*Sprint 2 ships player-owned communities with public + private visibility, an invite flow, a join-request flow, profile surfaces, and a seeded canonical community "Pocket Sports".*

#### Prerequisites

- Stack running via `./deploy.sh up`.
- Signed in as `carey` via the mock picker.

#### 18.1 — Seeded "Pocket Sports" community

- [ ] Navigate to `/communities`.
- [ ] Verify: **My communities** section lists "Pocket Sports" with "3 members" and a **Member** pill.
- [ ] Click the card to open `/communities/{id}`.
- [ ] Verify: title "Pocket Sports", Public pill, description, "3 members" meta.
- [ ] Verify: the Members list shows carey (Owner), george (Member), carey_b (Member) in join-order.
- [ ] Verify: since carey is the owner, the header shows a **Settings** button (not **Leave community**).
- [ ] Verify: Venues section shows the Sprint-3-placeholder copy.

#### 18.2 — Browse public communities

- [ ] On `/communities`, the "Browse public communities" section should also show Pocket Sports (because it's public).
- [ ] Search for `pocket` in the browse box and click Search.
- [ ] Verify: Pocket Sports still appears.
- [ ] Search for a prefix that doesn't match.
- [ ] Verify: "No public communities matched \"...\"." empty-state copy.

#### 18.3 — Create a new player-owned community

- [ ] Click **Create community** in the `/communities` header.
- [ ] Verify: `/communities/new` renders with Name / URL slug / Description / Visibility (Public + Private radio) fields.
- [ ] Type a unique name; watch the Slug field auto-generate.
- [ ] Submit with `Visibility = Private`.
- [ ] Verify: redirected to `/communities/{id}?flash=created` with "Community created." banner.
- [ ] Verify: the new community appears on `/communities` in **My communities** with "1 member".
- [ ] Verify: the detail page shows you as the Owner and renders the **Invite a player** section (owner-only).

#### 18.4 — Invite flow via /friends

- [ ] Open a second browser profile and sign in as `george` via the mock picker.
- [ ] Return to the carey window. On the new private community's detail page, type `george` in the "Invite a player" box and submit.
- [ ] Verify: redirected with "Invitation sent." flash.
- [ ] Switch to the george window. Home page: the sidebar **Friends** badge should count the community invitation (+1).
- [ ] Click **Friends** → **Requests** tab.
- [ ] Verify: a **Community invitations** section renders above the friend requests section, listing the new community with Accept / Decline buttons.
- [ ] Click **Accept**.
- [ ] Verify: redirect with "Friend request accepted." flash, and george is now a member on the community's detail page.

#### 18.5 — Join request flow (private community)

- [ ] In the carey window, create another private community.
- [ ] Switch to the george window and try to navigate to `/communities/{id}` directly using the new community's guid.
- [ ] Verify: 404-style "Community not found" page — private communities are fully hidden from non-members.
- [ ] For this step to work under current UX (no public discovery of private communities), use the direct detail route for invite-based flows and wait for the Sprint 4 in-app discovery surface. Mark this sub-step as **deferred to Sprint 4**.

#### 18.6 — Leave flow

- [ ] As george, navigate to Pocket Sports from the `/communities` list.
- [ ] Click **Leave community**.
- [ ] Verify: redirect to `/communities?flash=left` with the "You left the community." banner.
- [ ] Verify: Pocket Sports no longer appears in **My communities**.

#### 18.7 — Profile Communities section

- [ ] As carey, view `/players/me`.
- [ ] Verify: a **Communities** card renders between **Statistics** and **Recent games**, listing Pocket Sports (and any private community you created where you're the owner).
- [ ] Click Pocket Sports — verify it links to the community detail page.
- [ ] As a test of the privacy filter, view another player's profile (`/players/{guid}`) — verify the Communities card shows only communities you share or that are public.

#### 18.8 — No regression

- [ ] Navigate through `/games`, `/stats`, `/venues`, `/friends`, `/players/me` — verify all existing pages still render cleanly after the Sprint 2 additions.

### 19. Venue affiliation + Audience UI (Sprint 3)

*Sprint 3 adds venue → community affiliation, per-field audience pickers on Edit Profile, a migration banner, and viewer-scoped profile rendering that applies the full audience matrix.*

#### Prerequisites

- Stack running via `./deploy.sh rebuild` (picks up Sprint 3 code).
- Signed in as `carey` via the mock picker.
- `george` and `carey_b` exist as seeded players.
- `carey` and `george` are mutual friends (seeded in Sprint 1).
- `carey`, `george`, and `carey_b` are members of the seeded "Pocket Sports" community (Sprint 2).

#### 19.1 — Migration banner appears and dismisses

- [ ] Navigate to `/players/me/edit`.
- [ ] If `carey`'s `migrationBannerDismissed` flag is `false`, verify: a blue info banner reads *"We've moved from simple on/off visibility to four audience tiers…"* with a **Got it** button.
- [ ] Click **Got it**.
- [ ] Verify: the banner disappears and does not return on subsequent page loads.

#### 19.2 — Audience picker saves and persists

- [ ] On `/players/me/edit`, verify: four audience picker rows (Email, Phone, Real name, Avatar) are rendered in the Privacy section.
- [ ] Set Email audience to **Friends**. Set Phone to **Communities**. Leave Real name at **Private** and Avatar at **Public**.
- [ ] Click **Save changes**.
- [ ] Verify: redirect to `/players/me` (or flash confirmation).
- [ ] Return to `/players/me/edit`.
- [ ] Verify: the pickers show the previously saved values — Email = Friends, Phone = Communities, Real name = Private, Avatar = Public.

#### 19.3 — Profile view as Self shows everything

- [ ] Navigate to `/players/me`.
- [ ] Verify: all fields are visible regardless of audience settings — display name, real name (if set), email, phone, avatar, stats, recent games.
- [ ] Verify: the **Edit profile** button is present.

#### 19.4 — Profile view as Friend shows friend-tier fields

- [ ] Sign in as `george` (carey's friend) via a second browser profile / incognito.
- [ ] Navigate to `/players/{carey's guid}`.
- [ ] Verify: **Email** appears in the Contact card (audience = Friends; george is a friend ✓).
- [ ] Verify: **Phone** appears (audience = Communities; george is in Pocket Sports with carey ✓ — Communities is a wider tier than Friends, so friend also qualifies).
- [ ] Verify: **Real name** does NOT appear (audience = Private; only Self can see).
- [ ] Verify: **Avatar** is visible (audience = Public).

#### 19.5 — Profile view as same-community member shows community-tier fields but not friend-only

- [ ] Sign in as `carey_b` (shares Pocket Sports with carey but is NOT a friend).
- [ ] Navigate to `/players/{carey's guid}`.
- [ ] Verify: **Email** does NOT appear (audience = Friends; carey_b is not carey's friend).
- [ ] Verify: **Phone** DOES appear (audience = Communities; carey_b shares Pocket Sports ✓).
- [ ] Verify: **Real name** does NOT appear (audience = Private).
- [ ] Verify: **Avatar** is visible (audience = Public).

#### 19.6 — Profile view as stranger shows only public-tier fields

- [ ] Register a brand-new test account (or use a player not in any shared community or friendship with carey).
- [ ] Navigate to `/players/{carey's guid}`.
- [ ] Verify: **Email**, **Phone**, and **Real name** do NOT appear.
- [ ] Verify: **Avatar** is visible (audience = Public).
- [ ] Verify: **Display name**, **Member since**, and **Statistics** are visible (ungated fields).

#### 19.7 — Private venue in game history renders anonymously for non-members

- [ ] As `carey`, create a private venue on `/venues/new` (check the "Private" box).
- [ ] Play a game at the private venue (or use `mongosh` to insert a game doc referencing the private venue).
- [ ] View carey's profile as `george` or `carey_b`.
- [ ] Verify: the game row for the private venue shows *"Private venue"* in italic, muted text — not the actual venue name. No link is emitted.
- [ ] View carey's profile as `carey` (Self).
- [ ] Verify: the private venue's actual name appears normally (carey is the venue creator).

#### 19.8 — Venue affiliation picker and chip

- [ ] Sign in as `carey`. Navigate to `/venues/{id}/edit` for any venue carey created.
- [ ] Verify: a **Community affiliation** section appears with a dropdown listing "None" and every community carey is a member of.
- [ ] Select "Pocket Sports". Submit.
- [ ] Verify: redirect with "Affiliation saved." flash.
- [ ] Navigate to `/venues`.
- [ ] Verify: the affiliated venue's card shows a community chip linking to `/communities/{id}`.

#### 19.9 — No regression

- [ ] Navigate through `/games`, `/stats`, `/venues`, `/friends`, `/communities`, `/players/me` — verify all existing pages still render cleanly after the Sprint 3 additions.
- [ ] Repeat on a second browser (Firefox or Safari) for cross-browser confidence.

### 20. Admin role, leaderboard filters, ownership transfer, expiration sweep (Sprint 4)

*Sprint 4 widens community admin powers, adds leaderboard filtering by friends/community, implements two-phase ownership transfer, and sweeps stale pending items on startup.*

#### Prerequisites

- Stack running via `./deploy.sh rebuild`.
- Signed in as `carey` (owner of Pocket Sports) via the mock picker.
- `george` and `carey_b` exist as seeded players and Pocket Sports members.

#### 20.1 — Admin role: promote and demote

- [ ] Navigate to `/communities/{Pocket Sports id}`.
- [ ] Verify: the Members section shows carey as **Owner** (gold badge), george and carey_b as **Member**.
- [ ] As Owner, click **Make Admin** next to george.
- [ ] Verify: redirect with "Role updated." flash. george's badge changes to **Admin** (teal).
- [ ] Click **Remove Admin** next to george.
- [ ] Verify: george's badge reverts to **Member**.

#### 20.2 — Admin can invite, approve, remove

- [ ] Promote george to Admin.
- [ ] Sign in as george in a second browser.
- [ ] On the Pocket Sports detail page, verify: george sees the **Invite a player** section and any pending join requests.
- [ ] As george (Admin), type carey_b's display name and send an invite — this should fail with "That player is already a member." Confirm the error flash.
- [ ] Register a new test player in a third browser. As george (Admin), invite that player. Verify success.
- [ ] As george (Admin), try to remove carey_b. Verify success (Admin can remove Member).
- [ ] Promote carey_b back via carey (Owner), then as george (Admin) try to remove carey_b again — verify failure with "Only the community owner can remove an admin."

#### 20.3 — Transfer ownership (two-phase)

- [ ] As carey (Owner), scroll to the **Transfer ownership** section on the Pocket Sports detail page.
- [ ] Type `george` in the display-name field and click **Send request**.
- [ ] Verify: redirect with "Ownership transfer request sent." flash.
- [ ] Verify: the Transfer ownership section now shows "Ownership transfer pending — waiting for george to respond. Expires {date}."
- [ ] Switch to george's browser. Navigate to Pocket Sports.
- [ ] Verify: a warning banner reads "carey wants to hand you ownership of Pocket Sports." with **Accept ownership** / **No thanks** buttons.
- [ ] Click **Accept ownership**.
- [ ] Verify: redirect with "You are now the owner." flash. george is now Owner, carey is Member.
- [ ] (Optional) Transfer back to carey to restore the original state.

#### 20.4 — Transfer ownership decline

- [ ] As the current owner, initiate a transfer to another member.
- [ ] As the target, click **No thanks**.
- [ ] Verify: redirect with "Ownership transfer declined." flash.
- [ ] As the owner, verify: the Transfer ownership section shows the input again (no pending transfer).

#### 20.5 — Leaderboard filters

- [ ] Navigate to `/stats`.
- [ ] Verify: an authenticated viewer sees a filter dropdown with "All players", "Just friends", and an optgroup "My communities" containing "Pocket Sports" (and any other communities the viewer belongs to).
- [ ] Select "Just friends" and click Filter.
- [ ] Verify: only the viewer and their mutual friends appear (carey + george if they are friends).
- [ ] Select "Pocket Sports" and click Filter.
- [ ] Verify: only Pocket Sports members with completed games appear.
- [ ] Select "All players" and click Filter.
- [ ] Verify: the full leaderboard is restored.

#### 20.6 — Expiration sweep (log verification)

- [ ] Run `./deploy.sh rebuild` (triggers DataSeeder on startup).
- [ ] Check `./deploy.sh logs web` for the sweep output.
- [ ] Verify: no errors related to the sweep. If stale pending items existed, the log shows "Expiration sweep: N friend request(s), N invitation(s), N join request(s), N ownership transfer(s)."
- [ ] If no stale items exist (fresh dev DB), verify the sweep runs silently (no error, no log entry for zero counts).

#### 20.7 — No regression

- [ ] Navigate through `/games`, `/stats`, `/venues`, `/friends`, `/communities`, `/players/me`, `/players/me/edit` — verify all existing pages still render cleanly after the Sprint 4 additions.

### 21. Discovery, notifications, block, audit log (Sprint 5)

*Sprint 5 adds enhanced community browse cards, in-app notifications with an unread badge, email notification stubs, bidirectional player blocking, and an activity/audit log page.*

#### Prerequisites

- Stack running via `./deploy.sh rebuild`.
- Signed in as `carey` via the mock picker.
- `george` and `carey_b` exist as seeded players.

#### 21.1 — Enhanced community cards

- [ ] Navigate to `/communities`.
- [ ] Verify: each community card now shows the owner's display name (crown icon) and affiliated venue count (map-pin icon + "N venues") alongside the existing member count.
- [ ] Verify: the owner name and venue count appear in both "My communities" and "Browse public communities" sections.

#### 21.2 — Notifications page and badge

- [ ] As carey, send a friend request to carey_b (if not already friends).
- [ ] Sign in as carey_b in a second browser.
- [ ] Verify: the nav sidebar shows "Notifications" with a badge count > 0.
- [ ] Click "Notifications".
- [ ] Verify: `/notifications` renders with the friend request notification ("carey sent you a friend request."), newest first.
- [ ] Verify: unread notifications have a teal left-border accent.
- [ ] Refresh the page.
- [ ] Verify: all notifications are now marked read (no teal accent), and the nav badge count is 0.

#### 21.3 — Email notification stub in logs

- [ ] After sending a friend request (above), check `./deploy.sh logs web`.
- [ ] Verify: a log line appears like `[EMAIL STUB] To: carey_b@... Subject: NinetyNine: FriendRequestReceived`.
- [ ] Community invitation and ownership transfer events should also produce email stub log lines.

#### 21.4 — Block a player

- [ ] As carey, navigate to george's profile (`/players/{george's guid}`).
- [ ] (If a block UI exists) Block george. Otherwise, use `mongosh` to insert a player_blocks document.
- [ ] Verify: after blocking, carey and george no longer see each other in friend lists.
- [ ] Verify: the block is bidirectional — george also cannot see carey in search/leaderboard results (if filtering is wired to GetBlockedIdsAsync).
- [ ] Unblock george to restore normal state.

#### 21.5 — Activity log

- [ ] Navigate to `/settings/activity`.
- [ ] Verify: the page shows a chronological list of events (friend requests, community invitations, etc.) with type labels, messages, and timestamps.
- [ ] Verify: the "Load more" button appears when there are more than 25 events.
- [ ] Verify: the page is read-only — no edit or delete actions.

#### 21.6 — No regression

- [ ] Navigate through `/games`, `/stats`, `/venues`, `/friends`, `/communities`, `/players/me`, `/players/me/edit`, `/notifications`, `/settings/activity` — verify all pages render cleanly.

### 22. Tech debt cleanup + About page (Sprint 6)

*Sprint 6 resolves DEF-001 seeder reconcile, removes legacy ProfileVisibility bools, closes accessibility gaps, and ships the About page.*

#### 22.1 — Seeder reconcile

- [ ] Run `./deploy.sh rebuild` on an existing dev database.
- [ ] Check `./deploy.sh logs web` — verify "reconciled N player(s)" appears if any player was at a stale SchemaVersion.
- [ ] Run `./deploy.sh rebuild` again — verify no reconcile output on second run (steady-state = zero writes).

#### 22.2 — Legacy bool removal

- [ ] Check a player document in mongosh — verify `visibility` subdocument has only Audience enum fields (`emailAudience`, `phoneAudience`, `realNameAudience`, `avatarAudience`). Legacy bool fields may still exist in old docs but are ignored on deserialization.
- [ ] Navigate to `/players/me/edit` — verify audience pickers render correctly (no broken toggles from removed bool bindings).

#### 22.3 — Accessibility

- [ ] Open `/games/{id}/play` and trigger the frame-entry dialog.
- [ ] Press Tab repeatedly — verify focus cycles within the dialog and does not escape to the background.
- [ ] Press Escape — verify dialog closes.
- [ ] On any page, press Tab from the browser address bar — verify a "Skip to main content" link appears, and activating it jumps focus to the main content area.

#### 22.4 — About page

- [ ] Navigate to `/about` (unauthenticated) — verify the page renders.
- [ ] Navigate to `/about` (authenticated) — verify same content renders.
- [ ] Verify: "What is Ninety-Nine" section explains the game.
- [ ] Click the "How to Play" disclosure triangle — verify rules expand with equipment, scoring, example frame.
- [ ] Verify: Philosophy, Planned Features, Privacy, and Who Built This sections render.
- [ ] Verify: NavMenu shows an "About" link at the bottom with an info icon.

#### 22.5 — Community detail venues

- [ ] Navigate to a community detail page (e.g., Pocket Sports).
- [ ] Verify: the Venues section shows affiliated venues by name and address (not the placeholder text).

#### 22.6 — No regression

- [ ] Navigate through all main pages — verify no breakage from the legacy bool removal or seeder reconcile.

### 23. User lifecycle (Sprint 7)

*Sprint 7 simplifies registration, adds self-deletion with a 7-day cooldown, and handles retired player display.*

#### 23.1 — Registration

- [ ] Navigate to `/register`.
- [ ] Verify: no confirm-password field; password field only.
- [ ] Enter a display name that is already taken (e.g., "carey").
- [ ] Verify: "That display name is already taken." error with 3 suggestion chips (e.g., carey_99, carey_pool, carey_42).
- [ ] Click a suggestion chip — verify the form reloads with the suggested name pre-filled.
- [ ] Verify: retirement disclosure in the display-name hint text.

#### 23.2 — Deletion flow

- [ ] Sign in as carey. Navigate to `/players/me/edit`.
- [ ] Verify: an "Account" section at the bottom with a "Delete account" link.
- [ ] Click "Delete account" → `/settings/delete`.
- [ ] Verify: "What will be deleted" and "What will be preserved" lists.
- [ ] If carey owns communities: verify the deletion is blocked with links to transfer ownership.
- [ ] Transfer ownership if needed, then return to `/settings/delete`.
- [ ] Click "Continue to deletion" → `/settings/delete/confirm`.
- [ ] Type an incorrect display name and submit — verify error "Display name does not match."
- [ ] Type the correct display name and submit.
- [ ] Verify: redirect to `/settings/delete/scheduled` with the deletion date (7 days out).
- [ ] Click "Cancel deletion" — verify redirect to `/players/me`.
- [ ] Verify: no deletion is scheduled (the player is active again).

#### 23.3 — Retired player display

- [ ] (After executing a test deletion via mongosh or waiting for cooldown) View a retired player's profile.
- [ ] Verify: page shows "{DisplayName}" with "This player is no longer active. Their game history is preserved."
- [ ] Verify: no PII, no contact card, no stats section.
- [ ] Verify: the retired player does not appear on the leaderboard.
- [ ] Verify: PlayerBadge for the retired player shows the name without a link.

#### 23.4 — No regression

- [ ] Navigate through all main pages — verify no breakage from the registration or deletion changes.

### 24. SignalR real-time infrastructure (Sprint 8)

*Sprint 8 adds a SignalR hub for push notifications and leaderboard refresh without page reload.*

#### 24.1 — SignalR connection

- [ ] Sign in as carey. Open browser dev tools → Network tab.
- [ ] Verify: a WebSocket connection is established to `/hubs/notifications`.
- [ ] Open a second tab as carey — verify both tabs maintain connections.

#### 24.2 — Notification badge real-time update

- [ ] Sign in as carey in tab 1. Note the notification badge count.
- [ ] In a second browser, sign in as george and send carey a friend request.
- [ ] Within 30 seconds, verify: carey's notification badge updates without page refresh in tab 1.
- [ ] Verify: the aria-live region announces the change to screen readers.

#### 24.3 — Leaderboard auto-refresh

- [ ] Navigate to `/stats` as carey.
- [ ] In a second browser, complete a game as george.
- [ ] Within 60 seconds, verify: the leaderboard page reloads automatically.
- [ ] Switch to a background tab, complete another game — verify no reload occurs while the tab is hidden.

#### 24.4 — Graceful degradation

- [ ] Stop the web container (`docker compose stop web`). Wait. Restart.
- [ ] Verify: pages still render correctly via static SSR even if SignalR fails to connect.
- [ ] Verify: notification badge shows the server-rendered count (no JS error in console).

#### 24.5 — No regression

- [ ] Navigate through all main pages — verify no breakage from the SignalR additions.

### 25. Voting and polls (Sprint 9)

*Sprint 9 adds a general-purpose polling system with quorum, supermajority, anonymous voting, and site-wide feature polls on the About page.*

#### 25.1 — Create a community poll

- [ ] As carey (Owner of Pocket Sports), navigate to the community detail page.
- [ ] Click "Create poll" → `/communities/{id}/polls/new`.
- [ ] Create an Advisory poll with 2 options and 7-day duration.
- [ ] Verify: redirect to community detail with "Poll created." flash.
- [ ] Verify: the poll appears in the "Polls" section.

#### 25.2 — Vote on a poll

- [ ] As carey, vote on the poll by selecting an option and clicking "Vote".
- [ ] Verify: "Your vote has been recorded." flash.
- [ ] Verify: after voting, results are NOT shown (bandwagon prevention — poll is still open).
- [ ] Sign in as george in a second browser. Navigate to the same community.
- [ ] Vote on the poll.
- [ ] Verify: george sees "You've voted" note with close date.

#### 25.3 — Poll results (after close)

- [ ] Close the poll manually (via mongosh: set `status: "Closed"`, `closedAt: new Date()`) or wait for expiry.
- [ ] Verify: both carey and george see the results with bar chart, vote counts, and quorum indicator.

#### 25.4 — Feature poll on About page

- [ ] Create a site-wide FeatureProposal poll (via mongosh or a proto-admin flow).
- [ ] Navigate to `/about`.
- [ ] Verify: the poll appears in the "Planned Features" section with vote form.
- [ ] Vote on the feature poll.
- [ ] Verify: vote recorded; results shown after close.

#### 25.5 — Authorization

- [ ] As a plain Member (not Owner or Admin), verify: cannot create a MemberRemoval poll (only Advisory).
- [ ] As a non-member, verify: cannot vote on a community poll.

#### 25.6 — No regression

- [ ] Navigate through all main pages — verify no breakage from the polling additions.

### 26. Multi-player matches (Sprint 10)

*Sprint 10 adds head-to-head match creation, alternating-break scoring, and match history.*

#### 26.1 — Create a match

- [ ] Sign in as carey. Navigate to `/games/new`.
- [ ] Verify: the subtitle includes a "Start a match instead" link.
- [ ] Click the link → `/matches/new`.
- [ ] Verify: form fields for opponent, venue, format (Single/RaceTo/BestOf), break method, table size, table number, stakes.
- [ ] Select george as opponent, Bumpers as venue, Single format, Lag for break.
- [ ] Submit. Verify: redirect to `/matches/{id}/play`.

#### 26.2 — Match play flow

- [ ] On `/matches/{id}/play`, verify: scoreboard shows both players with 0 wins each.
- [ ] Click "Continue game 1" → redirects to `/games/{gameId}/play`.
- [ ] Score all 9 frames (use break-bonus + ball-count inputs).
- [ ] Return to `/matches/{id}/play`.
- [ ] Verify (Single format): match status is Completed, winner is shown.

#### 26.3 — Race-to format

- [ ] Create a new match with race-to-3 format against george.
- [ ] Play through 3 complete games.
- [ ] Verify: on game completion, the next game is auto-created if the race isn't won yet.
- [ ] Verify: if `BreakMethod` is not `PreviousLoserBreaks`, games alternate breakers.
- [ ] Verify: the privacy interstitial fires between games with "Hand the device to {name}".
- [ ] Verify: match completes when a player reaches 3 wins.

#### 26.4 — Match detail page

- [ ] Navigate to `/matches/{id}` for a completed match.
- [ ] Verify: header shows players, format, date, venue, status, winner, final score.
- [ ] Verify: each game is rendered with breaker, total score, and frame-by-frame breakdown.
- [ ] Verify: stakes field is NOT displayed (private per design).

#### 26.5 — Match history on profile

- [ ] Navigate to `/players/me`.
- [ ] Verify: "Recent matches" card appears with up to 5 most recent matches.
- [ ] Verify: each row shows date, "vs {opponent}", result (Won/Lost/InProgress/Abandoned).
- [ ] Click a match row → navigates to `/matches/{id}`.

#### 26.6 — Abandon match

- [ ] Create a match and navigate to `/matches/{id}/play`.
- [ ] Click "Abandon match".
- [ ] Verify: redirect to profile; match shows as Abandoned in history.

#### 26.7 — No regression

- [ ] Navigate through all main pages — verify no breakage from the match additions.

---

## Cross-Browser Results Table

Run the complete checklist (sections 1–15) three times — once per browser — and record the result for each section.

| Section | Chrome | Firefox | Safari |
|---|---|---|---|
| 1. Unauthenticated landing | | | |
| 2. Sign in via mock picker | | | |
| 3. Sign in via email/password | | | |
| 4. Register a new account | | | |
| 5. Forgot password flow | | | |
| 6. Start and play a game | | | |
| 7. View game history and details | | | |
| 8. Profile and editing | | | |
| 9. Venues | | | |
| 10. Statistics | | | |
| 11. Responsive mobile layout | | | |
| 12. Light mode toggle | | | |
| 13. Sign out | | | |
| 14. Dev theme test page | | | |
| 15. Health check endpoint | | | |
| 16. Sprint 0 foundation migration | | | |
| 17. Sprint 1 friends e2e | | | |
| 18. Sprint 2 communities e2e | | | |
| 19. Sprint 3 audience + affiliation | | | |
| 20. Sprint 4 admin + filters + transfer | | | |
| 21. Sprint 5 discovery + notifications + block | | | |
| 22. Sprint 6 tech debt + About page | | | |
| 23. Sprint 7 user lifecycle | | | |
| 24. Sprint 8 SignalR real-time | | | |
| 25. Sprint 9 voting and polls | | | |
| 26. Sprint 10 multi-player matches | | | |
| **Overall** | | | |

Cell values: **PASS** / **FAIL** / **SKIP** / **BLOCKED**

---

## Defect Reporting

When a step fails, open a new defect in `docs/defects.md` using the format:

```
## DEF-NNN: <short title>

- Severity: Critical / High / Medium / Low
- Browser(s): Chrome / Firefox / Safari / All
- Steps to reproduce: (reference checklist section and step)
- Expected: (what the checklist says should happen)
- Actual: (what actually happened)
- Screenshot / console log: (attach or paste)
```

---

## Notes on SmokeTests.cs (Automated HTTP Harness)

An automated in-process HTTP smoke test (`tests/NinetyNine.Web.Tests/SmokeTests.cs`) using `Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>` was evaluated but **deferred** for the following reasons:

1. `Program.cs` uses top-level statements without `public partial class Program`, so `WebApplicationFactory<Program>` cannot reference the `Program` type from a separate assembly without modifying production code — which is explicitly out of scope for this work package.
2. `Program.cs` calls `BsonConfiguration.Register()` and `builder.Services.AddNinetyNineRepository(...)` which connect to MongoDB at startup. Stubbing these in a test-only configuration requires either touching production code or adding a `WebApplicationFactory` subclass with significant DI overrides — effort that exceeds the "S" size estimate for this work package.
3. The existing `NinetyNine.Web.Tests.csproj` targets `net10.0` and uses `bUnit` but does not currently reference `Microsoft.AspNetCore.Mvc.Testing`. Adding it without a running Mongo instance would cause all startup-dependent tests to fail.

**Recommended path forward (future WP):** Add `public partial class Program {}` at the end of `Program.cs`, reference `Microsoft.AspNetCore.Mvc.Testing` in the Web test project, and implement a `CustomWebApplicationFactory` that replaces `INinetyNineDbContext` with a Testcontainers-backed Mongo fixture (consistent with the pattern already used in `NinetyNine.Repository.Tests` and `NinetyNine.Services.Tests`). Route smoke tests — asserting 200/302 on all §8.3 routes — can then be implemented with approximately 30–40 lines of xUnit code and run in CI without a browser.
