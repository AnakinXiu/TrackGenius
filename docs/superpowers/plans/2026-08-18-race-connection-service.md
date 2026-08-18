# RaceConnectionService Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A shared, observable `RaceConnectionService` owns port open/close and exposes the current protocol + port status, so the race engine resolves the protocol-correct `IMessageConsumer` and the race can only start when the connection is ready (UI hint + engine guard).

**Architecture:** New service in `TrackGenius.Communication` (lowest layer visible to both `Core` and `UI`), wrapping `CommunicateService` as the single write point. New protocol-keyed `MessageConsumerFactory` in `TrackGenius.Core` replaces the hard-coded `RobitronicMessageConsumer` in `RaceEngineFactory`. Composition stays manual in `App.xaml.cs`/`MainForm` (spec §3: no DI framework — constructor injection already used throughout; revisit triggers listed there).

**Tech Stack:** .NET 8 WPF, NUnit (existing `TrackGenius.UITests`, 39 tests green baseline), NSubstitute available but not required (tests use a hand-rolled `FakeSerialPortWrapper`).

## Global Constraints

- Target frameworks unchanged (`net8.0`/`net8.0-windows`); no `global.json`.
- New-file namespaces: `TrackGenius.Communication` (service), `TrackGenius.Core` (factories), tests under `TrackGenius.UITests.Communication` / `.Core`. File-scoped namespaces for new files.
- INPC style: existing `PropertyChanged.RaiseIfChanged(...)` helper from `TrackGenius.Const` where a backing field changes; explicit `PropertyChanged?.Invoke` for computed multi-property notifications is acceptable (match `RaceConnectionService` code below).
- No `<Nullable>` enable (CS8632 tolerated).
- Commit message ends with trailer `Co-Authored-By: Claude <noreply@anthropic.com>`. **Never commit `CLAUDE.md`** — stage explicit paths only.
- **Build lock:** if `dotnet build`/`test` fails with `MSB3021`/`MSB3027` ("file locked by TrackGenius"), the app is running — report BLOCKED with that error; do NOT commit unverified code.
- **NuGet:** a private source needs auth (401); if restore fails, retry with `--ignore-failed-sources`.
- Focused test command: `dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj`. Baseline: 39 passing; 2 failures in `TrackGenius.ProtocolTests` are PRE-EXISTING (existing `DetectedMessage` parser bug) — out of scope, do not fix or flag.
- Manual GUI verification (`dotnet run`) is the user's; agents must not launch the app.

---

## Task 1: `RaceConnectionService` + fake wrapper (TDD)

**Files:**
- Create: `source/TrackGenius.Communication/IRaceConnectionService.cs`
- Create: `source/TrackGenius.Communication/RaceConnectionService.cs`
- Test: `source/TrackGenius.UITests/Communication/FakeSerialPortWrapper.cs`
- Test: `source/TrackGenius.UITests/Communication/RaceConnectionServiceTests.cs`

**Interfaces (produced; consumed by Tasks 3–4):**
```csharp
// TrackGenius.Communication
public interface IRaceConnectionService : INotifyPropertyChanged
{
    IProtocol CurrentProtocol { get; }   // null when closed
    string CurrentProtocolName { get; }  // "" when closed
    string PortName { get; }             // "" when closed
    bool IsPortOpen { get; }
    bool CanStartRace { get; }           // IsPortOpen && CurrentProtocol != null
    void Open(string portName, IProtocol protocol); // records state on success, throws on failure
    void Close();                                    // clears state
}
```

- [ ] **Step 1: Write the fake wrapper (test infrastructure)**

`source/TrackGenius.UITests/Communication/FakeSerialPortWrapper.cs`:

```csharp
using System;
using RJCP.IO.Ports;
using TrackGenius.Communication;

namespace TrackGenius.UITests.Communication;

/// <summary>In-memory ISerialPortWrapper so the real CommunicateService can be exercised without hardware.</summary>
public sealed class FakeSerialPortWrapper : ISerialPortWrapper
{
    public string Name => "Fake";

    public bool IsOpened { get; private set; }

    public string LastOpenedPortName { get; private set; } = string.Empty;

    /// <summary>When true, OpenPort throws, simulating a device/driver failure.</summary>
    public bool FailOnOpen { get; set; }

    public event DataReceivedEventHandler DataReceived;

    public void OpenPort(string portName, int baud, int data, Parity parity, StopBits stopBits)
    {
        if (FailOnOpen)
            throw new InvalidOperationException("Simulated port open failure.");

        LastOpenedPortName = portName;
        IsOpened = true;
    }

    public void ClosePort() => IsOpened = false;

    public void SendBytes(byte[] sendData) { }

    public void RaiseDataReceived(DataReceivedArgs args) => DataReceived?.Invoke(this, args);
}
```

