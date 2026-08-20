# RaceDataListControl — Columnar Table with Toggleable Columns

**Date:** 2026-08-13
**Status:** Approved (design)
**Scope:** `TrackGenius.UI` — `Views/Controls/RaceDataListControl`, `ViewModels/RaceDataItem`, related converters and settings.

## 1. Goal

Turn `RaceDataListControl` from a fixed two-block card list into a columnar race standings table where:

- Every property of `RaceDataItem` is shown as its own column.
- Columns can be shown/hidden individually via a context menu opened by right-clicking a column header.
- The list is always ordered by `Position` (ascending), and re-orders live as positions change.
- `RacerPositionChange` is rendered inside the Position cell (not as its own column) as a colored triangle + absolute value.
- The existing per-row composed layout (notably the full-width `ProgressBar`) is preserved.

## 2. Constraints & Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Control type | **Composed `ItemsControl`/`ListView` with a free-form per-row template** (extend the current pattern), not `DataGrid` | `DataGrid` rows are rigid cell grids; a full-width element (the `ProgressBar`) beneath the cells per row is not natural. The current control already aligns header↔rows via `SharedSizeGroup`; generalizing it preserves row-layout flexibility. |
| Column visibility persistence | **Persist across restarts** | User preference; stored as a JSON file in `%LocalAppData%\TrackGenius\` (no VS settings designer / App.config needed). |
| Zero position-change display | **Dash (`—`)** | Explicit neutral marker. |
| Sorting | **Position-only, live auto-sort; headers are not click-sortable** | Matches "ordered by position"; avoids conflicting sort states. |

Non-goals (out of scope): click-to-sort on other columns, drag-to-reorder columns, column-width persistence, resizing columns by dragging.

## 3. Approach

Keep `RaceDataListControl` as a composed list. Generalize the existing `Grid.IsSharedSizeScope` + `SharedSizeGroup` alignment mechanism from two columns to N, and add per-column visibility bound to a single column-settings model.

Two properties of this design make it work cleanly:

1. **`SharedSizeGroup` unifies widths** — each column's group name appears once in the header and once in every row; SharedSizeScope gives all of them one shared width.
2. **`ColumnDefinition Width="Auto"` + cell `Visibility`** — collapsing a column's cell content collapses the Auto column to zero width. Hiding a column therefore removes it from the header and every row simultaneously, with no leftover gap. This is the property `DataGrid` cannot match for composed rows.

The full-width `ProgressBar` stays in a second row of each row's `Grid`, `Grid.ColumnSpan`ning all columns, so it is unaffected by column visibility.

## 4. Detailed Design

### 4.1 Column set and order

Columns are defined left-to-right. All are visible by default. Position is fixed first and cannot be hidden.

| # | Header | Binds to | Hideable | Notes |
|---|--------|----------|----------|-------|
| 0 | Position | `RacerPosition` | No | Hosts the change indicator. Always first. |
| 1 | Car # | `RacerNumber` | Yes | |
| 2 | Driver | `DriverName` | Yes | |
| 3 | Laps | `LapsCount` | Yes | |
| 4 | Gap | `GapTime` | Yes | `TimeSpan`, format `m:ss.fff` |
| 5 | Interval | `IntervalTime` | Yes | `TimeSpan`, format `m:ss.fff` |
| 6 | Last Lap | `LastLapTime` | Yes | `TimeSpan`, format `m:ss.fff` |
| 7 | Best Lap | `BestLapTime` | Yes | `TimeSpan`, format `m:ss.fff` |
| 8 | Transponder | `TransponderID` | Yes | |
| 9 | Notes | `Description` | Yes | |

`RacerPositionChange` is **not** a column — it renders inside the Position cell (§4.3).

### 4.2 Column settings model (single source of truth)

A small `INotifyPropertyChanged` model drives **both** the header context menu and per-cell visibility, so they can never disagree.

```csharp
// One per column; owns its own visibility state.
public sealed class RaceDataColumnOption : INotifyPropertyChanged
{
    public string Key { get; }      // stable persistence key, e.g. "Laps"
    public string Label { get; }    // header + menu text, e.g. "Laps"
    public bool CanHide { get; }    // false for Position
    public bool IsVisible { get; set; } // INPC; toggled by the menu, read by cells
}

// Exposed by RaceDataListControl (it owns the instance).
public sealed class RaceDataColumnSettings : INotifyPropertyChanged
{
    public RaceDataColumnOption Position { get; }   // IsVisible forced true
    public RaceDataColumnOption CarNumber { get; }
    public RaceDataColumnOption Driver { get; }
    public RaceDataColumnOption Laps { get; }
    public RaceDataColumnOption Gap { get; }
    public RaceDataColumnOption Interval { get; }
    public RaceDataColumnOption LastLap { get; }
    public RaceDataColumnOption BestLap { get; }
    public RaceDataColumnOption Transponder { get; }
    public RaceDataColumnOption Notes { get; }

