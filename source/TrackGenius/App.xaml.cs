using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using TrackGenius.Communication;
using TrackGenius.Protocol.Robitronic;
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

        protected override void OnStartup(StartupEventArgs e)
        {
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
            _mainForm?.Close();
            base.OnExit(e);
        }

        protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
        {
            base.OnSessionEnding(e);

            //TODO  your code
        }

        void App_DispatcherUnhandledException(object sender,
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Trace.TraceError($"Unhandled UI exception: {e.Exception}");

            if (IsRecoverableException(e.Exception))
            {
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

        private static MainForm CreateMainWindow()
        {
            var communicateService = new CommunicateService(new RobitronicProtocol());
            return new MainForm(communicateService);
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
