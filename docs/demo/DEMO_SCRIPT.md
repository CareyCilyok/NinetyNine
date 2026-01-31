# NinetyNine Demo Script

A Carey-facing narrative for demonstrating the NinetyNine scorekeeper.

---

## What's Improved

This release includes significant quality and polish improvements:

- **Dark Neon Theme**: Professional gaming aesthetic with electric blue, gold, and green accents
- **Celebration Effects**: Perfect frames (11 points) get gold highlighting; perfect games (99 points) trigger confetti
- **SQLite-Backed Tests**: Repository tests now use a real relational database for accurate behavior
- **One-Command Verification**: `.\scripts\verify.ps1` runs clean build + all 100 tests
- **One-Command Release**: `.\scripts\publish.ps1` produces a ready-to-ship zip

---

## What's Guaranteed

| Check | Status |
|-------|--------|
| `dotnet test -c Release` | 100 tests pass (Model, Repository, Services, Presentation) |
| `.\scripts\verify.ps1` | Clean build + tests in one command |
| `.\scripts\publish.ps1` | Produces `dist/NinetyNine_win-x64.zip` |
| App launches | No crash, dark theme renders correctly |
| UI matches rules | 9 frames, max 11/frame, 9-ball = 2 points |

---

## What's Next

Potential future enhancements (optional, not committed):

- **Cloud Sync**: Save games to a remote database for cross-device access
- **Leaderboards**: Track high scores and compare with other players
- **Mobile Support**: Avalonia can target iOS/Android with UI adjustments
