using Authenticator.Core;
using Newtonsoft.Json;
using SteamAuth;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Authenticator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        #region Constants

        private readonly IDictionary<LoginResult, string> LoginMessages = new Dictionary<LoginResult, string>()
        {
            { LoginResult.GeneralFailure, "An error occurred while processing your request." },
            { LoginResult.BadRSA, "An error occurred while processing your request (Bad RSA)." },
            { LoginResult.BadCredentials, "Incorrect username or password."},
            { LoginResult.NeedCaptcha, "Need Enter Captcha." },
            { LoginResult.Need2FA, "Invalid Code from mobile Authenticator" },
            { LoginResult.NeedEmail, "Invalie Code from email" },
            { LoginResult.TooManyFailedLogins, "Too many login failures."},
        };

        private readonly IDictionary<AuthenticatorLinker.LinkResult, string> LinkerMessages = new Dictionary<AuthenticatorLinker.LinkResult, string>()
        {
            { AuthenticatorLinker.LinkResult.MustProvidePhoneNumber, "Invalid Phone Number" },
            { AuthenticatorLinker.LinkResult.MustRemovePhoneNumber, "Must Remove Phone Number" },
            { AuthenticatorLinker.LinkResult.AwaitingFinalization, "Invalid SMS Code" },
            { AuthenticatorLinker.LinkResult.GeneralFailure, "Authenticator Linker General Failure" },
            { AuthenticatorLinker.LinkResult.AuthenticatorPresent, "Your account is already linked to mobile Authenticator"},
        };

        private readonly IDictionary<AuthenticatorLinker.FinalizeResult, string> FinaluzeMessage = new Dictionary<AuthenticatorLinker.FinalizeResult, string>()
        {
            { AuthenticatorLinker.FinalizeResult.Success, "Success" },
            { AuthenticatorLinker.FinalizeResult.BadSMSCode, "Bad SMS Code" },
            { AuthenticatorLinker.FinalizeResult.UnableToGenerateCorrectCodes, "Unable To Generate Correct Codes" },
            { AuthenticatorLinker.FinalizeResult.GeneralFailure, "Finalization General Failure"},
        };

        #endregion

        #region Fields

        private AuthWrapper AuthWrapper
        {
            get
            {
                return App.AuthWrapper;
            }
        }

        private SteamGuardAccount _account;
        private SteamGuardAccount Account => _account;

        private LoginShowReason _showReason = LoginShowReason.ActivateAuthenticator;
        private LoginShowReason ShowReason => _showReason;

        #endregion

        #region Life Cycle

        public LoginWindow()
        {
            InitializeComponent();
            InitializeAuthWrapper();
            Loaded += LoginWindow_Loaded;
            Closed += LoginWindow_Closed;
        }

        public LoginWindow(SteamGuardAccount account) : this()
        {
            _account = account ?? throw new ArgumentNullException(nameof(account));
            _showReason = LoginShowReason.RefreshSession;
            LoginBox.Text = account.AccountName;
            LoginBox.IsReadOnly = true;
        }

        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetWindowLabels();
        }

        private void LoginWindow_Closed(object sender, EventArgs e)
        {
            AuthWrapper.AuthLoginEvent -= AuthWrapper_AuthLoginEvent;
            AuthWrapper.AuthLinkerEvent -= AuthWrapper_AuthLinkerEvent;
            AuthWrapper.AuthFinalizeEvent -= AuthWrapper_AuthFinalizeEvent;
        }

        #endregion

        #region Callbacks

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsValid() == false)
            {
                MessageBox.Show("Enter Username and Password", "Login Error", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SetStatusValue("Loging...");
            await DisableUI();
            await Task.Delay(100); //TODO wtf?

            string username = LoginBox.Text.Trim();
            string password = PasswordBox.Password.Trim();

            AuthWrapper.SetUserCredentials(username, password);
            AuthWrapper.Login();
        }

        private async void AuthWrapper_AuthLoginEvent(object sender, AuthLoginEventArgs args)
        {
            App.Logger.Trace($"LoginWindow.AuthLoginEvent: {args.Result}");
            switch (args.Result)
            {
                case LoginResult.LoginOkay:
                    if (ShowReason == LoginShowReason.RefreshSession)
                    {
                        Account.Session = AuthWrapper.Session;
                        Close();
                    }
                    else if (ShowReason == LoginShowReason.ActivateAuthenticator)
                    {
                        SetStatusValue("Activate Authenticator...");
                        AuthWrapper.AddAuthenticator();
                    }
                    return;
                case LoginResult.Need2FA:
                    if (Set2FACode() == true)
                    {
                        AuthWrapper.Login();
                        return;
                    }
                    ShowMessage(args.Result);
                    break;
                case LoginResult.NeedEmail:
                    if (SetEmailCode() == true)
                    {
                        AuthWrapper.Login();
                        return;
                    }
                    ShowMessage(args.Result);
                    break;
                case LoginResult.NeedCaptcha:
                    App.Logger.Info($"LoginWindow.AuthLoginEvent Captcha GID: {AuthWrapper.CaptchaGID}");
                    if (SetCaptha(AuthWrapper.CaptchaGID) == true)
                    {
                        AuthWrapper.Login();
                        return;
                    }
                    ShowMessage(args.Result);
                    break;
                case LoginResult.GeneralFailure:
                case LoginResult.BadRSA:
                case LoginResult.BadCredentials:
                case LoginResult.TooManyFailedLogins:
                    ShowMessage(args.Result);
                    break;

                default:
                    break;
            }
            await EnableUI();
        }

        private async void AuthWrapper_AuthLinkerEvent(object sender, AuthLinkerEventArgs args)
        {
            App.Logger.Trace($"AuthLoginEvent.AuthLinkerEvent: {args.Result}");
            switch (args.Result)
            {
                case AuthenticatorLinker.LinkResult.MustProvidePhoneNumber:
                    if (SetPhoneNumber() == true)
                    {
                        AuthWrapper.AddAuthenticator();
                        return;
                    }
                    ShowMessage(args.Result);
                    break;
                case AuthenticatorLinker.LinkResult.AwaitingFinalization:
                    if (SaveMaFile() == true)
                    {
                        string smsCode = "";
                        if (GetSmsCode(ref smsCode) == true)
                        {
                            if (String.IsNullOrEmpty(smsCode) == false)
                            {
                                AuthWrapper.FinalizeAddAuthenticator(smsCode);
                                return;
                            }
                        }
                    }
                    ShowMessage(args.Result);
                    break;
                case AuthenticatorLinker.LinkResult.MustRemovePhoneNumber:
                case AuthenticatorLinker.LinkResult.GeneralFailure:
                case AuthenticatorLinker.LinkResult.AuthenticatorPresent:
                    ShowMessage(args.Result);
                    break;
                default:
                    break;
            }
            await EnableUI();
        }

        private async void AuthWrapper_AuthFinalizeEvent(object sender, AuthFinalizeEventArgs args)
        {
            App.Logger.Trace($"AuthLoginEvent.AuthFinalizeEvent: {args.Result}");
            if (args.Result == AuthenticatorLinker.FinalizeResult.Success)
            {
                SetStatusValue("Activation Success");
                MessageBox.Show("Activation Success:", "Success", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
            else
            {
                ShowMessage(args.Result);
            }
            await EnableUI();
        }

        #endregion

        #region Private Methods

        private bool IsValid()
        {
            return String.IsNullOrEmpty(LoginBox.Text.Trim()) == false && String.IsNullOrEmpty(PasswordBox.Password.Trim()) == false;
        }

        private MessageBoxResult ShowMessage<T>(T result)
        {
            if (typeof(T).IsEnum == false) return MessageBoxResult.None;

            string message = "";
            string caption = "";

            if (result is LoginResult)
            {
                caption = "Login Error";
                LoginResult? key = result as LoginResult?;
                if (key.HasValue)
                {
                    LoginMessages.TryGetValue(key.Value, out message);
                }
            }
            else if (result is AuthenticatorLinker.LinkResult)
            {
                caption = "Activation Error";
                AuthenticatorLinker.LinkResult? key = result as AuthenticatorLinker.LinkResult?;
                if (key.HasValue)
                {
                    LinkerMessages.TryGetValue(key.Value, out message);
                }
            }
            else if (result is AuthenticatorLinker.FinalizeResult)
            {
                caption = "Finalization Error";
                AuthenticatorLinker.FinalizeResult? key = result as AuthenticatorLinker.FinalizeResult?;
                if (key.HasValue)
                {
                    FinaluzeMessage.TryGetValue(key.Value, out message);
                }
            }

            if (String.IsNullOrEmpty(caption) == false && String.IsNullOrEmpty(message) == false)
            {
                return MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return MessageBoxResult.None;
        }

        private void SetStatusValue(string message)
        {
            StatusValueBlock.Content = message;
        }

        private bool SetEmailCode()
        {
            if (AuthWrapper.RequiresEmail == false) return false;

            SimpleDialogWindow dlg = new SimpleDialogWindow("Enter Code From Email") { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                AuthWrapper.EmailCode = dlg.Answer;
                return true;
            }
            return false;
        }

        private bool Set2FACode()
        {
            if (AuthWrapper.Requires2FA == false) return false;

            SimpleDialogWindow dlg = new SimpleDialogWindow("Enter Code From Mobile Authenticator") { Owner = this };
            if (dlg?.ShowDialog() == true)
            {
                AuthWrapper.TwoFactorCode = dlg.Answer;
                return true;
            }
            return false;
        }

        private bool SetCaptha(string gid)
        {
            if (AuthWrapper.RequiresCaptcha == false) return false;

            CaptchaDialogWindow dlg = new CaptchaDialogWindow(gid) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                AuthWrapper.CaptchaText = dlg.Answer;
                return true;
            }

            return false;
        }

        private bool SetPhoneNumber()
        {
            SimpleDialogWindow dlg = new SimpleDialogWindow("Enter Phone Number") { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                AuthWrapper.PhoneNumber = dlg.Answer;
                return true;
            }
            return false;
        }

        private bool SaveMaFile()
        {
            try
            {
                string sgFile = JsonConvert.SerializeObject(AuthWrapper.LinkedAccount, Formatting.Indented);
                string fileName = AuthWrapper.Session.SteamID + ".maFile";
                File.WriteAllText(fileName, sgFile);
                App.Logger.Info($"LoginWindow.SaveMaFile File Saved: {fileName}");

                return true;
            }
            catch (Exception ex)
            {
                App.Logger.Error($"LoginWindow.SaveMaFile Error {ex.Message}");
                return false;
            }
        }

        private bool GetSmsCode(ref string code)
        {
            SimpleDialogWindow dlg = new SimpleDialogWindow("Ente Code From SMS") { Owner = this };
            if (dlg?.ShowDialog() == true)
            {
                code = dlg.Answer;
                return true;
            }
            return false;
        }

        private DispatcherOperation DisableUI()
        {
            return Dispatcher.InvokeAsync(() =>
            {
                LoginButton.Content = "Loading...";
                LoginButton.IsEnabled = false;
                LoginBox.IsEnabled = false;
                PasswordBox.IsEnabled = false;
            }, DispatcherPriority.Send);
        }

        private DispatcherOperation EnableUI()
        {
            return Dispatcher.InvokeAsync(() =>
            {
                LoginButton.IsEnabled = true;
                LoginBox.IsEnabled = true;
                PasswordBox.IsEnabled = true;
                SetWindowLabels();
                LoginBox.Text = String.Empty;
                PasswordBox.Password = String.Empty;
                SetStatusValue("Enter your login and password");
            }, DispatcherPriority.Send);
        }

        private void InitializeAuthWrapper()
        {
            AuthWrapper.AuthLoginEvent += AuthWrapper_AuthLoginEvent;
            AuthWrapper.AuthLinkerEvent += AuthWrapper_AuthLinkerEvent;
            AuthWrapper.AuthFinalizeEvent += AuthWrapper_AuthFinalizeEvent;
        }

        private void SetWindowLabels()
        {
            SetStatusValue("Enter your login and password");
            switch (ShowReason)
            {
                case LoginShowReason.ActivateAuthenticator:
                    Title = "Activate Authenticator";
                    LoginButton.Content = "Activate";
                    break;
                case LoginShowReason.RefreshSession:
                    Title = $"Refresh Session [{Account.AccountName}]";
                    LoginButton.Content = "Login";
                    break;
                default:
                    break;
            }
        }

        #endregion
    }
}
