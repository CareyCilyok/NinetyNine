# NinetyNine v2 Roadmap — Post-Friends+Communities

<!-- markdownlint-disable MD060 -->

**Status:** Approved 2026-04-12 — Sprint 6 next
**Owner:** Carey
**Scope:** Technical debt cleanup, About page, user lifecycle, SignalR real-time, voting/polls, and multi-player matches.
**Predecessor:** [friends-communities-v1.md](friends-communities-v1.md) (Sprints 0–5, completed at v0.1.2)
**Estimation:** Modified Fibonacci story points (1, 2, 3, 5, 8, 13, 21)

This document is the **source of truth** for post-v1.2 planning. Any change to sprint scope or ordering that the user directs in conversation must be mirrored here.

---

## Table of contents

1. [Planning team](#planning-team)
2. [Sprint roadmap at a glance](#sprint-roadmap-at-a-glance)
3. [Sprint 6 — Tech debt + About page](#sprint-6--tech-debt--about-page)
4. [Sprint 7 — User lifecycle](#sprint-7--user-lifecycle)
5. [Sprint 8 — SignalR real-time infrastructure](#sprint-8--signalr-real-time-infrastructure)
6. [Sprint 9 — Voting and polls](#sprint-9--voting-and-polls)
7. [Sprint 10 — Multi-player matches](#sprint-10--multi-player-matches)
8. [Architectural decisions](#architectural-decisions)
9. [Changelog](#changelog)

---

## Planning team

This plan was assembled by consulting three domain experts in parallel, then synthesized into a unified sprint structure. All agents are available for execution.

### Agents consulted during planning

| Agent | Role | Contribution |
| --- | --- | --- |
| **George** (`poolplayer` SME) | Domain expert, pool culture, UX wording | Match naming ("Match"), break-method metadata, stakes field, About page tone and structure, voting mechanics (quorum, supermajority, anonymous voting), deletion behavior (name display, leaderboard, ownership transfer) |
| **Software Architect** (`comprehensive-review:architect-review`) | System design, data model, MongoDB constraints | SignalR migration path (keep static SSR + selective hub), voting entity design (polls + votes + compound unique index), Match entity (top-level, references Games), user lifecycle (soft delete, HMAC email hash), build order |
| **HCD Expert** (`hcd-expert`) | User journeys, accessibility, information architecture | About page progressive disclosure, voting UX (single-page form, inline surfacing, bandwagon prevention), match UX (type selector, privacy interstitial, comparison views), deletion flow (two-step + 7-day cooldown), registration simplification |

### Scrum team for execution

| Agent | Scrum Role | Responsibilities |
| --- | --- | --- |
| **Claude** (orchestrator) | Scrum Master / Tech Lead | Sprint execution, code implementation, test writing, commits |
| **George** (`poolplayer`) | Product Owner / Domain SME | UX copy review, pool-rules validation, feature acceptance from a player perspective |
| **Software Architect** (`comprehensive-review:architect-review`) | Architect | Design review at sprint boundaries, entity design validation |
| **HCD Expert** (`hcd-expert`) | UX/Accessibility Reviewer | WCAG compliance review, user flow validation, pre-ship accessibility audit |
| **Code Reviewer** (`pr-review-toolkit:code-reviewer`) | Quality Gate | Pre-push code review on complex stories (8+ SP) |

---

## Sprint roadmap at a glance

| Sprint | Scope | Total SP | Visible demo at end |
| --- | --- | --- | --- |
| **Sprint 6** | Tech debt + About page | 18 | Defects resolved; `/about` page live with rules, philosophy, planned features |
| **Sprint 7** | User lifecycle | 26 | Simplified registration; self-deletion with name retirement and forced ownership transfer |
| **Sprint 8** | SignalR real-time | 18 | Notification badge updates in real time; live leaderboard refresh |
| **Sprint 9** | Voting and polls | 34 | Community polls with quorum, supermajority, anonymous voting; results on About page |
| **Sprint 10** | Multi-player matches | 42 | Head-to-head match creation, alternating-break scoring on shared device, frame comparison |

**Version milestones:**

- v0.1.3 after Sprint 6 (tech debt + About page)
- v0.1.4 after Sprint 7 (user lifecycle)
- v0.1.5 after Sprint 8 (SignalR real-time)
- v0.1.6 after Sprint 9 (voting and polls)
- v0.1.7 after Sprint 10 (multi-player matches)
- Minor version bumps (v0.2.0, v0.3.0, etc.) at Carey's discretion — not automatic per sprint
- v1.0.0 — schema lock-down, requires formal migration tooling after this point

---

## Sprint 6 — Tech debt + About page

**Sprint goal:** Resolve all open technical debt from Sprints 0–5, close accessibility gaps, update stale docs, and ship the About page.

**Sprint velocity:** 18 SP

**Sprint DoD:**

- Build green, all existing tests pass
- `docs/defects.md` status fields accurate
- Legacy `ProfileVisibility` bool flags removed from model; no call-site references remain
- `/about` page renders for authenticated and unauthenticated visitors
- Smoke test §22 covers the About page
- Community detail page shows affiliated venues (no placeholder)

### S6.1 — DEF-001 seeder reconcile rewrite [5 SP]

**Assigned to:** Claude (implementation) + Architect (design review)

**As a** developer **I want** the DataSeeder to converge every seeded record to the current template on every startup **so that** new fields added in later sprints automatically land in existing dev databases.

**Acceptance criteria:**

- Seeder uses a "reconcile" pattern: for each known test player/venue, either create or update to match the template
- `SchemaVersion` comparison used for cheap change detection (no field-by-field checks when version matches)
- Zero writes when nothing changed; idempotent on repeated runs
- All existing per-field heal passes (`HealExistingTestPlayersAsync`, `HealProfileVisibilityAsync`) folded into the reconcile
- Friendship and community reconcile passes remain separate (they already follow the pattern)
- Integration test verifying reconcile converges a stale record

**Tasks:**

1. Design the reconcile loop: load player by display name, compare `SchemaVersion`, build target state from template, write if changed
2. Refactor `DataSeeder.SeedAsync` — replace the `if (existing is not null) return;` guard with a reconcile-all loop
3. Fold `HealExistingTestPlayersAsync` and `HealProfileVisibilityAsync` into the reconcile
4. Add `DataSeederReconcileTests` integration test: seed, mutate a field via `mongosh`-style update, re-seed, assert field converged
5. Verify `./deploy.sh rebuild` on a hot database produces "0 changes" on second run

### S6.2 — Stale docs + placeholder + legacy bool cleanup [3 SP]

**Assigned to:** Claude (implementation)

**Tasks:**

1. Update DEF-002, 003, 004, 006 status to "Fixed" in `docs/defects.md` with resolution dates
2. Community detail page: replace "Venue affiliation ships in the next sprint" placeholder with a query of `IVenueRepository` for venues where `CommunityId == this community`, rendered as a list with name + address
3. Remove legacy `ProfileVisibility` bool flags: delete the four `bool` properties (`EmailAddress`, `PhoneNumber`, `RealName`, `Avatar`), update all call sites that reference them (search for `.Visibility.EmailAddress`, `.Visibility.PhoneNumber`, `.Visibility.RealName`, `.Visibility.Avatar` as bools)
4. Update BSON class map: `ProfileVisibility` no longer has the bool fields; add `SetIgnoreExtraElements(true)` if not already present so old docs still deserialize
5. Update `CLAUDE.md` Friends & Communities section with v0.1.2 completion state

### S6.3 — Accessibility gap closure [2 SP]

**Assigned to:** Claude (implementation) + HCD Expert (review)

Per the HCD accessibility audit, two pre-existing issues must be resolved before shipping new surfaces:

**Tasks:**

1. **F-D2:** Fix `FrameInputDialog` focus trap — focus escapes the dialog on Tab. Add `tabindex` management so Tab cycles within the dialog while it's open.
2. **F-D4:** Add skip-to-main-content link in `MainLayout.razor` — visually hidden, appears on focus, jumps to `<main>` landmark.
3. HCD Expert reviews both fixes for WCAG 2.2 AA compliance.

### S6.4 — About page [5 SP]

**Assigned to:** Claude (implementation) + George (content review) + HCD Expert (accessibility review)

**As a** visitor or pool player **I want** an About page that explains the game, the site philosophy, and what's planned **so that** I understand what NinetyNine is and why it exists.

**Content structure** (per George + HCD):

1. **What is Ninety-Nine** — the game in two paragraphs
2. **How to play** — short plain-English rules with an example frame; collapsible via native `<details>` (per HCD); write our own version (per George: no reliable external source to link)
3. **Philosophy** — one paragraph: *"NinetyNine is run by players, for players. Nobody here answers to a sponsor, a venue owner, or an algorithm."*
4. **Planned features** — honest roadmap with priorities, no ship dates; links out to a future voting page when polls are built (Sprint 9)
5. **Privacy commitment** — one paragraph explaining the audience model
6. **Who built this** — a line or two, keeps it human

**Acceptance criteria:**

- Route: `@page "/about"`, no `[Authorize]` attribute — accessible to everyone
- Rules section uses `<details>` / `<summary>` for progressive disclosure
- Example frame in the rules section with a concrete score breakdown
- NavMenu gains an "About" link (Phosphor `info` icon) — placed last in the nav list
- Content reviewed by George for tone and accuracy
- No pool-room etiquette section (per George: "preachy on an About page")
- WCAG 2.2 AA: heading hierarchy, landmark regions, `<details>` keyboard-accessible

**Tasks:**

1. Create `Components/Pages/About.razor` + `About.razor.css`
2. Write rules content with example frame — consult George for accuracy
3. Write philosophy + privacy sections — consult George for tone
4. Add "Planned features" section with static list (Sprint 9 will make it dynamic via polls)
5. Add "About" nav link to `NavMenu.razor`
6. Download Phosphor `info` icon if missing
7. HCD Expert reviews accessibility

### S6.5 — Smoke test §22 [1 SP]

**Assigned to:** Claude

**Tasks:**

1. Add §22 to `docs/smoke-test-checklist.md` covering About page content, progressive disclosure, navigation
2. Add Sprint 6 row to Cross-Browser Results Table
3. Verify build green

### S6.6 — Sprint 6 tag [2 SP]

**Assigned to:** Claude

**Tasks:**

1. Run full test suite (Services + Repository projects individually)
2. `./deploy.sh rebuild` + live verification
3. Create annotated tag `v0.1.3`
4. Push tag to origin

---

## Sprint 7 — User lifecycle

**Sprint goal:** Simplify registration to email + display name, and implement self-deletion with name retirement, forced community ownership transfer, and a 7-day cooldown period.

**Sprint velocity:** 26 SP

**Sprint DoD:**

- Registration form is email + display name + password (no confirm-password)
- Display-name suggestions appear when first choice is taken
- `/settings/delete` page with two-step confirmation + 7-day cooldown
- Deletion blocks if player owns communities with no transfer target
- Retired players' display names show on game history with no link
- Leaderboard filters out retired players
- Smoke test §23 covers the full lifecycle

### S7.1 — Registration simplification [5 SP]

**Assigned to:** Claude (implementation) + HCD Expert (form review) + George (copy review)

**As a** new player **I want** to register with just my email and a display name **so that** onboarding is fast and friction-free.

**Acceptance criteria:**

- Registration form fields: email, display name, password with show/hide toggle
- Remove confirm-password field (per HCD)
- When display name is taken, server generates up to 3 alternative suggestions displayed as clickable pre-populated links (`/register?displayName=...`) (per HCD)
- Suggestion algorithm: append `_99`, `_pool`, or 2-digit random suffix
- Inline hint text under display name: "Your display name appears in game history. If you ever delete your account, this name is retired — no one else can use it."
- Form remains static SSR; suggestions are rendered server-side on the validation-failure re-render

**Tasks:**

1. Modify `Register.razor` — remove confirm-password, add show/hide toggle
2. Add display-name suggestion logic to `IPlayerService` — `SuggestDisplayNamesAsync(string preferred, int count = 3)`
3. Render suggestions as links on the form when validation returns "name taken"
4. Add retirement disclosure hint text under the display name field
5. George reviews hint text and suggestion suffixes for pool-culture fit
6. HCD Expert reviews form layout and accessibility

### S7.2 — Player model: retirement fields [2 SP]

**Assigned to:** Claude (implementation) + Architect (design review)

**As a** developer **I want** `Player` to support soft deletion **so that** PII can be erased while game history is preserved.

**Acceptance criteria:**

- `Player.RetiredAt: DateTime?` — null for active players
- `Player.EmailHash: string?` — HMAC-SHA256 of the lowercase email, computed at deletion time
- HMAC key stored in `appsettings.json` under `Security:EmailHashKey` (per Architect: application-level key, not per-user salt)
- BSON class map updated; existing docs with no `retiredAt` deserialize cleanly (null default)
- No BSON class map for `RetiredAt` as nullable DateTime — let the global convention handle it

**Tasks:**

1. Add fields to `Player.cs`
2. Update `BsonConfiguration.RegisterPlayerClassMap` if needed
3. Add `EmailHashKey` to `appsettings.json` and `appsettings.Development.json`
4. Unit test: HMAC produces deterministic output for the same email + key

### S7.3 — Player deletion service [8 SP]

**Assigned to:** Claude (implementation) + Architect (design review) + George (UX flow review)

**As a** player **I want** to delete my account **so that** my personal data is removed while my game history stays coherent.

**Acceptance criteria:**

- `IPlayerService.InitiateDeleteAsync(playerId)` — sets `DeletionScheduledAt = UtcNow + 7 days`, returns error if player owns communities with no transfer target
- `IPlayerService.CancelDeleteAsync(playerId)` — clears `DeletionScheduledAt` during the 7-day window
- `IPlayerService.ExecuteDeleteAsync(playerId)` — called by the expiration sweep after 7 days:
  - Compute `EmailHash` from `EmailAddress` using HMAC-SHA256
  - Clear PII: `EmailAddress`, `PhoneNumber`, `FirstName`, `MiddleName`, `LastName`, `PasswordHash`
  - Delete avatar from GridFS if present
  - Set `RetiredAt = UtcNow`, clear `DeletionScheduledAt`
  - Remove all friendships (`IFriendshipRepository.DeleteAsync`)
  - Remove all friend requests where player is sender or receiver
  - Remove all community memberships
  - Cancel all pending invitations/join-requests involving this player
  - Remove all blocks involving this player
- Pre-deletion gates:
  - Block if player owns communities — return `OwnerMustTransferFirst` error with list of owned community names
  - Block if player is mid-match (deferred to Sprint 10 — check added then)
- Leaderboard queries: `StatisticsService` filters on `RetiredAt == null`
- Game records survive with original `PlayerId`; display name renders with no link, no `[retired]` tag (per George)
- `DataSeeder` expiration sweep calls `ExecuteDeleteAsync` for players past their `DeletionScheduledAt`
- 8+ integration tests covering: initiate, cancel, execute, PII erasure verification, ownership-block, leaderboard exclusion

**Tasks:**

1. Add `DeletionScheduledAt: DateTime?` to `Player`
2. Implement `InitiateDeleteAsync` with ownership gate
3. Implement `CancelDeleteAsync`
4. Implement `ExecuteDeleteAsync` with full PII erasure + cascade
5. Add HMAC email hash computation
6. Update `StatisticsService.GetLeaderboardAsync` and friends to filter retired players
7. Update `DataSeeder.SweepExpiredPendingAsync` to also sweep scheduled deletions
8. Integration tests

### S7.4 — Deletion UI [5 SP]

**Assigned to:** Claude (implementation) + HCD Expert (flow review) + George (copy review)

**As a** player **I want** a clear, non-manipulative deletion flow **so that** I can leave without friction.

**Acceptance criteria:**

- `/settings/delete` page (requires auth)
- Step 1: information page — lists what will be deleted vs. preserved, links to community ownership transfer if needed
- Step 2: confirmation page — type your display name to confirm, "Delete my account" button
- On confirmation: redirect to `/settings/delete/scheduled` with "Your account will be deleted on {date}. Cancel anytime."
- Cancel button during cooldown: clears the schedule, redirects to `/players/me`
- Heading: "Delete your NinetyNine account" (neutral, per HCD)
- No reason collection (per HCD)
- "Sorry to see you go." — one line, on the final confirmation, not repeated
- If player owns communities: deletion form is disabled with a message listing which communities need ownership transfer, linking to each community's detail page

**Tasks:**

1. Create `Components/Pages/Settings/Delete.razor` + CSS — step 1 (info)
2. Create `Components/Pages/Settings/DeleteConfirm.razor` + CSS — step 2 (type name)
3. Create `Components/Pages/Settings/DeleteScheduled.razor` — cooldown + cancel
4. Wire cancel button to `IPlayerService.CancelDeleteAsync`
5. Add "Delete account" link to `/players/me/edit` settings section
6. George reviews all copy
7. HCD Expert reviews flow and accessibility

### S7.5 — Retired player display [3 SP]

**Assigned to:** Claude (implementation)

**As a** viewer **I want** retired players' names to appear in game history without a link **so that** the historical record is coherent.

**Acceptance criteria:**

- `PlayerBadge.razor` checks `RetiredAt` — if set, renders display name as plain text (no `<a href>`)
- Leaderboard excludes retired players (already done in S7.3)
- Profile page for a retired player shows "This player is no longer active." with no PII
- Community member lists exclude retired players
- Friend lists exclude retired players (friendships already deleted in S7.3)

**Tasks:**

1. Modify `PlayerBadge.razor` — add `RetiredAt` parameter, conditional link rendering
2. Modify `Profile.razor` — handle `RetiredAt is not null` case
3. Update community member list rendering to skip retired players
4. Verify leaderboard exclusion from S7.3

### S7.6 — Smoke test §23 [1 SP]

**Assigned to:** Claude

**Tasks:**

1. Add §23 covering registration simplification, deletion flow, cooldown, cancellation, ownership gate, retired player display
2. Add Sprint 7 row to Cross-Browser Results Table

### S7.7 — Sprint 7 tag [2 SP]

**Assigned to:** Claude

**Tasks:**

1. Full test suite run
2. Live verification via `./deploy.sh rebuild`
3. Create annotated tag `v0.1.4`
4. Push tag to origin

---

## Sprint 8 — SignalR real-time infrastructure

**Sprint goal:** Add a SignalR hub for push notifications so the notification badge, leaderboard, and community member list can update without a full page refresh.

**Sprint velocity:** 18 SP

**Architectural approach** (per Architect):

- **Keep static SSR as the default render mode.** Do not switch to global InteractiveServer.
- Add a single `NotificationHub` for server → client push.
- Use JS interop to update the notification badge count in NavMenu without making MainLayout interactive.
- **Never put `[SupplyParameterFromForm]` and `@rendermode InteractiveServer` on the same page** — these are fundamentally incompatible patterns.
- Use polling via `PeriodicTimer` in a `BackgroundService` for detecting changes (single-node Mongo lacks change stream support without a replica set).
- Future match-scoring hub (Sprint 10) will build on this infrastructure.

**Sprint DoD:**

- Notification badge updates within 30 seconds of a new notification without page refresh
- Leaderboard auto-refreshes for connected viewers when scores change
- SignalR connection gracefully degrades — static SSR still works if hub is down
- Smoke test §24 covers real-time badge and leaderboard refresh

### S8.1 — SignalR hub + authentication [5 SP]

**Assigned to:** Claude (implementation) + Architect (design review)

**As a** developer **I want** a SignalR hub with authenticated connections **so that** the server can push targeted messages to specific players.

**Acceptance criteria:**

- `NotificationHub : Hub` in `NinetyNine.Web/Hubs/`
- Hub mapped in `Program.cs` at `/hubs/notifications`
- Authenticated connections only — hub uses `[Authorize]` and resolves `PlayerId` from claims
- Connection tracking: hub maintains a `ConcurrentDictionary<Guid, HashSet<string>>` mapping PlayerId → connection IDs (supports multiple tabs)
- Hub methods: `OnConnectedAsync` registers, `OnDisconnectedAsync` unregisters
- Server-to-client method: `ReceiveUnreadCount(long count)`, `ReceiveLeaderboardUpdate()`

**Tasks:**

1. Add `Microsoft.AspNetCore.SignalR` NuGet if not already present
2. Create `NotificationHub.cs`
3. Register hub in `Program.cs` — `app.MapHub<NotificationHub>("/hubs/notifications")`
4. Add connection tracking service (`IHubConnectionTracker` / `HubConnectionTracker`) as singleton
5. Unit test: hub resolves PlayerId from claims on connect

### S8.2 — JS interop for badge updates [3 SP]

**Assigned to:** Claude (implementation) + HCD Expert (accessibility review)

**As a** player **I want** my notification badge to update without reloading the page **so that** I know about new activity immediately.

**Acceptance criteria:**

- `wwwroot/js/notification-badge.js` — connects to `/hubs/notifications`, listens for `ReceiveUnreadCount`, updates the badge DOM element
- Script loaded via `<script>` tag in `App.razor` (not per-page)
- Badge element identified by a `data-notification-badge` attribute on the nav badge `<span>`
- Graceful degradation: if SignalR connection fails, the badge shows the server-rendered count (no JS error)
- ARIA live region on the badge so screen readers announce count changes

**Tasks:**

1. Create `wwwroot/js/notification-badge.js`
2. Add `<script>` reference in `App.razor`
3. Update NavMenu badge `<span>` with `data-notification-badge` and `aria-live="polite"`
4. Test: manually invoke hub method, verify badge DOM updates
5. HCD Expert reviews ARIA live region behavior

### S8.3 — BackgroundService change poller [5 SP]

**Assigned to:** Claude (implementation) + Architect (design review)

**As a** developer **I want** a background service that detects new notifications and pushes updates to connected clients **so that** the hub is reactive without MongoDB change streams.

**Acceptance criteria:**

- `NotificationPollerService : BackgroundService` runs a `PeriodicTimer` at 30-second intervals
- Each tick: for each connected player (from `IHubConnectionTracker`), query `INotificationRepository.CountUnreadAsync`, compare to last-known count, send hub message if changed
- Leaderboard change detection: query latest game completion timestamp, compare to last-known, broadcast `ReceiveLeaderboardUpdate()` to all connected `/stats` viewers if changed
- Graceful shutdown via `CancellationToken`; no orphaned timers
- Configurable interval via `appsettings.json`

**Tasks:**

1. Create `NotificationPollerService.cs` in `NinetyNine.Web/Services/`
2. Register as hosted service in `Program.cs`
3. Implement per-player unread-count diffing
4. Implement leaderboard-change detection
5. Add `SignalR:PollIntervalSeconds` to `appsettings.json` (default 30)
6. Integration test: verify poller sends hub message on count change

### S8.4 — Leaderboard auto-refresh [3 SP]

**Assigned to:** Claude (implementation)

**As a** player viewing the leaderboard **I want** it to refresh when scores change **so that** I see current standings without manual reload.

**Acceptance criteria:**

- `Leaderboard.razor` includes a small JS module that listens for `ReceiveLeaderboardUpdate` and triggers a page reload (simplest approach under static SSR — full DOM diff would require InteractiveServer)
- Reload is debounced — max once per 60 seconds to avoid thrashing
- No reload if the page is in a background tab (Page Visibility API)

**Tasks:**

1. Create `wwwroot/js/leaderboard-refresh.js`
2. Add `<script>` reference in `Leaderboard.razor` only
3. Debounce + visibility check

### S8.5 — Smoke test §24 [1 SP]

**Assigned to:** Claude

### S8.6 — Sprint 8 tag [1 SP]

**Assigned to:** Claude

---

## Sprint 9 — Voting and polls

**Sprint goal:** General-purpose polling system for community-level and site-wide decisions, with quorum, supermajority, and anonymous voting.

**Sprint velocity:** 34 SP

**Entity design** (per Architect):

- `Poll` collection — embedded `PollOption[]`, plus `EligibleVoterCount` captured at open time, `QuorumThreshold`, `SupermajorityThreshold?`, `AnonymousVoting`, `Status`, `ExpiresAt`, `Result` (embedded on close)
- `Vote` collection (separate) — `PollId`, `PlayerId`, `OptionIndex`, `CastAt`; unique compound index on `(PollId, PlayerId)` enforces one-vote-per-player without transactions
- `PollResult` embedded in Poll on close — denormalized vote counts, quorum met, threshold met, outcome

**Voting mechanics** (per George):

- Duration: creator-set within 24h–14d, default 7d; 72h floor for member-removal votes
- Quorum: 50% of eligible members; if not met, result is advisory-only (Owner decides)
- Anonymous: mandatory for member-targeting votes; creator choice otherwise (default anonymous)
- Supermajority: 2/3 (round up) for member removal; simple majority for everything else
- Poll creation: Owner/Admin can create any poll type; Members can create advisory polls only
- Binding: member removal is binding if quorum + supermajority met; all others advisory by default

**Sprint DoD:**

- Community detail page shows active polls inline
- Owner/Admin can create polls; Members can create advisory polls
- Players can vote; results hidden until vote cast (bandwagon prevention)
- Expired polls auto-close via background sweep
- Binding member-removal polls auto-execute on close
- About page shows site-wide feature poll results
- Smoke test §25

### S9.1 — Poll + Vote entities [3 SP]

**Assigned to:** Claude (implementation) + Architect (entity review)

**As a** developer **I want** `Poll`, `PollOption`, `PollResult`, and `Vote` types **so that** the polling system has a typed data layer.

**Acceptance criteria:**

- `Poll` entity: `PollId`, `CommunityId?` (null for site-wide), `CreatedByPlayerId`, `Title`, `Description?`, `PollType` enum (`Advisory`, `MemberRemoval`, `FeatureProposal`), `Options: PollOption[]` (embedded), `EligibleVoterCount`, `QuorumThreshold` (default 0.5), `SupermajorityThreshold?` (default null = simple majority; 0.667 for member removal), `AnonymousVoting`, `Status` enum (`Open`, `Closed`, `Expired`), `CreatedAt`, `ExpiresAt`, `ClosedAt?`, `Result: PollResult?` (embedded, populated on close)
- `PollOption` value type: `Index`, `Label`, `TargetPlayerId?` (for member-removal polls)
- `PollResult` value type: `VoteCounts: int[]`, `TotalVotes`, `QuorumMet`, `ThresholdMet`, `WinningOptionIndex?`
- `Vote` entity: `VoteId`, `PollId`, `PlayerId`, `OptionIndex`, `CastAt`
- BSON class maps for all types
- Collections: `polls`, `votes`
- Indexes: unique compound `(PollId, PlayerId)` on votes; `(CommunityId, Status)` on polls

**Tasks:**

1. Create `Poll.cs`, `PollOption.cs`, `PollResult.cs`, `Vote.cs` in Model
2. Create `PollType`, `PollStatus` enums
3. Register BSON class maps in `BsonConfiguration`
4. Add collections to `INinetyNineDbContext` / `NinetyNineDbContext`
5. Create indexes in `EnsureIndexes`

### S9.2 — Poll + Vote repositories [3 SP]

**Assigned to:** Claude (implementation)

**Acceptance criteria:**

- `IPollRepository` / `PollRepository`: `CreateAsync`, `GetByIdAsync`, `ListByCommunityAsync(communityId, status?)`, `ListSiteWideAsync(status?)`, `UpdateAsync`
- `IVoteRepository` / `VoteRepository`: `CreateAsync`, `GetByPollAndPlayerAsync(pollId, playerId)`, `CountByPollAsync(pollId)`, `ListByPollAsync(pollId)`
- DI registration in `NinetyNine.Repository.DependencyInjection`

**Tasks:**

1. Create interfaces + implementations
2. Register in DI
3. Repository-level integration tests

### S9.3 — Poll service [8 SP]

**Assigned to:** Claude (implementation) + Architect (authz review) + George (mechanics review)

**As a** developer **I want** a poll service that enforces all voting invariants **so that** UI code is free of business rules.

**Acceptance criteria:**

- `IPollService.CreatePollAsync(communityId?, createdByPlayerId, title, description?, pollType, options[], durationDays, anonymousVoting?)` — enforces:
  - Duration bounds: 24h–14d; 72h floor for `MemberRemoval` type
  - Authz: Owner/Admin for any type; Member for `Advisory` only
  - `EligibleVoterCount` captured from community member count at creation
  - Max 10 active polls per community
- `IPollService.CastVoteAsync(pollId, playerId, optionIndex)` — enforces:
  - Poll is `Open` and not expired
  - Player is eligible (community member, or any authenticated player for site-wide)
  - One-vote-per-player (index + service guard)
  - Returns current results only if the player has already voted (bandwagon prevention)
- `IPollService.ClosePollAsync(pollId)` — called by sweep or manually by Owner/Admin:
  - Compute `PollResult`: vote counts, quorum check, threshold check
  - If binding `MemberRemoval` + quorum met + supermajority met → auto-execute via `ICommunityService.RemoveMemberAsync`
  - Log the full vote record
  - Notify the removed player with the stated reason (via `INotificationService`)
- `IPollService.GetPollWithResultsAsync(pollId, viewerId)` — returns poll + results; results are null if viewer hasn't voted yet (bandwagon prevention)
- `BackgroundService` sweep for expired polls (same pattern as S4.5 expiration sweep)
- 13+ integration tests covering: create authz, vote uniqueness, quorum, supermajority, auto-execute, bandwagon prevention, expiration

**Tasks:**

1. Create `IPollService` + `PollService`
2. Implement `CreatePollAsync` with authz and validation
3. Implement `CastVoteAsync` with uniqueness and eligibility checks
4. Implement `ClosePollAsync` with result computation and auto-execute
5. Implement bandwagon prevention in `GetPollWithResultsAsync`
6. Add poll expiration to the `DataSeeder` sweep
7. Register in DI
8. Integration tests

### S9.4 — Poll UI: community detail [8 SP]

**Assigned to:** Claude (implementation) + HCD Expert (accessibility review) + George (copy review)

**As a** community member **I want** to see and vote on active polls on the community page **so that** I can participate in community decisions.

**Acceptance criteria:**

- "Active polls" section on community detail page between Members and Venues sections
- Each poll card shows: title, description, options as radio buttons, vote button, expiry countdown
- Results display after voting: CSS-width bars with `<progress>` elements, vote counts, quorum progress ("8 of 24 members voted; 12 needed")
- Before voting: options are visible but results are hidden (bandwagon prevention)
- Vote submission via static SSR form post
- Owner/Admin see a "Create poll" button linking to poll creation form
- Single-page poll creation form: title, description, options (add/remove), duration picker (1–14 days), anonymous toggle, poll type dropdown
- Member-removal polls: option labels are player display names; anonymous voting forced on

**Tasks:**

1. Create `PollCard.razor` shared component — displays a single poll with vote form and results
2. Add "Active polls" section to `Detail.razor`
3. Create `Components/Pages/Communities/CreatePoll.razor` — poll creation form
4. Wire vote submission to `IPollService.CastVoteAsync`
5. Render results with `<progress>` elements + CSS bars
6. Add quorum progress indicator
7. George reviews all poll copy (labels, quorum messaging)
8. HCD Expert reviews keyboard navigation, `<progress>` accessibility, form labels

### S9.5 — Site-wide feature polls on About page [5 SP]

**Assigned to:** Claude (implementation) + George (copy review)

**As a** pool player **I want** to vote on proposed features on the About page **so that** the roadmap reflects what players actually want.

**Acceptance criteria:**

- `PollType.FeatureProposal` — site-wide polls (no `CommunityId`)
- About page's "Planned features" section queries open + closed `FeatureProposal` polls
- Open polls: render inline vote form (same `PollCard` component)
- Closed polls: render results with winning option highlighted
- Site-wide poll creation: restricted to a configurable set of player IDs (proto-admin) stored in `appsettings.json` under `SiteAdmins:PlayerIds[]` — no formal admin role in v1.0
- Eligible voters: any authenticated player

**Tasks:**

1. Add `FeatureProposal` to `PollType` enum
2. Update About page to query and render site-wide polls
3. Add `SiteAdmins:PlayerIds` config section
4. Update `IPollService.CreatePollAsync` to check site-admin for `FeatureProposal` type
5. George reviews "Planned features" section copy

### S9.6 — Smoke test §25 [2 SP]

**Assigned to:** Claude

### S9.7 — Sprint 9 tag [2 SP]

**Assigned to:** Claude

---

## Sprint 10 — Multi-player matches

**Sprint goal:** Head-to-head match creation, alternating-break scoring on a shared device, and frame-by-frame comparison.

**Sprint velocity:** 42 SP

**Data model** (per Architect + George):

- Top-level `Match` entity referencing existing `Game` documents by `GameId` (do not embed — Game has its own aggregate root and persistence lifecycle)
- Fields: `MatchId`, `Format` (Single / RaceTo / BestOf), `Target` (N), `PlayerIds: Guid[]`, `GameIds: Guid[]`, `BreakMethod` (Lagged / CoinFlip / MutualAgreement / PreviousLoserBreaks), `TableNumber?`, `Stakes?` (private free-text, never surfaced publicly), `Status`, `CreatedAt`, `CompletedAt?`, `WinnerPlayerId?`
- Build the schema for best-of-N now even if v1 UI only exposes single-game and race-to-N (per George: "retrofitting is a painful migration")

**UX** (per HCD + George):

- Match creation: type selector (Solo / Match) on `/games/new`, then match subtype (H2H / simultaneous) as a second choice
- Alternating-break: one shared device first (bar-box reality per George); mandatory privacy interstitial between turns ("Hand the device to {opponent}")
- Simultaneous mode: each player on own device; score-confirmation step (player A submits, player B sees and confirms — per George: "unilateral unacknowledged score entry is not acceptable")
- Frame comparison: stacked on mobile, side-by-side two-column on desktop
- "Check opponent's progress" manual refresh for simultaneous mode (auto-refresh via SignalR groups wired in S10.2)

**Sprint DoD:**

- `/games/new` offers Solo vs. Match type selection
- Head-to-head match creation with opponent selection and break method
- Alternating-break frame scoring on shared device with privacy interstitial
- Match detail page with frame-by-frame comparison
- Match history on player profile
- Smoke test §26

### S10.1 — Match entity + repository [5 SP]

**Assigned to:** Claude (implementation) + Architect (entity review)

**As a** developer **I want** a `Match` entity and repository **so that** head-to-head games have persistent state.

**Acceptance criteria:**

- `Match` entity in Model with all fields from the design
- `MatchFormat` enum: `Single = 0, RaceTo = 1, BestOf = 2`
- `BreakMethod` enum: `Lagged = 0, CoinFlip = 1, MutualAgreement = 2, PreviousLoserBreaks = 3`
- `MatchStatus` enum: `Created = 0, InProgress = 1, Completed = 2, Abandoned = 3`
- BSON class map with string-encoded Guid serializers
- `matches` collection with indexes: `(PlayerIds, Status)` for "my matches", `(Status, CreatedAt)` for active match listing
- `IMatchRepository`: `CreateAsync`, `GetByIdAsync`, `ListForPlayerAsync(playerId, status?, skip, limit)`, `UpdateAsync`
- DI registration

**Tasks:**

1. Create `Match.cs` with all enums
2. Register BSON class map
3. Add collection to DbContext
4. Create `IMatchRepository` + `MatchRepository`
5. Create indexes
6. Register in DI
7. Repository integration tests

### S10.2 — Match service [13 SP]

**Assigned to:** Claude (implementation) + Architect (design review) + George (rules review)

**As a** developer **I want** a match service that orchestrates multi-game play **so that** UI code is free of match-lifecycle logic.

**Acceptance criteria:**

- `IMatchService.CreateMatchAsync(creatorPlayerId, opponentPlayerId, format, target, breakMethod, tableNumber?, stakes?)` — creates Match + first Game, returns both
- `IMatchService.RecordFrameAsync(matchId, gameId, playerId, breakBonus, ballCount)` — delegates to existing `IGameService`, then checks match state:
  - If game is complete, check win condition
  - If match is not won, create next Game (alternating who breaks first based on `BreakMethod`)
  - If match is won, set `WinnerPlayerId`, `Status = Completed`, `CompletedAt`
- `IMatchService.GetMatchDetailAsync(matchId)` — returns Match + all Games with frames
- `IMatchService.AbandonMatchAsync(matchId, byPlayerId)` — sets `Status = Abandoned` (either player can abandon)
- Win-condition calculation:
  - `Single`: first game's winner wins the match
  - `RaceTo`: first player to win N games
  - `BestOf`: first player to win ⌈N/2⌉ games
- SignalR group management: on match create, join both player connection IDs to a `match:{matchId}` group; broadcast frame completions and match state changes to the group
- 13+ integration tests covering: create, frame recording, game completion triggering next game, match win detection, abandon, race-to-3 full sequence

**Tasks:**

1. Create `IMatchService` + `MatchService`
2. Implement match creation (creates Match + first Game)
3. Implement frame recording with game/match lifecycle
4. Implement win-condition for all three formats
5. Implement next-game creation with break alternation
6. Implement abandon
7. Wire SignalR group management (depends on Sprint 8 hub)
8. George reviews break-alternation logic and win conditions
9. Integration tests

### S10.3 — Match creation UI [5 SP]

**Assigned to:** Claude (implementation) + HCD Expert (flow review) + George (copy review)

**As a** player **I want** to start a head-to-head match **so that** I can compete against another player with tracked results.

**Acceptance criteria:**

- `/games/new` gains a Solo / Match type selector at the top (per HCD)
- Solo: existing single-player game creation flow (unchanged)
- Match: reveals match-specific fields:
  - Opponent selection: dropdown of friends + community co-members
  - Match format: Single game / Race to N / Best of N (with N picker)
  - Break method: Lagged / Coin flip / Mutual agreement (radio group)
  - Table number (optional text input)
  - Stakes (optional text input, labeled "For bragging rights")
- Venue selection: same as existing
- Submit creates the match + first game, redirects to `/matches/{id}/play`

**Tasks:**

1. Modify `/games/new` — add type selector that conditionally shows match fields
2. Add opponent dropdown (query friends + community co-members)
3. Add format / break-method / table / stakes fields
4. Wire form submit to `IMatchService.CreateMatchAsync`
5. George reviews field labels and "For bragging rights" stakes label
6. HCD Expert reviews form flow and keyboard navigation

### S10.4 — Alternating-break scoring UX [8 SP]

**Assigned to:** Claude (implementation) + HCD Expert (accessibility review) + George (UX review)

**As a** player **I want** to score a head-to-head match on a shared device with clear turn separation **so that** both players can track their frames honestly.

**Acceptance criteria:**

- `/matches/{id}/play` page — `@rendermode InteractiveServer` (like existing `Play.razor`)
- Shows current match state: scores per player, current game number, who is breaking
- Privacy interstitial between turns: full-screen overlay "Hand the device to {opponent name}", requires tap/click to dismiss (per George: social accountability built in)
- Frame scoring reuses existing `Play.razor` scoring UI (break bonus + ball count)
- After each frame: live score comparison visible, both players' running totals
- After each game: "Game {N} complete — {winner} wins" summary, then interstitial before next game (if match continues)
- Match completion: full comparison screen with "Match winner: {name}" header
- Break indicator per frame (who broke) visible in the comparison

**Tasks:**

1. Create `Components/Pages/Matches/Play.razor` + CSS
2. Implement privacy interstitial overlay component
3. Integrate existing frame-scoring UI
4. Implement game-completion → next-game transition
5. Implement match-completion screen
6. George reviews turn-switching UX and interstitial copy
7. HCD Expert reviews focus management during interstitial, keyboard accessibility

### S10.5 — Match detail + comparison view [5 SP]

**Assigned to:** Claude (implementation) + HCD Expert (layout review)

**As a** player **I want** to review a completed match frame-by-frame **so that** I can see where I gained or lost ground.

**Acceptance criteria:**

- `/matches/{id}` detail page — static SSR
- Header: match format, players, date, venue, winner, final score (games won per side)
- Frame-by-frame comparison table:
  - Desktop: two-column side-by-side, one column per player
  - Mobile: stacked, player A's frame above player B's frame, alternating
  - Each frame row: frame number, break indicator (who broke), break bonus, ball count, frame score
  - Running total column
  - Highlight the winning frame where the match was clinched
- Metadata: break method, table number (if set)
- Stakes field NOT displayed (private — per George)

**Tasks:**

1. Create `Components/Pages/Matches/Detail.razor` + CSS
2. Implement frame comparison table with responsive layout
3. Highlight clinching frame
4. HCD Expert reviews table accessibility (scope attributes, caption, responsive behavior)

### S10.6 — Match history on player profile [3 SP]

**Assigned to:** Claude (implementation)

**As a** player **I want** my match history visible on my profile **so that** visitors can see my competitive record.

**Acceptance criteria:**

- Profile page gains a "Recent matches" section after "Recent games"
- Each row: date, opponent name, match format, result (Won/Lost/Abandoned), final game score
- Links to `/matches/{id}`
- Limited to 5 most recent; same privacy gating as games (audience-controlled)

**Tasks:**

1. Add match query to `Profile.razor` `OnInitializedAsync`
2. Render match history section
3. Apply audience gating from `ViewerScopedPlayerProfile`

### S10.7 — Smoke test §26 [2 SP]

**Assigned to:** Claude

### S10.8 — Sprint 10 tag [1 SP]

**Assigned to:** Claude

---

## Architectural decisions

### AD-1: Static SSR + selective SignalR (not global InteractiveServer)

**Decision:** Keep static SSR as the default render mode. Add SignalR hubs for specific real-time needs. Use JS interop to bridge the gap.

**Rationale:** Global InteractiveServer would break every `[SupplyParameterFromForm]` form in the app. The existing form pattern is well-tested and consistent. SignalR hubs provide targeted real-time without the blast radius of a render-mode switch.

### AD-2: Soft delete for players (not hard delete or separate entity)

**Decision:** Add `RetiredAt: DateTime?` to the existing `Player` entity. PII cleared synchronously on delete. EmailHash retained via HMAC-SHA256 with application-level key.

**Rationale:** Keeps the entity model simple. Avoids orphaned foreign keys. Leaderboard and game-history queries add `RetiredAt == null` filter. Re-registration prevention is O(1) via email hash.

### AD-3: Votes in a separate collection (not embedded in Poll)

**Decision:** `Vote` documents in their own collection with a unique compound index on `(PollId, PlayerId)`.

**Rationale:** One-vote-per-player is enforced at the database level without transactions. Embedded votes would exceed the 16MB document limit for large communities and make the uniqueness constraint application-only.

### AD-4: Match references Games (does not embed)

**Decision:** `Match` is a top-level entity with `GameIds[]` referencing existing `Game` documents.

**Rationale:** `Game` is an established aggregate root with rich domain behavior (frames, scoring, state machine). Embedding would duplicate the domain logic. Match is a coordination entity; Game is the scoring entity.

### AD-5: Build order — SignalR before Matches

**Decision:** SignalR infrastructure (Sprint 8) ships before multi-player matches (Sprint 10).

**Rationale:** Live match scoring requires SignalR groups. Building matches without the hub would mean implementing a polling-based scoring UX and then replacing it — throwaway work.

### AD-6: No Redis in v0.1.x (defer to scaling trigger)

**Decision:** Do not add Redis to the stack for Sprints 6–10. The single Blazor Server instance, in-process `ConcurrentDictionary` for connection tracking, and 30-second MongoDB polling loop are adequate at the current scale (tens to low-hundreds of concurrent users).

**Rationale:** Redis adds operational complexity (a third stateful service in Docker Compose) and application complexity (cache invalidation, fallback paths on Redis failure, dual-store reasoning) without measurable benefit at this scale. The SignalR backplane is unnecessary for a single server instance. MongoDB with proper indexes serves notification counts, leaderboard results, and member lists in single-digit milliseconds for this data volume.

**Triggers to reconsider:**
- Multiple server instances behind a load balancer → add Redis as the SignalR backplane
- MongoDB query latency for leaderboard or notification counts exceeds 50ms → add Redis caching layer
- Simultaneous-mode match scoring requires sub-second cross-device state synchronization → add Redis for ephemeral match state

### AD-7: Live multi-player match updates are performance-critical

**Decision:** Multi-player match scoring on separate devices (the simultaneous / "each player on their own phone" use case) must deliver near-real-time frame updates. This is a core use case, not an edge case — pool players commonly help keep score on their own devices during a match.

**Rationale:** A 30-second polling interval is acceptable for notification badges and leaderboard refreshes but is unacceptable for live match scoring. When player A completes a frame, player B must see the update within 1–2 seconds to maintain the feel of a live shared experience. The Sprint 8 SignalR hub with per-match groups (wired in S10.2) is the delivery mechanism. If the polling-based change detection in `NotificationPollerService` cannot meet this latency target for match-specific events, the match service should bypass the poller and push directly to the hub on frame completion — a targeted optimization that does not require Redis or architectural changes, just a direct `IHubContext<NotificationHub>` injection in `MatchService`.

**Fallback:** If SignalR latency proves insufficient under real-world conditions (poor mobile connectivity, background tabs), Redis Pub/Sub becomes the justified upgrade path specifically for match state synchronization. This is the most likely trigger for AD-6's "reconsider Redis" decision.

---

## Changelog

| Date | Change | By |
| --- | --- | --- |
| 2026-04-12 | Initial plan drafted. Sprints 6–10 scoped from feature backlog captured same day. Three expert agents (George/poolplayer, Software Architect, HCD Expert) consulted in parallel; findings synthesized into sprint stories with modified Fibonacci story point estimates. | Carey + Claude synthesis of 3 specialist sub-plans |
| 2026-04-12 | Plan approved. Added AD-6 (no Redis in v0.1.x) and AD-7 (live match updates are performance-critical) based on Redis architecture assessment and owner input on the multi-device scoring use case. | Carey + Claude |
