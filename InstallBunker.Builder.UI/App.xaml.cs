using InstallBunker.Compiler.Core.Services;
using System.Windows;
using System.Windows.Threading;

namespace InstallBunker.Builder.UI
{
    public partial class App : System.Windows.Application
    {
        public const string ModulePackagePassword = "InstallBunker@Builder#2026!PKG";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Exit += App_Exit;
            SessionEnding += App_SessionEnding;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            TryCleanupCompilerChildProcesses();
            base.OnExit(e);
        }

        private void App_Exit(object sender, ExitEventArgs e)
        {
            TryCleanupCompilerChildProcesses();
        }

        private void App_SessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            TryCleanupCompilerChildProcesses();
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            TryCleanupCompilerChildProcesses();
        }

        private void CurrentDomain_ProcessExit(object? sender, EventArgs e)
        {
            TryCleanupCompilerChildProcesses();
        }

        private static void TryCleanupCompilerChildProcesses()
        {
            try
            {
                CompilerChildProcessRegistry.KillAllTrackedProcesses();
            }
            catch
            {
                // ignored
            }
        }
    }
}