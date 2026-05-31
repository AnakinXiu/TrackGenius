using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace TrackGenius
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            //注册Application_Error
            this.DispatcherUnhandledException +=
                new DispatcherUnhandledExceptionEventHandler(App_DispatcherUnhandledException);
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

        private void OnExit(ExitEventArgs e)
        {
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

            MessageBox.Show("An unexpected error occurred and the application needs to close.",
                "TrackGenius Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            e.Handled = false;
        }

        private static bool IsRecoverableException(Exception exception)
        {
            return exception is OperationCanceledException;
        }
    }
}
