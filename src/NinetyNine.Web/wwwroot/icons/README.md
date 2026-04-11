# NinetyNine — Icon assets

Icons used by the Blazor web application, organized by source.

## `phosphor/`

**Source**: [Phosphor Icons](https://phosphoricons.com/) v2.x  
**License**: MIT (see `phosphor/LICENSE`)  
**Attribution**: Not required but appreciated. MIT license text ships alongside the icons in `phosphor/LICENSE`.

Subset pulled from `github.com/phosphor-icons/core` on 2026-04-10 for the NinetyNine redesign. Only the icons we actually reference are committed; the full 9,000+ library is not.

**Regular weight** (42 icons) — default UI chrome, navigation, actions, stats, and the Avalonia-replaced icons:

```
arrow-clockwise, buildings, caret-down, caret-left, caret-right, caret-up,
chart-bar, chart-line-up, chart-pie, check, cloud-arrow-up, crosshair,
crown-simple, discord-logo, eye, eye-slash, game-controller, gear,
globe, google-logo, house, identification-card, key, list,
magnifying-glass, map-pin, medal, minus, pencil, plus, ranking,
sign-in, sign-out, star, target, telegram-logo, trash, trophy, user,
users-three, warehouse, x
```

**Fill weight** (9 icons) — for active/selected states that want extra visual weight:

```
chart-bar-fill, check-fill, house-fill, medal-fill, star-fill, target-fill,
trophy-fill, user-fill, x-fill
```

Adding more icons later is just a matter of copying the raw file from the Phosphor `core` repo into the appropriate subfolder.

## `pool/`

Pool / billiards specific iconography sourced from Wikimedia Commons. All items here are CC0 / Public Domain — no attribution required.

| File | Source | License |
|---|---|---|
| `cue-sports-pictogram.svg` | [File:Cue sports pictogram.svg](https://commons.wikimedia.org/wiki/File:Cue_sports_pictogram.svg) | Public Domain |
| `balls/1ball.svg` | [File:1ball.svg](https://commons.wikimedia.org/wiki/File:1ball.svg) (author: 0xDeadbeef) | CC0 |
| `balls/2ball.svg` | [File:2ball.svg](https://commons.wikimedia.org/wiki/File:2ball.svg) (author: 0xDeadbeef) | CC0 |
| `balls/4ball.svg` | [File:4ball.svg](https://commons.wikimedia.org/wiki/File:4ball.svg) (author: 0xDeadbeef) | CC0 |
| `balls/5ball.svg` | [File:5ball.svg](https://commons.wikimedia.org/wiki/File:5ball.svg) (author: 0xDeadbeef) | CC0 |
| `balls/6ball.svg` | [File:6ball.svg](https://commons.wikimedia.org/wiki/File:6ball.svg) (author: 0xDeadbeef) | CC0 |

### Known gaps

The following pool-specific icons were searched for across Wikimedia Commons, freesvg.org, SVG Repo, OpenClipart, and general web sources but could not be obtained under a zero-attribution license via direct programmatic download:

- Dedicated **eight-ball** icon (the 8-ball is the conceptual centerpiece of pool)
- **Cue stick** (isolated, not as part of a pictogram)
- **Pool table** top-down view
- **Triangle rack**
- **Chalk cube**
- **Scratch/foul** indicator (cue ball with X)

Available options for these:
- **CC-BY-SA 3.0/4.0 options exist** on Wikimedia Commons (e.g., `File:Billiardball-white.svg`, `File:Antu_8-ball-pool.svg`) but require persistent attribution — user preference is to avoid attribution obligations in the footer, so these were skipped.
- Commercial icon packs (Flaticon Premium, Noun Project Pro) have full sets but require payment or attribution on the free tier.
- Custom design was explicitly deferred per user direction ("source them, do not commission").

**Also missing from the numbered ball series**: 3, 7, 8, 9, 10, 11, 12, 13, 14, 15. Only balls 1, 2, 4, 5, 6 exist as CC0 uploads by 0xDeadbeef on Wikimedia Commons. Ball 9 exists but is CC-BY-SA. Ball 3 and 7-15 appear to not exist on Commons at all.

When the pool-specific icons are needed, options are:
1. Commission a small mini-set from a designer agent (overrides the current "don't commission" direction)
2. Purchase a pool icon pack from Flaticon / Noun Project / Iconfinder
3. Use the CC-BY-SA Commons icons and add an attribution footer line
4. Hand-draw trivial primitives inline (they're basic geometric shapes)

## Historical assets

The Avalonia-era FontAwesome-style SVGs previously in this directory (building, chart-bar, map-marker, etc.) have been **removed** and their equivalents are now in `phosphor/regular/`. The original 18 SVGs are preserved in the archive branch `archive/pre-blazor-rewrite` if they are ever needed again.

`docs/_salvaged/Icons.axaml` retains the Avalonia `ResourceDictionary` of geometry paths (Material, Fluent, FontAwesome, BoxIcons, VSImageLib) — pull individual paths from there only if a specific icon is needed and has no Phosphor equivalent.
