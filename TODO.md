# NinetyNine Game Experience - Complete TODO

**Goal**: Deliver a polished, end-to-end game experience that delights users from first launch to their 100th game.

---

## Phase 1: Core Game Loop Fixes (Critical Path)

### 1.1 Player Identity Persistence
- [ ] Create default player on first launch (save to `%APPDATA%\NinetyNine\Profiles/`)
- [ ] Load existing player profile on app startup
- [ ] Wire GamePageViewModel to use persisted player (not create new each game)
- [ ] Ensure PlayerId consistency across sessions for stats tracking

**Files**: `GamePageViewModel.cs`, `ProfileService.cs` (new), `App.axaml.cs`

### 1.2 Table Size ComboBox Binding
- [ ] Bind ComboBox SelectedItem to `SelectedTableSize` property
- [ ] Populate ComboBox from `TableSize` enum values
- [ ] Display friendly names ("7-Foot", "9-Foot", "10-Foot")

**Files**: `GamePage.axaml`, `GamePageViewModel.cs`

### 1.3 Resume In-Progress Games
- [ ] On app launch, check for existing in-progress game
- [ ] Show "Resume Game" option alongside "New Game" if one exists
- [ ] Load and display in-progress game state correctly
- [ ] Handle edge case: multiple in-progress games (show most recent)

**Files**: `GamePageViewModel.cs`, `GameService.cs`, `GamePage.axaml`

### 1.4 Venue Persistence
- [ ] Create VenueService to manage venues
- [ ] Save venues to `%APPDATA%\NinetyNine\Venues/`
- [ ] Populate venue dropdown/autocomplete from saved venues
- [ ] Allow creating new venues inline
- [ ] Track favorite venues based on usage

**Files**: `VenueService.cs` (new), `GamePage.axaml`, `GamePageViewModel.cs`

---

## Phase 2: Scoring Experience Polish

### 2.1 Visual Feedback During Scoring
- [ ] Highlight active frame with pulsing border animation
- [ ] Add subtle scale animation on +/- button press
- [ ] Show score preview as user adjusts (before completing)
- [ ] Color-code frame based on score quality (red < 5, yellow 5-8, green 9-10, gold 11)

**Files**: `FrameControl.axaml`, `FrameControlViewModel.cs`, `Themes/`

### 2.2 Running Total Display
- [ ] Add prominent running total display during gameplay
- [ ] Show "projected final score" based on average frame performance
- [ ] Display "pace" indicator (above/below average)

**Files**: `GamePage.axaml`, `GamePageViewModel.cs`

### 2.3 Frame Completion Feedback
- [ ] Add satisfying animation when frame completes
- [ ] Show frame score popup briefly before locking
- [ ] Play subtle sound effect (optional, user preference)
- [ ] Perfect frame (11) gets special celebration animation

**Files**: `FrameControl.axaml`, `CelebrationService.cs`, `SoundService.cs` (new)

### 2.4 Undo Last Frame
- [ ] Add "Undo" button to revert last completed frame
- [ ] Only allow undo of most recent frame (not arbitrary)
- [ ] Confirm undo action to prevent accidents
- [ ] Disable undo after game completion

**Files**: `GamePageViewModel.cs`, `GameService.cs`, `Game.cs`, `GamePage.axaml`

---

## Phase 3: Game Completion Experience

### 3.1 Enhanced Completion Screen
- [ ] Show game summary: all 9 frames with scores
- [ ] Display performance breakdown (best frame, break success rate)
- [ ] Compare to personal best/average
- [ ] Show achievement unlocks earned this game
- [ ] Add share functionality (copy stats to clipboard)

**Files**: `GamePage.axaml`, `GamePageViewModel.cs`, `GameSummaryControl.axaml` (new)

### 3.2 Perfect Game Celebration
- [ ] Full-screen confetti effect for 99-point game
- [ ] Special "LEGENDARY" badge display
- [ ] Record in achievements with timestamp
- [ ] Optional screenshot save

