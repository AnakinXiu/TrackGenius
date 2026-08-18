# RaceConnectionService — Shared Protocol/Port State for the Race Engine

**Date:** 2026-08-18
**Status:** Approved (design)
**Scope:** `TrackGenius.Communication` (new service), `TrackGenius.Core` (factory + consumer factory), `TrackGenius.UI` (two ViewModels, composition root).

## 1. Goal

Today the selected `IProtocol` and the serial-port state live only in the UI layer (`SettingPageViewModel`), and `RaceEngineFactory` hard-codes `new RobitronicMessageConsumer()`. Introduce one shared, observable service that owns the open/close flow and exposes the current protocol + port status, so that:

- The race engine resolves the correct `IMessageConsumer` for the actually-connected timing system.
- The race can only be started when the port is open and the protocol is known (UI hint + engine guard).

## 2. Constraints & Decisions

| Decision | Choice | Notes |
|---|---|---|
| Protocol change while port open | **Locked while port open** | `CurrentProtocol` is captured on successful open; changes only on the next successful open. |
| Consumer selection | **Protocol-keyed factory** | `MessageConsumerFactory.Create(IProtocol)` maps by `ProtocolName`; one entry per protocol. |
| Start gating | **UI hint + engine guard** | Start button disabled when not startable; engine factory also throws if called anyway. |
| Observability | **Observable service** | Service implements `INotifyPropertyChanged`; UI reacts live to open/close. |
| DI framework | **None — manual composition root** | See §3. |
| Service location | **`TrackGenius.Communication`** | Lowest layer visible to both consumers (`Core` and `UI` both reference it) that can see `CommunicateService` and `IProtocol`. |

Non-goals: runtime protocol hot-switching, persisting protocol selection across restarts, a full DI container migration.

## 3. DI Analysis (why no framework)

The actual problem is **shared state** (one instance seen by Settings page, race page, and engine), not dependency resolution. That is satisfied by constructing the service once in `App.xaml.cs` and passing it down — the same pattern already used for `CommunicateService`. The codebase already uses constructor injection throughout (`CommunicateService(ISerialPortWrapper, ILogger)`, `RaceEngine(…)`, `RaceEngineFactory(…)`, both ViewModels), so the testability benefit containers provide is already realized. The graph is ~6 nodes with one window; a container adds configuration surface without removing hand-wiring (WPF `NavigationView` pages are still parameterless-ctor + injected in `OnNavigated`). `Microsoft.Extensions.Logging` is already present, so adopting `Microsoft.Extensions.DependencyInjection` later is a small step.

**Revisit triggers** (any one): a second window or framework-created views, hosted/background services (e.g. race-timer service), or registrations whose lifetimes are hard to track by hand. Because everything is constructor-injected, that migration touches only `App.xaml.cs`.

## 4. Approach

A `RaceConnectionService` that **owns the open/close flow** (single write point) sits in `TrackGenius.Communication`, wrapping `CommunicateService`. The Settings page calls the service instead of `CommunicateService.StartService/CloseService` directly; the engine side reads state and maps protocol → consumer.

Rejected alternatives: a passive observer service (two writers, state drift) and putting `CurrentProtocol` on `CommunicateService` (mixes transport with selection state, VM remains writer).

## 5. Detailed Design

### 5.1 `IRaceConnectionService` / `RaceConnectionService` (new, `TrackGenius.Communication`)

```csharp
public interface IRaceConnectionService : INotifyPropertyChanged
{
    IProtocol CurrentProtocol { get; }     // set only on successful open; null when closed
    string CurrentProtocolName { get; }    // convenience for UI/log display
    string PortName { get; }               // of the currently open port ("" when closed)
    bool IsPortOpen { get; }               // delegates to CommunicateService.IsOpened
    bool CanStartRace { get; }             // IsPortOpen && CurrentProtocol != null

    void Open(string portName, IProtocol protocol); // delegates to StartService; on success records state + raises INPC; on failure throws, state unchanged
    void Close();                                    // delegates + clears protocol/port
}
```

