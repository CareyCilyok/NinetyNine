# Handoff: Play Screen Redesign (99 Pool)

## Overview

This handoff covers a redesign of the **live-scoring screen** (aka "Play") for the `NinetyNine` Blazor/Razor web app — the screen players use to enter their per-frame scores while playing a game of 99 Pool / Ninety-Nine Pool-Billiard.

The redesign replaces the existing numeric-input approach with a **paper-faithful translation of the official P&B (Pool & Billiard Rated Player International) scorecard**, supports **multi-player games with turn rotation**, and adds clear **"Finish inning / Finish frame"** affordances to make game flow unambiguous.

The design reference material, including the official rules PDF and photos of real filled-out scorecards, is included in `reference/`.

### Three artboards in the bundle

1. **3b. Play v2 · Paper-faithful (single player)** — the base redesign.
2. **3c. Play v2 · Ball picker open** — wireframe state showing the 3×3 ball-selector popover open.
3. **3d. Play v2 · Multi-player (active + up next)** — full multi-player layout with turn rotation and stat integration.

Open `NinetyNine Screens.html` in a browser to see all artboards on a pan/zoom design canvas. Focus any artboard (click its label) for a full-screen view.

---

## About the Design Files

The files in this bundle are **design references created in HTML/React (JSX, loaded via in-browser Babel)**. They are prototypes showing **intended look and behavior** — they are not production code to copy directly.

**Your task:** recreate these designs in the target codebase — `NinetyNine.Web` (Blazor Server / Razor components, located at `src/NinetyNine.Web/Components/`) — using its established patterns: `.razor` components, `.razor.css` for scoped styles, shared components in `Components/Shared/`, and any existing theme variables/utility classes.

Existing component to retrofit:
- `src/NinetyNine.Web/Components/Shared/ScoreCardGrid.razor` (+ `.razor.css`, + `ScoreCardMode.cs`)
- Tests: `tests/NinetyNine.Web.Tests/Components/ScoreCardGridTests.cs`

You should **replace or extend `ScoreCardGrid.razor`** rather than build a parallel component; reuse existing `ScoreCardMode` if it already distinguishes single- vs multi-player.

---

## Fidelity

**High-fidelity.** The prototypes include:
- Final colors (defined as CSS custom properties in `mockups/theme.css`)
- Final typography (system font stack, with a monospace for numerals)
- Final spacing, border radii, and shadows
- Exact interaction/animation timings
- State transitions (completed / active / pending frames; up-now vs up-next players)

Recreate these designs pixel-close using your existing Blazor theme. If `NinetyNine.Web` has its own design tokens, map the tokens listed at the bottom of this doc onto them.

---

## Rules of the Game (summary — see `reference/Play99_Rules_and_Scoresheets.pdf` for full text)

- A game of 99 is **9 innings (racks)** per player.
- **Ball Count**: 1 point per ball legally pocketed. Max 9 per frame.
- **Break Bonus**: 1 point if the player pockets at least one ball on the legal break of that frame. (The official PDF says "2 Break Bonus points," but the product decision for this redesign is **1 point**, confirmed by the team.)
- **Max per frame: 10 points** (1 break bonus + 9 balls).
- **Max total: 90 points** (9 frames × 10).
- In multi-player, players **alternate innings**: after player A completes frame N, player B plays frame N, then player A plays frame N+1, etc.

---

## Screens / Views

### Screen 1: Play v2 — Single Player (artboards 3b + 3c)

**Purpose:** A single player enters their score for the currently active frame using tap-based controls that mirror the paper scorecard. The player works through 9 frames left-to-right.

**Layout (1280 × 832 artboard):**