- [ ] **Step 2: Write the failing tests**

`source/TrackGenius.UITests/Communication/RaceConnectionServiceTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using TrackGenius.Communication;
using TrackGenius.Protocol.Kyosho;
using TrackGenius.Protocol.Robitronic;

namespace TrackGenius.UITests.Communication;

[TestFixture]
public class RaceConnectionServiceTests
{
    [Test]
    public void GivenClosedConnection_WhenCreated_ThenDefaultsAreClosedAndCannotStartRace()
    {
        var service = CreateService();

        Assert.Multiple(() =>
        {
            Assert.That(service.IsPortOpen, Is.False);
            Assert.That(service.CurrentProtocol, Is.Null);
            Assert.That(service.CurrentProtocolName, Is.Empty);
            Assert.That(service.PortName, Is.Empty);
            Assert.That(service.CanStartRace, Is.False);
        });
    }

    [Test]
    public void GivenClosedConnection_WhenOpenSucceeds_ThenProtocolPortRecordedAndCanStartRace()
    {
        var service = CreateService();

        service.Open("COM3", new RobitronicProtocol());

        Assert.Multiple(() =>
        {
            Assert.That(service.IsPortOpen, Is.True);
            Assert.That(service.CurrentProtocol, Is.InstanceOf<RobitronicProtocol>());
            Assert.That(service.CurrentProtocolName, Is.EqualTo("Robitronic"));
            Assert.That(service.PortName, Is.EqualTo("COM3"));
            Assert.That(service.CanStartRace, Is.True);
        });
    }

    [Test]
    public void GivenOpenConnection_WhenClosed_ThenStateClearedAndCannotStartRace()
    {
        var service = CreateService();
        service.Open("COM3", new RobitronicProtocol());

        service.Close();

        Assert.Multiple(() =>
        {
            Assert.That(service.IsPortOpen, Is.False);
            Assert.That(service.CurrentProtocol, Is.Null);
            Assert.That(service.CurrentProtocolName, Is.Empty);
            Assert.That(service.PortName, Is.Empty);
            Assert.That(service.CanStartRace, Is.False);
        });
    }

    [Test]
    public void GivenOpenConnection_WhenReopenedWithDifferentProtocol_ThenCurrentProtocolFollowsNewSelection()
    {
        var service = CreateService();
        service.Open("COM3", new RobitronicProtocol());

        service.Open("COM3", new KyoshoProtocol());

        Assert.That(service.CurrentProtocolName, Is.EqualTo("Kyosho"));
    }

    [Test]
    public void GivenClosedConnection_WhenOpenFails_ThenStateUnchangedAndCannotStartRace()
    {
        var (service, wrapper) = CreateServiceWithWrapper();
        wrapper.FailOnOpen = true;

        Assert.Throws<InvalidOperationException>(() => service.Open("COM3", new RobitronicProtocol()));

        Assert.Multiple(() =>
        {
            Assert.That(service.IsPortOpen, Is.False);
            Assert.That(service.CurrentProtocol, Is.Null);
            Assert.That(service.CanStartRace, Is.False);
        });
    }

    [Test]
    public void GivenService_WhenPortOpens_ThenPropertyChangedRaisedForStatusProperties()
    {
        var service = CreateService();
        var raised = new List<string>();
        service.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        service.Open("COM3", new RobitronicProtocol());

        Assert.That(raised, Does.Contain(nameof(RaceConnectionService.IsPortOpen)));
        Assert.That(raised, Does.Contain(nameof(RaceConnectionService.CanStartRace)));
    }

    private static RaceConnectionService CreateService() => CreateServiceWithWrapper().Service;

    private static (RaceConnectionService Service, FakeSerialPortWrapper Wrapper) CreateServiceWithWrapper()
    {
        var wrapper = new FakeSerialPortWrapper();
        var communicateService = new CommunicateService(wrapper, NullLogger<CommunicateService>.Instance);
        return (new RaceConnectionService(communicateService), wrapper);
    }
}
```

