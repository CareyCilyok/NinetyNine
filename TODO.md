# NinetyNine Release Checklist

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
