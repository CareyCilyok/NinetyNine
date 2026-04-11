# Design Assets Canvas (April 2026)

Findings from a web canvas of free icon packs and pool/billiards imagery for the NinetyNine redesign. Authorized by the user 2026-04-10. This is a landscape report, not a purchase order — nothing has been downloaded or integrated yet.

---

## 1. Executive Summary

**Icon packs** (all free, permissive licenses):

| Pack | License | Icon count | Pool-specific? | Verdict |
|---|---|---|---|---|
| **Phosphor** | MIT | ~9,000 | ❌ No eight-ball, pool, or cue icons. Has beach-ball, bowling-ball, basketball, baseball, disco-ball, hockey, golf, cricket, tennis, football | Best general pack; strong sport-variant coverage; 6 weight variants |
| **Lucide** | ISC | 5,000+ (v1) | ❌ No pool-specific. Has trophy, medal, gamepad, gamepad-2, gamepad-directional, dices (1-6), target, target-arrow, crosshair (5 variants), circle (40+ variants), star | Clean, stroke-based, excellent for dark themes; actively maintained |
| **Tabler** | MIT | 6,092 (v3.41) | ❌ No pool-specific. Has "Sport" and "Games" categories | Largest free pack; filled + outline variants |
| **Bootstrap Icons** | MIT | ~2,000 | ❌ | Already bundled with Bootstrap 5 (the current baseline). Safest default, smallest disruption |
| **Heroicons** | MIT | ~300 | ❌ | By Tailwind Labs; very polished but narrow — skip for NinetyNine, coverage is too thin |
| **Material Symbols** | Apache 2.0 | ~3,000 | ❌ | Google's. Heavy "Material" aesthetic may clash with a custom dark theme |

**Key finding**: No mainstream free icon pack includes pool/billiards/eight-ball/cue/rack/chalk icons. Pool-specific glyphs will need to come from custom SVGs or niche sources. This is the single biggest gap.

**Photography** (both recommended):

- **Unsplash** — free commercial use, no attribution required, 30,000+ billiards images
- **Pexels** — free commercial use, no attribution required, 5,000+ billiards images

**Numbered pool ball SVGs (1–15)**:

- Wikimedia Commons has a CC0 `File:1ball.svg` (author: 0xDeadbeef, July 2022, standard American pool ball design)
- Full 1–15 set availability **unconfirmed** from this canvas — needs direct browse of `Category:Billiard_balls` on Commons or manual file-by-file verification
- Fallback: commission a small custom SVG set (15 files, a few hours of design work)

---

## 2. Icon Pack Recommendation

### Primary: **Phosphor Icons** (MIT)

**Why**:

- **9,000+ icons** across 6 weight variants: `thin`, `light`, `regular`, `bold`, `fill`, `duotone`. The thin/light weights pair extremely well with dark themes (avoids the "chunky bright icon on dark background" problem).
- Strong generic coverage for everything NinetyNine needs outside of pool-specific glyphs: navigation (house, list, chart-bar, user, gear, sign-out), actions (plus, pencil, trash, check, x), stats (trophy, medal, crown, star, chart-line-up, chart-pie), gaming (game-controller, dice-*, crosshair, target), sport variants (basketball, baseball, football, bowling-ball, tennis-ball, beach-ball), UI chrome (caret-*, eye, eye-slash, funnel, magnifying-glass), social (google-logo, discord-logo, telegram-logo).
- Blazor/web integration via `@phosphor-icons/react` (not relevant) OR direct SVG imports (relevant — just copy the files we need from the `core` repo).
- `github.com/phosphor-icons/core` ships raw SVGs organized by weight — drop-in copyable.

**Minimal subset to import** (~30 icons across two weights):

Navigation / layout: `house`, `list`, `chart-bar`, `users-three`, `map-pin`, `user`, `gear`, `sign-out`, `caret-down`, `caret-right`, `magnifying-glass`, `x`, `check`

Game: `target`, `crosshair`, `plus`, `minus`, `arrow-clockwise`, `trash`

Stats: `trophy`, `medal`, `crown-simple`, `star`, `star-fill`, `chart-line-up`, `ranking`

Social/auth: `google-logo`, `discord-logo`, `telegram-logo`, `sign-in`

Ship `regular` weight as the default and `fill` weight for active/selected states.

### Secondary candidate: **Lucide**

If the user prefers a lighter, more minimal look, Lucide is the alternative. Identical coverage of the generic needs, single stroke style, smaller total size. Good for a stricter visual system but only one weight.

