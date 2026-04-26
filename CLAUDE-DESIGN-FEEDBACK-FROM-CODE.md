# Claude Design — feedback handoff from Claude Code

This file is the in-repo mirror of the milestone-tag annotations Claude Code has been writing to the project. Claude Design can read this file from the repo (commit & tag annotations on GitHub aren't reachable from the design-side tooling).

**How to use this file:**
- Read top-down. Each milestone section summarizes what shipped in that tag range and lists the open design questions.
- Reply by editing this file directly (add a `## Design feedback for milestone/vX.Y` section near the end with notes for Claude Code), or by tagging a sibling `CLAUDE-DESIGN-FEEDBACK-FROM-DESIGN.md`. Claude Code reads this file at the start of each session.
- When a question is resolved, mark it `~~Resolved:~~` inline rather than deleting it — keeps the audit trail.

---

## Milestone v0.5 — Concurrent multi-player matches + UX cleanup

### What shipped (v0.4.4 → v0.5.9)

| Tag | Scope |
| ------ | ---------------------------------------------------- |
| v0.4.4 | Stats-strip illumination during Efren-mode play |
| v0.5.0 | `Match.MatchRotation` + `CurrentPlayerSeat` (model) |
| v0.5.1 | `MatchService.CreateConcurrentMatchAsync` + `FinishInningAsync` + winner arbiter |
| v0.5.2 | New-match form: 2–4 players, per-player Efren |
| v0.5.3 | `ConcurrentMatchPlay` shell (active section + Up Next stack) |
| v0.5.4 | Polish: `Detail.razor` branches on Rotation; e2e test |
| v0.5.5 | Unify match form on alternating-innings flow — drop head-to-head/group selector |
| v0.5.6 | Fix NRE on `/matches/new` (InteractiveServer) |
| v0.5.7 | Fix `ScoreCardGrid` squash (40rem → 72rem container) |
| v0.5.8 | Home + NavMenu "Start new game" CTAs retargeted to `/matches/new` |
| v0.5.9 | WIP pills in NavMenu for under-construction features |

### The thesis

Pre-v0.5: matches were head-to-head only (Sequential rotation, with race-to/best-of formats). Group play wasn't a thing.

The user observed: a 2-player head-to-head match IS just a 2-player alternating-innings match. There's no fundamental difference worth two workflows. So the v0.5.x track:

1. Built the multi-player flow (alternating innings, per-player Game documents, seat rotation, group win condition)
2. Recognized head-to-head as a strict subset
3. Collapsed everything onto the unified flow (v0.5.5)
4. Retargeted entry points to the unified flow (v0.5.8)

The Sequential code paths still exist in the model/service layer but no UI surface creates them anymore.

### Design questions (still open unless marked resolved)

1. **`/matches/{id}/play` — Concurrent flow (`ConcurrentMatchPlay.razor`)**
   - Active section: `TurnCalloutCard` + full `ScoreCardGrid` Edit mode for the seat whose turn it is.
   - Up Next stack below: compact card per waiting seat (seat #, name, total score, current frame).
   - Cards on Efren-mode players carry a gold inset ring (same vocabulary as `FrameCell.frame-cell-efren`).
   - Match-complete leaderboard: winner row gold-ringed.
   - **Question:** Up Next cards in a 4-player match take meaningful vertical space below the scorecard. Horizontal strip instead? Smaller? Different placement?

2. **`/matches/new` — unified roster form**
   - Roster fieldset with seat 1 = "You" (Efren toggle), seats 2–4 = configurable opponent rows with player picker + per-row Efren toggle.
   - Add/Remove buttons grow the roster between 2 and 4 total.
   - Efren toggle uses `:has(input:checked)` for the gold-ring "on" state — same gold vocabulary as `ScoreCardGrid` + `FrameCell`.
   - **Question:** At 4 players, is the roster row layout still legible on mobile? Currently wraps the player select to a new line at ≤480px. Is that the right breakpoint?

3. **Stats-strip illumination (v0.4.4)**
   - `.sc-summary` stats-pill row glows gold when the active player's Game is the Efren variant.
   - Pairs with the per-tile gold ring on `FrameCell` tiles.
   - **Question:** The Total tile's threshold-teal glow (≥90 score) stacks on top of the strip's gold ring. Both signals coexist by design — the strip's gold border sits outside the per-tile teal box-shadow. Does this read cleanly when both fire?

4. **WIP pills in NavMenu (v0.5.9)**
   - 9px uppercase amber text on transparent-gold background, gold 1px border, 4px corner radius.
   - Marks: Friends, History, Stats, Venues, Communities, Notifications, Profile, **Browse players** (added in v0.8.2).
   - No pill: Home, New Game, About.
   - Sits via `margin-left:auto`; on Notifications, sits before the existing teal numeric badge with a 2-unit gap.
   - **Question:** Chose text "WIP" because the curated phosphor set has no wrench/cone/construction icon. Is "WIP" the right typographic call, or should I source an icon? "BETA" was considered and rejected as overloaded (implies feature-complete pre-release, which doesn't match "may break").

### Meta / open structural questions

- **A.** The Sequential code paths (`MatchService.CreateMatchAsync`, `MatchFormat` enum with `RaceTo`/`BestOf`, race-to/best-of UI in `Detail.razor`) are dead from the form side but live in the service/model. Should we delete them entirely, or preserve them for a future "tournament mode" that re-introduces multi-game matches?
- **B.** The new-match form has a "Start a solo game instead" escape link to `/games/new` (the legacy single-player form). With the unified flow as canonical, is that link still useful? Or should solo play be reachable only by lowering the match-flow minimum from 2 to 1 player and deleting `/games/new` entirely?
- **C.** `ConcurrentMatchPlay`'s Up Next cards show seat number / name / score / frame. They DON'T show: Efren mode (only by gold ring), per-player table size (always shared in this flow), the active player's photo or initials avatar. Worth adding any of those?

### Tripwires captured in memory

- Blazor `[SupplyParameterFromForm]` nulls the property on initial GET when the model has nested `List<T>` — switch to `@rendermode InteractiveServer` + plain field for those forms. See `~/.claude/projects/.../memory/blazor_form_patterns.md`
- .NET BCL has `System.IO.MatchType` in implicit usings — avoid `MatchType` as a domain enum name. We renamed to `MatchRotation`.

---

## Milestone v0.6 — Mock dataset + JSON snapshot for cross-agent reading

### What shipped (v0.6.0 → v0.6.3)

| Tag | Scope |
| --- | --- |
| v0.6.0 | 26 amateur + 7 pro players (incl. Fedor Gorst); 5 themed communities; `Player.FargoRating` on the model. |
| v0.6.1 | Game-history generator with per-Fargo-bracket score distribution (vetted by the `poolplayer` agent / "George"). Efren-mode penalty applied via stochastic rounding to preserve expected value. 17 statistical sanity tests. |
| v0.6.2 | 20 concurrent matches between similarly-rated players; pro matches are Efren-only; winners arbitrated identically to the live `MatchService`. |
| v0.6.3 | JSON snapshot under `src/NinetyNine.Services/SeedData/` — four files (players, communities, games, matches), each with an embedded description explaining its shape. Stable FNV-1a per-player RNG seed makes the snapshot byte-reproducible across environments. Regen via the env-gated test `MockDataSnapshotRegen`. |

### Quick entry points for Claude Design

| File | What's in it |
| --- | --- |
| `src/NinetyNine.Services/SeedData/mock-players.json` | 33 players. `amateurs[]` + `pros[]`. Each row has Fargo, bracket label, EfrenOnly flag. |
| `src/NinetyNine.Services/SeedData/mock-communities.json` | 5 communities. `memberDisplayNames[0]` is the owner. v0.8.0 added `parentCommunityName` ("Global" for all). |
| `src/NinetyNine.Services/SeedData/mock-games.json` | 243 games. Per-frame scores realistic for the player's Fargo. |
| `src/NinetyNine.Services/SeedData/mock-matches.json` | 20 concurrent matches. `playerFrameScores`: `int[9]` per seat. Winner arbitrated by highest TotalScore → PerfectFrames → earliest CompletedAt. |
| `src/NinetyNine.Services/SeedData/mock-*.schema.json` | (v0.7.1) JSON Schema draft 2020-12 for each data file. |

### Per-bracket score reference (for sanity-checking generated data)

| Bracket            | Fargo      | Std mean   | Efren mean   | Efren drop |
|--------------------|------------|------------|--------------|------------|
| Rec/Beginner       | 270–349    | ~12        | ~12          | ~3% |
| Developing C       | 350–449    | ~22        | ~21          | ~5% |
| Mid Amateur B      | 450–549    | ~38        | ~35          | ~8% |
| Strong B+          | 550–619    | ~52        | ~47          | ~10% |
| Advanced A-        | 620–699    | ~62        | ~56          | ~10% |
| Strong A+          | 700–749    | ~71        | ~63          | ~11% |
| Elite Amateur      | 750–799    | ~79        | ~70          | ~12% |
| Touring Pro        | 800–850    | ~87        | ~78          | ~13% |

All pros generate Efren-only games. Standard-mode Reyes/SVB/Filler data does NOT exist in this snapshot — by design.

### Design questions

(no v0.6 questions raised yet — feedback welcome on the Fargo-bracket presentation, the Efren styling on player rows, or how to surface skill bracket on the Browse Players page)

---

## v0.7 — Production-vs-development seeding + JSON Schema split + project rule

### What shipped

| Tag | Scope |
| --- | --- |
| v0.7.0 | `Seed:Mode` config (`Production`/`Development`); `deploy.sh --production` flag; mock data is now strictly dev-only. |
| v0.7.1 | Split `mock-*.json` data files from `mock-*.schema.json` files (JSON Schema draft 2020-12). Data files are pure data; schema info lives in the sibling. |
| v0.7.2 | Process: data model + JSON Schema = ground truth for mock data. `MockDataSchemaValidationTests` enforces it (drift fails CI). New "Development discipline" section in `CLAUDE.md`. |

### Why this matters for design

- Mock data has clear visual / structural promises baked into the schema files (e.g., bracket enum, frame score range). When evaluating a render or layout, you can trust the schema's enums and ranges.
- The **two appsettings + `Seed:Mode`** split means `./deploy.sh up` always shows the full mock dataset (the design-rich state). To see "what does this look like fresh, real-venues-only?" use `./deploy.sh up --production`.

---

## Milestone v0.8 — Hierarchical communities + Browse players

### What shipped (v0.8.0 → v0.8.2)

| Tag | Scope |
| --- | --- |
| v0.8.0 | `Community.ParentCommunityId` (nullable Guid; null = root); `EnsureGlobalCommunityAsync` seeds a "Global" root owned by carey; existing communities reparented under Global; `ICommunityService.GetTreeAsync()` + `SetParentAsync()` (with cycle detection); JSON Schema + snapshot updated. |
| v0.8.1 | Communities tree view at the top of `/communities`. Recursive `CommunityTreeNode.razor` shared component renders indented children with a teal-tinted dashed guide line. |
| v0.8.2 | `/players/browse` page (WIP). Lists every non-retired player with display name + Fargo pill; client-side filter input. Home + NavMenu links wired up. |

### What Claude Design should evaluate

1. **`/communities` — Hierarchy tree view (v0.8.1)**
   - New "Hierarchy" section sits above the existing "My communities" + "Browse public" sections.
   - Each node: link to detail page + child-count teal pill (only when > 0).
   - Children indented 1 level under a 1px dashed teal-tinted vertical guide.
   - Currently the seed has one level (Global → 5 themed communities + Pocket Sports). The structure supports arbitrary depth but isn't exercised yet.
   - **Questions:**
     - Is the dashed teal guide the right visual? (Considered: solid line, no line, indented background.)
     - Should the tree have expand/collapse affordances? (Currently always-expanded — fine for ~10 communities; would need toggling at ~50.)
     - Should Global itself appear as a node, or should the tree show only its children (Global is implicit)? Currently Global appears as a root with 6 children.

2. **`/players/browse` — Browse players (WIP, v0.8.2)**
   - Single-column list, one row per player, sorted by display name.
   - Filter input does in-memory `Contains` (case-insensitive) on display name.
   - Each row shows display name + Fargo rating pill (only when set; mocked-3 Carey/George/CareyB carry placeholder ratings).
   - Click → existing `/players/{id}` profile page.
   - **WIP-tagged in the page header AND the nav** — minimal first cut, intentionally rough.
   - **Questions:**
     - Worth showing avatars? (The `CommunityCard` and `PlayerBadge` components already exist; could reuse `InitialsAvatar` for a leading column.)
     - Sort: alpha-by-display-name only? Add Fargo desc / recent-active sort?
     - Should a Fargo bracket dropdown filter join the search input?
     - Pagination at what threshold? 33 players currently fits one screen; 200 wouldn't.

3. **Home page CTAs (v0.5.8 + v0.8.2)**
   - Hero row: "Start new game" + "View history".
   - "Jump back in" quick-grid: "Start new game", "View history", "Stats", "Browse players" (new).
   - **Question:** Is the four-card quick grid balanced? Should "Communities" join the grid (currently only reachable via NavMenu)?

### Open structural questions (v0.8)

- **D.** Cycle detection in `SetParentAsync` rejects cycles but doesn't currently expose a "you can't move community X under community Y because…" diagnostic. The error code is `CycleDetected`; the message is a single sentence. Worth more detail (e.g., showing the proposed ancestor chain)?
- **E.** Pocket Sports is now a child of Global, alongside the 5 themed mock communities. Visually it's just another sibling. Should the canonical seeded community get any special treatment (icon, sort order, "official" badge), or is it just data?

---

## How Claude Design replies

Add your feedback in any of these forms — Claude Code reads this file at session start:

1. **Inline edits:** strike through resolved items with `~~`, add resolution notes after.
2. **A new section** at the bottom: `## Design feedback round N — vX.Y` with bullet points keyed to the question identifiers (Q1, Q2, A, B, C, D, E…).
3. **A sibling file** `CLAUDE-DESIGN-FEEDBACK-FROM-DESIGN.md` if you want a separate document. Either works.

---

*This file is generated/maintained by Claude Code. The canonical source is the milestone-tag annotations on origin (`milestone/v0.5`, `milestone/v0.6`, etc.) and the per-tag commit messages. Regenerate by reading those tags + commits and updating this file when new milestones ship.*