**Files**: `ConfettiControl.axaml`, `CelebrationService.cs`, `AchievementService.cs` (new)

### 3.3 Game History Browser
- [ ] Add "History" section to view past games
- [ ] Filter by date range, score range, venue
- [ ] Show detailed breakdown for any past game
- [ ] Delete old games option (with confirmation)

**Files**: `GameHistoryPage.axaml` (new), `GameHistoryViewModel.cs` (new)

---

## Phase 4: Statistics Integration

### 4.1 Wire Stats to Real Player
- [ ] Pass actual PlayerId to StatisticsPageViewModel
- [ ] Remove demo/mock data fallback (or make it opt-in for demo mode)
- [ ] Ensure stats refresh when navigating to page (new games appear)

**Files**: `StatisticsPageViewModel.cs`, `MainView.axaml.cs`

### 4.2 Live Stats During Gameplay
- [ ] Show mini-stats panel during game (current avg, best today)
- [ ] Highlight when player is on pace for personal best
- [ ] Show frame-by-frame comparison to average

**Files**: `GamePage.axaml`, `GamePageViewModel.cs`

### 4.3 Charts and Visualizations
- [ ] Score trend line chart (last 30 games)
- [ ] Frame-by-frame performance heat map
- [ ] Break success rate over time
- [ ] Score distribution histogram

**Files**: `StatisticsPage.axaml`, `ChartControl.axaml` (new or use LiveCharts2)

---

## Phase 5: Profile Enhancement

### 5.1 Profile-Game Linkage
- [ ] Automatically link new games to current player profile
- [ ] Show profile stats that match game history
- [ ] Sync achievements between profile and game completion

**Files**: `ProfilePageViewModel.cs`, `GameService.cs`

### 5.2 Achievement System
- [ ] Define achievement criteria in dedicated service
- [ ] Check for new achievements after each game
- [ ] Show unlock notification with animation
- [ ] Track unlock date and game context

**Achievements to implement**:
- First Game
- First Perfect Frame (11 points)
- First 80+ Game
- First 90+ Game
- Perfect Game (99 points)
- 10 Games Played
- 50 Games Played
- 100 Games Played
- Break Master (70%+ break success over 10 games)
- Consistency King (std dev < 5 over 10 games)
- Venue Regular (10 games at same venue)
- Hot Streak (3 games above personal average in a row)

**Files**: `AchievementService.cs` (new), `AchievementDefinitions.cs` (new)

### 5.3 Multiple Player Profiles
- [ ] Allow creating additional player profiles
- [ ] Switch between profiles from settings
- [ ] Each profile has independent stats/achievements
- [ ] Guest mode (no stats saved)

**Files**: `ProfileService.cs`, `ProfileSelectorDialog.axaml` (new)

---

## Phase 6: Venue Management

### 6.1 Venue Page Functionality
- [ ] List all saved venues with stats
- [ ] Add/edit/delete venues
- [ ] Track games played at each venue
- [ ] Show player's best score at each venue
- [ ] Favorite venues feature

**Files**: `VenuePage.axaml`, `VenuePageViewModel.cs`, `VenueService.cs`

### 6.2 Venue Selection During Game Start
- [ ] Autocomplete venue name input
- [ ] "Recent Venues" quick-select
- [ ] "Add New Venue" inline option
- [ ] Remember last-used venue as default

**Files**: `GamePage.axaml`, `GamePageViewModel.cs`

---

## Phase 7: User Experience Polish

### 7.1 Keyboard Navigation
- [ ] Tab through frame controls
- [ ] Arrow keys for +/- in focused frame
- [ ] Enter to complete frame
- [ ] Escape to cancel/reset
- [ ] Ctrl+N for new game
- [ ] Ctrl+Z for undo

**Files**: `GamePage.axaml.cs`, `FrameControl.axaml.cs`

### 7.2 Accessibility
- [ ] Screen reader support for all controls
- [ ] High contrast mode support
- [ ] Minimum touch target sizes (44x44px)
- [ ] Focus indicators visible

