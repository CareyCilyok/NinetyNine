# Salvaged artifacts from the Avalonia implementation

Preserved from `archive/pre-blazor-rewrite` branch for reference. Not referenced by the active Blazor build.

## Contents

### `Icons.axaml`
Avalonia `ResourceDictionary` containing geometry-based icon definitions from Material, Fluent UI, FontAwesome, BoxIcons, Picol, and Visual Studio image libraries. Each `DrawingImage` has a `GeometryDrawing.Geometry` property containing an SVG-compatible path string.

**To reuse a path**: copy the `Geometry="..."` value into an SVG `<path d="..."/>` element. You may need to adjust the `viewBox` (the Avalonia paths were mostly authored for 24×24 or 32×32 frames).

### Live SVGs
The 18 FontAwesome-style SVG files that shipped alongside `Icons.axaml` (building, chart-bar, globe, map-marker variants, users, etc.) are NOT here — they were copied into `src/NinetyNine.Web/wwwroot/icons/` where the Blazor app can reference them directly via `<img src="/icons/building.svg" />` or as CSS backgrounds.
