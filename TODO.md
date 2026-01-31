# NinetyNine TODO

## Definitions

- **Resume in-progress**: Restore active frame number + completed frame values + running total
- **StorageService**: Abstraction for local persistence (default `%APPDATA%\NinetyNine\` on Windows)
- **Frame**: One rack of 9-ball; scored by Break Bonus (0/1) + Ball Count (0–10)
- **Perfect frame**: 11 points (1 break bonus + 10 balls including 9-ball as 2)
- **Perfect game**: 99 points (9 perfect frames)

## Rules Alignment (see README.md)

- 9 frames per game
- Max 11 points per frame (Break Bonus 0–1, Ball Count 0–10)
- Max 99 points per game
- 9-ball = 2 Ball Count points
- Fouls end the frame (no points for balls pocketed on foul)
- Break scratch: balls count, but no Break Bonus

## Constraints

- **No model changes** unless required by a failing test or user-visible feature.
- **Ship linear**: See `ROADMAP_v1.md` for milestones A→D.

---

## Next Up (v1.0)

Tracks `ROADMAP_v1.md` milestones. Complete in order.

### Milestone A — Play + Save ✅
- [x] Default player profile created/loaded on launch
- [x] New games use persisted player ID
- [x] Completed games persist via StorageService
- [x] Resume most recent in-progress game

### Milestone B — Stats Are Real ✅
- [x] Statistics pulls from persisted games
- [x] Profile reflects real totals

### Milestone C — Quality of Life ✅
- [x] TableSize ComboBox enum-bound
- [x] Undo last frame
- [x] Venue string (autocomplete deferred)
- [x] Prominent running total display

### Milestone D — Release Polish ✅
- [x] App icon + About section
- [x] Settings (celebrations enabled by default)
- [x] Export/backup (single JSON)

---

## Done Gates

### Manual Playthrough
- [ ] Start new game
- [ ] Complete 2 frames, confirm running total correct
- [ ] Close app, reopen, resume game
- [ ] Complete remaining frames, end game
- [ ] Navigate to Statistics, verify game appears
- [ ] Navigate to Profile, verify totals updated

### Build Verification
- [ ] `.\scripts\verify.ps1` passes (clean build + 100 tests)
- [ ] `.\scripts\publish.ps1` produces `dist\NinetyNine_win-x64.zip`
- [ ] Zip runs on clean Windows machine (no .NET SDK installed)

---

## Quick Wins (< 30 min each)

- [ ] TableSize ComboBox binding (currently hardcoded)
- [ ] Prominent running total display in GamePage
- [ ] Remove placeholder/demo panels if any remain
- [ ] Recent games list on Statistics page

---

## Backlog (Post-v1.0)

Items below are deferred. Do not work on these until v1.0 ships.

### Visual Polish
- Pulsing border animation on active frame
- Scale animation on +/- button press
- Color-code frames by score quality
- Frame completion animation
- Perfect frame celebration animation

### Enhanced Completion Screen
- Game summary showing all 9 frames
- Performance breakdown (best frame, break success rate)
- Compare to personal best/average
- Share functionality (copy stats to clipboard)

### Game History Browser
- View past games with filters (date, score, venue)
- Detailed breakdown for any past game
- Delete old games

### Charts and Visualizations
- Score trend line chart
- Frame-by-frame performance heat map
- Break success rate over time
- Score distribution histogram

### Achievement System
- First Game, First Perfect Frame, First 80+ Game
- Perfect Game (99 points)
- 10/50/100 Games Played
- Break Master, Consistency King
- Unlock notifications

### Multiple Player Profiles
- Create additional profiles
- Switch between profiles
- Guest mode (no stats saved)

### Full Venue Management
- List all venues with stats
- Add/edit/delete venues
- Track games per venue
- Best score per venue

### Keyboard Navigation
- Tab through frames
- Arrow keys for +/-
- Enter to complete frame
- Ctrl+N for new game, Ctrl+Z for undo

### Accessibility
- Screen reader support
- High contrast mode
- Minimum touch targets (44x44px)

### Settings & Preferences
- Theme selection (dark/light)
- Default table size
- Default venue
- Auto-save interval

### Data Management
- Import data from backup
- Clear all data (with confirmation)
- Data storage location display

### Performance
- Profile app startup time
- Optimize stats calculations
- Lazy load pages

---

*Last Updated: 2026-01-31*
*See: ROADMAP_v1.md for milestone details*
