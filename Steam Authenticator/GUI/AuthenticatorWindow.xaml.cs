using Authenticator.Core;
using Authenticator.GUI.Commands;
using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace Authenticator
{
    /// <summary>
    /// Interaction logic for AuthenticatorWindow.xaml
    /// </summary>
    public partial class AuthenticatorWindow : Window
    {
        #region Constants

        private readonly int MinTimeout = 5;
        private readonly int MaxTimeout = 99;
        private readonly int DefaultTimeout = 30;
        private readonly string TextEditorPath = "notepad.exe";

        #endregion

        #region Fields & Properties

        private Core.Authenticator _authenticator;
        private Core.Authenticator Authenticator => _authenticator;

        private bool _isNeedSaveSettings = true;

        private ContextMenu _acceptButtonContextMenu;
        private ContextMenu AcceptButtonContextMenu => _acceptButtonContextMenu;

        private ContextMenu _confirmationContextMenu;
        private ContextMenu ConfirmationContextMenu => _confirmationContextMenu;

        private TaskbarIcon _taskbarIcon;
        private TaskbarIcon TaskbarIcon => _taskbarIcon;

        private CancellationTokenSource _updateConfirmationsCancellationTokenSource;
        private CancellationTokenSource _processConfirmationCancellationTokenSource;

        private System.Timers.Timer _guardCodeTimer;
        private System.Timers.Timer GuardCodeTimer
        {
            get => _guardCodeTimer;
            set => _guardCodeTimer = value;
        }

        #endregion

        #region Life Cycle

        internal AuthenticatorWindow(Core.Authenticator authenticator)
        {
            _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));

            InitializeComponent();
            InitializeGuardTimer();
            InitializeButtonContextMenu();
            InitializeConfirmationContextMenu();
            InitializeTaskBarIcon();

            TimeoutNumBox.MaxValue = MaxTimeout;
            TimeoutNumBox.MinValue = MinTimeout;

            SubscribeToWindowEvents();
        }

        private void AuthenticatorWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Title += $" [{App.SteamGuardHelper.CurrentSteamGuard.AccountName}]";

            SubscribeToAuthenticatorEvents();

            SetGuardCodeValue();
            GuardCodeTimer.Start();
            InitializeAuthenticator();

            SubscribeToMenuEvents();
            SubscribeOther();
        }

        private void AuthenticatorWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            UnsubscribeFromAuthenticatorEvents();
            UnsubscribeFromMenuEvents();
            UnsubscribeOther();
            GuardCodeTimer.Stop();
            CancelToken(_updateConfirmationsCancellationTokenSource);
            CancelToken(_processConfirmationCancellationTokenSource);
        }

        private void AuthenticatorWindow_Closed(object sender, EventArgs e)
        {
            UnsubscribeFromWindowEvents();
            if (_isNeedSaveSettings)
            {
                Authenticator.SaveSetting(
                    AutoUpdateMenu.IsChecked,
                    AutoConfirmationMenu.IsEnabled && AutoConfirmationMenu.IsChecked,
                    (int)TimeoutNumBox.Value
                );
            }
            Clean();
        }

        private void AuthenticatorWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                TaskbarIcon.Visibility = Visibility.Visible;
            }
        }

        private void SubscribeOther()
        {
            GuardCodeTimer.Elapsed += GuardCodeTimer_Tick;
            TaskbarIcon.TrayBalloonTipClicked += TaskbarIcon_TrayBalloonTipClicked;
        }

        private void SubscribeToWindowEvents()
        {
            Loaded += AuthenticatorWindow_Loaded;
            Closing += AuthenticatorWindow_Closing;
            Closed += AuthenticatorWindow_Closed;
            StateChanged += AuthenticatorWindow_StateChanged;
        }

        private void SubscribeToAuthenticatorEvents()
        {
            Authenticator.StateChangedEvent += Authenticator_StateChanged;
            Authenticator.ConfirmationsEvent += Authenticator_ConfirmationsEvent;
            Authenticator.ConfirmationEvent += Authenticator_ConfirmationEvent;
        }

        private void SubscribeToMenuEvents()
        {
            ConfirmationContextMenu.Opened += ConfirmationContextMenu_Opened;
            AcceptButtonContextMenu.Opened += AcceptButtonContextMenu_Opened;
            SettingsMenu.SubmenuOpened += SettingsMenu_SubmenuOpened;
            AutoUpdateMenu.Checked += AutoUpdateMenu_Checked;
            AutoUpdateMenu.Unchecked += AutoUpdateMenu_Unchecked;
            AutoConfirmationMenu.Checked += AutoConfirmationMenu_Checked;
            AutoConfirmationMenu.Unchecked += AutoConfirmationMenu_Unchecked;

            SubscribeToTaskIconMenu();
        }

        private void SubscribeToTaskIconMenu()
        {
            try
            {
                // TODO: Hard Code
                foreach (var item in TaskbarIcon.ContextMenu.Items)
                {
                    if (!(item is MenuItem)) continue;

                    MenuItem currentItem = item as MenuItem;
                    if (currentItem.Name == "TaskBarAutoUpdateMenu")
                    {
                        currentItem.Checked += TaskBarAutoUpdateMenu_Checked;
                        currentItem.Unchecked += TaskBarAutoUpdateMenu_Unchecked;
                    }
                    else if (currentItem.Name == "TaskBarAutoConfirmMenu")
                    {
                        currentItem.Checked += TaskBarAutoConfirmMenu_Checked;
                        currentItem.Unchecked += TaskBarAutoConfirmMenu_Unchecked;
                    }
                    else if (currentItem.Name == "TaskBarNotificationMenu")
                    {
                        currentItem.Checked += TaskBarNotificationMenu_Checked;
                        currentItem.Unchecked += TaskBarNotificationMenu_Unchecked;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Warn($"AuthenticatorWindow.SubscribeToTaskIconMenu Error {ex.Message}");
            }
        }

        private void UnsubscribeOther()
        {
            GuardCodeTimer.Elapsed -= GuardCodeTimer_Tick;
            TaskbarIcon.TrayBalloonTipClicked -= TaskbarIcon_TrayBalloonTipClicked;
        }

        private void UnsubscribeFromWindowEvents()
        {
            Loaded -= AuthenticatorWindow_Loaded;
            Closing -= AuthenticatorWindow_Closing;
            Closed -= AuthenticatorWindow_Closed;
            StateChanged -= AuthenticatorWindow_StateChanged;
        }

        private void UnsubscribeFromAuthenticatorEvents()
        {
            Authenticator.StateChangedEvent -= Authenticator_StateChanged;
            Authenticator.ConfirmationsEvent -= Authenticator_ConfirmationsEvent;
            Authenticator.ConfirmationEvent -= Authenticator_ConfirmationEvent;
        }

        private void UnsubscribeFromMenuEvents()
        {
            ConfirmationContextMenu.Opened -= ConfirmationContextMenu_Opened;
            AcceptButtonContextMenu.Opened -= AcceptButtonContextMenu_Opened;
            SettingsMenu.SubmenuOpened -= SettingsMenu_SubmenuOpened;
            AutoUpdateMenu.Checked -= AutoUpdateMenu_Checked;
            AutoUpdateMenu.Unchecked -= AutoUpdateMenu_Unchecked;
            AutoConfirmationMenu.Checked -= AutoConfirmationMenu_Checked;
            AutoConfirmationMenu.Unchecked -= AutoConfirmationMenu_Unchecked;

            UnsubscribeFromTaskIconMenu();
        }

        private void UnsubscribeFromTaskIconMenu()
        {
            try
            {
                // TODO: Hard Code
                foreach (var item in TaskbarIcon.ContextMenu.Items)
                {
                    if (!(item is MenuItem)) continue;

                    MenuItem currentItem = item as MenuItem;
                    if (currentItem.Name == "TaskBarAutoUpdateMenu")
                    {
                        currentItem.Checked -= TaskBarAutoUpdateMenu_Checked;
                        currentItem.Unchecked -= TaskBarAutoUpdateMenu_Unchecked;
                    }
                    else if (currentItem.Name == "TaskBarAutoConfirmMenu")
                    {
                        currentItem.Checked -= TaskBarAutoConfirmMenu_Checked;
                        currentItem.Unchecked -= TaskBarAutoConfirmMenu_Unchecked;
                    }
                    else if (currentItem.Name == "TaskBarNotificationMenu")
                    {
                        currentItem.Checked -= TaskBarNotificationMenu_Checked;
                        currentItem.Unchecked -= TaskBarNotificationMenu_Unchecked;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Warn($"AuthenticatorWindow.UnsubscribeFromTaskIconMenu Error {ex.Message}");
            }
        }

        private void Clean()
        {
            _taskbarIcon.Visibility = Visibility.Collapsed;
            _taskbarIcon.Dispose();
            _taskbarIcon = null;
            _authenticator.Dispose();
            _authenticator = null;
            _updateConfirmationsCancellationTokenSource = null;
            _processConfirmationCancellationTokenSource = null;
            _guardCodeTimer.Dispose();
            _guardCodeTimer = null;
        }

        #endregion

        #region Public Methods

        public void ShowWindow()
        {
            try
            {
                TaskbarIcon.Visibility = Visibility.Collapsed;
                Show();
                WindowState = WindowState.Normal;
                Activate();
            }
            catch (Exception ex)
            {
                App.Logger.Error($"AuthenticatorWindow.ShowTaskMenu Error: {ex.Message}");
            }
        }

        #endregion

        #region Callbacks

        #region Athenticator Callbacks

        private void Authenticator_StateChanged(object sender, AuthenticatorStateChangedEventArgs e)
        {
            SetAuthenticatorStateMessage(e.State);
            SetIndicatorColor(e.State);
        }

        private void Authenticator_ConfirmationEvent(object sender, AuthenticatorConfirmationEventArgs e)
        {
            switch (e.Action)
            {
                case ConfirmationActionResult.Accept:
                    SetStatusMessage($"Confirmation {e.Item.DisplayKey} Accepted");
                    break;
                case ConfirmationActionResult.Decline:
                    SetStatusMessage($"Confirmation {e.Item.DisplayKey} Declined");
                    break;
                case ConfirmationActionResult.Error:
                    SetStatusMessage($"Confirmation {e.Item.DisplayKey} Error");
                    break;
                default:
                    break;
            }
        }

        private void Authenticator_ConfirmationsEvent(object sender, AuthenticatorConfirmationsEventArgs e)
        {
            if (e.Count == 0) return;

            switch (e.Action)
            {
                case ConfirmationActionResult.Fetched:
                    ShowBalloon($"Fetched {e.Count} Confirmations");
                    break;
                case ConfirmationActionResult.Added:
                    ShowBalloon($"Added {e.Count} Confirmations");
                    break;
                case ConfirmationActionResult.Accept:
                    ShowBalloon($"Accepted {e.Count} Confirmations");
                    break;
                case ConfirmationActionResult.Decline:
                    ShowBalloon($"Declined {e.Count} Confirmations");
                    break;
                default:
                    break;
            }
        }

        #endregion

        #region Timer Callback

        private void GuardCodeTimer_Tick(object sender, EventArgs e)
        {
            SetGuardCodeValue();
        }

        #endregion

        #region Menus Opened Callbacks

        private void AcceptButtonContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            SetStateMenuItems(AcceptButtonContextMenu, Authenticator.AutoUpdate == false && Authenticator.UpdateInProcess == false);
        }

        private void ConfirmationContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (!(ConfirmationList.SelectedItem is ConfirmationItem confirmation)) return;
            SetStateMenuItems(sender as ContextMenu, confirmation.Status == ConfirmationStatus.Waiting && Authenticator.AutoConfirm == false);
        }

        private void SettingsMenu_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            SetStateMenuItems(SettingsMenu.ContextMenu, !Authenticator.ConfirmationsProcess);
        }

        private void TaskBarContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            try
            {
                // TODO: Hard Code
                foreach (var item in TaskbarIcon.ContextMenu.Items)
                {
                    if (!(item is MenuItem)) continue;

                    MenuItem currentItem = item as MenuItem;
                    if (currentItem.Name == "TaskBarNotificationMenu")
                    {
                        currentItem.IsChecked = ShowNotificationMenu.IsChecked;
                    }
                    else if (currentItem.Name == "TaskBarUpdateMenu")
                    {
                        currentItem.IsEnabled = Authenticator.AutoUpdate == false && Authenticator.UpdateInProcess == false;
                    }
                    else if (currentItem.Name == "TaskBarAcceptMenu" || currentItem.Name == "TaskBarDeclineMenu")
                    {
                        currentItem.IsEnabled = Authenticator.AutoConfirm == false
                            && Authenticator.UpdateInProcess == false
                            && Authenticator.ConfirmationsProcess == false;
                    }
                    else if (currentItem.Name == "TaskBarAutoUpdateMenu")
                    {
                        currentItem.IsChecked = AutoUpdateMenu.IsChecked;
                    }
                    else if (currentItem.Name == "TaskBarAutoConfirmMenu")
                    {
                        currentItem.IsChecked = AutoConfirmationMenu.IsChecked;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Warn($"AuthenticatorWindow.TaskBarContextMenu Error {ex.Message}");
            }
        }

        #endregion

        #region Buttons Callbacks

        private void CopyCodeButton_Click(object sender, RoutedEventArgs e)
        {
            CopyCodeToClipboard();
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            AcceptButtonContextMenu.IsOpen = true;
        }

        private void CleanButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Clean confirmations list?", Title, MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
            {
                Authenticator.CleanConfirmations();
            }
        }

        #endregion

        #region Button Actions Menu Callbacks

        private async void UpdateConfirmationMenu_Click(object sender, RoutedEventArgs e)
        {
            await UpdateAuthenticatorConfirmations();
        }

        private async void AcceptAllMenu_Click(object sender, RoutedEventArgs e)
        {
            await ProcessConfirmations(ConfirmationAction.Accept);
        }

        private async void DeclineAllMenu_Click(object sender, RoutedEventArgs e)
        {
            await ProcessConfirmations(ConfirmationAction.Decline);
        }

        #endregion

        #region Confirmation Context Menu Callbacks

        private async void AcceptMenu_Click(object sender, RoutedEventArgs e)
        {
            await ProcessSelectedConfirmation(ConfirmationAction.Accept);
        }

        private async void DeclineMenu_Click(object sender, RoutedEventArgs e)
        {
            await ProcessSelectedConfirmation(ConfirmationAction.Decline);
        }

        #endregion

        #region Main Menu Callbacks

        private void NewMenu_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Close This Window? (End Confirmation)", Title, MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
            {
                GuardCodeTimer.Stop();
                CancelToken(_updateConfirmationsCancellationTokenSource);
                CancelToken(_processConfirmationCancellationTokenSource);
                _isNeedSaveSettings = false;
                Authenticator.CleanSettings();
                new StartWindow().Show();
                Close();
            }
        }

        private void AboutMenu_Click(object sender, RoutedEventArgs e)
        {
            new AboutWindow() { Owner = this }.ShowDialog();
        }

        private void ExitMenu_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void AutoUpdateMenu_Checked(object sender, RoutedEventArgs e)
        {
            await StartAuthenticator();
        }

        private void AutoUpdateMenu_Unchecked(object sender, RoutedEventArgs e)
        {
            CancelToken(_updateConfirmationsCancellationTokenSource);
        }

        private async void AutoConfirmationMenu_Checked(object sender, RoutedEventArgs e)
        {
            CancelToken(_updateConfirmationsCancellationTokenSource);
            if (AutoConfirmationMenu.IsChecked
                && AutoConfirmationMenu.IsEnabled
                && AutoConfirmationMenu.IsChecked)
            {
                await StartAuthenticator();
            }
        }

        private async void AutoConfirmationMenu_Unchecked(object sender, RoutedEventArgs e)
        {
            CancelToken(_updateConfirmationsCancellationTokenSource);
            if (AutoUpdateMenu.IsChecked)
            {
                await StartAuthenticator();
            }
        }

        private void LogsMenu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var logDirectory = new DirectoryInfo("logs");
                var file = logDirectory.GetFiles()
                    .Where(f => f.Extension.Contains("log"))
                    .OrderBy(f => f.LastWriteTime)
                    .LastOrDefault();
                if (file != null)
                {
                    Process.Start(TextEditorPath, file.FullName);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Can't open log", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void HistoryMenu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var historyDirectory = new DirectoryInfo(App.History.HistoryPath);
                var file = historyDirectory.GetFiles()
                    .Where(f => f.Name == "history.log")
                    .FirstOrDefault();
                if (file != null)
                {
                    Process.Start(TextEditorPath, file.FullName);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Can't open history", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        #region TaskBar Icon Callbacks

        private void CopyGuardCode_Click(object sender, RoutedEventArgs e)
        {
            CopyCodeToClipboard();
        }

        private void TaskBarShowTaskMenu_Click(object sender, RoutedEventArgs e)
        {
            ShowWindow();
        }

        private void ExitTaskMenu_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TaskBarNotificationMenu_Checked(object sender, RoutedEventArgs e)
        {
            ShowNotificationMenu.IsChecked = true;
        }

        private void TaskBarNotificationMenu_Unchecked(object sender, RoutedEventArgs e)
        {
            ShowNotificationMenu.IsChecked = false;
        }

        private async void TaskBarUpdateMenu_Click(object sender, RoutedEventArgs e)
        {
            await UpdateAuthenticatorConfirmations();
        }

        private async void TaskBarAcceptMenu_Click(object sender, RoutedEventArgs e)
        {
            await ProcessConfirmations(ConfirmationAction.Accept);
        }

        private async void TaskBarDeclineMenu_Click(object sender, RoutedEventArgs e)
        {
            await ProcessConfirmations(ConfirmationAction.Decline);
        }

        private void TaskBarAutoUpdateMenu_Checked(object sender, RoutedEventArgs e)
        {
            AutoUpdateMenu.IsChecked = true;
        }

        private void TaskBarAutoUpdateMenu_Unchecked(object sender, RoutedEventArgs e)
        {
            AutoUpdateMenu.IsChecked = false;
        }

        private void TaskBarAutoConfirmMenu_Checked(object sender, RoutedEventArgs e)
        {
            AutoConfirmationMenu.IsChecked = true;
        }

        private void TaskBarAutoConfirmMenu_Unchecked(object sender, RoutedEventArgs e)
        {
            AutoConfirmationMenu.IsChecked = false;
        }

        private void TaskbarIcon_TrayBalloonTipClicked(object sender, RoutedEventArgs e)
        {
            ShowWindow();
        }

        #endregion

        #endregion

        #region Private Methods

        #region Initialize Methods

        private void InitializeGuardTimer(int intervalInSeconds = 1)
        {
            _guardCodeTimer = new System.Timers.Timer
            {
                Interval = TimeSpan.FromSeconds(intervalInSeconds).TotalMilliseconds
            };
        }

        private void InitializeButtonContextMenu()
        {
            _acceptButtonContextMenu = FindResource("ActionsMenuKey") as ContextMenu;
            AcceptButtonContextMenu.PlacementTarget = ActionButton;
            AcceptButtonContextMenu.Placement = PlacementMode.Bottom;
            AcceptButtonContextMenu.VerticalOffset = 3;
        }

        private void InitializeConfirmationContextMenu()
        {
            _confirmationContextMenu = FindResource("ConfirmationContextMenu") as ContextMenu;
            ConfirmationList.ItemContainerStyle.Setters.Add(new Setter(ContextMenuProperty, _confirmationContextMenu));
        }

        private void InitializeTaskBarIcon()
        {
            if (FindResource("AuthenticatorNotifyIcon") is TaskbarIcon taskbarIcon)
            {
                _taskbarIcon = taskbarIcon;
            }
            _taskbarIcon.LeftClickCommand = new ShowWindowCommand(this);
        }

        private void InitializeAuthenticator()
        {
            AutoUpdateMenu.IsChecked = Authenticator.AutoUpdate;
            AutoConfirmationMenu.IsChecked = Authenticator.AutoConfirm;
            TimeoutNumBox.Value = Authenticator.Timeout;
            ConfirmationList.ItemsSource = Authenticator.ConfirmationsSource;
            SetAuthenticatorStateMessage(Authenticator.State);
            SetIndicatorColor(Authenticator.State);
        }

        #endregion

        #region GUI Update Methods

        private void SetGuardCodeValue()
        {
            try
            {
                GuardCodeBox.Dispatcher.Invoke(async () => { GuardCodeBox.Text = await Authenticator?.GetGuardCodeAsync(); });
                GuardCodeProgressBar.Dispatcher.Invoke(async () => { GuardCodeProgressBar.Value = GuardCodeProgressBar.Maximum - await Authenticator?.GetSecondsUntilChangeAsync(); });
                CopyCodeButton.Dispatcher.Invoke(() => { if (CopyCodeButton.IsEnabled == false) CopyCodeButton.IsEnabled = true; });
            }
            catch (Exception ex)
            {
                App.Logger.Warn($"AuthenticatorWindow.SetGuardCodeValue Error: {ex.Message}");
            }
        }

        private void SetStatusMessage(string message)
        {
            Dispatcher.Invoke(() => { StatusBox.Text = message; }, DispatcherPriority.Normal);
        }

        private void SetStateMenuItems(MenuBase menu, bool isEnabled)
        {
            if (menu == null) return;
            foreach (MenuItem item in menu.Items)
            {
                item.IsEnabled = isEnabled;
            }
        }

        private void SetAuthenticatorStateMessage(AuthenticatorState state)
        {
            switch (state)
            {
                case AuthenticatorState.Ready:
                    SetStatusMessage("Ready");
                    break;
                case AuthenticatorState.Error:
                    SetStatusMessage("Authenticator Error");
                    CancelToken(_updateConfirmationsCancellationTokenSource);
                    break;
                case AuthenticatorState.Wait:
                    SetStatusMessage($"Waiting. Last Update: {Authenticator.LastUpdateTime}");
                    break;
                case AuthenticatorState.ConfirmationUpdating:
                    SetStatusMessage("Confirmation Updating...");
                    break;
                case AuthenticatorState.ConfirmationUpdated:
                    SetStatusMessage($"Confirmation Updating Сompleted. Confirmations: [{Authenticator.ConfirmationsSource.Count}] Last Update: [{Authenticator.LastUpdateTime}]");
                    break;
                case AuthenticatorState.ConfirmationError:
                    SetStatusMessage($"Confirmation Updating Error");
                    break;
                case AuthenticatorState.ConfirmationProcessing:
                    SetStatusMessage("Confirmation Processing...");
                    break;
                case AuthenticatorState.ConfirmationProcessed:
                    SetStatusMessage("Confirmation Processing Completed");
                    break;
                case AuthenticatorState.SessionRefreshing:
                    SetStatusMessage($"Session Refreshing...");
                    break;
                case AuthenticatorState.SessionRefreshed:
                    SetStatusMessage($"Session Refreshing Completed");
                    break;
                case AuthenticatorState.Relogin:
                    SetStatusMessage($"Relogin...");
                    break;
                case AuthenticatorState.ReloginSuccess:
                    SetStatusMessage($"Relogin Completed");
                    break;
                case AuthenticatorState.ReloginError:
                    SetStatusMessage($"Relogin Error");
                    CancelToken(_updateConfirmationsCancellationTokenSource);
                    break;
                default:
                    break;
            }
        }

        private void SetIndicatorColor(AuthenticatorState state)
        {
            if (!Authenticator.AutoUpdate)
            {
                return;
            }
            Brush current = Brushes.Transparent;
            switch (state)
            {
                case AuthenticatorState.Ready:
                    current = Brushes.Black;
                    break;
                case AuthenticatorState.Error:
                case AuthenticatorState.ReloginError:
                case AuthenticatorState.ConfirmationError:
                    current = Brushes.Red;
                    break;
                case AuthenticatorState.Wait:
                case AuthenticatorState.ConfirmationUpdating:
                case AuthenticatorState.ConfirmationUpdated:
                case AuthenticatorState.ConfirmationProcessing:
                case AuthenticatorState.ConfirmationProcessed:
                case AuthenticatorState.SessionRefreshing:
                case AuthenticatorState.SessionRefreshed:
                case AuthenticatorState.Relogin:
                case AuthenticatorState.ReloginSuccess:
                    current = Authenticator.AutoConfirm ? Brushes.Green : Brushes.Yellow;
                    break;
                default:
                    break;
            }
            IndicatorRect.Dispatcher.Invoke(() => IndicatorRect.Fill = current, DispatcherPriority.Render);
        }

        #endregion

        #region Helpers

        private async Task StartAuthenticator()
        {
            using (_updateConfirmationsCancellationTokenSource = new CancellationTokenSource())
            {
                await Authenticator.StartConfirmationAsync(GetTimeout(), AutoConfirmationMenu.IsChecked, ConfirmationAction.Accept, _updateConfirmationsCancellationTokenSource.Token);
            }
        }

        private async Task UpdateAuthenticatorConfirmations()
        {
            try
            {
                await Authenticator.UpdateConfirmationsAsync();
            }
            catch (Exception ex)
            {
                App.Logger.Error($"AuthenticatorWindow.UpdateConfirmations Error: {ex.Message}");
            }
        }

        private async Task ProcessConfirmations(ConfirmationAction action)
        {
            try
            {
                IEnumerable<ConfirmationItem> confirmations = Authenticator.ConfirmationsSource.Where(c => c.Status == ConfirmationStatus.Waiting).ToList();
                using (_processConfirmationCancellationTokenSource = new CancellationTokenSource())
                {
                    await Authenticator.ProcessConfirmationsAsync(confirmations, action, _processConfirmationCancellationTokenSource.Token);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error($"AuthenticatorWindow.ProcessConfirmations Error: {ex.Message}");
            }
        }

        private async Task ProcessSelectedConfirmation(ConfirmationAction action)
        {
            var operation = action.ToString();
            var confirmation = GetSelectedConfirmation();
            if (confirmation != null)
            {
                SetStatusMessage($"{operation} Confirmation: ID({confirmation.ID})...");
                var result = await Authenticator.ProcessConfirmationAsync(confirmation, action);
                SetStatusMessage($"Confirmation {confirmation.ID} {operation} " + (result == true ? "[success]" : "[error]"));
            }
        }

        private int GetTimeout()
        {
            var timeout = (int)(TimeoutNumBox.Value);
            if (timeout < MinTimeout || MaxTimeout < timeout)
            {
                timeout = DefaultTimeout;
                TimeoutNumBox.Value = timeout;
            }
            return timeout;
        }

        private ConfirmationItem GetSelectedConfirmation(ConfirmationStatus isNeedConfirmationStatus = ConfirmationStatus.Waiting)
        {
            ConfirmationItem result = null;
            if (ConfirmationList.SelectedItem is ConfirmationItem confirmation)
            {
                if (confirmation.Status == isNeedConfirmationStatus || isNeedConfirmationStatus == ConfirmationStatus.Unknow)
                {
                    result = confirmation;
                }
            }

            return result;
        }

        private void CopyCodeToClipboard()
        {
            try
            {
                if (string.IsNullOrEmpty(GuardCodeBox.Text) == false)
                {
                    Clipboard.SetText(GuardCodeBox.Text);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error($"AuthenticatorWindow.CopyCodeToClipboard Error: {ex.Message}");
            }
        }

        private void CancelToken(CancellationTokenSource source)
        {
            try
            {
                if (source != null
                    && !source.IsCancellationRequested)
                {
                    source.Cancel();
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error($"AuthenticatorWindow.CancelToken Error: {ex.Message}");
            }
        }

        private void ShowBalloon(string message)
        {
            TaskbarIcon.Dispatcher.Invoke(() =>
            {
                if (TaskbarIcon.Visibility == Visibility.Visible && ShowNotificationMenu.IsChecked)
                {
                    TaskbarIcon.ShowBalloonTip(Title, message, BalloonIcon.Info);
                }
            }, DispatcherPriority.Normal);
        }

        #endregion

        #endregion
    }
}
