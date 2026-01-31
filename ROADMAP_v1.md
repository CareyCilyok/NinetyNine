# NinetyNine v1.0 Roadmap

## Scope Definition

**What v1.0 is**: A scorekeeper that records Break Bonus (0/1) + Ball Count (0–10) across 9 frames. Max 11 points per frame, max 99 per game. The 9-ball counts as 2 Ball Count points.

**What v1.0 is NOT**: Shot-by-shot simulation, tournament manager, or social platform.

See `README.md` for complete game rules.

---

## Milestone A — Play + Save ✅ COMPLETE

**Goal**: A single player can complete a game and have it persist.

- [x] Default player profile created on first launch (single profile, no multi-user)
- [x] Player profile loaded automatically on subsequent launches
- [x] New games use the persisted player ID
- [x] Completed games persist locally via **StorageService abstraction**
  - Default location: `%APPDATA%\NinetyNine\` on Windows
- [x] Resume most recent in-progress game on app launch
  - "Resume" = restore active frame number + completed frame scores + running total

**Done gate**: Start game → complete 2 frames → close app → reopen → resume game → finish → game saved.

**Commit**: `91cfee0` (2026-01-31)

---

## Milestone B — Stats Are Real ✅ COMPLETE

**Goal**: Statistics and Profile pages reflect actual persisted games.

- [x] Statistics page pulls from persisted games (not mock data)
- [x] Profile page reflects real totals: games played, average score, highest score
- [x] Stats refresh when navigating to page (new games appear immediately)

**Done gate**: Complete 3 games → navigate to Statistics → see all 3 games with correct averages.

**Commit**: `4489013` (2026-01-31)

---

## Milestone C — Quality of Life ✅ COMPLETE

**Goal**: Minor friction removed, basic conveniences added.

- [x] Table size ComboBox bound to `TableSize` enum (7-Foot, 9-Foot, 10-Foot)
- [x] Undo last frame (most recent completed frame only, not arbitrary)
- [x] Venue stored as string (autocomplete deferred to backlog)
- [x] Prominent running total display during gameplay (in footer)

**Done gate**: Select table size from dropdown → play 2 frames → undo frame 2 → re-enter → complete game.

**Commit**: `cdd8996` (2026-01-31)

---

## Milestone D — Release Polish ✅ COMPLETE

**Goal**: Ready for distribution to real users.

- [x] App icon (existing icon in Assets)
- [x] About section with version info (added to Profile page)
- [x] Settings: celebrations enabled by default (toggles deferred to backlog)
- [x] Export/backup: single JSON file containing all games and profile
- [x] `.\scripts\verify.ps1` passes
- [x] `.\scripts\publish.ps1` produces working `dist\NinetyNine_win-x64.zip`

**Done gate**: Fresh Windows machine → unzip → run → play full game → export data → verify JSON contains game.

**Commit**: `7a8a91e` (2026-01-31)

---

## Rules

- **No model changes** unless required by a failing test or user-visible feature.
- **Ship linear**: Complete A before starting B, B before C, etc.
- **Persist locally** via StorageService abstraction (implementation details are internal).

---

*Created: 2026-01-31*
