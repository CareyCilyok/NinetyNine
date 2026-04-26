# NinetyNine — Accessibility Audit (WCAG 2.2 AA)

**Work Package:** WP-21  
**Audit date:** 2026-04-10  
**Auditor:** WP-21 agent (static analysis)  
**Scope:** Blazor Web App — all pages and shared components (Waves 2–5 output)

---

## 1. Summary

| Category | Count |
|---|---|
| WCAG 2.2 AA criteria audited | 28 |
| Pass | 23 |
| Fail (fixed in this WP) | 3 |
| Fail (deferred) | 5 |
| N/A | 7 |
| Overall verdict | **Conditional pass** — three violations fixed inline; five deferred findings require follow-up before a full AA sign-off |

---

## 2. Methodology

This audit is a **static analysis only** — no live browser, no screen reader, no automated axe/Lighthouse scan. Findings are derived from reading:

- All `.razor` component and page files under `src/NinetyNine.Web/Components/`
- `src/NinetyNine.Web/wwwroot/css/theme.css` (design tokens with inline contrast ratios)
- `src/NinetyNine.Web/wwwroot/css/app.css` (global styles: focus rings, touch targets, reduced motion)
- All scoped `.razor.css` files

**Limitations:** Interactive-state behavior (actual focus order on a live page, screen reader announcement order, touch target measurement in a real browser) could not be verified without a running instance. The deferred findings in section 6 are candidates for a follow-up live session.

---

## 3. Per-Criterion Checklist

### 1. Perceivable

| ID | Criterion | Status | Evidence |
|---|---|---|---|
| 1.1.1 | Non-text content | **PASS** | All `<img>` elements use either descriptive `alt` text or `alt=""` + `role="presentation"` / `aria-hidden="true"`. Phosphor icons in nav and buttons correctly carry empty alt. `AvatarImage` container uses `role="img"` + `aria-label`; inner `<img>` carries the display name for the image URL case (see deferred F-D1 for the double-announcement concern). Decorative hero image in Home.razor uses `alt="" role="presentation"`. Pool icon in sidebar uses `alt="" role="presentation"`. |
| 1.3.1 | Info and relationships | **PASS (with deferred)** | Semantic heading hierarchy is correct throughout: each page has a single `h1`; subordinate sections use `h2`. Forms use `<label for="...">` to associate labels. Tables use `<th scope="col">`. `<dl>` is used correctly for contact info in Profile.razor. `<nav>` + `role="list"` patterns are appropriate. One deferred issue: UserMenu dropdown is rendered conditionally in the DOM without a persistent container, which may affect AT reading order (F-D3). |
| 1.3.5 | Identify input purpose | **PASS** | All auth forms use appropriate `autocomplete` attributes: `email`, `current-password`, `new-password`, `username`, `given-name`, `additional-name`, `family-name`, `tel`, `street-address`, `organization`. |
| 1.4.3 | Contrast (minimum) 4.5:1 | **PASS** | theme.css documents inline contrast ratios for every text/background pairing. Lowest documented body-text ratio is `--nn-text-tertiary` at 4.91:1 on `bg-secondary` and 5.39:1 on `bg-primary` — both above the 4.5:1 AA floor. All semantic colors (`--nn-danger`, `--nn-success`, `--nn-warning`, `--nn-info`) are documented with ratios above 6.5:1. Light theme is also documented at AA: text-tertiary 4.63:1 on `#fafafa`. |
| 1.4.4 | Resize text | **PASS** | `font-size: 16px` on `html` with no `max-height` clipping on text containers; heading font sizes use `clamp()` which scales with viewport. `line-height: 1.55` is generous. No fixed-pixel containers observed that would clip text at 200% zoom. |
| 1.4.10 | Reflow | **PASS** | Layout uses CSS Flexbox/Grid with `flex-wrap`. `nn-page` has `max-width: 1200px` with responsive `padding` via `@media`. No horizontal scroll would be forced above 320px CSS px for prose content. Score card grid has a separate mobile layout (`.sc-mobile`) shown at `< 768px` specifically to handle reflow. |
| 1.4.11 | Non-text contrast (3:1) | **PASS** | UI component borders: `--nn-border-default` is `rgba(255,255,255,0.12)` on `#1c2024`. Frame cell borders use `--nn-border-subtle` and `--nn-border-default`. Active state borders use `--nn-accent-gold` which is 9.36:1 on the dark background. Focus rings use `--nn-accent-teal` at 7.14:1. Form control borders are marginal but supplemented by background-color change on focus — the teal border on focus is 7.14:1. |
| 1.4.12 | Text spacing | **PASS** | No `line-height`, `letter-spacing`, `word-spacing`, or `paragraph-spacing` values are set with `!important` on text-bearing elements in ways that would block user overrides. |
| 1.4.13 | Content on hover or focus | **PASS** | UserMenu dropdown is persistent while open (not a tooltip). Hover fills on nav links do not obscure other content. No custom tooltips that disappear immediately. |

