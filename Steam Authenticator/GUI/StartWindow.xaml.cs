using System;
using System.Windows;

namespace Authenticator
{
    /// <summary>
    /// Interaction logic for StartWindow.xaml
    /// </summary>
    public partial class StartWindow : Window
    {
        #region Life Cycle

        public StartWindow()
        {
            InitializeComponent();
        }

        #endregion

        #region Callbacks

        private void RunAuthButton_Click(object sender, RoutedEventArgs e)
        {
            App.Logger.Trace("StartWindow.RunAuthButton");
            AuthenticatorSettingsWindow settingsWindow = new AuthenticatorSettingsWindow();
            if (settingsWindow.ShowDialog() == true)
            {
                try
                {
                    Core.Authenticator authenticator = new Core.Authenticator(settingsWindow.AccountFilePath, settingsWindow.Password);
                    AuthenticatorWindow window = new AuthenticatorWindow(authenticator);
                    window.Show();
                    Close();
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"AuthenticatorWindow.RunAuthButton Error: {ex.Message}");
                }
            }
        }

        private void AddAuthButton_Click(object sender, RoutedEventArgs e)
        {
            App.Logger.Trace("StartWindow.AddAuthButton");
            LoginWindow window = new LoginWindow
            {
                Owner = this,
            };
            window.Closed += ChildWindow_Closed;
            Hide();
            window.Show();
        }

        private void RemoveAuthButton_Click(object sender, RoutedEventArgs e)
        {
            App.Logger.Trace("StartWindow.RemoveAuthButton");
            AuthenticatorSettingsWindow authenticatorSettingsWindow = new AuthenticatorSettingsWindow(SettingMode.Deactivate);
            if (authenticatorSettingsWindow.ShowDialog() == true)
            {
                MessageBoxResult result = MessageBox.Show("Deactivate Account Authenticator?", Title, MessageBoxButton.OKCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.Cancel)
                {
                    App.Logger.Trace("StartWindow.RemoveAuthButton Deactivation Canceled");
                    return;
                }
                try
                {
                    App.SteamGuardHelper.Initialize(authenticatorSettingsWindow.AccountFilePath);
                    bool success = App.SteamGuardHelper.CurrentSteamGuard.DeactivateAuthenticator();
                    string message = success == true ? "Deactivated" : "Not Deactivated";
                    App.Logger.Info($"StartWindow.RemoveAuthButton Account Authenticator {message}");
                    MessageBox.Show($"Account Authenticator {message}", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"StartWindow.RemoveAuthButton Error: {ex.Message}");
                }
            }
        }

        private void ChildWindow_Closed(object sender, EventArgs e)
        {
            Show();
        }

        #endregion
    }
}