- Standard app shell (left nav, top header area — reuse existing `Shell` / layout).
- Within the main content area:
  1. **Location/time strip** (top) — flex row, left side shows location + table size + start time; right side shows an "In progress" status pill.
  2. **Turn callout card** — full-width card, padding `18px 22px`, subtle gold gradient background, gold-tinted border.
     - Left: "YOUR TURN" eyebrow (gold, uppercase, tracking 0.14em), "Frame N of 9" headline (22px, 700), helper text ("Tap **Break** if you pocketed on the break. Tap **Ball** to check off which balls you sank.")
     - Right: two PoolBall SVGs (the current frame's ball + the 9-ball, each 54px) and a **primary button**: `Finish frame N →`
  3. **Score card grid** — a 9-column CSS grid of `FrameCell` components, gap 8px.
  4. **Stat pill row** — 4 equal-width pills: Total (/90), Avg/Frame, Completed (/9), Current.
  5. **Footer strip** — footnote about max per frame, exit link.

### Screen 2: Play v2 — Multi-Player (artboard 3d)

**Purpose:** 2–4 players take alternating innings. The active player's scorecard is prominent with all stat pills; other players' scorecards are shown below in rotation order (who plays next → next-next → ...), more compactly.

**Layout (1280 × 1280 artboard):**

1. Location/time strip — same as single-player, but shows player count (e.g. "7 ft · 3 players") and inning number.
2. **Turn indicator card** — larger than the single-player callout. Left side: 44px gold avatar circle (first letter of name), "[Name]'s inning" eyebrow, "Frame N of 9" headline, subhead "Rack N ball at top · [NextName] is up next". Right side: two PoolBalls + **Finish inning →** button.
3. **Active player section:**
   - Player row label (30px gold avatar, name at 16px/700, "UP NOW" gold pill, running score on right at 22px/800).
   - 9-column grid of FrameCells.
   - **4 stat pills below** (Total / Avg / Completed / Current).
4. **"Up next" divider** — small uppercase label "UP NEXT" + hairline rule.
5. **Up-next player sections** (one per non-active player, in rotation order):
   - Compact row label (22px muted avatar, name at 13px/600, `#2` / `#3` monospace order badge, running score at 16px/700).
   - 9-column grid of FrameCells (gap 6px).
   - Section opacity 0.82 to de-emphasize.
6. Footer strip.

---

## Key Component: `FrameCell`

This is the heart of the redesign. Full implementation is in `mockups/FrameCellV2.jsx`.

### Visual anatomy (per cell)

```
┌─────────────────────┐
│       ①             │  ← frame-number badge, 22×22 circle,
│                     │    centered, fills gold when active
│                     │    / teal when completed / outlined when pending
│  ┌─────┬─────┐      │
│  │Break│Ball │      │  ← side-by-side controls, grid 1fr/1fr, gap 6
│  │  ☑  │  8  │      │    Break = checkbox toggle (1pt)
│  │     │     │      │    Ball = number, tap to open picker
│  └─────┴─────┘      │
│  ─────────────      │  ← hairline divider
│        9            │  ← running total, 30px/800, centered, no label
└─────────────────────┘
```

### Props

```ts
{
  n: number,                 // 1..9
  breakBonus: boolean,       // true = +1 break bonus awarded
  balls: Set<number>,        // which of balls 1-9 were pocketed this frame
  runningTot: number | null, // cumulative after this frame; null = pending & empty
  state: 'completed' | 'active' | 'pending',
  onChange: (patch) => void, // patch = { breakBonus?, balls? }
  isOpen: boolean,           // is the ball-picker popover open for this cell
  onOpen: () => void,
  onClose: () => void,
}
```

### States

| State | Border | Background | Frame badge | Opacity |
|---|---|---|---|---|
| `completed` | 1.5px solid teal | `--nn-bg-secondary` | filled teal | 1 |
| `active` | 1.5px solid gold + glow shadow | `--nn-accent-gold-muted` | filled gold | 1, `translateY(-2px)` |
| `pending` | 1.5px **dashed** default-gray | `--nn-bg-secondary` | outlined only | 0.6 |

Min-height: 146px. Padding: 8px 8px 10px. Border-radius: `--nn-radius-md`.

### Controls

**Break checkbox button** (left column):
- Label "BREAK" (9px/700, uppercase, 0.05em tracking, tertiary color)
- 20×20 rounded square (radius 5px). Unchecked = transparent with strong-border; checked = solid teal with a white check SVG.
- Toggling calls `onChange({ breakBonus: !breakBonus })`.

**Ball button** (right column):
- Label "BALL"
- Large number (18px/700) showing `balls.size`, or `—` when 0. Colored teal when > 0.
- Clicking toggles the ball-picker popover open/closed.

**Running total row** (bottom):
- Hairline top border (1px `--nn-border-subtle`).
- Number only (30px/800, monospace, letter-spacing -0.03em, centered, 40px min-height).
- Gold color if this cell hits `frameTotal === 10` (perfect frame). Tertiary color if `runningTot == null`. Otherwise primary text color.
- **No label** — previous "RUNNING" label was removed per design feedback.

---

## Ball Picker Popover

Appears below the active cell when its Ball button is clicked. Full implementation in `mockups/FrameCellV2.jsx` (`BallPicker` component).

### Layout

- Absolute-positioned, `top: 100%; left: 50%; transform: translate(-50%, 8px)`, z-index 20.
- Width 224px, padding 14px, radius `--nn-radius-lg`, shadow `--nn-shadow-lg`.
- Top-center caret (12×12 rotated square, matching background).

### Contents

1. **Header row:** "BALLS POCKETED" label (11px/700 uppercase) + "CLEAR" button (right).
2. **3×3 grid of ball buttons**, gap 8px:
   - Each cell is a square button (aspect-ratio 1, padding 6px).
   - Inside: a `PoolBall` SVG at 44px.
   - Unchecked: transparent background, subtle border, ball rendered dim (`grayscale(0.7) opacity(0.45)`).
   - Checked: teal-tinted background, teal border, full-color ball, plus a 14px teal checkmark badge in the top-right corner.
3. **Done button** (full-width primary): "Done · N ball" / "Done · N balls".

Closing the picker (outside click OR Done) does NOT lose selections — `balls` Set is the source of truth. The popover just collapses back to the cell, which shows the count.

### PoolBall SVG

Inline SVG per ball number 1–9. Standard P&B colors:

| # | Color | Hex | Striped |
|---|---|---|---|
| 1 | Yellow | `#e5c107` | solid |
| 2 | Blue | `#0a3a8c` | solid |
| 3 | Red | `#b8211b` | solid |
| 4 | Purple | `#4a1d72` | solid |
| 5 | Orange | `#d66616` | solid |
| 6 | Green | `#0b6b3a` | solid |
| 7 | Maroon | `#6b1f18` | solid |
| 8 | Black | `#0f0f0f` | solid |
| 9 | Yellow | `#e5c107` | **striped** (horizontal band on white) |

Rendered with radial gradient for 3-d shading (highlight upper-left, shadow lower-right), a ~8.5px white number disc in the center, monospace number. Reproducible from `PoolBall` in `mockups/FrameCellV2.jsx`.

---

## Interactions & Behavior

### Single-player flow

1. Start on frame 1 (active). All others pending.
2. Player taps **Break** (optional, if they broke and pocketed ≥1 ball).
3. Player taps **Ball** → picker opens → player checks off the balls they pocketed → taps **Done** (picker collapses, count shows in cell).
4. Player taps **Finish frame N →** in the top callout → frame N marked completed, frame N+1 becomes active.
5. Repeat through frame 9; after frame 9 completes, show the Complete screen (not included here, but exists as artboard 4).

### Multi-player flow

1. Start with player 1 active, frame 1.
2. Player 1 records their frame (Break + Ball picker).
3. Player 1 taps **Finish inning →** → their frame 1 marks completed; active player rotates to player 2 (still frame 1 for that player).
4. Continue through all players' frame 1, then players' frame 2, etc.
5. The layout reorders each turn: active player moves to top with full stats; previous active player drops to the bottom of the "Up next" list.

### Animations / transitions

- **Cell state changes:** `transform 140ms, box-shadow 140ms`. Active cell lifts `translateY(-2px)`.
- **Break / Ball buttons:** `all 120ms` on click for background + border changes.
- **Pool balls in picker:** `filter 120ms` when toggling dim/full-color.
- **Popover:** no explicit entry/exit animation in the mock; add a gentle fade/scale if your design system has one.

### Outside-click behavior

Clicking anywhere outside an open picker closes it. Implemented via a `document.addEventListener('click', ...)` hook deferred one tick so the opening click doesn't immediately close. Translate to Blazor with a similar approach (listen on document, or use an overlay div).

---

## State Management

The active player and per-player frame state need to persist on the server (Blazor Server) so refreshes don't lose the game. Current mockup state (all client-side):

```ts
type Frame = {
  n: number;
  breakBonus: boolean;
  balls: Set<number>;  // subset of {1..9}
  state: 'completed' | 'active' | 'pending';
}

type Player = {
  id: string;
  name: string;
  frames: Frame[];  // always length 9
}

type GameState = {
  players: Player[];   // ordered: rotation cycles through in this order
  activePlayerId: string;
  openCell: { playerId: string; frameN: number } | null;  // picker UI state
}
```

**Derived values (computed, not stored):**
- `frameTotal(f) = (f.breakBonus ? 1 : 0) + f.balls.size` (0..10)
- `runningTotalAfter(idx)` = sum of `frameTotal(frames[0..idx])` for cells that have data
- `score(player)` = last non-null running total
- `activeFrameN(player)` = `frames.find(state === 'active').n`
- `upNextOrder` = players in rotation starting after `activePlayerId`, excluding the active one

**Transitions:**
- `toggleBreak(playerId, frameN)` → flip `breakBonus`
- `toggleBall(playerId, frameN, ballN)` → add/remove from `balls` Set
- `finishInning()` → set active frame's state to `completed`, next frame's state to `active`, rotate `activePlayerId` to next player. Close open cell.

---

## Design Tokens

All tokens are defined in `mockups/theme.css`. The relevant ones for this feature:

### Colors

```css
/* Backgrounds */
--nn-bg-primary:    #0f1412
--nn-bg-secondary:  #1a211e
--nn-bg-tertiary:   #242c29

/* Text */
--nn-text-primary:   #f4f1ea
--nn-text-secondary: #b8b2a4
--nn-text-tertiary:  #7a7366
--nn-text-on-accent: #0a1512

/* Borders */
--nn-border-subtle:  #2a332f
--nn-border-default: #3a4440
--nn-border-strong:  #556059

/* Accents */
--nn-accent-teal:       #1fb892   (rgb 31,184,146)
--nn-accent-teal-hover: #26d0a6
--nn-accent-teal-muted: #0e5c48

--nn-accent-gold:       #e0b46c   (rgb 224,180,108)
--nn-accent-gold-hover: #edc88a
--nn-accent-gold-muted: #5a4520
```

### Ball colors

See the table in "PoolBall SVG" above. These are game assets, not theme tokens.

### Spacing

Standard 4px increments. Grid gaps: 8px (active player cells), 6px (compact cells).

### Radius

```css
--nn-radius-sm: 4px
--nn-radius-md: 8px
--nn-radius-lg: 12px
```

### Shadows

```css
--nn-shadow-sm:  0 1px 2px rgba(0,0,0,0.25)
--nn-shadow-md:  0 2px 8px rgba(0,0,0,0.35)
--nn-shadow-lg:  0 8px 24px rgba(0,0,0,0.45)
--nn-shadow-glow-gold:
  0 0 0 1px rgba(224,180,108,0.6),
  0 0 18px rgba(224,180,108,0.35)
```

### Typography

- **Body / UI:** system font stack (`system-ui, -apple-system, sans-serif`).
- **Numerals:** monospace (`ui-monospace, "SF Mono", Menlo, monospace`) — used for frame numbers, running totals, scores, order badges.
- **Weights used:** 600, 700, 800.
- **Letter spacing:** -0.02 to -0.03em on large numerals; +0.05–0.14em on uppercase labels/eyebrows.

---

## Assets

- **Pool balls (1–9):** rendered inline as SVG by the `PoolBall` component. No image files; re-implement as a Razor component or reusable SVG partial.
- **Icons (map-pin, arrow-right, trophy, check, target, chart-line, plus):** currently referenced by string name via an `Icon` component in the mockup Shell. Use your existing icon system (Lucide, Heroicons, or in-repo SVGs).
- **Reference material** in `reference/`:
  - `Play99_Rules_and_Scoresheets.pdf` — official P&B rules + printable scoresheet template.
  - `scoresheet-photo-1/2/3.jpeg` — photos of real filled-out scoresheets (layout inspiration).

---

## Files

Mockup source (what you'll reference, not ship):

```
NinetyNine Screens.html          — design canvas entrypoint; open this in a browser
mockups/
  theme.css                      — all CSS custom properties / tokens
  design-canvas.jsx              — canvas host (pan/zoom, artboards)
  Shell.jsx                      — app shell (left nav, header area, Icon, StatPill, etc.)
  Screens.jsx                    — all screen components; PlayScreenV2 & PlayScreenV2Multi live here (search for those names)
  FrameCellV2.jsx                — FrameCell + BallPicker + PoolBall (the core of the redesign)
reference/
  Play99_Rules_and_Scoresheets.pdf
  scoresheet-photo-{1,2,3}.jpeg
```

Target codebase files to modify (search these with Claude Code):

```
src/NinetyNine.Web/Components/Shared/ScoreCardGrid.razor
src/NinetyNine.Web/Components/Shared/ScoreCardGrid.razor.css
src/NinetyNine.Web/Components/Shared/ScoreCardMode.cs
tests/NinetyNine.Web.Tests/Components/ScoreCardGridTests.cs
tests/NinetyNine.Web.Tests/ScoreCardGridTests.cs
```

---

## Implementation Suggestions for Claude Code

When you start, consider this order:

1. **Extract design tokens** into your existing theme file (probably `wwwroot/css/...` or a `_variables.scss`). Map the `--nn-*` names onto your existing token names if different.
2. **Build `PoolBall` as a standalone Razor component** (`Components/Shared/PoolBall.razor`) first — it's reusable and has no dependencies.
3. **Rewrite `ScoreCardGrid.razor`** to be a pure renderer of a `IReadOnlyList<Frame>` model, driven by a `ScoreCardMode` enum that already differentiates modes. Add `Compact` and `WithStats` variants.
4. **Build `FrameCell.razor`** using the state table above. Keep the ball-picker popover as a child component (`BallPicker.razor`) and control its open/closed state from the cell.
5. **Build `PlayView.razor`** (the single-player screen) using `ScoreCardGrid` + the top callout + stat pills.
6. **Build `PlayMultiView.razor`** using the same `ScoreCardGrid` for both active and up-next sections, differentiated by a `Compact` prop.
7. **Add turn-rotation to the game service / state store** (whatever manages live game state server-side). `finishInning` flips the active player and advances the frame.
8. **Update tests** in `ScoreCardGridTests.cs` to cover the new states, the multi-player rendering, and the finish-inning flow.

Don't ship the HTML mockups. They're there to look at while you code.
