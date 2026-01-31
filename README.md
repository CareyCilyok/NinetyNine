# NinetyNine

Scorekeeper for the pool game of Ninety-Nine - a 9-ball practice game where you track your performance across 9 frames.

## Features

- **9-Frame Scoring**: Track Break Bonus (0-1) and Ball Count (0-10) for each frame
- **Running Totals**: See your cumulative score as you play
- **Game Persistence**: Games auto-save after each frame; resume where you left off
- **Statistics**: View your game history, averages, and best scores
- **Player Profile**: Track your progress over time
- **Data Export**: Export all your data to JSON for backup

## Quick Start

```powershell
cd C:\Users\gpayn\NinetyNine\NinetyNine

# Run the app
dotnet run --project App

# Run in Release mode (faster)
dotnet run --project App -c Release
```

## Development

```powershell
# Run tests
dotnet test NinetyNine.sln

# Verify (clean build + all 101 tests)
.\scripts\verify.ps1

# Publish (creates dist/NinetyNine_win-x64.zip)
.\scripts\publish.ps1
```

## Project Structure

```
NinetyNine/
├── App/                    # Application entry point
├── Model/                  # Domain models (Game, Frame, Player, Venue)
├── Presentation/           # Avalonia UI
│   ├── Controls/           # Reusable UI controls (FrameControl)
│   ├── Pages/              # Main pages (Game, Statistics, Profile, Venue)
│   ├── Services/           # UI services (GameService, PlayerService, etc.)
│   ├── Themes/             # Dark theme with neon accents
│   └── ViewModels/         # MVVM ViewModels
├── Repository/             # Data persistence (Entity Framework)
├── Services/               # Business logic services
├── scripts/                # Build and publish scripts
├── Model.Tests/            # Unit tests for models
├── Presentation.Tests/     # Unit tests for ViewModels
├── Repository.Tests/       # Integration tests for data layer
└── Services.Tests/         # Unit tests for services
```

## How to Play

1. Click **New Game** to start
2. For each frame:
   - Use **+/-** buttons to set your **Break Bonus** (0 or 1) and **Ball Count** (0-10)
   - Click **Complete Frame** to record and advance
3. After 9 frames, see your final score out of 99

### Scoring Quick Reference

| Component | Points | Notes |
|-----------|--------|-------|
| Break Bonus | 0-1 | 1 point if you pocket a ball on the break |
| Ball Count | 0-10 | 1 point per ball; 9-ball counts as 2 |
| Frame Max | 11 | Break Bonus + Ball Count |
| Game Max | 99 | 9 frames × 11 points |

## Data Storage

Games and player data are stored in:
```
%APPDATA%\NinetyNine\
```

---

## Background

The game of Ninety-Nine (or simply '99') was created by [Pool & Billiard Magazine](http://poolmag.com).
The original rules can be found [here](http://poolmag.com/game-rules/). The modified rules for
this version are below.

## Rules

### Object of the Game
'99' is played with nine object balls numbered one through nine, and a cue ball.
For those familiar with 'playing the Ghost', '99' is like playing the 9-ball Ghost
but a partial score is awarded for breaking well and pocketing balls.

Play is divided into Nine Frames (racks) for each player, with a maximum Frame
Score of 11 (Eleven) Points, and thus a maximum Game Score of 99 Points.
On each shot after the break, the first ball contacted by the
cue ball must be the lowest numbered ball on the table.
When this requirement is met on a legal shot, any ball pocketed as a result
is scored and allows the player to continue their frame. A player's frame will
end when they fail to legally pocket a ball, commit a foul, or complete the
frame by legally pocketing the last ball.

### Racking the Balls
The object balls are racked in a normal 9-ball diamond shape. With the 1-ball on
the foot spot, the 9-ball in the center and the remaining balls racked randomly.

### Beginning Play
Players begin each frame with the cue ball "in hand" behind the head string. After the break, no matter what the outcome, the player begins with ball in hand anywhere on the table, and continues until the player fails to legally pocket a ball or pockets all the balls.

Balls pocketed on the break and all legally pocketed balls after the break count as one point each toward the player's "Ball Count" points (see Scoring).

Scratching on the break does NOT end the player's frame, and balls made on a "Scratch Break" are treated just as if the player did not scratch. They stay off the table and are counted in the player's "Ball Count" score for the frame. However, the Break Bonus is not awarded.

If the nine ball is made on a break in which the player scratches, it remains off the table and the player continues the frame. A nine ball made on any break is counted as 2 "Ball Count" points.

### Scoring

**Break Bonus**: If the player legally pockets one or more balls on the break, they are awarded 1 "Break Bonus" point.

**Ball Count**: The total number of object balls pocketed both during and legally after the break during a frame. A maximum of 10 "Ball Count" points are available in each frame. The 9-ball has a value of 2 "Ball Count" points and each of the other balls has a value of 1 "Ball Count" point.

**Fouls**: Balls pocketed on a scratch (except on the break), foul or other illegal shot DO NOT count toward a player's score and end the frame.

---

*Version 1.0.0*
