# NinetyNine - Quick Start

**What is it?** A scorekeeper for the pool game "Ninety-Nine" (99).

**Quick rules:** 9 frames, max 11 points/frame, max 99 total. Break bonus (0-1) + Ball count (0-10). The 9-ball = 2 points. See [README.md](README.md#rules) for full rules.

## Run from Zip (fastest)

1. Unzip `NinetyNine_win-x64.zip`
2. Run `NinetyNine.Application.exe`
3. If Windows SmartScreen appears: click **More info** → **Run anyway**

## Run from Source

```powershell
# Verify (clean build + tests)
.\scripts\verify.ps1

# Publish (creates dist/NinetyNine_win-x64.zip)
.\scripts\publish.ps1

# Or run directly
dotnet run --project .\App\Application.csproj
```