- [ ] **Step 3: Run to verify failure**

Run: `dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj`
Expected: FAIL — `RaceConnectionService`/`IRaceConnectionService` do not exist (compile error).

- [ ] **Step 4: Implement**

`source/TrackGenius.Communication/IRaceConnectionService.cs`:

```csharp
using System.ComponentModel;
using TrackGenius.Protocol.Interfaces;

namespace TrackGenius.Communication;

/// <summary>
/// Shared, observable state of the race-timing connection: the protocol of the
/// currently open port and whether a race can be started. Single write point is
/// <see cref="RaceConnectionService.Open"/>/<see cref="RaceConnectionService.Close"/>.
/// </summary>
public interface IRaceConnectionService : INotifyPropertyChanged
{
    IProtocol CurrentProtocol { get; }

    string CurrentProtocolName { get; }

    string PortName { get; }

    bool IsPortOpen { get; }

    bool CanStartRace { get; }

    void Open(string portName, IProtocol protocol);

    void Close();
}
```

`source/TrackGenius.Communication/RaceConnectionService.cs`:

```csharp
using System;
using System.ComponentModel;
using JetBrains.Annotations;
using TrackGenius.Protocol.Interfaces;

namespace TrackGenius.Communication;

public class RaceConnectionService : IRaceConnectionService
{
    private readonly CommunicateService _communicateService;

    public event PropertyChangedEventHandler PropertyChanged;

    public RaceConnectionService([NotNull] CommunicateService communicateService)
    {
        _communicateService = communicateService ?? throw new ArgumentNullException(nameof(communicateService));
        _communicateService.PortOpenStateEventHandler += OnPortOpenStateChanged;
    }

    public IProtocol CurrentProtocol { get; private set; }

    public string CurrentProtocolName => CurrentProtocol?.ProtocolName ?? string.Empty;

    public string PortName { get; private set; } = string.Empty;

    // Delegates to CommunicateService so unplugged-device state can never go stale.
    public bool IsPortOpen => _communicateService.IsOpened;

    public bool CanStartRace => IsPortOpen && CurrentProtocol != null;

    public void Open([NotNull] string portName, [NotNull] IProtocol protocol)
    {
        if (string.IsNullOrWhiteSpace(portName))
            throw new ArgumentException("Port name cannot be null or empty.", nameof(portName));
        if (protocol == null)
            throw new ArgumentNullException(nameof(protocol));

        _communicateService.StartService(portName, protocol);

        // StartService throws on failure; reaching here means the port opened with this protocol.
        CurrentProtocol = protocol;
        PortName = portName;
        RaisePropertiesChanged();
    }

    public void Close()
    {
        _communicateService.CloseService();

        CurrentProtocol = null;
        PortName = string.Empty;
        RaisePropertiesChanged();
    }

    private void OnPortOpenStateChanged(object sender, EventArgs e)
    {
        // Propagate external state changes (e.g. unexpected close) to subscribers.
        if (!_communicateService.IsOpened)
        {
            CurrentProtocol = null;
            PortName = string.Empty;
        }

        RaisePropertiesChanged();
    }

    private void RaisePropertiesChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPortOpen)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanStartRace)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentProtocol)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentProtocolName)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PortName)));
    }
}
```

- [ ] **Step 5: Run tests → green**

Run: `dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj`
Expected: PASS — all previous 39 + 6 new tests.

- [ ] **Step 6: Commit**

```bash
git add source/TrackGenius.Communication/IRaceConnectionService.cs source/TrackGenius.Communication/RaceConnectionService.cs source/TrackGenius.UITests/Communication
git commit -m "Add observable RaceConnectionService for shared protocol/port state"
```

---

## Task 2: `MessageConsumerFactory` (TDD)

**Files:**
- Create: `source/TrackGenius.Core/MessageConsumerFactory.cs`
- Test: `source/TrackGenius.UITests/Core/MessageConsumerFactoryTests.cs`

**Interfaces (produced; consumed by Task 3):** `TrackGenius.Core.MessageConsumerFactory.Create(IProtocol)` → `IMessageConsumer`; `"Robitronic"` → `RobitronicMessageConsumer`; unknown → `NotSupportedException`; null → `ArgumentNullException`.

- [ ] **Step 1: Write the failing test**

`source/TrackGenius.UITests/Core/MessageConsumerFactoryTests.cs`:

