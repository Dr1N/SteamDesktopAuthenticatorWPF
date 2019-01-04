using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Authenticator
{
    public enum SettingMode
    {
        StartNew,
        Deacivate,
    }

    /// <summary>
    /// Interaction logic for AuthenticatorSettingsWindow.xaml
    /// </summary>
    public partial class AuthenticatorSettingsWindow : Window
    {
        #region Constants

        private readonly string FilePrompt = "Select Account maFile...";

        #endregion

        #region Fields

        public string Password { get; private set; }
        public string AccountFilePath { get; private set; }
        private SettingMode Mode { get; set; }

        #endregion

        #region Life Cycle

        public AuthenticatorSettingsWindow()
        {
            InitializeComponent();
            FilePathBox.Content = FilePrompt;
        }

        public AuthenticatorSettingsWindow(SettingMode mode) : this()
        {
            Mode = mode;
            PasswordPanel.Visibility = Mode == SettingMode.StartNew ? Visibility.Visible : Visibility.Collapsed;
            Height = 120;
        }

        protected override void OnClosed(EventArgs e)
        {
            string msg = $"AuthenticatorSettingsWindow Closed: Result - {DialogResult}";
            if (DialogResult == true)
            {
                msg += $" Selected File: [[{AccountFilePath}]]";
            }
            App.Logger.Trace($"AuthenticatorSettingsWindow.Cloded: {msg}");
            base.OnClosed(e);
        }

        #endregion

        #region Callbacks

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsValid() == true)
            {
                Password = PasswordBox.Password.Trim();
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBoxResult result = MessageBox.Show("Select valid maFile and enter steam account password", Title, MessageBoxButton.OKCancel, MessageBoxImage.Information);
                if (result == MessageBoxResult.Cancel)
                {
                    DialogResult = false;
                    Close();
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            AccountFilePath = AskAccountFilePath();
            FilePathBox.Content = String.IsNullOrEmpty(AccountFilePath) == false ? AccountFilePath : FilePrompt;
        }

        #endregion

        #region Private Methods

        private string AskAccountFilePath()
        {
            string result = null;
            OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                Multiselect = false,
                Filter = "ma files (*.maFile)|*.maFile",
            };
            if (openFileDialog.ShowDialog() == true)
            {
                App.Logger.Info($"AuthenticatorSettingsWindow.AskAccountFilePath Selected maFile: {openFileDialog.FileName}");

                result = openFileDialog.FileName;
            }

            return result;
        }

        private bool IsValid()
        {
            bool result = false;
            if (Mode == SettingMode.StartNew)
            {
                result = String.IsNullOrEmpty(AccountFilePath) == false && String.IsNullOrEmpty(PasswordBox.Password.Trim()) == false;
            }
            else if (Mode == SettingMode.Deacivate)
            {
                result = String.IsNullOrEmpty(AccountFilePath) == false;
            }

            return result;
        }

        #endregion
    }
}