**Files**: Various AXAML files

### 7.3 Error Handling & Feedback
- [ ] Toast notifications for save success/failure
- [ ] Graceful handling of corrupted game files
- [ ] Auto-recovery from crashes (restore in-progress game)
- [ ] Helpful error messages (not stack traces)

**Files**: `NotificationService.cs` (new), `GameService.cs`

### 7.4 Loading States
- [ ] Show loading spinner while stats calculate
- [ ] Skeleton UI while pages load
- [ ] Progress indicator for long operations

**Files**: `LoadingControl.axaml` (new), various pages

---

## Phase 8: Settings & Preferences

### 8.1 User Preferences
- [ ] Sound effects on/off
- [ ] Celebration animations on/off
- [ ] Default table size
- [ ] Default venue
- [ ] Theme selection (dark/light/custom)
- [ ] Auto-save interval

**Files**: `SettingsPage.axaml` (new), `SettingsService.cs` (new)

### 8.2 Data Management
- [ ] Export all data (JSON/CSV)
- [ ] Import data from backup
- [ ] Clear all data (with confirmation)
- [ ] Data storage location display

**Files**: `SettingsPage.axaml`, `DataExportService.cs` (new)

---

## Phase 9: Final Polish

### 9.1 App Icon & Branding
- [ ] High-res app icon (all sizes)
- [ ] Splash screen on launch
- [ ] About dialog with version info
- [ ] Credits/acknowledgments

### 9.2 Performance
- [ ] Profile app startup time
- [ ] Optimize stats calculations
- [ ] Lazy load pages
- [ ] Memory usage audit

### 9.3 Testing
- [ ] End-to-end test: full game flow
- [ ] Edge case tests: 0-point game, 99-point game
- [ ] Profile persistence tests
- [ ] Stats calculation accuracy tests

---

## Priority Order for Implementation

**Must Have (MVP)**:
1. Player Identity Persistence (1.1)
2. Table Size Binding (1.2)
3. Resume In-Progress Games (1.3)
4. Wire Stats to Real Player (4.1)
5. Profile-Game Linkage (5.1)

**Should Have (v1.0)**:
6. Venue Persistence (1.4)
7. Visual Feedback During Scoring (2.1)
8. Enhanced Completion Screen (3.1)
9. Achievement System (5.2)
10. Undo Last Frame (2.4)

**Nice to Have (v1.1+)**:
11. Game History Browser (3.3)
12. Charts and Visualizations (4.3)
13. Keyboard Navigation (7.1)
14. Settings & Preferences (8.x)
15. Multiple Player Profiles (5.3)

---

## Quick Wins (< 30 min each)

- [ ] Fix TableSize ComboBox binding
- [ ] Add running total display to GamePage
- [ ] Color-code frames by score
- [ ] Add pulsing animation to active frame
- [ ] Show "Personal Best!" when achieved
- [ ] Add keyboard shortcut hints to buttons

---

## Pre-Release Verification

- [ ] `dotnet test .\NinetyNine.sln -c Release` => 0 failed
- [ ] App launches (smoke test)
- [ ] `.\scripts\verify.ps1` passes
- [ ] `.\scripts\publish.ps1` outputs `dist/NinetyNine_win-x64.zip`
- [ ] README contains: run / test / publish commands

## Demo Checklist

- [ ] Start new game
- [ ] Run 1-2 frames with scoring
- [ ] Confirm Break Bonus (0 or 1) works correctly
- [ ] Confirm Ball Count (0-10, 9-ball = 2 points) calculates correctly
- [ ] Confirm Frame Score (max 11) displays correctly
- [ ] Confirm running total updates after each frame
- [ ] Complete full 9-frame game
- [ ] Verify game saves to disk
- [ ] Check Statistics page shows completed game
- [ ] Check Profile page reflects new stats

---

*Last Updated: 2026-01-31*
*Branch: claude/code-review-completion-ZEUTT*