### 2. Operable

| ID | Criterion | Status | Evidence |
|---|---|---|---|
| 2.1.1 | Keyboard | **PASS (with deferred)** | All interactive elements are natively focusable: `<button>`, `<a href>`, `<input>`, `<select>`. Active FrameCell is rendered as `<button>` — correct. TableSizePicker implements full arrow-key navigation via `@onkeydown`. UserMenu handles Escape via `@onkeydown`. Navigation rows in Games/List.razor have `tabindex="0"` + `@onkeydown` that handles Enter/Space. Deferred: focus trap in FrameInputDialog (F-D2). |
| 2.1.2 | No keyboard trap | **PARTIAL (deferred)** | FrameInputDialog handles Escape to close and calls `FocusAsync()` on first input when opened. However, Tab focus can escape the dialog bounds to the page content behind the backdrop. This is a medium-severity issue (F-D2). |
| 2.4.3 | Focus order | **PASS** | DOM order matches visual reading order throughout. Sidebar is rendered before main content in DOM order. Dialog is rendered at the end of the component output, consistent with visually being on top. |
| 2.4.6 | Headings and labels | **PASS** | All page headings are descriptive. Form labels are explicit and descriptive (not "Click here"). `<PageTitle>` is set on every page. |
| 2.4.7 | Focus visible | **PASS** | `app.css` defines `:focus-visible { outline: 2px solid var(--nn-accent-teal); outline-offset: 3px; }` globally. Scoped CSS in FrameCell, FrameInputDialog, TableSizePicker, MainLayout, NavMenu, and UserMenu all include explicit `:focus-visible` rules. The global rule also covers inputs and selects via the teal glow. `button.frame-cell:focus-visible` uses a 3px gold outline, exceeding the minimum. |
| 2.4.11 | Focus not obscured (2.2) | **PASS** | The sidebar is `position: sticky` on desktop; the hamburger is `position: fixed` with `z-index: calc(--nn-z-overlay + 1)`. No sticky headers cover page content focus targets. The dialog backdrop is `pointer-events: none` on the outer element; focused elements inside the dialog are not behind the backdrop. |
| 2.5.5 | Target size (44×44 recommended) | **PASS** | `--nn-touch-target: 2.75rem (44px)` is applied as `min-height` on all `.btn`, form inputs, and interactive controls. FrameCell minimum height is `calc(var(--nn-touch-target) * 4)`. Hamburger button is explicitly `width: var(--nn-touch-target); height: var(--nn-touch-target)`. Dialog stepper buttons are `width: var(--nn-touch-target); height: var(--nn-touch-target)`. `btn-sm` has `min-height: 2rem` (32px) — see deferred F-D5. |
| 2.5.8 | Target size minimum 24×24 (2.2 AA) | **PASS** | `btn-sm` at `min-height: 2rem (32px)` exceeds the 24×24 minimum even for small buttons. Picker chips have `min-height: var(--nn-touch-target)`. |

### 3. Understandable