### Keep: the 18 salvaged FontAwesome-style SVGs

Already in `src/NinetyNine.Web/wwwroot/icons/`. Several are useful for venue/map concepts (`map-marker`, `map-marked-alt`, `warehouse`, `building`, `smoking-ban`) that don't have direct Phosphor equivalents. Mix-and-match is fine as long as the visual weights are compatible.

---

## 3. Pool-Specific Icons — The Gap

**Nothing in the major free packs has pool/billiards/eight-ball/cue/rack/chalk icons.** Phosphor's `disco-ball`, `beach-ball`, and `bowling-ball` are the closest generic stand-ins. Options:

### Option A — Custom-design a small set (recommended)

Commission / hand-draw a small pool-specific icon set:

- `eight-ball` (the classic black 8-ball circle with "8" or white dot)
- `cue-stick` (diagonal stick)
- `pool-table` (top-down rectangle with 6 pockets)
- `rack` (triangle of balls)
- `chalk` (small cube)
- `scratch` (cue ball with X) — for foul markers

~6 icons × ~30 min each with ImageShop/Figma = ~3 hours of design work. Matches the Phosphor weight/stroke exactly so it blends seamlessly. **Best long-term choice** — owns its own IP, no license question.

### Option B — Wikimedia Commons salvage

`Category:Billiard_balls` has 90+ files. Most are JPG/PNG and CC-BY or similar (not CC0). A few SVGs exist but need individual license verification. Workable but time-consuming and legally noisy.

### Option C — Flaticon / Iconfinder (paid or attribution-required)

Large pool-icon selections but attribution required on the free tier or pay per icon. Not recommended — the attribution requirement creates a persistent UI footer obligation, and the paid cost per pack is disproportionate to the need.

### Option D — Nothing

Use generic substitutes (`target` for scoring, `trophy` for stats, `users` for players) and skip the pool-specific glyphs entirely. Minimal work but the app won't visually signal "pool game" anywhere.

**Recommendation**: **Option A** (custom mini-set) paired with Phosphor for everything else.

---

## 4. Numbered Pool Ball Graphics (1–15)

