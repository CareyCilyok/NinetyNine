# Friends + Communities — v1.0 → v1.2 Plan

<!-- markdownlint-disable MD060 -->
<!-- Table column alignment is relaxed for this planning doc: content changes -->
<!-- frequently and re-aligning pipes every edit is busywork. Rendered output -->
<!-- is identical; only the raw-source linter cares. -->

**Status:** Accepted 2026-04-11 — Sprint 0 in progress
**Owner:** Carey
**Scope:** Add mutual friendships, public/private communities, venue↔community affiliation, and a 4-tier profile audience model to NinetyNine.

This document is the **source of truth** for Friends + Communities planning. Any change to fork selections, sprint scope, ordering, or DoD that the user directs in conversation **must** be mirrored here in the same PR or commit. See the [changelog](#changelog) at the bottom.

---

## Table of contents

1. [North star](#north-star)
2. [Fork selections](#fork-selections)
3. [Audience tier semantics](#audience-tier-semantics)
4. [Sprint roadmap at a glance](#sprint-roadmap-at-a-glance)
5. [Sprint 0 — Foundation](#sprint-0--foundation)
6. [Sprint 1 — Friendships](#sprint-1--friendships)
7. [Sprint 2 — Communities (player-owned)](#sprint-2--communities-player-owned)
8. [Sprint 3 — Venue affiliation + Audience UI](#sprint-3--venue-affiliation--audience-ui)
9. [Sprint 4 — v1.1 polish](#sprint-4--v11-polish)
10. [Sprint 5 — v1.2 discovery + notifications](#sprint-5--v12-discovery--notifications)
11. [Cross-sprint engineering concerns](#cross-sprint-engineering-concerns)
12. [Changelog](#changelog)

---

## North star

**Most usable yet privacy-centric**, with a data model that supports the eventual full-tier audience system without a second migration.

Every fork below was picked against that single criterion. When the two sides conflict, privacy wins — but never at the cost of a materially worse UX.

---

## Fork selections

| Fork | Decision | Why this is both usable and privacy-centric |
|---|---|---|
| **A — Friend request workflow** | Request / accept from day 1 | One extra click vs. mutual-add is a negligible UX cost; in return you get recipient consent (privacy), the rate-limit / spam / harassment controls from the security plan, and an immutable audit of who initiated. Mutual-add would also be a throwaway you'd ship twice. |
| **B — Venue-owned communities** | Data model in v1.0 (`OwnerType` + nullable FKs); UI in v1.1 | Keeps the MVP small without painting the schema into a corner. When we need venue-owned UI, there's no migration. |
| **C — `Communities` audience tier semantics** | "Shares at least one community with the viewer" (union, most-permissive wins within the tier) | Pick-a-community-per-field is unusable in a small app. Shared-membership is the only interpretation that survives contact with reality. |
| **D — bool → Audience migration** | `true` → `Friends`, not `Public` | Strictly-tighter default (security's "no silent widening" principle). One-time banner on Edit Profile after migration explains what changed and how to widen back. |
| **E — Mongo transactions on accept** | Idempotent compensating (no replica set) | Current Mongo is single-node; transactions would need a replica-set rebuild. Compensating is safe here because the friendship unique index prevents dupes and the request-status flip is idempotent. |
| **Public tier semantics** | Any authenticated NinetyNine user, **not** the open internet | `robots.txt` + `X-Robots-Tag: noindex, nofollow` on all authenticated routes. If "World" (internet-public) is ever wanted, that becomes a 5th tier — do not conflate. |
| **Friends list as a gated field** | Yes — per-field Audience, defaults to `Friends` | Consistent with other profile fields; prevents friend graph from leaking on public profiles. |
| **Avatar Audience default** | `Public` (explicit exception to "most-private default") | Preserves current behavior; avoids a visible regression across leaderboards, game history, and badges. Documented exception. |
| **Community membership: who can invite?** | Owner + Admin only (v1.0) | Clearest trust model, fewest abuse vectors. Members can propose in v1.1 if requested. |
| **Private community discoverability** | Fully hidden to non-members | No "searchable shell", no existence leak. |
| **Friend request auto-expiration** | 30 days, swept by `DataSeeder` heal pass | Keeps the request inbox clean; no UI complexity. |
| **Soft cap on owned communities per player** | 10 | Prevents accidental / malicious proliferation; can be raised trivially. |
| **Instance admin role** | None in v1.0 | No surveillance surface. Use `mongosh` if debugging is genuinely needed. |
| **Block vs. mute** | Block only (bidirectional) | Mute weakens the harassment model; defer to v1.2 if ever requested. |
| **Mutual friends count on profiles** | Defer to v1.2 | Leaks graph structure; not needed for the core loop. |

---

## Audience tier semantics

These definitions are locked and must be referenced everywhere the word "Audience" appears in code, UI copy, or further planning.

- **Private** — Self only. Admin access would be logged *and* visible to the target, but there is no admin role in v1.0, so this effectively means "nobody else".
- **Friends** — Self + mutual accepted friends. One-sided requests grant nothing.
- **Communities** — Self + anyone who shares at least one community with the viewer where *both* are currently approved members. Public vs. private community status does not change this rule for the viewer — both count as "shared membership". When the viewer is in multiple communities with the target, the union applies: any one shared community is sufficient (most-permissive wins within the tier).
- **Public** — Self + any authenticated NinetyNine user. Never unauthenticated.

Audience values are ordered most-private-first in code (`Private = 0, Friends = 1, Communities = 2, Public = 3`) so a relationship check can use a simple integer comparison: `relationship >= fieldAudience`.

---

## Sprint roadmap at a glance

| Sprint | Scope | Visible demo at end |
|---|---|---|
| **Sprint 0** — Foundation | Data model, enum, heal pass, indexes | `./deploy.sh` logs show heal-pass migrated N players; `mongosh` shows 4 new collections + indexes; no UI changes |
| **Sprint 1** — Friendships | `/friends`, request/accept/decline/cancel/unfriend, seed pre-befriended | Carey sends George a friend request, George accepts in the browser |
| **Sprint 2** — Communities (player-owned) | `/communities`, `/communities/new`, `/communities/{id}`, invitations, join flows | Carey creates private "Bumpers Regulars", invites George, George accepts |
| **Sprint 3** — Venue affiliation + Audience UI | `Venue.CommunityId`, Audience picker on Edit Profile, profile reads apply the matrix | Bumpers Billiards is chip-labeled "Pocket Sports"; Carey sets phone to Communities, George sees it, carey_b (non-member) does not |
| **Sprint 4** — v1.1 polish | Venue-owned community UI, admin role, leaderboard community filter, friends leaderboard, expiration sweep | Bumpers venue page creates its own community; leaderboard filter shows "Just friends" |
| **Sprint 5** — v1.2 discovery | Community browse/search, notification delivery stub, block, audit log surface | Carey finds a new public community via search |

Each sprint is sized ~M (one focused week of solo work). Sprint 0 is a hard prerequisite; Sprints 1–3 can overlap only at the seam between services and UI.

---

## Sprint 0 — Foundation

**Sprint goal:** Every collection, index, BSON class map, and migration heal pass is in place. No UI changes. All existing smoke tests still pass.

**Sprint DoD:**

- Build green, all existing tests pass
- `mongosh` shows `friendships`, `friend_requests`, `communities`, `community_members` collections with their indexes
- `./deploy.sh logs web` shows heal-pass output for bool-to-Audience migration on all 3 seeded players
- No visual change in the app

### S0.1 — Add `Audience` enum to model [S]

**As a** developer **I want** a typed 4-tier audience enum **so that** services can reason about visibility without passing raw strings.

**Acceptance criteria:**

- Public `enum Audience { Private = 0, Friends = 1, Communities = 2, Public = 3 }` in `NinetyNine.Model`
- XML doc comment on each value restating the locked semantics
- `ProfileVisibility` class has 4 new `Audience` properties: `EmailAudience`, `PhoneAudience`, `RealNameAudience`, `AvatarAudience`
- Old `bool` properties are kept temporarily and are NOT removed yet. They carry an XML comment pointing at the new `*Audience` replacement but are **not** marked `[Obsolete]` — `TreatWarningsAsErrors` is enabled on every csproj in this solution, so an `[Obsolete]` attribute would cascade ~20 build errors at every call site (Profile.razor, EditProfile.razor, Login.razor, DataSeeder). The legacy properties will be deleted in Sprint 3 once `GetProfileForViewerAsync` is the single read path. (See changelog 2026-04-11.)
- Defaults: `EmailAudience = Private`, `PhoneAudience = Private`, `RealNameAudience = Private`, `AvatarAudience = Public`

**Tasks:**

1. Edit `src/NinetyNine.Model/Player.cs` — add enum and new properties
2. Update XML doc on `ProfileVisibility`
3. Bump the `Player` class comment to mention schema version 2
4. Unit test covering the numeric ordering invariant (`Private < Friends < Communities < Public`)

### S0.2 — New domain entities [M]

**As a** developer **I want** `Friendship`, `FriendRequest`, `Community`, `CommunityMembership`, `CommunityInvitation`, `CommunityJoinRequest` POCOs **so that** repositories have types to read and write.

**Acceptance criteria:**

- Each class in `NinetyNine.Model/` with a Guid id, required fields per the backend plan, `CreatedAt = DateTime.UtcNow` defaults
- `Friendship` has `PlayerAId`, `PlayerBId` (canonically ordered, lower first), `PlayerIdsKey` string (`"{a}:{b}"`), `Since`, `CreatedVia`
- `Community` has `OwnerType` enum (`Player | Venue`), `OwnerPlayerId?`, `OwnerVenueId?` (mutually exclusive), `Visibility` enum (`Public | Private`), `SchemaVersion = 1`, `Slug`, `Description?`
- `Venue` gains `CommunityId?` and `CreatedByPlayerId?` (latter is retroactive — null for existing venues; the first editor of a legacy venue claims authorship)
- Unit tests: canonical ordering invariant on `Friendship`; enum mutual-exclusion invariant on `Community`

**Tasks:**

1. Add `Friendship.cs`, `FriendRequest.cs`, `Community.cs`, `CommunityMembership.cs`, `CommunityInvitation.cs`, `CommunityJoinRequest.cs` under `src/NinetyNine.Model/`
2. Modify `Venue.cs` — add `CommunityId`, `CreatedByPlayerId`
3. Unit tests in `tests/NinetyNine.Model.Tests/`

### S0.3 — BSON class maps + collections [M]

**As a** developer **I want** all new types registered with Mongo's class map and collections created with their indexes **so that** repositories can read and write.

**Acceptance criteria:**

- `BsonConfiguration.Register()` includes maps for all 6 new types
- `schemaVersion` field added to `players`, `venues`, `communities` maps
- Bool-tolerant deserializer for `visibility` fields (accepts both `bool` and string enum, writes back string)
- On app startup, all 4 new collections exist with these indexes:

| # | collection.field(s) | purpose | unique? |
|---|---|---|---|
| 1 | `friendships.playerIdsKey` | edge existence check | yes |
| 2 | `friendships.playerA` | A's friends | no |
| 3 | `friendships.playerB` | B's friends | no |
| 4 | `friend_requests.{toPlayerId, status}` | inbox filtered by pending | no |
| 5 | `friend_requests.{fromPlayerId, status}` | sent-requests view | no |
| 6 | `friend_requests.{fromPlayerId, toPlayerId}` partial `status=Pending` | prevent duplicate pending | yes (partial) |
| 7 | `communities.name` collation strength 2 | case-insensitive uniqueness | yes |
| 8 | `communities.slug` | url lookup | yes |
| 9 | `communities.visibility` | public community browse | no |
| 10 | `communities.ownerPlayerId` | "communities I own" | no |
| 11 | `communities.ownerVenueId` | venue-owned communities | no |
| 12 | `community_members.{communityId, joinedAt}` | members ordered by join | no |
| 13 | `community_members.{playerId, communityId}` | "my communities", dedupe | yes |
| 14 | `community_members.{communityId, role}` | list mods/owners | no |
| 15 | `venues.communityId` | venues in a community | no |

**Tasks:**

1. Extend `NinetyNine.Repository.BsonConfiguration.Register()`
2. Add `FriendshipRepository`, `FriendRequestRepository`, `CommunityRepository`, `CommunityMemberRepository` stubs that ensure indexes on first call
3. Wire DI in `NinetyNine.Repository.ServiceCollectionExtensions`
4. Verify with `docker exec ninetynine-mongo-1 mongosh … db.friendships.getIndexes()` after startup

### S0.4 — Repository interfaces and implementations [M]

**As a** developer **I want** clean repository interfaces for each new entity **so that** services depend on contracts, not implementations.

**Acceptance criteria:**

- `IFriendshipRepository`: `GetByPairAsync`, `ListForPlayerAsync`, `CreateAsync`, `DeleteAsync`, `CountForPlayerAsync`
- `IFriendRequestRepository`: `GetPendingAsync(from, to)`, `ListIncomingAsync(playerId, status?)`, `ListOutgoingAsync(playerId, status?)`, `CreateAsync`, `UpdateStatusAsync(id, status, respondedAt)`, `SweepExpiredAsync(olderThan)`
- `ICommunityRepository`: `GetByIdAsync`, `GetBySlugAsync`, `ListByOwnerPlayerAsync`, `ListByOwnerVenueAsync`, `SearchPublicByNameAsync(prefix)`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`
- `ICommunityMemberRepository`: `GetMembershipAsync(communityId, playerId)`, `ListMembersAsync(communityId, paged)`, `ListCommunitiesForPlayerAsync(playerId)`, `AddAsync`, `RemoveAsync`, `CountMembersAsync`

**Tasks:**

1. Add interfaces in `src/NinetyNine.Repository/Repositories/`
2. Concrete Mongo implementations in the same folder
3. Unit tests using the existing Testcontainers pattern from `NinetyNine.Repository.Tests`

### S0.5 — Heal pass: bool → Audience [S]

**As a** developer **I want** `DataSeeder` to migrate existing players from bool visibility to the Audience enum **so that** no player is stuck on the old schema.

**Acceptance criteria:**

- New method `HealProfileVisibilityAsync` in `DataSeeder`, runs before the player-heal and venue-reconcile passes
- For each player with `schemaVersion < 2`:
  - `EmailAddress: true → Friends`, `false → Private`
  - `PhoneNumber: true → Friends`, `false → Private`
  - `RealName: true → Friends`, `false → Private`
  - `Avatar: true → Public`, `false → Private` (the one exception — avatar stays public by default)
  - Set `schemaVersion = 2`
  - Log: `"Migrated visibility for player {name}: email={e}, phone={p}, realName={r}, avatar={a}"`
- Set a transient `migrationBannerDismissed: false` flag on migrated players so the Edit Profile page can show the one-time notice in Sprint 3
- Idempotent: second startup is a no-op

**Tasks:**

1. Extend `DataSeeder.SeedAsync` to call the new pass
2. Add logic to `HealProfileVisibilityAsync`
3. Manual verification: stop stack, reset one player to `schemaVersion: 1` with bool flags via `mongosh`, restart, verify heal fires once

### S0.6 — Smoke test + doc updates [S]

- Add `docs/smoke-test-checklist.md` §16 stub: "Foundation migration verification"
- Update `CLAUDE.md` with a brief "Friends & Communities data model" section pointing at this plan
- Commit message references `DEF-008` for the visibility default pattern

---

## Sprint 1 — Friendships

**Sprint goal:** Two authenticated users can become mutual friends via the browser with no `mongosh` escape hatches.

**Sprint DoD:**

- `/friends` page serves a tabbed list, requests, and find surface
- Request → accept → friends-list update works in browser
- Seeded test players are pre-befriended
- Smoke test §16 covers the full happy path

### S1.1 — `IFriendService` [M]

**As a** developer **I want** a service that encapsulates the friendship lifecycle with rate limits **so that** UI code is free of business rules.

**Acceptance criteria:**

- All 10 methods from the backend architect's plan (`SendRequestAsync`, `CancelRequestAsync`, `AcceptRequestAsync`, `DeclineRequestAsync`, `RemoveFriendAsync`, `ListFriendsAsync`, `ListIncomingRequestsAsync`, `ListOutgoingRequestsAsync`, `AreFriendsAsync`, `GetRelationshipAsync`)
- Invariants enforced in code + unit tests:
  - Cannot friend yourself
  - At most one Pending request per direction per pair
  - Accept creates a Friendship + flips the request to Accepted in a compensating-idempotent pattern
  - Reverse-direction Pending auto-resolves on accept
  - Unfriending is immediate and bidirectional (single doc delete)
- Rate limits enforced at the service level (not middleware):
  - Max 10 outbound pending at any time per player
  - Max 20 sent per 24h per player
  - Max 3 sent to the same target per 30 days
  - Declined request locks re-request to the same target for 90 days
- Returns a `Result<T>` discriminated type; errors are domain-named (`FriendRequestRateLimited`, `AlreadyFriends`, `SelfFriendship`, etc.)

**Tasks:**

1. Add `IFriendService` + `FriendService` in `src/NinetyNine.Services/`
2. Add a rate-limit helper (query `friend_requests` count by sender/time-window)
3. Unit tests covering every invariant in `tests/NinetyNine.Services.Tests`
4. Wire DI in `ServiceCollectionExtensions`

### S1.2 — `/friends` page with tabs [M]

**As a** signed-in user **I want** a single page that shows my friends, pending requests, and a search box **so that** I can manage all friendship state in one place.

**Acceptance criteria:**

- Route: `@page "/friends"` with `@attribute [Authorize]`
- Three tabs: **Friends** (list), **Requests** (incoming + outgoing), **Find**
- "Find" tab: display-name-only search box (no email enumeration per security plan)
- Search results show each candidate with avatar, display name, relationship state chip (`None` / `Request sent` / `Request received` / `Friends`)
- Each result has an action button: `Send request` / `Cancel request` / `Accept` / `Remove friend`
- Buttons go through `IFriendService`; no direct repository access
- Rate limit errors render as an inline alert (`"You've sent 10 pending requests — wait for responses or cancel some."`)
- Empty states per HCD's copy table
- Static SSR (per DEF-003) — forms use `EditForm method="post"` with auto-injected antiforgery

**Tasks:**

1. Create `src/NinetyNine.Web/Components/Pages/Friends/Index.razor` (+ `.razor.css`)
2. Create a small `FriendshipStateChip.razor` shared component
3. Forms for send/accept/decline/cancel/unfriend — all use `EditForm` + `[SupplyParameterFromForm]`
4. Implement the three tab views as `@if` branches on a `string _tab` query parameter (`?tab=friends|requests|find`) so deep-linking works and no interactive circuit is needed
5. Route test + bUnit component test for tab switching

### S1.3 — Nav additions [S]

**As a** signed-in user **I want** to reach the Friends page in one click and see my pending-request count in the sidebar **so that** I know when someone needs my response.

**Acceptance criteria:**

- Sidebar gains a "Friends" item between "Home" and "New Game" with the Phosphor `users` icon
- Badge on the item shows the count of pending incoming requests (small pill)
- User menu also gains "Friends" with the same badge
- Both badges are computed server-side during render; no polling, no JS

**Tasks:**

1. Edit `Components/Layout/NavMenu.razor` and `UserMenu.razor`
2. Inject `IFriendService` in a small `CurrentUserBadges` cascading parameter (avoid N+1 renders)
3. bUnit tests asserting the badge appears only when count > 0

### S1.4 — Home page "Friend requests" card [S]

**As a** signed-in user with pending friend requests **I want** a prominent card on my home page **so that** I notice them without having to open a menu.

**Acceptance criteria:**

- On `Home.razor`, if `ListIncomingRequestsAsync().Count > 0`, render a card above the "Jump back in" grid
- Card shows the first 3 requesters by avatar + display name and a "View all" link to `/friends?tab=requests`
- Card is hidden if count is zero

**Tasks:**

1. Modify `Home.razor`
2. Add empty-state CSS so the card doesn't push the layout around

### S1.5 — Seeded pre-befriended test players [S]

**As a** developer running the app for the first time **I want** the 3 seeded test players to already be friends **so that** the feature isn't empty on first run.

**Acceptance criteria:**

- `DataSeeder` reconcile pass inserts canonical friendships: `(carey, george)`, `(carey, carey_b)`, `(george, carey_b)`
- Idempotent via the `friendships.playerIdsKey` unique index
- Logs: `"Seeded friendship: carey <-> george"` etc.

**Tasks:**

1. Extend `DataSeeder` with `ReconcileSeededFriendshipsAsync`
2. Call from `SeedAsync` after the player-heal pass
3. Log output verification

### S1.6 — Smoke test §16 [S]

Add to `docs/smoke-test-checklist.md`:

- 16.1 Send friend request flow
- 16.2 Accept + decline flow
- 16.3 Rate-limit messaging
- 16.4 Nav badge count
- 16.5 Home card visibility

---

## Sprint 2 — Communities (player-owned)

**Sprint goal:** A signed-in user can create a player-owned community (public or private), invite friends, and accept invitations.

**Sprint DoD:**

- `/communities`, `/communities/new`, `/communities/{id}` all live
- Invitation → accept flow works end-to-end
- Join public community works in one click
- Join private community via request → owner approves works
- Seeded "Pocket Sports" public community exists and contains all 3 test players
- Smoke test §17 passes

### S2.1 — `ICommunityService` + rate limits [M]

**As a** developer **I want** one service that owns community CRUD, invitation, and join flows **so that** the UI stays thin.

**Acceptance criteria:**

- All 15 methods from the backend plan (`CreateAsync`, `UpdateAsync`, `DeleteAsync`, `TransferOwnershipAsync`, `InviteAsync`, `RespondToInvitationAsync`, `RequestToJoinAsync`, `ApproveJoinRequestAsync`, `DenyJoinRequestAsync`, `JoinPublicAsync`, `LeaveAsync`, `RemoveMemberAsync`, `SetMemberRoleAsync`, `ListMembersAsync`, `ListCommunitiesForPlayerAsync`)
- `CreateAsync` enforces the 10-communities-per-owner cap and name uniqueness (case-insensitive)
- `InviteAsync` enforces max 5 pending invites from any single inviter to any single target per year
- `JoinPublicAsync` is rejected for private communities (`Result.Fail("PrivateCommunityRequiresInvite")`)
- Ownership-transfer target must already be a member; sole-owner-cannot-leave rule enforced
- Unit tests for every invariant

### S2.2 — `/communities/new` [M]

**As a** signed-in user **I want** a form to create a community **so that** I can start organizing my pool group.

**Acceptance criteria:**

- Route `/communities/new` with `[Authorize]`
- Fields: Name (required, 2–40 chars), Slug (auto-generated, editable), Description (optional, 500 chars), Visibility radio (Public/Private). Avatar upload deferred.
- On submit: calls `ICommunityService.CreateAsync`; on success redirects to `/communities/{id}`; on rate-limit or cap failure, inline error
- `EditForm` + `[SupplyParameterFromForm]` (no `<AntiforgeryToken />` per DEF-003)
- bUnit component test

### S2.3 — `/communities` list + `/communities/{id}` detail [L]

**As a** signed-in user **I want** to see the communities I belong to and drill into any one of them **so that** I can see members, venues, and act on my role.

**Acceptance criteria:**

- `/communities` shows "My communities" (joined list) and "Browse public communities" (public only, paginated)
- `/communities/{id}` shows: header (name, description, visibility pill, member count), Members tab, Venues tab (empty in this sprint), Actions area (Join/Leave/Invite/Delete/Settings) gated by the security auth matrix
- **Private community detail** is 404 to non-members — never reveal existence
- Member list respects `ListMembersAsync` which returns per-viewer data (owner sees everyone; public-community non-member sees display names only; private-community member sees everything)
- Settings area visible only to Owner (edit name/description/visibility, transfer ownership, delete)

**Tasks:**

1. `src/NinetyNine.Web/Components/Pages/Communities/Index.razor`
2. `.../Communities/Detail.razor`
3. `.../Communities/New.razor` (from S2.2)
4. Shared `CommunityCard.razor` component
5. Route-level authorization helper that returns 404 for private-community non-members
6. bUnit tests

### S2.4 — Invitation + join-request flows [M]

**As a** community owner **I want** to invite players and approve join requests **so that** I control who's in private communities.

**Acceptance criteria:**

- Owner can invite by display-name picker (reuse the find-friends search component where sensible)
- Target's `/friends` page (or user menu badge) shows pending invitations with Accept/Decline
- For private communities, a non-member can submit a join request from the detail page; owner sees pending requests with Approve/Deny
- Invitation auto-expires after 14 days (enforced on write, swept by the heal pass in Sprint 4)
- All lifecycle transitions go through `ICommunityService`

### S2.5 — Profile surfaces [S]

**As a** signed-in user viewing another player's profile **I want** to see their communities (respecting their visibility settings) **so that** I understand who I'd be playing with.

**Acceptance criteria:**

- Profile page shows a "Communities" section listing shared + public communities the target belongs to
- Private communities the viewer is NOT a member of are NOT listed (existence leak)
- Count is always Audience-gated on the target's `CommunityAudience` (new field, default `Friends`)

### S2.6 — Seed "Pocket Sports" [S]

**As a** developer **I want** a seeded public community **so that** the feature isn't empty on first run.

**Acceptance criteria:**

- `DataSeeder` reconcile creates a community `Pocket Sports` (public, owner = carey) if it doesn't exist
- All 3 seeded test players are members
- All 17 seeded venues have `CommunityId = pocketSports.Id` (unless already affiliated with another community, in which case leave alone)

### S2.7 — Smoke test §17 [S]

- 17.1 Create public community
- 17.2 Create private community
- 17.3 Invite flow (private)
- 17.4 Public join
- 17.5 Private join-request flow
- 17.6 Member list visibility rules
- 17.7 Private community 404s to non-members

---

## Sprint 3 — Venue affiliation + Audience UI

**Sprint goal:** Venues can be affiliated with a community by the venue creator, Edit Profile uses the new per-field Audience picker with the migration banner, and profile reads respect the full Audience matrix including the `Communities` tier shared-membership check.

**Sprint DoD:**

- Venue edit page has a community affiliation picker for the venue creator
- Edit Profile uses the new picker, shows the one-time migration banner if applicable
- `IPlayerService.GetProfileForViewerAsync` is the single read path and applies the matrix
- Profile page respects every Audience tier
- Smoke test §18 covers the full privacy flow

### S3.1 — Venue affiliation service method [S]

**As a** venue's creator **I want** to set or clear my venue's community affiliation **so that** the venue appears alongside related ones.

**Acceptance criteria:**

- `IVenueService.SetCommunityAffiliationAsync(venueId, communityId?, byPlayerId)`
- Authz: actor must be the venue's `CreatedByPlayerId` (for legacy venues with null `CreatedByPlayerId`, allow the first editor to claim and set themselves)
- Unit tests

### S3.2 — Venue edit UI: affiliation picker [M]

**As a** venue owner **I want** to pick a community from a dropdown **so that** my venue is discoverable through community pages.

**Acceptance criteria:**

- `/venues/{id}/edit` gains a "Community" section
- Dropdown lists: "None", every community the current user is a member of
- Chip display on `/venues` list and `/venues/{id}`

### S3.3 — `AudiencePicker` component [M]

**As a** user editing my profile **I want** a per-field visibility picker that's granular without being overwhelming **so that** I can default most things to "Friends" and widen only what I care about.

**Acceptance criteria** (HCD's recommended design):

- New `AudiencePicker.razor` shared component — 4 segments (Private / Friends / Communities / Public) with icons (lock, pair, people-group, globe)
- Keyboard-accessible (radio-group semantics)
- A "Global default" picker at the top of the privacy section
- Three section-level pickers (General Info / Stats / Game History) inherit from global unless overridden
- Each individual field can override with a small "custom" indicator dot
- Saving is form-post; no client-side JS
- bUnit tests cover every combination

### S3.4 — Edit Profile uses picker + migration banner [M]

**As a** user whose visibility just migrated from bool to Audience **I want** a one-time banner explaining what changed and how to change it back **so that** I'm not surprised.

**Acceptance criteria:**

- Edit Profile shows the picker for each of the 4 visibility fields
- If `migrationBannerDismissed == false`, a dismissable alert at the top: *"We've moved from simple on/off visibility to four audience tiers. Any field you had visible is now set to Friends. You can widen or narrow each field any time."*
- Dismiss button flips the flag and persists

### S3.5 — `GetProfileForViewerAsync` + audience matrix [L]

**As a** developer **I want** one service method that returns a projected `ViewerScopedPlayerProfile` with every field either populated or nulled out based on the viewer's relationship to the target **so that** UI code never touches the Audience enum directly.

**Acceptance criteria:**

- `IPlayerService.GetProfileForViewerAsync(targetId, viewerId?)` is the ONLY read path the UI uses for profile rendering
- Relationship resolution (in order of evaluation):
  1. `viewerId == targetId` → Self, all fields visible
  2. Mutual friendship via `IFriendshipRepository.GetByPairAsync` → Friend
  3. Shared community via intersect of `community_members` lookups → Community
  4. Any authenticated viewer → Public
  5. Null viewer (unauthenticated) → Anonymous, only DisplayName + Avatar if `AvatarAudience == Public`
- For each field, the audience check is: `relationship >= audience` using the numeric enum ordering
- Unit tests for every viewer-relationship × field-audience combo (16 cases minimum)
- Performance: single round trip per profile render (no N+1); shared-community check uses a single aggregate pipeline

### S3.6 — Profile page hookup [M]

- `Profile.razor` calls `GetProfileForViewerAsync`, renders gated fields with a "Private" placeholder instead of empty
- Private-venue games in recent-games appear as "Private venue" (no name, no link) for non-members
- Friends count is itself Audience-gated

### S3.7 — Smoke test §18 [S]

- 18.1 Migration banner appears once, dismisses permanently
- 18.2 Audience picker saves and persists
- 18.3 Profile view as Self shows everything
- 18.4 Profile view as Friend shows friend-tier fields
- 18.5 Profile view as Same-community member shows community-tier fields but not friend-only fields
- 18.6 Profile view as Stranger shows only public-tier fields
- 18.7 Private venue in game history renders anonymously for non-members

---

## Sprint 4 — v1.1 polish

**Sprint goal:** Venue-owned communities have a UI; admin role exists; leaderboard can be filtered by community; friends-only leaderboard works.

### S4.1 — Venue-owned community creation UI [M]

- `/venues/{id}/edit` gets a "Create a community for this venue" button
- Pre-fills name = venue name, visibility = Public, owner type = Venue, owner venue id = current venue
- After creation, the venue is auto-affiliated with the new community

### S4.2 — Admin role [M]

- `ICommunityService.SetMemberRoleAsync` supports `Admin` transitions (Owner-only)
- Detail page "Members" tab shows role badges; Owner can promote/demote
- Admin rights per the security matrix (can invite, approve joins, remove members — cannot remove Owner, cannot remove other Admins, cannot delete community)

### S4.3 — Transfer ownership flow [M]

- Owner can initiate transfer to any existing member
- Target receives an invitation-style notice; 7-day expiration; target must accept
- On acceptance: roles swap atomically (compensating-idempotent like friend-accept)

### S4.4 — Leaderboard community filter [M]

- `/stats` gains a dropdown: "All players" / "Just friends" / "[community name]" (one option per community the viewer is in)
- `IStatisticsService.GetLeaderboardForCommunityAsync` and `GetLeaderboardForFriendsAsync`

### S4.5 — Invitation/join-request expiration sweep [S]

- `DataSeeder` heal pass now sweeps:
  - Friend requests older than 30 days with status `Pending` → `Expired`
  - Community invitations older than 14 days → `Expired`
  - Community join requests older than 30 days → `Expired`
- Logs sweeps

### S4.6 — Smoke test §19 [S]

---

## Sprint 5 — v1.2 discovery + notifications

**Sprint goal:** Public communities are discoverable via search; in-app notifications are surfaced via a notification list; optional email notification stub logs to console.

### S5.1 — Community discovery/search [M]

- `/communities/browse` page with name-prefix search over public communities
- Results show member count, owner type badge, affiliated venue count
- Reuses the `communities.name` index

### S5.2 — In-app notification surface [M]

- New collection `notifications` with fields: id, playerId, type, createdAt, readAt?, payload
- Events from the backend event list emit notification writes via an in-process event bus
- New page `/notifications` lists the user's notifications, marks read on view
- User menu "Notifications" link with unread badge

### S5.3 — Email notification stub [S]

- `INotificationDeliveryService` with an implementation that logs formatted emails via the existing `ConsoleEmailSender` pattern
- Wired only for a small set of high-signal events (friend request received, community invitation received, ownership transfer pending)

### S5.4 — Block feature [M]

- `PlayerBlock` collection: `blockerId`, `blockedId`, `createdAt`
- Blocking auto-unfriends, auto-removes from any shared private community
- Block is bidirectional in the UI: neither party sees the other in search, leaderboards, or member lists
- Unblock reverses the list view but does NOT restore friendships or memberships

### S5.5 — Audit log surfacing [S]

- New page `/settings/activity` shows the current user's own audit log (filtered to their own actions + actions on them)
- Read-only; server-paged

### S5.6 — Smoke test §20 [S]

---

## Cross-sprint engineering concerns

### Testing strategy

- **Unit tests** (xUnit, `NinetyNine.Services.Tests` + `NinetyNine.Repository.Tests`): every invariant from the backend plan and every authz rule from the security matrix. Every service method has at least one "happy path" and one "forbidden" test.
- **Integration tests** (Testcontainers Mongo, existing pattern): repository query correctness for the 16 data access patterns.
- **bUnit component tests** (`NinetyNine.Web.Tests`): the `AudiencePicker`, `FriendshipStateChip`, `CommunityCard`, and the `/friends` tab switcher.
- **Smoke tests**: sections §16–§20 added to `docs/smoke-test-checklist.md` as each sprint lands.

### Documentation updates per sprint

- `CLAUDE.md` — brief section per sprint, pointing at the service interfaces and the audience matrix
- `docs/architecture.md` — updated diagrams showing the 4 new collections
- `docs/plans/friends-communities-v1.md` — **this document is the source of truth**; every user-directed change to fork selections or sprint scope must be mirrored here
- `docs/defects.md` — any new defects found during smoke testing

### Feature flags

Not strictly required at this scale, but a single `Features:FriendsAndCommunities:Enabled` bool in `appsettings.json` guards all new nav items, pages, and services. Off in production on day 1, turned on after Sprint 3 smoke passes. Sprint 4 and 5 land behind the same flag.

### Rollback plan

Every sprint's DB changes are additive (new collections, new fields). No destructive migrations. If Sprint 3 goes wrong, the flag can be flipped off and the Audience enum reverts to dead-code while the `bool` properties (still present in the model) continue to serve reads.

---

## Changelog

| Date | Change | By |
|---|---|---|
| 2026-04-11 | Initial plan accepted; Sprint 0 started. Fork selections A–E locked; all open questions answered per "most usable yet privacy-centric" north star. | Carey + Claude synthesis of 5 specialist sub-plans |
| 2026-04-11 | S0.1 deviation: legacy `ProfileVisibility.{EmailAddress,PhoneNumber,RealName,Avatar}` bool properties are NOT marked `[Obsolete]` despite the plan's acceptance criteria calling for it. `TreatWarningsAsErrors=true` in every csproj would turn ~20 obsolete-usage warnings into build errors at call sites that don't migrate until Sprint 3. XML doc comments now carry the "legacy — use `*Audience`" signal instead. Functionally equivalent; no scope change. | Claude (during S0.1 implementation) |