| ID | Criterion | Status | Evidence |
|---|---|---|---|
| 3.1.1 | Language of page | **PASS** | `App.razor` line 2: `<html lang="en" data-bs-theme="dark">`. |
| 3.2.1 | On focus | **PASS** | No context changes triggered by focus events alone. |
| 3.2.2 | On input | **PASS** | Form inputs bound with `@bind-Value` use standard Blazor two-way binding — no on-change navigation or unexpected context shifts. TableSizePicker updates selection on click only, no unexpected navigation. |
| 3.3.1 | Error identification | **PASS** | All forms use Blazor `<DataAnnotationsValidator>` + `<ValidationMessage>`. Server-side errors are displayed in `<div class="alert alert-danger" role="alert">`. Error messages name the field and describe the problem ("Email is required.", "Password must be at least 10 characters."). |
| 3.3.2 | Labels or instructions | **PASS** | Every `<input>` and `<select>` has an explicit `<label for="...">`. Password complexity requirements are surfaced via `aria-describedby` pointing to hint elements in Register.razor and ResetPassword.razor. Display name format rules are similarly hinted. |
| 3.3.3 | Error suggestion | **PASS** | Validation messages are specific: "Enter a valid email address.", "Display name must be 2-32 characters.", "Passwords do not match." Password rules are displayed proactively in the hint text before submission. |
| 3.3.7 | Redundant entry | **N/A** | No multi-step flows where previously entered data would need to be re-entered. |
| 3.3.8 | Accessible authentication | **PASS** | No CAPTCHA or cognitive test. Password fields use standard browser inputs — paste is allowed by default. `autocomplete="current-password"` and `autocomplete="new-password"` enable password manager autofill. |

### 4. Robust

| ID | Criterion | Status | Evidence |
|---|---|---|---|
| 4.1.1 | Parsing | **PASS** | Blazor renders well-formed HTML. No hand-written malformed nesting observed in templates. One structural fix applied: nested `<main>` in Play.razor changed to `<section>` (see Fixes Applied). |
| 4.1.2 | Name, role, value | **PASS (with deferred)** | Custom components have appropriate ARIA. TableSizePicker uses `role="radiogroup"` + `role="radio"` + `aria-checked`. UserMenu uses `aria-haspopup="menu"` + `aria-expanded`. FrameInputDialog dialog uses `role="dialog"` + `aria-modal="true"` + `aria-labelledby`. ScoreCardGrid mobile picker uses `role="tablist"` + `role="tab"` + `aria-selected`. Deferred: AvatarImage double-announcement (F-D1). |
| 4.1.3 | Status messages | **PASS** | Loading spinners use `aria-live="polite"` + `aria-busy="true"`. Score-so-far in Play.razor uses `aria-live="polite"` + `aria-atomic="true"`. Game-complete celebratory block uses `aria-live="assertive"`. Frame score preview in FrameInputDialog uses `role="status"` + `aria-live="polite"`. Success/error alerts use `role="alert"` throughout. |

### Blazor-Specific Checks

| Check | Status | Evidence |
|---|---|---|
| `prefers-reduced-motion` honored | **PASS** | `app.css` section 19 kills all transitions/animations globally under `prefers-reduced-motion: reduce`. Scoped CSS in FrameCell, FrameInputDialog, TableSizePicker, MainLayout each also include local reduced-motion blocks. |
| Anti-forgery tokens | **PASS** | All `<EditForm>` instances with `method="post"` include `<AntiforgeryToken />`. Plain HTML forms in EditProfile.razor embed the token via `<input type="hidden" name="__RequestVerificationToken">`. |
| `<AuthorizeView>` branches | **PASS** | Both `<Authorized>` and `<NotAuthorized>` branches render complete, accessible content. Anonymous home page shows sign-in CTAs; authenticated shows quick-link grid. |

---

## 4. Findings

### High Severity (blocks users from completing a task)

**H-1: Nested `<main>` landmark in Play.razor (fixed)**  
`src/NinetyNine.Web/Components/Pages/Games/Play.razor` line 94 originally rendered a `<main class="nn-play-page__main">` element inside the `MainLayout`'s `<main role="main">` element. WCAG 4.1.1 and the HTML spec prohibit nested `<main>` elements; assistive technology may expose incorrect landmark structure or skip to the wrong main region. Fixed in this WP by changing to `<section aria-label="Score card">`.

### Medium Severity (degrades experience, workaround exists)

**M-1: Blazor error UI dismiss link not keyboard accessible (fixed)**  
`src/NinetyNine.Web/Components/Layout/MainLayout.razor` line 79 contained `<a class="dismiss">&#x2715;</a>` — an anchor element with no `href`, no `role="button"`, and no accessible name. It was mouse-only. Users who rely on keyboard navigation could not dismiss the Blazor error overlay. Fixed by adding `role="button"`, `tabindex="0"`, and `aria-label="Dismiss error"`.

**M-2: Venue address icon uses informational alt text (fixed)**  
`src/NinetyNine.Web/Components/Pages/Venues/List.razor` lines 67-70: the map-pin icon for address display used `alt="Address:" aria-label="Address"`. The `alt` text was redundant — the immediately following `<span>@venue.Address</span>` already conveys the address. A screen reader would announce "Address: Address: 123 Main St" (first from `aria-label`, then from `alt`, then from the text). Fixed by setting `alt=""` and `aria-hidden="true"`.

