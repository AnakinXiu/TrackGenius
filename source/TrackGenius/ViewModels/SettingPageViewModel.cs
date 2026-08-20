using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TrackGenius.Communication;
using TrackGenius.Communication.interfaces;
using TrackGenius.Const;
using TrackGenius.Protocol.Interfaces;
using TrackGenius.UI.Commands;
using Wpf.Ui.Appearance;

namespace TrackGenius.UI.ViewModels;

public class SettingPageViewModel : INotifyPropertyChanged
{
    public sealed record ProtocolOption(string Name, IProtocol Protocol);

    private ISerialPortDescription _selectedSerialPort;
    private readonly CommunicateService _communicateService;
    private readonly IRaceConnectionService _connectionService;
    private readonly ILogger _userBehaviorLogger;
    private CancellationTokenSource _messagePollingCancellationTokenSource;
    private Task _messagePollingTask;
    private ThemeType _selectedTheme;
    private ProtocolOption _selectedProtocol;
    private string _lastError;

    public List<ThemeType> Themes { get; } = Enum.GetValues(typeof(ThemeType)).Cast<ThemeType>().ToList();  

    public List<ISerialPortDescription> SerialPorts { get; }

    public List<ProtocolOption> Protocols { get; }

    public ObservableCollection<string> Messages { get; } = [];

    public string LastError
    {
        get => _lastError;
        private set => PropertyChanged.RaiseIfChanged(this, ref _lastError, value, Equals, nameof(LastError));
    }

    public bool IsPortOpened => _connectionService.IsPortOpen;

    public string ButtonContent => IsPortOpened ? "Close Port" : "Open Port";

    public string IsPortOpenedString => IsPortOpened ? "Opened" : "Closed";

    public ISerialPortDescription SelectedSerialPort
    {
        get => _selectedSerialPort;
        set => PropertyChanged.RaiseIfChanged(this, ref _selectedSerialPort, value, Equals, nameof(SelectedSerialPort));
    }

    public ICommand OpenClosePortCommand { get; }

    public ProtocolOption SelectedProtocol
    {
        get => _selectedProtocol;
        set => PropertyChanged.RaiseIfChanged(this, ref _selectedProtocol, value, Equals, nameof(SelectedProtocol));
    }

    public ThemeType SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (PropertyChanged.RaiseIfChanged(this, ref _selectedTheme, value, nameof(SelectedTheme)))
            {
                ApplyTheme(value);
            }
        }
    }

    public SettingPageViewModel(CommunicateService communicateService, IRaceConnectionService connectionService, ILogger userBehaviorLogger)
    {
        _communicateService = communicateService ?? throw new ArgumentNullException(nameof(communicateService));
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        _userBehaviorLogger = userBehaviorLogger ?? NullLogger.Instance;

        _userBehaviorLogger.LogInformation("ViewModelInitialized ViewModel={ViewModel}", nameof(SettingPageViewModel));

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

        SerialPorts = SerialPortEnumerator.GetValidPortDescriptions().ToList();
        Protocols = Protocol.Protocols.AvailableProtocols.Select(p => new ProtocolOption(p.ProtocolName, p)).ToList();
        _selectedProtocol = Protocols.First();
        OpenClosePortCommand = new RelayCommand(OpenClosePort);

        _selectedTheme = GetThemeTypeFromCurrentTheme();
    }

    private static ThemeType GetThemeTypeFromCurrentTheme()
    {
        return ApplicationThemeManager.GetAppTheme() switch
        {
            ApplicationTheme.Dark => ThemeType.Dark,
            _ => ThemeType.Light,
        };
    }

    private static void ApplyTheme(ThemeType theme)
    {
        var applicationTheme = theme switch
        {
            ThemeType.Dark => ApplicationTheme.Dark,
            _ => ApplicationTheme.Light,
        };

        ApplicationThemeManager.Apply(applicationTheme);
    }

    private void OpenClosePort()
    {
        if (SelectedSerialPort == null)
            return;

        if (SelectedProtocol?.Protocol == null)
            return;

        _userBehaviorLogger.LogInformation(
            "UserOpenClosePortRequested ActionName={ActionName} PortName={PortName} ProtocolName={ProtocolName} IsPortOpened={IsPortOpened}",
            nameof(OpenClosePort),
            SelectedSerialPort.PortName,
            SelectedProtocol.Name,
            _communicateService.IsOpened);

        StopMessagePolling();

        try
        {
            if (_communicateService.IsOpened)
            {
                _connectionService.Close();
                _userBehaviorLogger.LogInformation("UserPortClosed ActionName={ActionName} PortName={PortName}", nameof(OpenClosePort), SelectedSerialPort.PortName);
            }
            else
            {
                _connectionService.Open(SelectedSerialPort.PortName, SelectedProtocol.Protocol);
                Messages.Clear();
                LastError = string.Empty;
                StartMessagePolling();
                _userBehaviorLogger.LogInformation(
                    "UserPortOpened ActionName={ActionName} PortName={PortName} ProtocolName={ProtocolName}",
                    nameof(OpenClosePort),
                    SelectedSerialPort.PortName,
                    SelectedProtocol.Name);
            }
        }
        catch (ArgumentException ex)
        {
            HandleCommunicationError(ex, nameof(OpenClosePort));
        }
        catch (ObjectDisposedException ex)
        {
            HandleCommunicationError(ex, nameof(OpenClosePort));
        }
        catch (InvalidOperationException ex)
        {
            HandleCommunicationError(ex, nameof(OpenClosePort));
        }
    }

    private void StartMessagePolling()
    {
        _messagePollingCancellationTokenSource = new CancellationTokenSource();
        _messagePollingTask = PollMessageAsync(_messagePollingCancellationTokenSource.Token);
        _userBehaviorLogger.LogDebug("MessagePollingStarted");
    }

    private async Task PollMessageAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_communicateService.TryGetNextMessage(out var message))
                {
                    var messageText = message.Deserialize();
                    if (Application.Current?.Dispatcher is { } dispatcher)
                    {
                        dispatcher.BeginInvoke(() => Messages.Add(messageText));
                    }
                    else
                    {
                        Messages.Add(messageText);
                    }

                    continue;
                }

                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException ex)
        {
            HandleCommunicationError(ex, nameof(PollMessageAsync));
        }
        catch (InvalidOperationException ex)
        {
            HandleCommunicationError(ex, nameof(PollMessageAsync));
        }
    }

    private void StopMessagePolling()
    {
        var cts = _messagePollingCancellationTokenSource;
        _messagePollingCancellationTokenSource = null;

        if (cts != null)
        {
            cts.Cancel();
            cts.Dispose();
        }

        _messagePollingTask = null;
        _userBehaviorLogger.LogDebug("MessagePollingStopped");
    }

    private void HandleCommunicationError(Exception exception, string actionName)
    {
        _userBehaviorLogger.LogError(
            exception,
            "UserCommunicationActionFailed ActionName={ActionName} ViewModel={ViewModel}",
            actionName,
            nameof(SettingPageViewModel));

        LastError = exception.Message;

        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            dispatcher.BeginInvoke(() => Messages.Add(exception.Message));
            return;
        }

        Messages.Add(exception.Message);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}