```csharp
using System;
using NUnit.Framework;
using TrackGenius.Core;
using TrackGenius.Protocol.Kyosho;
using TrackGenius.Protocol.Robitronic;

namespace TrackGenius.UITests.Core;

[TestFixture]
public class MessageConsumerFactoryTests
{
    private readonly MessageConsumerFactory _factory = new();

    [Test]
    public void GivenRobitronicProtocol_WhenCreated_ThenRobitronicConsumerReturned()
        => Assert.That(_factory.Create(new RobitronicProtocol()), Is.InstanceOf<RobitronicMessageConsumer>());

    [Test]
    public void GivenUnsupportedProtocol_WhenCreated_ThenNotSupportedExceptionThrown()
    {
        var kyosho = new KyoshoProtocol();
        Assert.Throws<NotSupportedException>(() => _factory.Create(kyosho),
            $"No message consumer is registered for protocol '{kyosho.ProtocolName}'.");
    }

    [Test]
    public void GivenNullProtocol_WhenCreated_ThenArgumentNullExceptionThrown()
        => Assert.Throws<ArgumentNullException>(() => _factory.Create(null));
}
```

- [ ] **Step 2: Run → fail** (type missing)

- [ ] **Step 3: Implement**

`source/TrackGenius.Core/MessageConsumerFactory.cs`:

```csharp
using System;
using JetBrains.Annotations;
using TrackGenius.Protocol.Interfaces;

namespace TrackGenius.Core;

/// <summary>
/// Maps the connection's current protocol to the message consumer that understands
/// its messages. Adding a protocol later = one case here + its consumer class.
/// </summary>
public class MessageConsumerFactory
{
    public IMessageConsumer Create([NotNull] IProtocol protocol)
    {
        if (protocol == null)
            throw new ArgumentNullException(nameof(protocol));

        return protocol.ProtocolName switch
        {
            "Robitronic" => new RobitronicMessageConsumer(),
            _ => throw new NotSupportedException(
                $"No message consumer is registered for protocol '{protocol.ProtocolName}'."),
        };
    }
}
```

- [ ] **Step 4: Run → green**

- [ ] **Step 5: Commit**

```bash
git add source/TrackGenius.Core/MessageConsumerFactory.cs source/TrackGenius.UITests/Core/MessageConsumerFactoryTests.cs
git commit -m "Add protocol-keyed MessageConsumerFactory"
```

---

## Task 3: `RaceEngineFactory` guard + protocol-based consumer (TDD)

**Files:**
- Modify: `source/TrackGenius.Core/RaceEngineFactory.cs` (full replacement below)
- Test: `source/TrackGenius.UITests/Core/RaceEngineFactoryTests.cs`

**Interfaces (consumed):** Task 1's `IRaceConnectionService`/`RaceConnectionService` + `FakeSerialPortWrapper`; Task 2's `MessageConsumerFactory`. Note the signature change: `RaceEngineFactory(IRaceConnectionService, CommunicateService)` — Task 4 updates its only call site.

- [ ] **Step 1: Write the failing test**

`source/TrackGenius.UITests/Core/RaceEngineFactoryTests.cs`:

```csharp
using System;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using TrackGenius.Communication;
using TrackGenius.Protocol.Kyosho;
using TrackGenius.Protocol.Robitronic;
using TrackGenius.UITests.Communication;

namespace TrackGenius.UITests.Core;

[TestFixture]
public class RaceEngineFactoryTests
{
    [Test]
    public void GivenNoOpenConnection_WhenCreateRaceEngine_ThenInvalidOperationExceptionThrown()
    {
        var factory = CreateFactory(out _);

        Assert.Throws<InvalidOperationException>(() => factory.CreateRaceEngine());
    }

    [Test]
    public void GivenOpenRobitronicConnection_WhenCreateRaceEngine_ThenEngineCreated()
    {
        var factory = CreateFactory(out var connection);
        connection.Open("COM3", new RobitronicProtocol());

        var engine = factory.CreateRaceEngine();

        Assert.That(engine, Is.Not.Null);
        engine.Dispose();
        connection.Close();
    }

    [Test]
    public void GivenOpenUnsupportedProtocol_WhenCreateRaceEngine_ThenNotSupportedExceptionThrown()
    {
        var factory = CreateFactory(out var connection);
        connection.Open("COM3", new KyoshoProtocol());

        Assert.Throws<NotSupportedException>(() => factory.CreateRaceEngine());

        connection.Close();
    }

    private static RaceEngineFactory CreateFactory(out RaceConnectionService connection)
    {
        var wrapper = new FakeSerialPortWrapper();
        var communicateService = new CommunicateService(wrapper, NullLogger<CommunicateService>.Instance);
        connection = new RaceConnectionService(communicateService);
        return new RaceEngineFactory(connection, communicateService);
    }
}
```