For future features (per user's note — not needed for v1).

### Confirmed CC0

- `File:1ball.svg` on Wikipedia / Wikimedia Commons — CC0, author 0xDeadbeef, July 2022. Standard American pool ball design with numbered "1". Directly usable with zero legal friction.

### Unconfirmed

The full numbered set (2ball.svg through 15ball.svg) could not be verified from this canvas. The author's naming convention suggests a series but direct fetches to `File:8ball.svg` and `File:15ball.svg` returned 404 on Wikipedia. They may exist on Commons under different naming, or only 1ball was uploaded.

### Recommended next step (when we need the balls)

1. Browse `Category:Billiard_balls` directly on Commons and inspect each SVG's license
2. If a complete CC0 set doesn't exist, commission a custom set — they're trivial to generate (parametric SVG templates with number substitution, ~2 hours total for all 15)
3. Custom set is actually preferable because we control the exact style, anti-aliasing, stroke weight, and can match the dark theme

**Verdict**: Defer. Not needed until post-v1 pool-ball-aware features (like per-ball scoring detail) are actually on the roadmap.

---

## 5. Billiards Photography

For decorative backgrounds, hero imagery on the landing page, empty-state illustrations, venue cards, etc.

### Primary sources

| Source | License | Attribution? | Billiards inventory | Best for |
|---|---|---|---|---|
| **Unsplash** | Unsplash License (free commercial, irrevocable worldwide) | Appreciated but **not required** | 30,000+ billiards images | Hero shots, landing page backgrounds, atmospheric dark billiards rooms |
| **Pexels** | Pexels License (free commercial) | **Not required** | 5,000+ billiards photos | Similar to Unsplash; check both for the best shot |

### Unsplash License — key points

- Free, irrevocable, non-exclusive, worldwide copyright license
- Use, copy, modify, distribute, perform, commercial — all allowed
- **No attribution required** (appreciated)
- **Restriction 1**: Cannot compile Unsplash photos to replicate a similar/competing service (i.e., don't build a "free stock photos" clone)
- **Restriction 2**: Cannot sell unaltered copies (prints, merch) without adding creative modification
- Both restrictions are irrelevant to NinetyNine's use case

### Pexels License — key points

- Free for personal and commercial use
- **No attribution required**
- Modifications allowed
- **Restriction**: Depicted trademarks / brands / people still have their own rights. Don't imply endorsement. Irrelevant for atmospheric pool-room shots without recognizable people or logos.

### Usage notes for NinetyNine

- Target searches: `billiard room` (best for dark atmospheric), `billiard ball`, `pool table`, `night pool hall`
- Download a curated set (~10-15 photos) once a design brief exists — don't grab imagery speculatively
- Process each: resize to target dimensions, convert to WebP with JPEG fallback, strip EXIF metadata (privacy + size), test both light and dark theme over the imagery
- Store in `src/NinetyNine.Web/wwwroot/img/` under subdirectories: `hero/`, `venues/`, `backgrounds/`, `empty-states/`
- Keep an `img/ATTRIBUTION.md` file crediting photographers even though not required — nice gesture, easy to maintain

### Other photo sources worth considering later

- **Pixabay** — similar license, includes SVGs and illustrations alongside photos
- **Wikimedia Commons** — CC-BY / CC-BY-SA / CC0 mix, requires per-file license check, more legally mixed than Unsplash/Pexels
- **OpenClipart** — public domain vector clipart, mostly dated style but useful for niche needs

---

## 6. License Summary Table

| Asset | Source | License | Attribution | Commercial? | Fit for NinetyNine |
|---|---|---|---|---|---|
| Phosphor Icons | github.com/phosphor-icons/core | MIT | Not required (include LICENSE file in repo) | ✅ | **Primary** — include the ~30 icons we need |
| Lucide Icons | github.com/lucide-icons/lucide | ISC | Not required | ✅ | Secondary candidate if Phosphor feels too heavy |
| Tabler Icons | github.com/tabler/tabler-icons | MIT | Not required | ✅ | Viable alternative; largest total count |
| Bootstrap Icons | github.com/twbs/icons | MIT | Not required | ✅ | Already bundled — use as a safety net |
| Salvaged Avalonia SVGs | archive/pre-blazor-rewrite branch | Originally FontAwesome Free (CC-BY 4.0) | Required per original FA license | ✅ | **Keep** — add FA attribution to footer |
| 1ball.svg | Wikimedia Commons | CC0 Public Domain | None | ✅ | Usable now; full set needs verification |
| Unsplash photos | unsplash.com | Unsplash License | Not required (appreciated) | ✅ (with restrictions on building competing services) | **Primary** photography source |
| Pexels photos | pexels.com | Pexels License | Not required | ✅ (with trademark/endorsement limits) | **Secondary** photography source |

**Note on the salvaged SVGs**: these are FontAwesome-style and almost certainly originate from FontAwesome Free, which is CC-BY 4.0 and does require attribution. We should add a footer credit line like `"Icons: FontAwesome Free (CC-BY 4.0)"` OR replace them with Phosphor equivalents to eliminate the attribution obligation. My lean: **replace with Phosphor**, keep the archive SVGs only where no Phosphor match exists.

---

## 7. Proposed Next Steps (pending user direction)

1. **Commit this canvas document** (so the findings survive session rotation and the designer agent can consume it directly).
2. **Download Phosphor subset**: pick the ~30 regular + fill icons listed in §2 from `github.com/phosphor-icons/core`, drop them into `src/NinetyNine.Web/wwwroot/icons/phosphor/{regular,fill}/`, commit with a LICENSE file.
3. **Commission the 6 custom pool-specific icons** via a designer agent (eight-ball, cue-stick, pool-table, rack, chalk, scratch) — stroke weight matched to Phosphor regular. Save as `wwwroot/icons/pool/*.svg`.
4. **Build a small test gallery page** (`/dev/icons`) in Development mode that renders every imported icon in both light and dark themes side-by-side. Useful as a sanity check during the redesign.
5. **Defer numbered pool balls** until a post-v1 feature needs them.
6. **Defer photography canvas** until a designer has produced a layout direction and we know where imagery actually lands.
7. **Kick off the redesign pass** with a designer agent (or team) once this asset foundation is in place. The designer will get: (a) this canvas document, (b) the architecture.md §8 direction, (c) the memory entries about dark-theme preference and historical refs, and (d) the current "absolutely terrible" Blazor state as the before-snapshot.

---

## 8. Open Questions for the User

1. **Phosphor vs. Lucide**: lean Phosphor for the breadth and weight variants — confirm or override?
2. **Custom pool icons**: OK to commission the mini-set via an agent, or would you rather source them from paid sites (Iconfinder / Noun Project)?
3. **FontAwesome attribution**: OK with adding an FA credit to the footer, or would you rather replace all the salvaged SVGs with Phosphor equivalents to keep the footer clean?
4. **Photography timing**: grab a curated hero/background set now, or wait until the designer knows where they'll land?
