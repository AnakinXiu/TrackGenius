# UI Theme Tokens & Control Restyling Design

**Date:** 2026-08-20
**Status:** Approved in brainstorming
**Source designs:** `G:\OneDrive\TrackGenius\UI Design\Dark.png`, `Light.png`

## Problem

The application's UI uses WPF-UI's Fluent defaults: blue accent, unstyled raw
controls, and hard-coded color literals (`Green`, `Red`, `Gray`) in
`RaceDataListControl`. The two design images specify a distinct visual
language — green primary accent, purple/amber/red semantic colors, card-like
leaderboard rows — with light and dark variants. None of it is expressed in
the code, and theme colors cannot be adjusted in one place.

## Goal

Apply the design images' control style and color theme to the **existing**
UI surfaces, with every theme color defined as a named resource ("variant")
that can be modified later, and the existing Light/Dark theme switching
carrying both palettes plus persistence.

## Decisions (from brainstorming)

| Decision | Choice |
|---|---|
| Scope | Restyle existing surfaces only — no new panels/pages/layout from the images |
| Fluent accent | Green `#10B981` via WPF-UI accent override (all stock Fluent controls go green) |
| Persistence | Save theme choice to JSON next to the existing preferences store |
| Backdrop | Keep Mica; palette paints control surfaces only |
| Race table | Card-like rows per the images (not just token swap) |
| Architecture | Semantic token layer (own keys) on top of WPF-UI themes |
| Verification | Implementer screenshots both themes and compares to design images; user does final pass |

## Architecture

New folder `source/TrackGenius/Theme/` containing:

1. **`Palette.Light.xaml` / `Palette.Dark.xaml`** — two resource dictionaries
   defining the **identical key set** (validated at startup). All brushes are
   `SolidColorBrush` with `Color` resources alongside where a control needs
   gradients/animations later; keys are consumed via `DynamicResource`.
2. **`RaceStyles.xaml`** — property-setting styles consuming the tokens (no
   control re-templating; WPF-UI's templates do the rendering).
3. **`ThemeManager.cs`** (`TrackGenius.UI.Theme`) — the single owner of
   theme switching: swaps the merged palette dictionary in
   `Application.Resources`, calls `ApplicationThemeManager.Apply`, applies
   the green accent (`ApplicationAccentColorManager`), validates palette key
   parity, and persists the choice.

```
App.OnStartup
  → ThemePreferencesStore.Load()          (System default)
  → ThemeManager.Apply(savedTheme)        (palette swap + Fluent theme + green accent)
SettingPageViewModel.SelectedTheme setter
  → ThemeManager.Apply(value)             (same path + persist)
XAML surfaces
  → {DynamicResource RaceXxxBrush}        (palette tokens)
  → {StaticResource RacePrimaryButton}    (styles)
```

## Token set

| Token | Light | Dark | Used for |
|---|---|---|---|
| `RaceAccentBrush` | `#10B981` | `#10B981` | START button, positive deltas, active selection |
| `RaceAccentHoverBrush` | `#059669` | `#059669` | Accent hover state |
| `RaceAccentSecondaryBrush` | `#8B5CF6` | `#8B5CF6` | Best-lap highlight |
| `RaceWarningBrush` | `#F59E0B` | `#F59E0B` | ARM-style actions, warnings |
| `RaceDangerBrush` | `#EF4444` | `#EF4444` | STOP-style actions, negative deltas |
| `RaceSuccessBrush` | `#10B981` | `#10B981` | Status dots, connected state |
| `RaceBackgroundBrush` | `#F8F9FA` | `#121212` | Page/nav backgrounds |
| `RaceCardBackgroundBrush` | `#FFFFFF` | `#1E1E1E` | Leaderboard rows, panels |
| `RaceBorderBrush` | `#E5E7EB` | `#2D2D2D` | Borders, dividers, grid lines |
| `RaceTextPrimaryBrush` | `#111827` | `#FFFFFF` | Headings, table values |
| `RaceTextSecondaryBrush` | `#374151` | `#A3A3A3` | Labels, sub-text |
| `RaceTextMutedBrush` | `#6B7280` | `#6B7280` | Footer, least-important text |
| `RaceRowHoverBrush` | `#F3F4F6` | `#2D2D2D` | Row/nav hover backgrounds |

Accent-family colors are identical across themes (the design images use the
same vivid accents on both); surface/text colors differ. Re-theming later =
editing values in exactly these two files.

## Styles (`RaceStyles.xaml`)

- `RacePrimaryButtonStyle` — for `ui:Button`: green fill (`RaceAccentBrush`),
  white foreground, hover `RaceAccentHoverBrush`, corner radius 8, padding
  16,10. Based on WPF-UI's button style so Fluent states (pressed, disabled,
  focus) survive.
