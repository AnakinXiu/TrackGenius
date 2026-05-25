# TrackGenius - Current Revalidation & Improvement Guide

## Validation Summary

This file has been updated after comparing the previous AI investigation with the current codebase.

### Confirmed current state

- The solution now targets `.NET 8`.
- The `Protocol` project/folder naming appears corrected.
- The main functional, reliability, and constructor-level design issues are still present.

---

## Revalidation Result of Previous Issues

| # | Issue | Current Status | Notes |
|---|-------|----------------|-------|
| 1 | Runtime pipeline is not connected | Still exists | `CommunicateService` still does not raise `MessageReceived` after parsing, and `RaceEngine.OnCarDetected` is still not connected to the detection flow. |
| 2 | Naming / config inconsistencies | Partially resolved | `App.xaml` is now correct (`TrackGenius.App`). `Protocol` naming is corrected. `SplitChar` usage was not revalidated here. |
| 3 | Legacy / duplicate package references | Still exists | `TrackGenius.Communication.csproj` still contains many unnecessary `System.*` package references. Test projects still mix NUnit and MSTest and keep legacy test project properties. |
| 4 | Incomplete domain model | Still exists | `Driver.DriverID` and `Driver.NickName` still throw `NotImplementedException`. `Race.UpdateRaceStatus` is still minimal. `RaceTimer` still uses `DateTime.Now.Ticks`. Concrete `Car` / `Transponder` implementations were not found during revalidation. |
| 5 | Error handling & reliability | Still exists | `SerialPortWrapper.ReadBytes` still copies into `result` incorrectly. `App.DispatcherUnhandledException` still swallows exceptions by setting `e.Handled = true`. Some parameterless argument exceptions still exist. |
| 6 | No dependency injection / composition root | Still exists | Constructors still create concrete dependencies directly instead of receiving abstractions. |
| 7 | Low test coverage | Still exists | Only `TrackGenius.ConstTests` and `TrackGenius.ProtocolTests` exist. No dedicated tests for `Communication`, `Core`, or `Model`. |

---

## Verified Current Findings

### 1. Runtime pipeline remains broken

### `TrackGenius.Communication/CommunicateService.cs`

Current behavior:
- Parses incoming bytes into a message.
- Enqueues the parsed message.
- Does **not** raise `MessageReceived`.

Impact:
- Consumers subscribed to `MessageReceived` never receive parsed messages.
- The live lap-counting pipeline cannot complete.

### `TrackGenius.Core/RaceEngine.cs`

Current behavior:
- Constructor subscribes `_communicateService.MessageReceived += _messageConsumer.ConsumeMessage;`
- `OnCarDetected` exists but is never subscribed to any consumer event.

Impact:
- Even if parsing works, race status updates are still not connected to detection output.

---

### 2. Reliability issue still exists in serial read path

### `TrackGenius.Communication/SerialPortWrapper.cs`

Current behavior:
- `ReadBytes()` allocates `result` with `dataLength`.
- Then calls `_buffer.CopyTo(result, dataLength);`

Why this is wrong:
- `dataLength` is used as the destination index, so copying starts beyond the beginning of the target buffer.
- This can cause incorrect data copy behavior and breaks incoming packet handling.

Recommended fix:
- Replace with `Array.Copy(_buffer, 0, result, 0, dataLength);`

---

### 3. Domain model is still incomplete

### `TrackGenius.Model/Entity/Driver.cs`

Current behavior:
- `DriverID` throws `NotImplementedException`
- `NickName` getter/setter throw `NotImplementedException`

Impact:
- Any constructor or workflow that touches these properties can fail at runtime.

### `TrackGenius.Model/Race/Race.cs`

Current behavior:
- `UpdateRaceStatus()` only increments `LapsCount`.

Missing behavior:
- Lap time calculation
- Duplicate detection protection
- `RacedTime` update

### `TrackGenius.Model/Race/RaceTimer.cs`

Current behavior:
- Uses `DateTime.Now.Ticks`

Risk:
- Not monotonic
- Lower-quality timing for race measurement than `Stopwatch`

---

### 4. Package and project-file cleanup is still needed

### `TrackGenius.Communication/TrackGenius.Communication.csproj`

Still contains unnecessary references for `.NET 8`, including many `System.*` packages.

### `TrackGenius.ConstTests/TrackGenius.ConstTests.csproj`
### `TrackGenius.ProtocolTests/TrackGenius.ProtocolTests.csproj`

Still contain:
- `MSTest.TestAdapter`
- `MSTest.TestFramework`
- Legacy test-project properties such as:
  - `<ProjectTypeGuids>`
  - `<VSToolsPath>`
  - `<ReferencePath>`
  - TeamTest import

Also still use old package versions such as:
- `Microsoft.NET.Test.Sdk` `16.0.1`
- `NUnit` `3.12.0`
- `Castle.Core` `4.4.0`

---

### 5. Exception handling remains weak

### `TrackGenius/App.xaml.cs`