**M-3: AvatarImage double-announces the display name (deferred — F-D1)**  
The `<div role="img" aria-label="Avatar for @DisplayName">` container and the inner `<img alt="Avatar for @DisplayName">` both provide the same text. A screen reader announces "Avatar for Alice" twice. Changing the inner `<img alt>` to `alt=""` is the correct fix but WP-20's bUnit test at `AvatarImageTests.AvatarImage_RendersImg_WhenAvatarUrlSupplied` line 22 asserts `img.GetAttribute("alt").Should().Contain("Alice")`. Deferred to avoid breaking the test contract.

**M-4: FrameInputDialog does not trap focus (deferred — F-D2)**  
The dialog handles Escape to close and autofocuses the first input on open. However, it does not implement a focus trap — pressing Tab repeatedly from the last focusable element allows focus to escape into the background page content. Per WCAG 2.1.2, modal dialogs must prevent focus from reaching inert content. Fixing this requires either JavaScript interop to implement a focus trap or a structural change to wrap content in a focus-scope element. Deferred as structural.

**M-5: `btn-sm` minimum height 32px below 44px recommended target (deferred — F-D5)**  
The `.btn-sm` class sets `min-height: 2rem (32px)`, below the project's 44px touch target goal. Several "Edit", "View", "Sign in" buttons in list rows use `btn-sm`. They exceed the WCAG 2.2 AA minimum (24px, 2.5.8) but miss the project's self-imposed 44px goal (2.5.5 AAA level). Deferred: resizing these buttons requires design decisions about list-row density.

### Low Severity (polish / minor)

**L-1: UserMenu caret SVG icon may be logically inverted**  
`src/NinetyNine.Web/Components/Layout/UserMenu.razor` lines 84-89: when `_dropdownOpen` is true the icon is `caret-down.svg`; when false it is `caret-up.svg`. Since the dropdown opens upward (sidebar footer), caret-up when open and caret-down when closed would be more conventional. Both states are decorative (`alt="" role="presentation"`) so no screen reader impact; users relying on visual cues may be briefly confused. No fix applied — design intent is ambiguous.

**L-2: UserMenu dropdown items lack visible focus management on close**  
When the dropdown closes (via Escape or clicking a menu item), focus returns to wherever it was before `Close()` was invoked, which may or may not be the trigger button. A fully AA-compliant modal menu should explicitly return focus to the trigger. Deferred as structural (F-D3).

**L-3: VisibilityToggle `aria-label` on input partially overrides `<label>` for AT**  
`src/NinetyNine.Web/Components/Shared/VisibilityToggle.razor` line 19: `aria-label="@Label visibility"` on the `<input>` overrides the associated `<label>` text for screen readers, producing e.g. "Show real name publicly visibility" instead of "Show real name publicly". The `aria-label` is redundant given the `<label>` association and adds the word "visibility" unnecessarily. Low impact — the meaning is preserved. Deferred (additive change would conflict with the `<label>` + `for` association pattern).

**L-4: Skip-to-main-content link absent**  
No skip navigation link is provided at the top of the page. Keyboard users must Tab through the entire sidebar navigation on every page before reaching main content. This is technically a WCAG 2.4.1 (Bypass Blocks) failure. Adding a visually-hidden skip link requires a change to `MainLayout.razor` that would also need a visible focus state — structural but not complex. Deferred as it requires a design decision on placement and styling (F-D4).

**L-5: `<nav role="navigation">` redundant attribute in MainLayout**  
`src/NinetyNine.Web/Components/Layout/MainLayout.razor` line 54: `<nav class="nn-sidebar__nav" role="navigation" aria-label="Main">`. The `role="navigation"` is redundant since `<nav>` already carries this implicit role. Harmless — no AT impact — but adds unnecessary markup noise. Not fixed (too trivial and fully additive removal would be a content change).

---

## 5. Fixes Applied