- `RaceDangerButtonStyle` — same shape, `RaceDangerBrush`.
- `RaceCardBorder` style for `Border` — `CornerRadius` 8,
  `BorderBrush={DynamicResource RaceBorderBrush}`, thickness 1,
  background `RaceCardBackgroundBrush`.
- `RaceHeaderTextStyle` (secondary color, SemiBold) and
  `RaceMutedTextStyle` for `TextBlock`.
- `RaceBestLapTextStyle` — `RaceAccentSecondaryBrush`, SemiBold.
- Implicit `ProgressBar` tuning where the control is used: fill
  `RaceAccentBrush`, track `RaceBorderBrush` (explicit style, not implicit
  app-wide).
- `RaceStatusDot` template-lite style (Ellipse in a `Viewbox`-free simple
  `Style` targeting `Ellipse`) with `Fill=RaceSuccessBrush`.

## Surface application

- **`RaceDataListControl`**: outer `Border` → `RaceCardBorder`; header row
  text → `RaceHeaderTextStyle`; row `Border` → card background, 8px radius,
  hover trigger → `RaceRowHoverBrush`; ▲ triangle `RaceAccentBrush`, ▼
  `RaceDangerBrush` (replacing `Green`/`Red` literals); Best Lap cell →
  `RaceBestLapTextStyle`; `ProgressBar` → themed fill/track.
- **`QuickRacePage`**: Start `Button` → `ui:Button` with
  `RacePrimaryButtonStyle`. No structural changes.
- **`SettingsPage`**: no XAML changes needed (cards/combos already Fluent;
  accent override carries green). `ui:Design.*` attributes untouched.
- **`MainForm`**: status-bar text blocks → `RaceTextSecondaryBrush` /
  `RaceTextMutedBrush`. Nav selection renders green via the Fluent accent
  override; pane structure untouched.

## Theme switching & persistence

- `ThemePreferences` (enum: `System`, `Light`, `Dark`) +
  `ThemePreferencesStore` in `TrackGenius.UI.Persistence` following
  `RaceDataColumnPreferencesStore` exactly: JSON file
  `%LocalAppData%\TrackGenius\theme.json`, ctor-injectable path for tests,
  `Load()` catches everything → default (`System`), `Save()` best-effort.
- `ThemeManager.ApplySystemThemeWatch()` restores the current
  `SystemThemeWatcher` behavior for the `System` choice, with the green
  accent applied whenever the system theme resolves.
- `SettingPageViewModel` keeps its `Themes` list; `ThemeType` gains a
  `System` member (`= 2`, after the existing `Light = 0`, `Dark = 1` so
  persisted-integer compat holds) and the combo shows all three. Selection
  routes through `ThemeManager.Apply`, which persists. The ctor initializer
  reads the persisted preference (not the live theme) so the combo shows
  `System` when the user chose system-follow.
- `App.OnStartup`: load preference → `ThemeManager.Apply` before creating
  `MainForm`. The current `DefaultTheme`/`ApplyAppearance` block is replaced
  by this path.

## Error handling

- Palette key parity failure (one dictionary missing a key): logged via the
  app logger, fall back to the *other* palette entirely rather than running
  half-themed. Startup continues.
- Corrupt/locked preferences file → `System` default, logged, app continues
  (identical posture to the column preferences store).
- Accent override no-op on platforms without WPF-UI backdrop support —
  tokens still carry the design; no crash path.

## Testing

- **Unit** (`TrackGenius.UITests`):
  - `ThemeManager` tests: applying Light/Dark swaps the merged palette
    dictionary and both palettes expose the full key set;
    `RaceAccentBrush` resolves to `#10B981` under both themes.
  - `ThemePreferencesStore` round-trip (temp file): save → load → equal;
    corrupt content → `System`; missing file → `System`.
- **Build gate**: solution builds; existing 75 UITests remain green.
- **Visual verification** (implementer, before hand-off): run the app,
  screenshot Quick Race + Settings in Light and Dark, compare against the
  two design images for palette fidelity, attach screenshots for the user's
  final pass.

## Out of scope

- New layout/panels from the images (events log, driver panel, track map,
  race header bar, ARM/STOP/RESET buttons) — future work.
- Control re-templating beyond property styles.
- Font family changes (designs suggest Inter/Roboto; current Fluent stack
  retained).
- Auto Light/Dark switching beyond the existing `SystemThemeWatcher`
  behavior.