Current behavior:
- Global dispatcher exception handler sets `e.Handled = true`
- No logging, rethrowing, or user-visible error reporting

Impact:
- Failures can be hidden during runtime
- Debugging production issues becomes harder

### `TrackGenius.Const/ByteArrayExtension.cs`

Parameterless `ArgumentNullException` / `ArgumentOutOfRangeException` patterns still appear to exist and should be updated with parameter names.

---

## Constructor-Level Analysis

The constructor design is still one of the main structural problems in the current project.

### 1. `CommunicateService` constructor is tightly coupled

Current constructor behavior:
- Accepts `IMessageParser`
- Internally creates `new SerialPortWrapper()`

Why this is a problem:
- Prevents injecting a fake or mock serial port in tests
- Couples message handling to one concrete transport implementation
- Makes the communication layer harder to verify in isolation

Advice:
- Change the constructor to accept an `ISerialPortWrapper` dependency.
- Keep the parser dependency injection that already exists.

Preferred direction:
- `CommunicateService(IMessageParser messageParser, ISerialPortWrapper serialPortWrapper)`

---

### 2. `RaceEngine` constructor still creates infrastructure directly

Current constructor behavior:
- Accepts `IMessageConsumer`
- Still creates `new CommunicateService(new RobitronicMessageParser())`

Why this is a problem:
- Mixes orchestration logic with infrastructure creation
- Hardcodes a specific protocol parser inside the engine
- Makes testing and protocol replacement difficult

Advice:
- Inject `CommunicateService` or abstractions for both communication and parser selection.
- Keep `RaceEngine` focused on race orchestration, not object creation.

Preferred direction:
- `RaceEngine(IMessageConsumer messageConsumer, CommunicateService communicateService)`
- Or better, depend on interfaces for communication and message dispatch.

---

### 3. `Race` constructor hardcodes `RaceTimer`

Current constructor behavior:
- Creates `new RaceTimer(10)` internally

Why this is a problem:
- Countdown value is hidden and fixed at construction time
- Prevents tests from controlling race timing behavior
- Binds the domain object to one concrete timer setup

Advice:
- Accept a `RaceTimer` or countdown configuration through the constructor.
- Keep default creation outside the entity when possible.

Preferred direction:
- `Race(Guid raceID, RaceType raceType, RaceClass raceClass, ICollection<RaceStatus> racersCollection, RaceTimer raceTimer)`
- Or pass `countDownTime` as an explicit constructor argument and build the timer in a higher layer.

---

### 4. UI still constructs services directly

### `TrackGenius/Views/MainForm.xaml.cs`

Current behavior:
- Creates `CommunicateService` directly in command handler
- Resolves parser from `MessageParserFactory` directly in the view

Why this is a problem:
- UI becomes responsible for infrastructure decisions
- Harder to test and harder to replace protocols cleanly

Advice:
- Move service construction to application startup / composition root.
- Pass dependencies into the window or its view model.

---

## Updated Priority Order

### Priority 1 - Fix immediately

1. Fix `SerialPortWrapper.ReadBytes()` copy logic.
2. Raise `MessageReceived` in `CommunicateService.OnDataReceived()`.
3. Connect message-consumer output to `RaceEngine.OnCarDetected()`.
4. Stop silently swallowing global UI exceptions.

### Priority 2 - Remove runtime crash points

5. Implement `Driver.DriverID` and `Driver.NickName`.
6. Replace `DateTime.Now.Ticks` with `Stopwatch` in `RaceTimer`.
7. Improve `Race.UpdateRaceStatus()` for timing and duplicate filtering.

### Priority 3 - Improve construction and architecture

8. Refactor constructors to accept dependencies instead of creating them internally.
9. Add a composition root in `App.xaml.cs`.
10. Remove direct service creation from UI code.

### Priority 4 - Clean project files and tests

11. Remove unnecessary `System.*` package references from library projects.
12. Standardize tests on NUnit only.
13. Remove legacy SDK-style test project settings.
14. Add tests for `Communication`, `Core`, and `Model`.

---

## Practical Recommendation

If only one refactoring theme is addressed first, it should be **constructor decoupling**.

Reason:
- It unlocks testability.
- It makes the message pipeline easier to wire correctly.
- It reduces hidden coupling between UI, communication, parser, and race logic.
- It makes later fixes safer because objects can be verified in isolation.

Recommended first constructor refactor sequence:
1. `CommunicateService`
2. `RaceEngine`
3. `MainForm` / application startup
4. `Race`

---

## Current Overall Conclusion

The project has improved in one important area: it now targets `.NET 8`, and the earlier `App.xaml` / `Protocol` naming issues are no longer the main concern.

However, the previous investigation is still largely valid. The most important issues remain unresolved, especially:
- broken runtime message flow
- incorrect serial-buffer copy logic
- incomplete model implementation
- tight constructor coupling
- outdated and duplicated test/package configuration

The next work should focus on **fixing the runtime pipeline first**, then **constructor-level decoupling**, then **project cleanup and test expansion**.
