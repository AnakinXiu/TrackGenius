using System;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Serilog;
using TrackGenius.Communication;
using TrackGenius.Speech;
using TrackGenius.UI.Logging;
using TrackGenius.UI.Theme;
using TrackGenius.UI.ViewModels;
using Wpf.Ui.Controls;

namespace TrackGenius.UI
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private MainForm _mainForm;
        private LoggingContext _loggingContext;
        private ILogger<App> _logger;

        protected override void OnStartup(StartupEventArgs e)
        {
            _loggingContext = LoggingBootstrapper.Configure();
            _logger = _loggingContext.LoggerFactory.CreateLogger<App>();
            _logger.LogInformation("Application startup. SessionId={SessionId}, LogDirectory={LogDirectory}", _loggingContext.SessionId, _loggingContext.LogDirectory);

            base.OnStartup(e);

            //注册Application_Error
            this.DispatcherUnhandledException +=
                new DispatcherUnhandledExceptionEventHandler(App_DispatcherUnhandledException);

            _mainForm = CreateMainWindow();
            MainWindow = _mainForm;

            // Apply the persisted theme before first render: palette + Fluent theme + green accent.
            ThemeManager.Apply(ThemeManager.PreferencesStore.Load());

            _mainForm.Show();
        }

        protected void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
        }

        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);

            //TODO  your code
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _logger?.LogInformation("Application exiting.");
                _mainForm?.Close();
                base.OnExit(e);
            }
            finally
            {
                _loggingContext?.LoggerFactory.Dispose();
                Log.CloseAndFlush();
            }
        }

        protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
        {
            base.OnSessionEnding(e);

            //TODO  your code
        }

        void App_DispatcherUnhandledException(object sender,
            DispatcherUnhandledExceptionEventArgs e)
        {
            _logger?.LogError(e.Exception, "ApplicationUnhandledException");

            if (IsRecoverableException(e.Exception))
            {
                _logger?.LogWarning(e.Exception, "ApplicationRecoverableException");
                e.Handled = true;
                return;
            }

            var dialog = new Wpf.Ui.Controls.MessageBox
            {
                Title = "TrackGenius Error",
                Content = "An unexpected error occurred and the application needs to close.",
                CloseButtonText = "OK",
            };
            _ = dialog.ShowDialogAsync();

            e.Handled = false;
        }

        private static bool IsRecoverableException(Exception exception)
        {
            return exception is OperationCanceledException;
        }

        private MainForm CreateMainWindow()
        {
            var serialPortLogger = _loggingContext.LoggerFactory.CreateLogger<SerialPortWrapper>();
            var serviceLogger = _loggingContext.LoggerFactory.CreateLogger<CommunicateService>();
            var userBehaviorLogger = _loggingContext.LoggerFactory.CreateLogger("TrackGenius.UserBehavior.MainFormParamViewModel");

            var serialPortWrapper = new SerialPortWrapper(serialPortLogger);
            var communicateService = new CommunicateService(serialPortWrapper, serviceLogger);
            var connectionService = new RaceConnectionService(communicateService);

            // Speech pipeline (interface phase): composed but disabled — no real TTS backend yet.
            var speechAnnouncer = new RaceAnnouncer(
                new AnnouncementScheduler(),
                new SpeechTemplateRenderer(),
                new AnnouncementPolicy(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(15), 20),
                new SpeechQueue(20, _loggingContext.LoggerFactory.CreateLogger<SpeechQueue>()),
                new TrackGenius.Speech.Fakes.FakeTtsEngine(),
                new TrackGenius.Speech.Fakes.FakeAudioPlayer(),
                _loggingContext.LoggerFactory.CreateLogger<RaceAnnouncer>())
            {
                Enabled = false,
            };

            return new MainForm(communicateService, connectionService, userBehaviorLogger, speechAnnouncer);
        }
    }
}
