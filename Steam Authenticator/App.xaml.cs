using Authenticator.Core.Interfaces;
using NLog;
using System;
using System.Windows;

namespace Authenticator
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static ILogger Logger = LogManager.GetCurrentClassLogger();
        public static IHistory History = new NLogHistory();

        private static readonly AuthWrapper _authWrapper;
        public static AuthWrapper AuthWrapper => _authWrapper;

        private static readonly SteamGuardHelper _steamGuardHelper;
        public static SteamGuardHelper SteamGuardHelper => _steamGuardHelper;

        static App()
        {
            _authWrapper = new AuthWrapper();
            _steamGuardHelper = new SteamGuardHelper();
        }

        public App()
        {
            Startup += App_Startup;
            Exit += App_Exit;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

#if !DEBUG
            try
            {
                foreach (var rule in LogManager.Configuration.LoggingRules)
                {
                    rule.DisableLoggingForLevel(LogLevel.Trace);
                }
                LogManager.ReconfigExistingLoggers();
            }
            catch {  }
#endif
        }

        #region Callbacks

        private void App_Startup(object sender, StartupEventArgs e)
        {
            Logger.Info("------------------------------------");
            Logger.Info("\tApp Start");
            Logger.Info("------------------------------------");
            try
            {
                Settings settings = GetSettings();
                if (settings != null)
                {
                    Core.Authenticator authenticator = new Core.Authenticator(settings);
                    new AuthenticatorWindow(authenticator).Show();
                }
                else
                {
                    new StartWindow().Show();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"App_Startup Error: {ex.Message}");
                new StartWindow().Show();
            }
        }

        private void App_Exit(object sender, ExitEventArgs e)
        {
            Logger.Info("------------------------------------");
            Logger.Info($"App Exit: {e.ApplicationExitCode}");
            Logger.Info("------------------------------------");
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Error($"App.DomainUnhandledException: {e.ExceptionObject}");
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Logger.Error($"App.DispatcherUnhandledException: {e.Exception.Message}");
        }

        #endregion

        #region Private Methods

        private Settings GetSettings()
        {
            try
            {
                return Settings.Load();
            }
            catch (Exception ex)
            {
                Logger.Error($"App.GetSEttings Error: {ex.Message}");
                return null;
            }
        }

        #endregion
    }
}
