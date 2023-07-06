using Authenticator.Core;
using Newtonsoft.Json;
using SteamAuth;
using SteamKit2;
using SteamKit2.Authentication;
using SteamKit2.Internal;
using System;
using System.Threading.Tasks;

namespace Authenticator
{
    public class AuthWrapper
    {
        public event EventHandler<AuthLoginEventArgs> AuthLoginEvent;
        public event EventHandler<AuthLinkerEventArgs> AuthLinkerEvent;
        public event EventHandler<AuthFinalizeEventArgs> AuthFinalizeEvent;

        public SteamGuardAccount LinkedAccount => _linker.LinkedAccount;

        public SessionData Session { get; private set; }

        private string Username { get; set; }

        private string Password { get; set; }

        private AuthenticatorLinker _linker;

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

        public async Task LoginAsync(string username, string password)
        {
            App.Logger.Info($"AuthWrapper.Login...");

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                throw new Exception($"{nameof(Username)} or {nameof(Password)} is empty");
            }

            try
            {
                var steamClient = new SteamClient();
                steamClient.Connect();

                var authSession = await steamClient.Authentication.BeginAuthSessionViaCredentialsAsync(
                    new AuthSessionDetails
                    {
                        Username = username,
                        Password = password,
                        IsPersistentSession = false,
                        PlatformType = EAuthTokenPlatformType.k_EAuthTokenPlatformType_MobileApp,
                        ClientOSType = EOSType.Android9,
                        Authenticator = new UserConsoleAuthenticator(),
                    });
                var pollResponse = await authSession.PollingWaitForResultAsync();
                var sessionData = new SessionData()
                {
                    SteamID = authSession.SteamID.ConvertToUInt64(),
                    AccessToken = pollResponse.AccessToken,
                    RefreshToken = pollResponse.RefreshToken,
                };

                Username = username;
                Password = password;
                Session = new SessionData()
                {
                    SteamID = authSession.SteamID.ConvertToUInt64(),
                    AccessToken = pollResponse.AccessToken,
                    RefreshToken = pollResponse.RefreshToken,
                };

                AuthLoginEvent?.Invoke(this, new AuthLoginEventArgs(true));
            }
            catch (Exception ex)
            {
                App.Logger.Error($"AuthWrapper.Login: {ex.Message}");
                AuthLoginEvent?.Invoke(this, new AuthLoginEventArgs(false));
            }
        }

        public async Task<bool> ReloginAsync(SteamGuardAccount account, string password)
        {
            App.Logger.Info($"AuthWrapper.Relogin...");

            var result = false;
            try
            {
                var steamClient = new SteamClient();
                steamClient.Connect();

                var authSession = await steamClient.Authentication.BeginAuthSessionViaCredentialsAsync(
                    new AuthSessionDetails
                    {
                        Username = Username,
                        Password = password,
                        IsPersistentSession = false,
                        PlatformType = EAuthTokenPlatformType.k_EAuthTokenPlatformType_MobileApp,
                        ClientOSType = EOSType.Android9,
                        Authenticator = new UserConsoleAuthenticator(),
                        GuardData = JsonConvert.SerializeObject(account)
                    });

                var pollResponse = await authSession.PollingWaitForResultAsync();
                result = true;
            }
            catch (Exception ex)
            {
                App.Logger.Error($"AuthWrapper.Relogin Error: {ex.Message}");
            }

            App.Logger.Info($"AuthWrapper.Relogin Result: {result}");

            return result;
        }

        public async Task AddAuthenticator()
        {
            App.Logger.Info($"AuthWrapper.AddAuthenticator");

            if (_linker == null) _linker = new AuthenticatorLinker(Session);

            var result = await _linker.AddAuthenticator();

            AuthLinkerEvent?.Invoke(this, new AuthLinkerEventArgs(result));
        }

        public async Task FinalizeAddAuthenticator(string smsCode)
        {
            App.Logger.Info($"AuthWrapper.FinalizeAddAuthenticator");

            AuthenticatorLinker.FinalizeResult result = await _linker.FinalizeAddAuthenticator(smsCode);

            AuthFinalizeEvent?.Invoke(this, new AuthFinalizeEventArgs(result));
        }
    }
}