- [ ] **Step 2: Run → fail** (constructor mismatch: current factory takes only `CommunicateService`)

- [ ] **Step 3: Implement — replace `RaceEngineFactory.cs` in full**

```csharp
using System;
using JetBrains.Annotations;
using TrackGenius.Communication;

namespace TrackGenius.Core;

public class RaceEngineFactory
{
    private readonly IRaceConnectionService _connectionService;
    private readonly CommunicateService _communicateService;
    private readonly MessageConsumerFactory _messageConsumerFactory = new();

    public RaceEngineFactory([NotNull] IRaceConnectionService connectionService,
                             [NotNull] CommunicateService communicateService)
    {
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        _communicateService = communicateService ?? throw new ArgumentNullException(nameof(communicateService));
    }

    public RaceEngine CreateRaceEngine()
    {
        if (!_connectionService.CanStartRace)
            throw new InvalidOperationException(
                $"The race cannot be started: the serial port is not open with a valid protocol (Port='{_connectionService.PortName}', Protocol='{_connectionService.CurrentProtocolName}').");

        var messageConsumer = _messageConsumerFactory.Create(_connectionService.CurrentProtocol);
        return new RaceEngine(messageConsumer, _communicateService);
    }
}
```

- [ ] **Step 4: Run → green** (39 + 6 + 3 + 3 new tests passing)

- [ ] **Step 5: Commit**

```bash
git add source/TrackGenius.Core/RaceEngineFactory.cs source/TrackGenius.UITests/Core/RaceEngineFactoryTests.cs
git commit -m "Gate RaceEngineFactory on connection state and select consumer by protocol"
```

---

## Task 4: UI wiring + composition

No unit tests (UI layer; no harness — consistent with existing practice). Gate = build succeeds with 0 errors. Read each file before editing; apply the exact transformations below.

**Files:**
- Modify: `source/TrackGenius/ViewModels/SettingPageViewModel.cs`
- Modify: `source/TrackGenius/ViewModels/RacePageViewModel.cs`
- Modify: `source/TrackGenius/Views/Pages/QuickRacePage.xaml`
- Modify: `source/TrackGenius/Views/MainForm.xaml.cs`
- Modify: `source/TrackGenius/App.xaml.cs` (`CreateMainWindow`)

**Interfaces (consumed):** Task 1 `IRaceConnectionService`; Task 3 `RaceEngineFactory(IRaceConnectionService, CommunicateService)`.

- [ ] **Step 1: `SettingPageViewModel` — route open/close through the service**

  1. Add field `private readonly IRaceConnectionService _connectionService;` and constructor parameter `IRaceConnectionService connectionService` (after `communicateService`), with the usual null guard, storing it. (`IRaceConnectionService` lives in `TrackGenius.Communication`, which this file already imports.)
  3. Replace the existing subscription block
     ```csharp
     _communicateService.PortOpenStateEventHandler += (_, _) =>
     {
         OnPropertyChanged(nameof(IsPortOpenedString));
         OnPropertyChanged(nameof(ButtonContent));
     };
     ```
     with
     ```csharp
     _connectionService.PropertyChanged += (_, e) =>
     {
         if (e.PropertyName is nameof(IRaceConnectionService.IsPortOpen)
                               or nameof(IRaceConnectionService.CanStartRace))
         {
             OnPropertyChanged(nameof(IsPortOpened));
             OnPropertyChanged(nameof(IsPortOpenedString));
             OnPropertyChanged(nameof(ButtonContent));
         }
     };
     ```
  4. In `OpenClosePort()`, replace `_communicateService.CloseService()` with `_connectionService.Close()` and `_communicateService.StartService(SelectedSerialPort.PortName, SelectedProtocol.Protocol)` with `_connectionService.Open(SelectedSerialPort.PortName, SelectedProtocol.Protocol)`. Keep logging, `Messages.Clear()`, `LastError`, polling start/stop, and the catch blocks exactly as they are (`InvalidOperationException`/`ArgumentException` still flow from the service).
  5. Change `IsPortOpened` to `public bool IsPortOpened => _connectionService.IsPortOpen;` (drop the `_communicateService?.IsOpened` form).

