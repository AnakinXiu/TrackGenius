# TrackGenius TODO Mapping (Current Branch)

Source: `doc/CurrentToDo.md`  
Branch: `FixCommunicatePipeline`

## Updated Priority Order Mapping (1-14)

| # | TODO step | Status | Exact file(s) checked |
|---|---|---|---|
| 1 | Fix `SerialPortWrapper.ReadBytes()` copy logic | Done | `TrackGenius.Communication/SerialPortWrapper.cs` (`Array.Copy(_buffer, result, dataLength)` present) |
| 2 | Raise `MessageReceived` in `CommunicateService.OnDataReceived()` | Pending | `TrackGenius.Communication/CommunicateService.cs` (message enqueued only; no event invoke) |
| 3 | Connect message-consumer output to `RaceEngine.OnCarDetected()` | Pending | `TrackGenius.Core/RobitronicMessageConsumer.cs`, `TrackGenius.Core/RaceEngine.cs` |
| 4 | Stop silently swallowing global UI exceptions | Pending | `TrackGenius/App.xaml.cs` (`e.Handled = true` only) |
| 5 | Implement `Driver.DriverID` and `Driver.NickName` | Pending | `TrackGenius.Model/Entity/Driver.cs` (`NotImplementedException`) |
| 6 | Replace `DateTime.Now.Ticks` with `Stopwatch` in `RaceTimer` | Pending | `TrackGenius.Model/Race/RaceTimer.cs` |
| 7 | Improve `Race.UpdateRaceStatus()` for timing and duplicate filtering | Pending | `TrackGenius.Model/Race/Race.cs` |
| 8 | Refactor constructors to accept dependencies | Pending | `TrackGenius.Communication/CommunicateService.cs`, `TrackGenius.Core/RaceEngine.cs`, `TrackGenius.Model/Race/Race.cs` |
| 9 | Add a composition root in `App.xaml.cs` | Pending | `TrackGenius/App.xaml.cs` |
| 10 | Remove direct service creation from UI code | Pending | `TrackGenius/Views/MainForm.xaml.cs`, `TrackGenius/ViewModels/MainFormParamViewModel.cs` |
| 11 | Remove unnecessary `System.*` package references from library projects | Pending | `TrackGenius.Communication/TrackGenius.Communication.csproj` |
| 12 | Standardize tests on NUnit only | Pending | `TrackGenius.ConstTests/TrackGenius.ConstTests.csproj`, `TrackGenius.ProtocolTests/TrackGenius.ProtocolTests.csproj` |
| 13 | Remove legacy SDK-style test project settings | Pending | Same two test project files above |
| 14 | Add tests for `Communication`, `Core`, and `Model` | Pending | No dedicated test projects currently present in solution |

## Additional Revalidation Notes

- `App.xaml` class reference is already correct: `x:Class="TrackGenius.App"` (`TrackGenius/App.xaml`) � **Done**.
- `Protocal` naming issue appears resolved (no remaining `Protocal` references found) � **Done**.
- Parameterless argument exceptions still exist and should be updated with parameter names � **Pending**:
  - `TrackGenius.Const/ByteArrayExtension.cs`
  - `TrackGenius.Protocol/Parser/RobitronicMessageParser.cs`
  - `TrackGenius.Protocol/Robitronic/DetectedMessage.cs`
  - `TrackGenius.Protocol/Robitronic/TimeStampMessage.cs`