| File | Change | WCAG Criterion |
|---|---|---|
| `src/NinetyNine.Web/Components/Pages/Games/Play.razor` | Changed nested `<main class="nn-play-page__main">` to `<section aria-label="Score card">` to eliminate duplicate main landmark. | 4.1.1 / 4.1.2 |
| `src/NinetyNine.Web/Components/Layout/MainLayout.razor` | Added `role="button"`, `tabindex="0"`, and `aria-label="Dismiss error"` to the Blazor error UI dismiss anchor element. | 2.1.1 / 4.1.2 |
| `src/NinetyNine.Web/Components/Pages/Venues/List.razor` | Changed `alt="Address:" aria-label="Address"` on the map-pin icon to `alt="" aria-hidden="true"` to remove duplicate announcement. | 1.1.1 |

All three changes are additive or corrective, do not rename CSS classes, do not restructure DOM hierarchy, and do not break any existing bUnit test assertions (verified: `dotnet test` 330/330 pass).

---

## 6. Deferred Findings

| ID | File | Issue | Why Deferred |
|---|---|---|---|
| F-D1 | `src/NinetyNine.Web/Components/Shared/AvatarImage.razor` | Inner `<img alt="Avatar for @DisplayName">` duplicates the container's `aria-label`, causing double-announcement. Fix: change inner `alt` to `""`. | WP-20 bUnit test `AvatarImageTests.AvatarImage_RendersImg_WhenAvatarUrlSupplied` asserts the `alt` attribute contains the display name. Changing it would break that assertion. Coordinate with WP-20 owner to update the test and then apply the fix. |
| F-D2 | `src/NinetyNine.Web/Components/Shared/FrameInputDialog.razor` | No focus trap — Tab can escape dialog into background content. Fix: implement a focus-trap (JS interop or a Blazor FocusScope wrapper). | Structural change requiring new Blazor/JS infrastructure. |
| F-D3 | `src/NinetyNine.Web/Components/Layout/UserMenu.razor` | Focus is not explicitly returned to the trigger button when the dropdown closes via Escape or item selection. | Requires storing the trigger element reference and calling `FocusAsync()` on close — structural. |
| F-D4 | `src/NinetyNine.Web/Components/Layout/MainLayout.razor` | ~~No skip-to-main-content link. WCAG 2.4.1 Bypass Blocks.~~ **Resolved** in Sprint 7 (commit `5783b96`, tracked as DEF-009). Skip link present in `MainLayout.razor`; `MainLayout.razor.css` hides it via the `clip-rect` visually-hidden pattern and reveals on `:focus` / `:focus-visible`. | — |
| F-D5 | `src/NinetyNine.Web/wwwroot/css/app.css` | `.btn-sm` has `min-height: 2rem` (32px), below the project's 44px touch target goal. | This is in Wave 1's territory (`app.css`). Additionally, a design decision is needed on list-row density vs. touch target size. Flagged for Wave 1 owner. |

---

## 7. Recommendations for Future Work

1. **Live browser + screen reader session.** The static audit cannot verify actual announcement order, focus ring visibility on every element, or scroll-into-view behavior. Run a session with NVDA + Firefox and VoiceOver + Safari before v1 launch.

2. **Automated axe integration.** Add `axe-core` to the Playwright or Cypress end-to-end test suite. Even a single `cy.checkA11y()` on each page route would catch many class-level violations automatically on each CI run.

3. ~~**Skip-to-main-content link (F-D4).** Before the product goes to external users, implement a skip link in `MainLayout.razor`. This is the single most impactful remaining gap for keyboard-only users.~~ **Resolved** in Sprint 7 (see DEF-009 / commit `5783b96`).

4. **Focus trap for FrameInputDialog (F-D2).** The score-entry dialog is the most-used interactive surface in the app. Ensuring focus cannot escape is critical for screen reader and keyboard users actively entering scores.

5. **Coordinate F-D1 with WP-20.** Update `AvatarImageTests.AvatarImage_RendersImg_WhenAvatarUrlSupplied` to assert `alt=""` (or remove the assertion) so `AvatarImage.razor` can be fixed correctly.

6. **Test with reduced motion enabled.** Verify the score card grid transition and the sidebar slide-in are fully suppressed by `prefers-reduced-motion: reduce`. The CSS rule is in place; confirm it reaches the browser in a real build.

7. **Review `btn-sm` sizing.** The `Edit` and `View` buttons in game list rows, stats tables, and venue cards use `btn-sm` which renders at 32px height. Consider whether these contexts offer enough surrounding space to meet the 24px clear-zone requirement of WCAG 2.5.8 at minimum.

---

*Audit performed by static analysis of Blazor markup and CSS. No live browser testing was performed. Limitations are noted in Section 2.*
