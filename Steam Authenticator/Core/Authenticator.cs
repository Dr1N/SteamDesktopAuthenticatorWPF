using SteamAuth;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Authenticator.Core
{
    internal class Authenticator : IDisposable
    {
        #region Constants

        private readonly int MaxErrors = 10;

        #endregion

        #region Events

        public event EventHandler<AuthenticatorStateChangedEventArgs> StateChangedEvent = delegate { };
        public event EventHandler<AuthenticatorConfirmationsEventArgs> ConfirmationsEvent = delegate { };
        public event EventHandler<AuthenticatorConfirmationEventArgs> ConfirmationEvent = delegate { };

        #endregion

        #region Fields & Properties

        private readonly object _collectionLock = new object();

        private readonly ObservableCollection<ConfirmationItem> _confirmations = new ObservableCollection<ConfirmationItem>();
        public ObservableCollection<ConfirmationItem> ConfirmationsSource => _confirmations;

        private readonly string _accountFilePath;
        public string AccountFilePath => _accountFilePath;

        private readonly string _password;
        public string Password => _password;

        private bool _isUpdateInProcess = false;
        public bool UpdateInProcess => _isUpdateInProcess;

        private bool _isConfirmationInProcess = false;
        public bool ConfirmationsProcess => _isConfirmationInProcess;

        private bool _isAutoUpdate = false;
        public bool AutoUpdate => _isAutoUpdate;

        private bool _isAutoConfirm = false;
        public bool AutoConfirm => _isAutoConfirm;

        private int _timeout;
        public int Timeout => _timeout;

        private DateTime? _lastUpdate = null;
        public string LastUpdateTime
        {
            get
            {
                if (_lastUpdate != null)
                {
                    return $"{_lastUpdate.Value.ToLongTimeString()}";
                }
                return "";
            }
        }

        private AuthenticatorState _state;
        public AuthenticatorState State
        {
            get
            {
                return _state;
            }
            set
            {
                _state = value;
                StateChangedEvent.Invoke(this, new AuthenticatorStateChangedEventArgs(_state));
            }
        }

        private SemaphoreSlim _updateSemaphore;
        private SemaphoreSlim _confirmationSemaphore;
        private SemaphoreSlim _autoUpdateSemaphore;

        private CancellationTokenSource _autoCancellationTokenSource;
        private bool _isAutoStarted;

        #endregion

        #region Life Cycle

        public Authenticator(string path, string password)
        {
            if (string.IsNullOrEmpty(password.Trim())) throw new ArgumentNullException(nameof(password));
            if (string.IsNullOrEmpty(path.Trim())) throw new ArgumentNullException(nameof(path));

            _updateSemaphore = new SemaphoreSlim(1, 1);
            _confirmationSemaphore = new SemaphoreSlim(1, 1);
            _autoUpdateSemaphore = new SemaphoreSlim(1, 1);

            BindingOperations.EnableCollectionSynchronization(_confirmations, _collectionLock);

            _password = password;
            App.SteamGuardHelper.Initialize(path);
            State = AuthenticatorState.Ready;
        }

        public Authenticator(Settings settings) : this(settings.AccountFilePath, settings.Password)
        {
            _isAutoUpdate = settings.IsUpdate;
            _isAutoConfirm = settings.IsConfirm;
            _timeout = settings.Timeout;
            _accountFilePath = settings.AccountFilePath;

            if (_isAutoUpdate)
            {
                _isAutoStarted = true;
                _autoCancellationTokenSource = new CancellationTokenSource();
                StartConfirmationAsync(_timeout, _isAutoConfirm, ConfirmationAction.Accept, _autoCancellationTokenSource.Token);
            }
        }

        #endregion

        #region Public Methods

        public Task StartConfirmationAsync(int timeout, bool autoConfirm, ConfirmationAction action, CancellationToken cancellationToken)
        {
            App.Logger.Info($"Authenticator.StartConfirmation: confirm [{autoConfirm}] action [{action}] timeout [{timeout}]");
            if (!_isAutoStarted
                && _autoCancellationTokenSource != null
                && !_autoCancellationTokenSource.IsCancellationRequested)
            {
                _autoCancellationTokenSource.Cancel();
            }
            _isAutoStarted = false;

            return Task.Run(async () =>
            {
                _autoUpdateSemaphore.Wait();
                _isAutoUpdate = true;
                _isAutoConfirm = autoConfirm;
                _timeout = timeout;
                int errorCounter = 0;
                var error = false;
                while (!cancellationToken.IsCancellationRequested && !disposedValue)
                {
                    try
                    {
                        App.Logger.Trace($"Authenticator.StartConfirmation Error Counter: {errorCounter}");
                        var success = await UpdateConfirmationsAsync();
                        if (!success && (++errorCounter > MaxErrors))
                        {
                            State = AuthenticatorState.Error;
                            error = true;
                            break;
                        }
                        else
                        {
                            errorCounter = 0;
                        }

                        if (_isAutoConfirm)
                        {
                            IEnumerable<ConfirmationItem> confirmations = ConfirmationsSource.Where(c => c.Status == ConfirmationStatus.Waiting).ToList();
                            await ProcessConfirmationsAsync(confirmations, action, cancellationToken);
                        }
                        State = AuthenticatorState.Wait;
                        await Task.Delay(TimeSpan.FromSeconds(_timeout), cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        App.Logger.Info($"Authenticator.StartConfirmation: Canceled");
                        break;
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Info($"Authenticator.StartConfirmation: Error {ex.Message}");
                        break;
                    }
                }
                _isAutoUpdate = false;
                State = error ? AuthenticatorState.Error : AuthenticatorState.Ready;
                _autoUpdateSemaphore.Release();
            });
        }

        public async Task<string> GetGuardCodeAsync()
        {
            var steamTime = await TimeAligner.GetSteamTimeAsync();
            if (App.SteamGuardHelper.CurrentSteamGuard != null && steamTime != 0)
            {
                return App.SteamGuardHelper.CurrentSteamGuard.GenerateSteamGuardCodeForTime(steamTime);
            }
            return string.Empty;
        }

        public async Task<int> GetSecondsUntilChangeAsync()
        {
            var steamTime = await TimeAligner.GetSteamTimeAsync();
            long currentSteamChunk = steamTime / 30L;

            return (int)(steamTime - (currentSteamChunk * 30L));
        }

        public Task<bool> UpdateConfirmationsAsync()
        {
            return Task.Run(async () =>
            {
                try
                {
                    _updateSemaphore.Wait();
                    _isUpdateInProcess = true;
                    App.Logger.Trace($"Authenticator.UpdateConfirmations");
                    State = AuthenticatorState.ConfirmationUpdating;
                    int beforeCount = ConfirmationsSource.Count;
                    if (await GetConfirmations())
                    {
                        App.Logger.Info($"Authenticator.UpdateConfirmationsAsync Success. Confirmations: ({ConfirmationsSource.Count}) Added: [{ConfirmationsSource.Count - beforeCount}]");
                        State = AuthenticatorState.ConfirmationUpdated;
                        _lastUpdate = DateTime.Now;
                        ConfirmationsEvent.Invoke(this, new AuthenticatorConfirmationsEventArgs(ConfirmationActionResult.Added, ConfirmationsSource.Count - beforeCount));

                        return true;
                    }
                    else
                    {
                        App.Logger.Error($"Authenticator.UpdateConfirmationsAsync Error");
                        State = AuthenticatorState.ConfirmationError;

                        return false;
                    }
                }
                finally
                {
                    _isUpdateInProcess = false;
                    _updateSemaphore.Release();
                }
            });
        }

        public Task<bool> ProcessConfirmationsAsync(IEnumerable<ConfirmationItem> confirmations, ConfirmationAction action, CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                _confirmationSemaphore.Wait();
                _isConfirmationInProcess = true;
                bool success = true;
                try
                {
                    App.Logger.Info($"Authenticator.ProcessConfirmationsAsync Action: {action}");
                    State = AuthenticatorState.ConfirmationProcessing;
                    if (confirmations == null || confirmations.Count() == 0)
                    {
                        App.Logger.Trace($"Authenticator.ProcessConfirmationsAsync [Empty]");
                        return success;
                    }

                    App.Logger.Info($"Authenticator.ProcessConfirmationsAsync Confirmations for process: {confirmations.Count()}");
                    int counter = 0;
                    foreach (ConfirmationItem confirmation in confirmations)
                    {
                        try
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            await ProcessConfirmationAsync(confirmation, action);
                            counter++;
                        }
                        catch (OperationCanceledException)
                        {
                            App.Logger.Info($"Authenticator.ProcessConfirmationsAsync Canceled");
                            break;
                        }
                    }

                    ConfirmationActionResult confirmationActionResult = action == ConfirmationAction.Accept ? ConfirmationActionResult.Accept : ConfirmationActionResult.Decline;
                    ConfirmationsEvent.Invoke(this, new AuthenticatorConfirmationsEventArgs(confirmationActionResult, counter));

                    return success;
                }
                finally
                {
                    State = AuthenticatorState.ConfirmationProcessed;
                    _isConfirmationInProcess = false;
                    _confirmationSemaphore.Release();
                }
            });
        }

        public Task<bool> ProcessConfirmationAsync(ConfirmationItem confirmation, ConfirmationAction action)
        {
            return Task.Run(() =>
            {
                confirmation.Status = ConfirmationStatus.Processing;
                bool result = false;
                var actionResult = ConfirmationActionResult.None;
                if (action == ConfirmationAction.Accept)
                {
                    result = App.SteamGuardHelper.CurrentSteamGuard.AcceptConfirmation(confirmation);
                    confirmation.Status = result ? ConfirmationStatus.Accepted : ConfirmationStatus.Unknow;
                    actionResult = result ? ConfirmationActionResult.Accept : ConfirmationActionResult.Error;
                }
                else if (action == ConfirmationAction.Decline)
                {
                    result = App.SteamGuardHelper.CurrentSteamGuard.DenyConfirmation(confirmation);
                    confirmation.Status = result ? ConfirmationStatus.Declined : ConfirmationStatus.Unknow;
                    actionResult = result ? ConfirmationActionResult.Decline : ConfirmationActionResult.Error;
                }

                App.Logger.Info($"Authenticator.ProcessConfirmationAsync {action.ToString()}: {confirmation}\t" + (result == true ? "[success]" : "[error]"));
                App.History.Write($"{confirmation} {action.ToString()}: {result}");

                ConfirmationEvent.Invoke(this, new AuthenticatorConfirmationEventArgs(actionResult, confirmation));

                return result;
            });
        }

        public void CleanConfirmations()
        {
            App.Logger.Trace($"Authenticator.CleanConfirmations: [{ConfirmationsSource.Count}]");
            try
            {
                ConfirmationsSource.Clear();
            }
            catch (Exception ex)
            {
                App.Logger.Warn($"Authenticator.CleanConfirmations Error: [{ex.Message}]");
            }
        }

        #region Settings

        public void SaveSetting(bool autoUpdate, bool autoConfirm, int timeout)
        {
            try
            {
                Settings settings = new Settings
                {
                    AccountFilePath = App.SteamGuardHelper.AccountFilePath,
                    Password = _password,
                    IsUpdate = autoUpdate,
                    IsConfirm = autoConfirm,
                    Timeout = timeout,
                };
                settings.Save();
                App.Logger.Info($"Authenticator.SaveSetting Success");
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Authenticator.SaveSetting Error: {ex.Message}");
            }
        }

        public void CleanSettings()
        {
            try
            {
                Settings.Clear();
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Authenticator.CleanSettings Error: {ex.Message}");
            }
        }

        #endregion

        #endregion

        #region Confirmation Methods

        private async Task<bool> GetConfirmations()
        {
            bool success = false;
            int counter = 0;
            bool isNeedRepeat;
            do
            {
                isNeedRepeat = false;
                if (++counter > 3)
                {
                    App.Logger.Trace($"Authenticator.GetConfirmations Counter: [{counter}]");
                    break;
                }
                try
                {
                    Confirmation[] confirmations = await App.SteamGuardHelper.CurrentSteamGuard.FetchConfirmationsAsync();
                    App.Logger.Info($"Authenticator.GetConfirmations Fetched: {confirmations.Length}");
                    foreach (var confirmation in confirmations)
                    {
                        ConfirmationItem item = new ConfirmationItem(confirmation) { Number = ConfirmationsSource.Count + 1 };
                        if (!ConfirmationsSource.Contains(item))
                        {
                            ConfirmationsSource.Add(item);
                            ConfirmationEvent.Invoke(this, new AuthenticatorConfirmationEventArgs(ConfirmationActionResult.Added, item));
                        }
                        App.Logger.Trace($"Authenticator.GetConfirmations Added Confirmation: {item}");
                    }
                    success = true;
                }
                catch (SteamGuardAccount.WGTokenInvalidException ex)
                {
                    App.Logger.Warn($"Authenticator.GetConfirmations WGTokenInvalidException: {ex.Message}");
                    isNeedRepeat = await RefreshSession();
                }
                catch (SteamGuardAccount.WGTokenExpiredException ex)
                {
                    App.Logger.Warn($"Authenticator.GetConfirmations WGTokenExpiredException: {ex.Message}");
                    isNeedRepeat = await RefreshSession();
                }
                catch (WebException ex)
                {
                    App.Logger.Error($"Authenticator.GetConfirmations WebException: {ex.Message}");
                    break;
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"Authenticator.GetConfirmations Exception: {ex.Message}");
                    break;
                }
            } while (isNeedRepeat);

            return success;
        }

        private async Task<bool> RefreshSession()
        {
            App.Logger.Trace($"Authenticator.RefreshSessionAsync");
            State = AuthenticatorState.SessionRefreshing;
            bool sessionResult = await App.SteamGuardHelper.CurrentSteamGuard.RefreshSessionAsync();
            App.Logger.Info($"Authenticator.RefreshSessionAsync Result: {sessionResult}");

            if (sessionResult == false)
            {
                State = AuthenticatorState.Relogin;
                var reLoginResult = await App.AuthWrapper.ReloginAsync(App.SteamGuardHelper.CurrentSteamGuard, Password);
                State = reLoginResult ? AuthenticatorState.ReloginSuccess : AuthenticatorState.ReloginError;
            }
            else
            {
                State = AuthenticatorState.SessionRefreshed;
            }

            return State == AuthenticatorState.SessionRefreshed || State == AuthenticatorState.ReloginSuccess;
        }

        #endregion

        #region IDisposable Support

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _updateSemaphore?.Dispose();
                    _confirmationSemaphore?.Dispose();
                    _autoUpdateSemaphore?.Dispose();
                    _autoCancellationTokenSource?.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}
