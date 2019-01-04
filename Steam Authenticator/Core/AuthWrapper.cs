using Authenticator.Core;
using SteamAuth;
using System;
using System.Threading.Tasks;

namespace Authenticator
{
    public class AuthWrapper
    {
        #region Constants

        private readonly int MaxAttemps = 5;
        
        #endregion

        #region Events

        public event EventHandler<AuthLoginEventArgs> AuthLoginEvent;
        public event EventHandler<AuthLinkerEventArgs> AuthLinkerEvent;
        public event EventHandler<AuthFinalizeEventArgs> AuthFinalizeEvent;

        #endregion

        #region Properties

        private string Username { get; set; }
        private string Password { get; set; }

        #endregion

        #region Wrapped Login Properties

        private UserLogin _userLogin;
        private UserLogin UserLogin => _userLogin;
        
        public bool RequiresCaptcha
        {
            get
            {
                return _userLogin.RequiresCaptcha;
            }
        }

        public string CaptchaGID
        {
            get
            {
                return _userLogin.CaptchaGID;
            }
        }

        public string CaptchaText
        {
            get
            {
                return _userLogin.CaptchaText;
            }
            set
            {
                _userLogin.CaptchaText = value;
            }
        }

        public bool RequiresEmail
        {
            get
            {
                return _userLogin.RequiresEmail;
            }
        }

        public string EmailCode
        {
            get
            {
                return _userLogin.EmailCode;
            }
            set
            {
                _userLogin.EmailCode = value;
            }
        }

        public bool Requires2FA
        {
            get
            {
                return _userLogin.Requires2FA;
            }
        }

        public string TwoFactorCode
        {
            get
            {
                return _userLogin.TwoFactorCode;
            }
            set
            {
                _userLogin.TwoFactorCode = value;
            }
        }

        public SessionData Session
        {
            get
            {
                return _userLogin.Session;
            }
        }

        #endregion

        #region Wrapped Linker Properties

        private AuthenticatorLinker _linker;
        private AuthenticatorLinker Linker => _linker;
        
        public string PhoneNumber
        {
            get
            {
                return _linker.PhoneNumber;
            }
            set
            {
                _linker.PhoneNumber = value;
            }
        }

        public string DeviceID
        {
            get
            {
                return _linker.DeviceID;
            }
        }

        public SteamGuardAccount LinkedAccount
        {
            get
            {
                return _linker.LinkedAccount;
            }
        }

        public bool Finalized
        {
            get
            {
                return _linker.Finalized;
            }
        }

        #endregion

        #region Public Methods

        public void SetUserCredentials(string username, string password)
        {
            _userLogin = new UserLogin(username, password);
            Username = username;
            Password = password;
        }

        public void Login()
        {
            App.Logger.Info($"AuthWrapper.Login...");
            if (String.IsNullOrEmpty(Username) || String.IsNullOrEmpty(Password))
            {
                throw new Exception($"{nameof(Username)} or {nameof(Password)} is empty");
            }
            LoginResult response = UserLogin.DoLogin();

            AuthLoginEvent?.Invoke(this, new AuthLoginEventArgs(response));
        }

        public Task LoginAsync()
        {
            App.Logger.Info($"AuthWrapper.LoginAsync...");
            return Task.Run(() => {
                Login();
            });
        }

        public bool ReLogin(SteamGuardAccount account, string password)
        {
            App.Logger.Info($"AuthWrapper.Relogin...");
            bool result = false;
            try
            {
                int counter = 0;
                UserLogin userLogin = new UserLogin(account.AccountName, password);
                LoginResult response = LoginResult.BadCredentials;
                while (true)
                {
                    counter++;
                    response = userLogin.DoLogin();
                    App.Logger.Info($"AuthWrapper.Relogin Response: {response}");
                    if (response == LoginResult.LoginOkay)
                    {
                        account.Session = userLogin.Session;
                        result = true;
                        break;
                    }
                    else if (response == LoginResult.Need2FA)
                    {
                        userLogin.TwoFactorCode = account.GenerateSteamGuardCode();
                    }
                    else if (counter >= MaxAttemps)
                    {
                        App.Logger.Warn($"AuthWrapper.Relogin Attemps: {counter}");
                        break;
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                App.Logger.Error($"AuthWrapper.Relogin Error: {ex.Message}");
                result = false;
            }
            App.Logger.Info($"AuthWrapper.Relogin Result: {result}");

            return result;
        }

        public Task<bool> ReloginAsync(SteamGuardAccount account, string password)
        {
            App.Logger.Trace($"AuthWrapper.ReloginAsync");
            return Task.Run(async () => {
                bool result = false;
                try
                {
                    int counter = 0;
                    App.Logger.Trace($"AuthWrapper.ReloginAsync Try Relogin");
                    UserLogin userLogin = new UserLogin(account.AccountName, password);
                    LoginResult response = LoginResult.BadCredentials;
                    while (true)
                    {
                        counter++;
                        response = userLogin.DoLogin();
                        App.Logger.Trace($"AuthWrapper.ReloginAsync Response: {response}");
                        if (response == LoginResult.LoginOkay)
                        {
                            account.Session = userLogin.Session;
                            result = true;
                            break;
                        }
                        else if (response == LoginResult.Need2FA)
                        {
                            userLogin.TwoFactorCode = account.GenerateSteamGuardCode();
                        }
                        else if (counter >= MaxAttemps)
                        {
                            App.Logger.Warn($"AuthWrapper.ReloginAsync Max Attemps: {counter}");
                            break;
                        }
                        await Task.Delay(1000);
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"AuthWrapper.ReloginAsync Error: {ex.Message}");
                    result = false;
                }
                App.Logger.Info($"AuthWrapper.ReloginAsync Result: {result}");

                return result;
            });
        }

        public void AddAuthenticator()
        {
            App.Logger.Info($"AuthWrapper.AddAuthenticator...");
            if (_linker == null)
            {
                _linker = new AuthenticatorLinker(UserLogin.Session);
            }
            AuthenticatorLinker.LinkResult result = _linker.AddAuthenticator();
            AuthLinkerEvent?.Invoke(this, new AuthLinkerEventArgs(result));
        }

        public void FinalizeAddAuthenticator(string smsCode)
        {
            App.Logger.Info($"AuthWrapper.FinalizeAddAuthenticator...");
            AuthenticatorLinker.FinalizeResult result = _linker.FinalizeAddAuthenticator(smsCode);
            AuthFinalizeEvent?.Invoke(this, new AuthFinalizeEventArgs(result));
        }

        #endregion
    }
}