    public IList<RaceDataColumnOption> All { get; } // aggregate, in display order
}
```

- **Header context menu** — `ContextMenu` with `ItemsSource="{Binding ColumnSettings.All, RelativeSource=...}"`. `ItemContainerStyle` produces a `MenuItem` per option: `IsCheckable="True"`, `IsChecked` two-way bound to `IsVisible`, `Header` bound to `Label`, `IsEnabled` bound to `CanHide`.
- **Cell visibility** — each row cell binds `Visibility` to its option's `IsVisible` via `BooleanToVisibilityConverter`, reaching the control with `RelativeSource AncestorType=local:RaceDataListControl`, e.g. `Visibility="{Binding ColumnSettings.Laps.IsVisible, Converter={StaticResource BoolToVis}, RelativeSource=...}"`.
- Position is non-hideable: its menu item is disabled (`CanHide=false`), and the settings loader re-pins it visible regardless of persisted state.

`RaceDataItem` remains the direct row `DataContext`. No data→cell reshaping is introduced.

### 4.3 Position cell and change indicator

Position is a `DataTemplate` within the row:

- **Big number** — `RacerPosition`, FontSize ~20, SemiBold.
- **Small indicator** (FontSize ~12) driven by `RacerPositionChange`:

  | `RacerPositionChange` | Glyph | Color | Trailing text |
  |---|---|---|---|
  | `< 0` (improved) | ▲ | green | `\|value\|` |
  | `> 0` (worsened) | ▼ | red | `\|value\|` |
  | `== 0` | — | neutral | none |

Sign logic lives in a unit-testable `IValueConverter`:

- `RacerPositionChangeSignConverter` → maps `int` to a `PositionChangeState { Improved, Worsened, Unchanged }`. `DataTrigger`s on this enum set glyph/color/visibility.
- `AbsoluteValueConverter` → `int` → `int`/string `|value|` for the trailing number.

Glyphs are literal `▲` (U+25B2), `▼` (U+25BC), `—` (U+2014) in `TextBlock`s.

### 4.4 Row layout (composed, preserved)

Each row is a `Grid`:

- Row 0: one cell per column (Position cell + the nine property cells), each `Visibility`-bound per §4.2, each in its own `ColumnDefinition Width="Auto"` with the matching `SharedSizeGroup`.
- Row 1: the existing `ProgressBar`, `Grid.ColumnSpan`ning all columns, full width — unchanged from today.

The header is the same column structure (labels) inside the same `Grid.IsSharedSizeScope`, so widths stay aligned as columns show/hide and as content widens.

### 4.5 Live sorting by position

On `ItemsSource` change, the control obtains `CollectionViewSource.GetDefaultView(ItemsSource)`, clears `SortDescriptions`, adds `SortDescription("RacerPosition", Ascending)`, and sets `IsLiveSortingRequested = true` / `IsLiveSorting = true`. Because `RaceDataItem` raises `PropertyChanged` for `RacerPosition`, live sorting re-orders rows in real time as positions update. No header click-sorting is wired.

### 4.6 Persistence

A small JSON file in the user's local app-data folder, read/written via `System.Text.Json` (BCL — no new package, no VS settings designer, no App.config sections).

- Path: `%LocalAppData%\TrackGenius\raceDataColumns.json`
- Format: `{ "hiddenColumns": ["Laps", "Notes"] }` (comma list of hidden option `Key`s; absent/empty file ⇒ all visible)

Pure, unit-testable helpers handle the JSON; a thin store handles file I/O:

- `RaceDataColumnPreferences.ParseHiddenColumns(string json)` → `IReadOnlyList<string>` (tolerant of null/empty/corrupt → empty list).
- `RaceDataColumnPreferences.SerializeHiddenColumns(IEnumerable<string> keys)` → JSON string.
- `RaceDataColumnPreferencesStore` — owns the file path (default `%LocalAppData%\TrackGenius\...`, injectable for tests), `Load()` (missing/locked/corrupt ⇒ empty list, never throws to the UI) and `Save(IEnumerable<string> hiddenKeys)`.

Load: on control initialization, read hidden keys → set each option's `IsVisible` (`Key` present ⇒ `false`). Position is re-pinned visible regardless.

Save: whenever an option's `IsVisible` changes, recompute the hidden-key set (`All.Where(o => !o.IsVisible).Select(o => o.Key)`) and `Save()`.

### 4.7 Files touched / added

- **Modified**
  - `Views/Controls/RaceDataListControl.xaml` — new columnar header + row template, header `ContextMenu`, shared-size columns.
  - `Views/Controls/RaceDataListControl.xaml.cs` — `ColumnSettings` property, build/live-sort on `ItemsSource` changed, context-menu data wiring, load/save via the preferences store.
- **Added**
  - `ViewModels/RaceDataColumnOption.cs`, `ViewModels/RaceDataColumnSettings.cs` — the column model + hidden-key parse/apply helpers.
  - `Converters/RacerPositionChangeSignConverter.cs`, `Converters/AbsoluteValueConverter.cs` (under `TrackGenius.UI`).
  - `Persistence/RaceDataColumnPreferences.cs` (pure JSON helpers) and `Persistence/RaceDataColumnPreferencesStore.cs` (file I/O).

## 5. Testing

No UI test harness exists in the solution, so testable logic is isolated into converters and verified with NUnit (matching existing `Given…_When…_Then…` style in `TrackGenius.ProtocolTests`):

- `RacerPositionChangeSignConverter`: negative ⇒ `Improved`, positive ⇒ `Worsened`, zero ⇒ `Unchanged`.
- `AbsoluteValueConverter`: negative ⇒ positive magnitude, positive ⇒ unchanged, zero ⇒ zero.
- `RaceDataColumnPreferences` JSON round-trip: `SerializeHiddenColumns` → `ParseHiddenColumns` stability; tolerance of null/empty/corrupt input.
- `RaceDataColumnSettings.ApplyHiddenKeys` / `BuildHiddenKeysString`: hidden-key set ⇒ `IsVisible` state and back, including the Position-always-visible pinning.

XAML stays declarative and is not unit-tested.

## 6. Risks / open details (to settle during implementation)

- **Column sizing** — `Auto` (fit widest content) vs. a minimum width. `Auto` is required for the collapse-to-zero behavior; min-widths would be applied per column if the table looks too tight.
- **Per-column divider lines** — the current two-column design has a divider; with ten columns dividers may look noisy. Likely drop them in favor of a clean table gridline/zebra style, decided visually.
- **TimeSpan formatting** — confirm `m:ss.fff` is the desired lap-time format.