- [ ] **Step 2: `RacePageViewModel` — `CanStartRace` + engine lifetime fix**

  1. Add field `private readonly IRaceConnectionService _connectionService;` and constructor parameter `IRaceConnectionService connectionService` (after `raceEngineFactory`), null-guarded.
  2. Add property `public bool CanStartRace => _connectionService.CanStartRace;`
  3. In the constructor subscribe:
     ```csharp
     _connectionService.PropertyChanged += (_, e) =>
     {
         if (e.PropertyName is nameof(IRaceConnectionService.IsPortOpen)
                               or nameof(IRaceConnectionService.CanStartRace))
             OnPropertyChanged(nameof(CanStartRace));
     };
     ```
  4. Fix the engine-lifetime bug — replace
     ```csharp
     private void StartRace()
     {
         using var raceEngine = _raceEngineFactory.CreateRaceEngine();
         raceEngine.RaceStart(new List<RaceData>());
     }
     ```
     with
     ```csharp
     private RaceEngine _raceEngine;

     private void StartRace()
     {
         if (!CanStartRace)
             return;

         // Keep the engine alive for the race: disposing it immediately would
         // unsubscribe its handlers before any detection could arrive.
         _raceEngine?.Dispose();
         _raceEngine = _raceEngineFactory.CreateRaceEngine();
         _raceEngine.RaceStart(new List<RaceData>());
     }
     ```
     (`using System.ComponentModel;` etc. are already present; add `using TrackGenius.Communication;` if missing.)

- [ ] **Step 3: `QuickRacePage.xaml` — disable Start when not startable**

  Change the Start button to `<Button Content="Start" Command="{Binding StartRaceCommand}" IsEnabled="{Binding CanStartRace}"/>`.

- [ ] **Step 4: `MainForm.xaml.cs` — pass the service through**

  1. Constructor signature: `public MainForm(CommunicateService communicateService, IRaceConnectionService connectionService, ILogger userBehaviorLogger)` (null-guard `connectionService` like the others; keep the `_userBehaviorLogger`/`_comService` fields, adding a `_connectionService` field).
  2. Replace `var raceEngineFactory = new RaceEngineFactory( _comService);` with `var raceEngineFactory = new RaceEngineFactory(connectionService, _comService);`
  3. Replace the `_viewModel = new MainWindowViewModel(new RacePageViewModel(raceEngineFactory), new SettingPageViewModel(_comService, _userBehaviorLogger));` line with
     ```csharp
     _viewModel = new MainWindowViewModel(
         new RacePageViewModel(raceEngineFactory, connectionService),
         new SettingPageViewModel(_comService, connectionService, _userBehaviorLogger));
     ```

- [ ] **Step 5: `App.xaml.cs` — compose the service**

  In `CreateMainWindow()`, after `var communicateService = new CommunicateService(serialPortWrapper, serviceLogger);` add
  ```csharp
  var connectionService = new RaceConnectionService(communicateService);
  ```
  and change the return to `return new MainForm(communicateService, connectionService, userBehaviorLogger);`

- [ ] **Step 6: Build the whole solution**

Run: `dotnet build source/TrackGenius.sln`
Expected: 0 errors (pre-existing warnings only). Then run `dotnet test source/TrackGenius.UITests/TrackGenius.UITests.csproj` → all pass.

- [ ] **Step 7: Commit**

```bash
git add source/TrackGenius/ViewModels/SettingPageViewModel.cs source/TrackGenius/ViewModels/RacePageViewModel.cs source/TrackGenius/Views/Pages/QuickRacePage.xaml source/TrackGenius/Views/MainForm.xaml.cs source/TrackGenius/App.xaml.cs
git commit -m "Wire UI and composition root through RaceConnectionService"
```

- [ ] **Step 8: Manual verification (for the user — agents must not launch the app)**

  1. Run the app; on Quick Race the **Start button is disabled**.
  2. Settings → open a port (Robitronic) → Start button **enables** without page reload.
  3. Close the port → Start button disables again.
  4. Start a race with the port open → no error; detections still update standings (existing pipeline).
  5. Start with the port closed (should be impossible via UI; if forced via debugger) → `InvalidOperationException` with the Port/Protocol message.