- `IsPortOpen` delegates to `_communicateService.IsOpened` (never stale, e.g. device unplugged).
- Re-raises `IsPortOpen`/`CanStartRace` changes on `CommunicateService.PortOpenStateEventHandler` so external close paths propagate.
- Thread: open/close called on UI thread as today; no new threading introduced.

### 5.2 `MessageConsumerFactory` (new, `TrackGenius.Core`)

`IMessageConsumer Create(IProtocol protocol)` — switch on `protocol.ProtocolName`: `"Robitronic"` → `new RobitronicMessageConsumer()`; unknown → `NotSupportedException` naming the protocol. Adding a protocol later = one case + its consumer class.

### 5.3 `RaceEngineFactory` (modified)

- Constructor: `(IRaceConnectionService connectionService, CommunicateService communicateService)`.
- `CreateRaceEngine()`: guard first — `InvalidOperationException` with a clear message when `!connectionService.CanStartRace`; then resolve the consumer via `MessageConsumerFactory.Create(connectionService.CurrentProtocol)` instead of the hard-coded `RobitronicMessageConsumer`.

### 5.4 UI wiring

- **`SettingPageViewModel`**: `OpenClosePort` calls `_connectionService.Open(...)` / `Close()`; it becomes the only writer. Message polling (`TryGetNextMessage`) stays on `CommunicateService`. `IsPortOpenedString`/`ButtonContent` re-raise from the service's INPC (replacing the direct `PortOpenStateEventHandler` subscription).
- **`RacePageViewModel`**: takes `IRaceConnectionService`; exposes `CanStartRace` (Start button disabled when false) that re-raises when the service changes; re-checks before starting.
- **Engine lifetime bug (existing, fixed here):** `StartRace()` currently does `using var raceEngine = …; RaceStart(…)` — the engine is disposed immediately, unsubscribing before any detection arrives. Fix: hold the engine in a field; dispose the previous instance at the start of a new `StartRace`. (A dedicated Stop flow is out of scope.)

### 5.5 Composition root (`App.xaml.cs`)

Construct `RaceConnectionService(communicateService)` immediately after `CommunicateService`; pass to `MainForm` → `SettingPageViewModel`, `RacePageViewModel` (via `MainWindowViewModel`), and `RaceEngineFactory`. Manual composition per §3.

## 6. Testing (NUnit, `TrackGenius.UITests`)

`Communication` is referenced transitively by the UI project, so the service/factory logic is testable there; `CommunicateService` is faked through its `ISerialPortWrapper` seam:

- `RaceConnectionService`: closed defaults (`CurrentProtocol` null, `CanStartRace` false) → open sets protocol/port + raises INPC → open failure leaves state unchanged → close clears → protocol locked while open (selection change without reopen does not alter it).
- `MessageConsumerFactory`: Robitronic → `RobitronicMessageConsumer`; unknown name → `NotSupportedException`.
- `RaceEngineFactory`: guard throws when `CanStartRace` false; creates engine wired to protocol-matched consumer when true.

UI ViewModel behavior stays untested (no UI harness), consistent with existing practice.

## 7. Files

**Added:**
- `TrackGenius.Communication/IRaceConnectionService.cs`, `RaceConnectionService.cs`
- `TrackGenius.Core/MessageConsumerFactory.cs`

**Modified:**
- `TrackGenius.Core/RaceEngineFactory.cs` (guard + protocol-keyed consumer)
- `TrackGenius/ViewModels/SettingPageViewModel.cs` (open/close via service)
- `TrackGenius/ViewModels/RacePageViewModel.cs` (`CanStartRace` + engine lifetime fix)
- `TrackGenius/Views/MainForm.xaml.cs`, `TrackGenius/App.xaml.cs` (composition)

**Added (tests):** `TrackGenius.UITests/Communication/RaceConnectionServiceTests.cs`, `TrackGenius.UITests/Core/MessageConsumerFactoryTests.cs`, `TrackGenius.UITests/Core/RaceEngineFactoryTests.cs`.

## 8. Risks / details settled at implementation

- INPC raised from `PortOpenStateEventHandler` must not race with `Open`/`Close` (both UI-thread; keep it simple).
- `CurrentProtocolName` when closed: `string.Empty` (UI shows placeholder as today).
