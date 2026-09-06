# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

TrackGenius is a Windows desktop application (WPF, .NET 8) for RC car track timing, race control, and data management. It talks to physical lap-counting hardware over a serial port, parses timing-system-specific binary protocols (Robitronic, Kyosho), and drives a Fluent (WPF-UI) shell for live race management.

The solution lives under `source/` (not the repo root): `source/TrackGenius.sln`.

## Build, Run, Test

The .NET 9 SDK is installed locally, but **all projects target `net8.0` / `net8.0-windows`** — there is no `global.json` pinning the SDK.

```bash
# Build entire solution
dotnet build source/TrackGenius.sln

# Run the desktop app (WinExe project)
dotnet run --project source/TrackGenius/TrackGenius.UI.csproj

# Run all tests
dotnet test source/TrackGenius.sln

# Run a single test (filter by fully-qualified name)
dotnet test source/TrackGenius.ProtocolTests/TrackGenius.ProtocolTests.csproj --filter "FullyQualifiedName~DetectedMessageTests.GivenNullByteArray"
```

Tests use **NUnit** + **NSubstitute** for mocks. Test projects exist only for `Protocol` and `Const` (`TrackGenius.ProtocolTests`, `TrackGenius.ConstTests`) — there is currently no coverage for `Communication`, `Core`, or `Model`. Test methods follow a `Given…_When…_Then…` naming convention; match it when adding tests.

## Solution Architecture

Seven projects, layered from hardware up to UI. Dependency direction is strictly UI → Core → Communication → Protocol/Model → Const.

| Project (folder) | Namespace | Role |
|---|---|---|
| `TrackGenius` (UI) | `TrackGenius.UI` | WPF `WinExe` app, WPF-UI Fluent shell, MVVM, **composition root** |
| `TrackGenius.Core` | `TrackGenius.Core` | Race orchestration: `RaceEngine`, `IMessageConsumer` / `RobitronicMessageConsumer` |
| `TrackGenius.Communication` | `TrackGenius.Communication` | Serial I/O (`SerialPortWrapper` via `RJCP.SerialPortStream`) and `CommunicateService` |
| `TrackGenius.Protocol` | `TrackGenius.Protocol` | Timing-system protocols + message parsers; `IProtocol`, `IMessageParser` |
| `TrackGenius.Model` | `TrackGenius.Model` | Domain: `Race`, `RaceStatus`, `RaceTimer`, `Driver`, `CarDetectMessage` |
| `TrackGenius.Const` | `TrackGenius.Const` | Shared primitives/extensions (`TransponderType`, `ByteArrayExtension`) |

**Namespace caveat:** the UI project's folder is `TrackGenius` but its csproj is `TrackGenius.UI.csproj` and its root namespace is `TrackGenius.UI` (sub-namespaces: `TrackGenius.UI.ViewModels`, `.Views`, `.Pages`, `.Commands`, `.Logging`). All other projects use namespace = project name.

## The Core Pipeline (read this before touching data flow)

The live lap-counting path spans four projects and is event-driven end to end:

```
SerialPortWrapper.DataReceived            (TrackGenius.Communication — RJCP serial port)
  → CommunicateService.OnDataReceived     (parses raw bytes via IMessageParser.ParseMessage,
                                            enqueues IUplinkMessage, raises MessageReceived)
    → RaceEngine (ctor subscribes)         (TrackGenius.Core)
      → IMessageConsumer.ConsumeMessage   (RobitronicMessageConsumer maps DetectedMessage
                                            → CarDetectMessage, raises CarDetected)
        → RaceEngine.OnCarDetected
          → Race.UpdateRaceStatus          (TrackGenius.Model — lap counting +
                                            duplicate-pass suppression via MinLapIntervalMilliseconds)
```

There is also a **separate, polling-based UI path**: `SettingPageViewModel.PollMessageAsync` drains `CommunicateService.TryGetNextMessage()` (a `ConcurrentQueue<IUplinkMessage>`) every 50 ms to render raw messages. Both paths consume the same queue/enqueue in `CommunicateService.OnDataReceived`.

**Key abstraction — `IProtocol` pairs parser + serial settings.** `IProtocol` exposes `ProtocolName`, `MessageParser` (`IMessageParser`), and `SerialPortSettings` (`ISerialPortSettings`) as a single bundle. `CommunicateService.StartService(portName, IProtocol)` consumes them together. **Do not pass a parser and serial settings separately** — provide them via an `IProtocol` (see `RobitronicProtocol`, `KyoshoProtocol`, and the registry `Protocol.Protocols.AvailableProtocols`).

**Messages:** `ICommonMessage` is the base. `IUplinkMessage` = device→PC (constructed by parsers from raw bytes; `ParseMessage(byte[])`). `IDownlinkMessage` = PC→device (`Serialize()` → bytes via `CommunicateService.SendCommand`). Concrete message types are protocol-specific (e.g. `Robitronic/DetectedMessage`, `TimeStampMessage`).

## Composition Root & Logging

There is no DI container. `TrackGenius/App.xaml.cs` (`OnStartup`) is the composition root: it calls `LoggingBootstrapper.Configure()`, then **manually constructs** `SerialPortWrapper` → `CommunicateService` → `MainForm`, threading typed `ILogger<T>` instances through. When adding a service, construct and inject it here rather than `new`-ing dependencies inside the consumer.

Logging uses `Microsoft.Extensions.Logging` (abstraction) over **Serilog** (provider), bootstrapped by `TrackGenius.UI.Logging.LoggingBootstrapper`. Logs are split by domain into rolling files under `<appbase>/logs` (`communication-*`, `user-behavior-*`, `application-*`, `error-*`). The full taxonomy — category prefixes (`TrackGenius.Communication.*`, `TrackGenius.UserBehavior.*`, `TrackGenius.App.*`), level policy, PascalCase event names, and required structured properties — is defined in `source/doc/Logging.md`. **Use structured logging with named placeholders and stable event names**, e.g. `logger.LogInformation("PortOpenRequested PortName={PortName} ProtocolName={ProtocolName}", portName, protocolName)`.

## UI / MVVM Conventions

- Shell: `MainForm` is a WPF-UI `FluentWindow` with a `NavigationView`. Pages (`QuickRacePage`, `SettingsPage`) are instantiated by the NavigationView via parameterless ctor, then have their ViewModel injected in `MainForm.OnNavigated`.
- ViewModels implement `INotifyPropertyChanged` by hand, using the `PropertyChanged.RaiseIfChanged(...)` helper for property setters.
- Commands are the custom `RelayCommand` / `RelayCommand<T>` in `TrackGenius.UI.Commands` (not a framework command library).
- Appearance/theme is applied via `Wpf.Ui.Appearance.ApplicationThemeManager`.

## Workflow Notes

- **Default integration branch is `develop`** (not `main`/`master`). Open PRs against `develop`.
- `doc/CurrentToDo.md` and `doc/mappedTasks.md` track known issues and a 14-step priority list. `CurrentToDo.md` was last revalidated on an older branch and overstates what's still broken — several items (serial read fix, `MessageReceived` wiring, constructor decoupling, `RaceTimer` Stopwatch, partial composition root) are already resolved. **Verify against current code before treating a "Still exists" item as real.**
