using System;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Serilog;
using TrackGenius.Communication;
using TrackGenius.Logging;
using TrackGenius.UI;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace TrackGenius
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        // Default appearance settings. These can be persisted to user settings later
        // and re-applied at startup to honour the user's preference.
        private const ApplicationTheme DefaultTheme = ApplicationTheme.Unknown; // System-follow
        private const WindowBackdropType DefaultBackdrop = WindowBackdropType.Mica;

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

            ApplyAppearance();

            _mainForm = CreateMainWindow();
            MainWindow = _mainForm;
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

            return new MainForm(communicateService, userBehaviorLogger);
        }

        private static void ApplyAppearance()
        {
            // Apply the configured theme. ApplicationTheme.Unknown means "follow the system theme".
            ApplicationThemeManager.Apply(DefaultTheme, DefaultBackdrop, updateAccent: true);

            if (DefaultTheme == ApplicationTheme.Unknown)
            {
                SystemThemeWatcher.Watch(null, DefaultBackdrop, updateAccents: true);
            }
        }
    }
}
