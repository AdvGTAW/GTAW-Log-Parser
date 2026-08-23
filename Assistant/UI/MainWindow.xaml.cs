using System;
using Octokit;
using System.IO;
using System.Windows;
using System.Threading;
using System.Diagnostics;
using System.Windows.Input;
using System.Globalization;
using Assistant.Controllers;
using Assistant.Localization;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Windows.Threading;

namespace Assistant.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private const string GitHubRepository = "GTAW-Log-Parser";
        private System.Windows.Forms.NotifyIcon _trayIcon;

        private GitHubClient _client;
        private bool _isUpdateCheckRunning;
        private bool _isUpdateCheckManual;
        private bool _isLoadingSettings;
        private readonly DispatcherTimer _livePreviewTimer;
        private static bool isRestarting;

        /// <summary>
        /// Initializes the main window
        /// </summary>
        /// <param name="startMinimized"></param>
        public MainWindow(bool startMinimized)
        {
            _client = new GitHubClient(new ProductHeaderValue(AppController.ProductHeader));
            _client.SetRequestTimeout(new TimeSpan(0, 0, 0, Properties.Settings.Default.UpdateCheckTimeout));
            StartupController.InitializeShortcut();

            InitializeComponent();
            InitializeTrayIcon();

            if (startMinimized)
                _trayIcon.Visible = true;

            LoadSettings();

            SetupServerList();
            BackupController.Initialize();

            _livePreviewTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _livePreviewTimer.Tick += LivePreviewTimer_Tick;
            _livePreviewTimer.Start();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                RefreshCaptureStatus();
                if (Properties.Settings.Default.CheckForUpdatesAutomatically)
                    TryCheckingForUpdates();
            }));
        }

        /// <summary>
        /// Adds menu options under "Server" on the menu
        /// strip for each Language in LocalizationController
        /// </summary>
        private void SetupServerList()
        {
            string currentLanguage = LocalizationController.GetLanguageFromCode(LocalizationController.GetLanguage());
            for (int i = 0; i < ((LocalizationController.Language[])Enum.GetValues(typeof(LocalizationController.Language))).Length; ++i)
            {
                LocalizationController.Language language = (LocalizationController.Language)i;

                MenuItem menuItem = new MenuItem
                {
                    Header = language.ToString()
                };

                LanguageToolStripMenuItem.Items.Add(menuItem);
                menuItem.Click += (s, e) =>
                {
                    if (menuItem.IsChecked)
                        return;

                    CultureInfo cultureInfo = new CultureInfo(LocalizationController.GetCodeFromLanguage(language));
                    if (MessageBox.Show(Strings.ResourceManager.GetString("SwitchServer", cultureInfo),
                        Strings.ResourceManager.GetString("Restart", cultureInfo), MessageBoxButton.YesNo,
                        MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                    LocalizationController.SetLanguage(language);

                    isRestarting = true;

                    ProcessStartInfo startInfo = Process.GetCurrentProcess().StartInfo;
                    startInfo.FileName = AppController.ExecutablePath;
                    startInfo.Arguments = $"{AppController.ParameterPrefix}restart";
                    Process.Start(startInfo);

                    System.Windows.Application.Current.Shutdown();
                };

                if (currentLanguage == language.ToString())
                    menuItem.IsChecked = true;
            }
        }

        /// <summary>
        /// Saves the main settings
        /// </summary>
        private void SaveSettings()
        {
            Properties.Settings.Default.RemoveTimestamps = RemoveTimestamps.IsChecked == true;
            Properties.Settings.Default.CheckForUpdatesAutomatically = CheckForUpdatesOnStartup.IsChecked == true;
            Properties.Settings.Default.LivePreview = LivePreview.IsChecked == true;

            Properties.Settings.Default.Save();
            AppController.InitializeServerIp();
        }

        /// <summary>
        /// Loads the main settings
        /// </summary>
        private void LoadSettings()
        {
            OpenForums.Visibility = Properties.Settings.Default.DisableForumsButton ? Visibility.Collapsed : Visibility.Visible;
            OpenFacebrowser.Visibility = Properties.Settings.Default.DisableFacebrowserButton ? Visibility.Collapsed : Visibility.Visible;
            OpenUCP.Visibility = Properties.Settings.Default.DisableUCPButton ? Visibility.Collapsed : Visibility.Visible;
            OpenGithubReleases.Visibility = Properties.Settings.Default.DisableReleasesButton ? Visibility.Collapsed : Visibility.Visible;
            OpenGithubProject.Visibility = Properties.Settings.Default.DisableProjectButton ? Visibility.Collapsed : Visibility.Visible;
            UpdateCheckProgress.Foreground = StyleController.DarkMode ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Black;

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            // ReSharper disable once UnreachableCode
#pragma warning disable 162
            Version.Text = string.Format(Strings.VersionInfo, AppController.Version, AppController.IsBetaVersion ? Strings.BetaShort : string.Empty);
#pragma warning restore 162
            StatusLabel.Content = string.Format(Strings.BackupStatus, Properties.Settings.Default.BackupChatLogAutomatically ? Strings.Enabled : Strings.Disabled);
            Counter.Text = string.Format(Strings.CharacterCounter, 0, 0);

            _isLoadingSettings = true;
            RemoveTimestamps.IsChecked = Properties.Settings.Default.RemoveTimestamps;
            CheckForUpdatesOnStartup.IsChecked = Properties.Settings.Default.CheckForUpdatesAutomatically;
            LivePreview.IsChecked = Properties.Settings.Default.LivePreview;
            _isLoadingSettings = false;

            Properties.Settings.Default.FirstStart = false;
            Properties.Settings.Default.Save();
        }

        /// <summary>
        /// Parses the current chat log and sets
        /// the text of the main text box to it
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Parse_Click(object sender, RoutedEventArgs e)
        {
            AppController.InitializeServerIp();
            Parsed.Text = AppController.ParseChatLog(RemoveTimestamps.IsChecked == true, true);
        }

        private void LivePreviewTimer_Tick(object sender, EventArgs e)
        {
            RefreshCaptureStatus();
            if (LivePreview.IsChecked != true)
                return;

            string chat = AppController.ParseChatLog(RemoveTimestamps.IsChecked == true);
            if (!string.Equals(Parsed.Text, chat, StringComparison.Ordinal))
                Parsed.Text = chat;
        }

        private void RefreshCaptureStatus()
        {
            switch (FiveMChatCaptureController.CaptureState)
            {
                case FiveMChatCaptureState.Capturing:
                    CaptureStatus.Content = "Chat capture: Active";
                    break;
                case FiveMChatCaptureState.WaitingForChat:
                    CaptureStatus.Content = "Chat capture: Waiting for GTAW chat";
                    break;
                default:
                    CaptureStatus.Content = "Chat capture: Waiting for FiveM";
                    break;
            }
        }

        /// <summary>
        /// Displays a save file dialog to save the
        /// contents of the main text box to the disk
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Parsed_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Counter == null)
                return;

            if (string.IsNullOrWhiteSpace(Parsed.Text))
            {
                Counter.Text = string.Format(Strings.CharacterCounter, 0, 0);
                return;
            }

            Counter.Text = string.Format(Strings.CharacterCounter, Parsed.Text.Length, Parsed.Text.Split('\n').Length);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveParsed_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Parsed.Text))
                {
                    if (!Properties.Settings.Default.DisableErrorPopups)
                        MessageBox.Show(Strings.NothingParsed, Strings.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                    
                    return;
                }

                Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = "chatlog.txt",
                    Filter = "Text File | *.txt"
                };

                if (dialog.ShowDialog() != true) return;
                using (StreamWriter sw = new StreamWriter(dialog.OpenFile()))
                {
                    sw.Write(Parsed.Text);
                }
            }
            catch
            {
                MessageBox.Show(Strings.SaveError, Strings.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Copies the contents of the
        /// main text box to the clipboard
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CopyParsedToClipboard_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Parsed.Text))
            {
                if (!Properties.Settings.Default.DisableErrorPopups)
                    MessageBox.Show(Strings.NothingParsed, Strings.Error, MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }

            string error;
            if (!TryCopyTextToClipboard(Parsed.Text, out error) && !Properties.Settings.Default.DisableErrorPopups)
                MessageBox.Show(error, Strings.Error, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        internal static bool TryCopyTextToClipboard(string text, out string error)
        {
            error = null;
            try
            {
                System.Windows.Forms.Clipboard.SetDataObject(text, true, 10, 100);
                return true;
            }
            catch (ExternalException)
            {
                error = "Windows could not access the clipboard. Close any application that may be using it and try again.";
                return false;
            }
        }

        /// <summary>
        /// Toggles the "Check For Updates On Startup" option
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckForUpdatesOnStartup_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!_isLoadingSettings && CheckForUpdatesOnStartup.IsChecked == true)
                TryCheckingForUpdates();
        }

        private void LivePreview_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings)
                return;

            Properties.Settings.Default.LivePreview = LivePreview.IsChecked == true;
            Properties.Settings.Default.Save();
        }

        /// <summary>
        /// Removes the timestamps from the parsed chat log
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoveTimestamps_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Parsed.Text))
                return;

            if (RemoveTimestamps.IsChecked == true)
            {
                AppController.PreviousLog = Parsed.Text;
                Parsed.Text = Regex.Replace(AppController.PreviousLog, @"\[\d{1,2}:\d{1,2}:\d{1,2}\] ", string.Empty);
            }
            else if (!string.IsNullOrWhiteSpace(AppController.PreviousLog))
                Parsed.Text = AppController.PreviousLog;
        }

        /// <summary>
        /// Toggles the controls on the main window
        /// </summary>
        /// <param name="enable"></param>
        private void ToggleControls(bool enable = false)
        {
            Dispatcher?.Invoke(() =>
            {
                IsMinButtonEnabled = enable;
                //IsCloseButtonEnabled = enable;

                Parse.IsEnabled = enable;
                SaveParsed.IsEnabled = enable;
                CopyParsedToClipboard.IsEnabled = enable;
                Parsed.IsEnabled = enable;
                CheckForUpdatesOnStartup.IsEnabled = enable;
                RemoveTimestamps.IsEnabled = enable;
                Logo.IsEnabled = enable;

                foreach (MenuItem item in MenuStrip.Items)
                {
                    item.IsEnabled = enable;
                }

                OpenProgramSettings.IsEnabled = enable;
                OpenGithubProject.IsEnabled = enable;
                OpenGithubReleases.IsEnabled = enable;
                OpenUCP.IsEnabled = enable;
                OpenFacebrowser.IsEnabled = enable;
                OpenForums.IsEnabled = enable;
            });
        }

        /// <summary>
        /// Tries checking for updates
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckForUpdatesToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            TryCheckingForUpdates(true);
        }

        /// <summary>
        /// Disables the controls on the main window
        /// and checks for updates
        /// </summary>
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private void TryCheckingForUpdates(bool manual = false)
        {
            if (!_isUpdateCheckRunning)
            {
                _isUpdateCheckRunning = true;
                _resetEvent.Reset();

                UpdateCheckProgress.Visibility = Visibility.Visible;
                UpdateCheckProgress.IsActive = true;

                if (manual)
                    ToggleControls();

                _isUpdateCheckManual = manual;
                ThreadPool.QueueUserWorkItem(_ => CheckForUpdates(ref _isUpdateCheckManual));
                ThreadPool.QueueUserWorkItem(_ => FinishUpdateCheck());
            }
            else if (manual && !_isUpdateCheckManual)
            {
                _isUpdateCheckManual = true;
                ToggleControls();
            }
        }

        /// <summary>
        /// Enables the controls on the main window
        /// and disables the progress ring
        /// </summary>
        private void FinishUpdateCheck()
        {
            _resetEvent.WaitOne();
            
            ToggleControls(true);
            StopUpdateIndicator();
            
            _isUpdateCheckRunning = false;
        }

        /// <summary>
        /// Disables the progress ring
        /// </summary>
        private void StopUpdateIndicator()
        {
            Dispatcher?.Invoke(() =>
            {
                UpdateCheckProgress.IsActive = false;
                UpdateCheckProgress.Visibility = Visibility.Collapsed;
            });
        }

        /// <summary>
        /// Displays a message box
        /// on the main UI thread
        /// </summary>
        /// <param name="text"></param>
        /// <param name="title"></param>
        /// <param name="buttons"></param>
        /// <param name="image"></param>
        private void DisplayUpdateMessage(string text, string title, MessageBoxButton buttons, MessageBoxImage image)
        {
            ToggleControls(true);
            StopUpdateIndicator();

            Dispatcher?.Invoke(() =>
            {
                if (MessageBox.Show(text, title, buttons, image) == MessageBoxResult.Yes)
                    Process.Start(Strings.ReleasesLink);
            });
        }

        private void PromptForUpdate(Release release, string installedVersion, string newVersion)
        {
            Dispatcher?.Invoke(() =>
            {
                string message = "A new version of the chat log parser is available.\n\nInstalled Version: " + installedVersion
                    + "\nAvailable Version: " + newVersion
                    + "\n\nDownload, verify and install it now? The current version will be kept for rollback.";
                if (MessageBox.Show(message, "Update Available", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                    ThreadPool.QueueUserWorkItem(_ => InstallUpdate(release));
            });
        }

        private void InstallUpdate(Release release)
        {
            ToggleControls();
            Dispatcher?.Invoke(() =>
            {
                UpdateCheckProgress.Visibility = Visibility.Visible;
                UpdateCheckProgress.IsActive = true;
            });

            string error;
            if (!UpdateController.TryInstall(release, out error))
            {
                Dispatcher?.Invoke(() =>
                {
                    ToggleControls(true);
                    StopUpdateIndicator();
                    MessageBox.Show("The update could not be installed.\n\n" + error, "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
                return;
            }

            Dispatcher?.Invoke(() => System.Windows.Application.Current.Shutdown());
        }

        private void RestorePreviousVersionToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ToggleControls();
            Dispatcher?.Invoke(() =>
            {
                UpdateCheckProgress.Visibility = Visibility.Visible;
                UpdateCheckProgress.IsActive = true;
            });

            ThreadPool.QueueUserWorkItem(_ => FindPreviousOfficialRelease());
        }

        private void FindPreviousOfficialRelease()
        {
            try
            {
                IReadOnlyList<Release> releases = GetOfficialReleases();
                Release previousRelease = releases.FirstOrDefault(release => !release.Prerelease && IsVersionNewer(AppController.Version, release.TagName));

                if (previousRelease == null)
                    throw new IOException("No earlier stable release is available on GitHub.");

                Dispatcher?.Invoke(() =>
                {
                    ToggleControls(true);
                    StopUpdateIndicator();

                    string message = "Revert GTAWAssistant from " + AppController.Version + " to " + previousRelease.TagName
                        + "?\n\nThe release will be downloaded from GitHub, verified, and your current version will be kept as a rollback option.";
                    if (MessageBox.Show(message, "Revert to Previous Release", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        ThreadPool.QueueUserWorkItem(_ => InstallUpdate(previousRelease));
                });
            }
            catch (Exception exception)
            {
                Dispatcher?.Invoke(() => OfferLocalRollback(GetFriendlyExceptionMessage(exception)));
            }
        }

        private void OfferLocalRollback(string remoteError)
        {
            ToggleControls(true);
            StopUpdateIndicator();

            if (!UpdateController.HasRollback())
            {
                MessageBox.Show("The previous release could not be retrieved from GitHub.\n\n" + remoteError,
                    "Revert Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string message = "The previous release could not be retrieved from GitHub.\n\n" + remoteError
                + "\n\nA local rollback copy is available. Restore it instead?";
            if (MessageBox.Show(message, "Restore Previous Version", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                RestoreLocalPreviousVersion();
        }

        private void RestoreLocalPreviousVersion()
        {
            string error;
            if (!UpdateController.TryRestorePreviousVersion(out error))
            {
                MessageBox.Show("The previous version could not be restored.\n\n" + error, "Restore Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            System.Windows.Application.Current.Shutdown();
        }

        /// <summary>
        /// Checks for updates
        /// </summary>
        /// <param name="manual"></param>
#pragma warning disable 162
        [SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalse")]
        [SuppressMessage("ReSharper", "UnreachableCode")]
        private void CheckForUpdates(ref bool manual)
        {
            try
            {
                string installedVersion = AppController.Version;
                IReadOnlyList<Release> releases = GetOfficialReleases();

                string newVersion = string.Empty;
                bool isNewVersionBeta = false;
                Release selectedRelease = null;

                // Prereleases are a go
                if (!Properties.Settings.Default.IgnoreBetaVersions)
                {
                    selectedRelease = releases.FirstOrDefault();
                }
                else
                {
                    // If the user does not want to
                    // look for prereleases during
                    // the update check, ignore them
                    foreach (Release release in releases)
                    {
                        if (release.Prerelease)
                            continue;

                        selectedRelease = release;
                        break;
                    }
                }

                if (selectedRelease == null)
                    throw new IOException("No GitHub release could be found.");

                newVersion = selectedRelease.TagName;
                isNewVersionBeta = selectedRelease.Prerelease;

                if (IsVersionNewer(newVersion, installedVersion))
                { // Update available
                    if (Visibility != Visibility.Visible)
                        ResumeTrayStripMenuItem_Click(this, EventArgs.Empty);

                    PromptForUpdate(selectedRelease, installedVersion + (AppController.IsBetaVersion ? " Beta" : string.Empty), newVersion + (isNewVersionBeta ? " Beta" : string.Empty));
                }
                else if (manual) // Latest version
                    DisplayUpdateMessage(string.Format(Strings.RunningLatest, installedVersion + (AppController.IsBetaVersion ? " Beta" : string.Empty)), Strings.Information, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception exception)
            {
                if (manual)
                    DisplayUpdateMessage(string.Format(Strings.NoInternet, AppController.Version + (AppController.IsBetaVersion ? " Beta" : string.Empty))
                        + "\n\n" + GetFriendlyExceptionMessage(exception), Strings.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }

            _resetEvent.Set();
        }

        private IReadOnlyList<Release> GetOfficialReleases()
        {
            Exception lastException = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    return _client.Repository.Release.GetAll("AdvGTAW", GitHubRepository).GetAwaiter().GetResult();
                }
                catch (Exception exception)
                {
                    lastException = UnwrapException(exception);
                    if (attempt == 0)
                        Thread.Sleep(750);
                }
            }

            throw new IOException("GitHub could not be reached after two attempts: " + GetFriendlyExceptionMessage(lastException), lastException);
        }

        private static Exception UnwrapException(Exception exception)
        {
            AggregateException aggregate = exception as AggregateException;
            if (aggregate != null)
            {
                AggregateException flattened = aggregate.Flatten();
                if (flattened.InnerExceptions.Count == 1)
                    return UnwrapException(flattened.InnerExceptions[0]);
            }

            return exception;
        }

        private static string GetFriendlyExceptionMessage(Exception exception)
        {
            Exception rootException = UnwrapException(exception);
            return string.IsNullOrWhiteSpace(rootException?.Message) ? "The connection to GitHub failed." : rootException.Message;
        }
#pragma warning restore 162

        private static bool IsVersionNewer(string available, string installed)
        {
            Version availableVersion;
            Version installedVersion;
            if (!TryParseVersion(available, out availableVersion) || !TryParseVersion(installed, out installedVersion))
                return string.CompareOrdinal(installed, available) < 0;

            return availableVersion.CompareTo(installedVersion) > 0;
        }

        private static bool TryParseVersion(string value, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string normalized = value.Trim().TrimStart('v', 'V');
            int prerelease = normalized.IndexOf('-');
            if (prerelease >= 0)
                normalized = normalized.Substring(0, prerelease);

            string[] parts = normalized.Split('.');
            int[] numbers = new[] { 0, 0, 0, 0 };
            if (parts.Length == 0 || parts.Length > 4)
                return false;

            for (int i = 0; i < parts.Length; i++)
                if (!int.TryParse(parts[i], out numbers[i]))
                    return false;

            version = new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
            return true;
        }

        /// <summary>
        /// Opens the backup settings window
        /// </summary>
        private static BackupSettingsWindow backupSettings;
        private void BackupSettingsToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (Properties.Settings.Default.BackupChatLogAutomatically)
            {
                if (!Properties.Settings.Default.DisableWarningPopups && MessageBox.Show(Strings.BackupWillBeOff, Strings.Warning, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                    return;

                StatusLabel.Content = string.Format(Strings.BackupStatus, Strings.Disabled);
            }
            else
                if (!Properties.Settings.Default.DisableInformationPopups)
                    MessageBox.Show(Strings.SettingsAfterClose, Strings.Information, MessageBoxButton.OK, MessageBoxImage.Information);

            BackupController.AbortAll();
            SaveSettings();

            if (backupSettings == null)
            {
                backupSettings = new BackupSettingsWindow(this);
                backupSettings.IsVisibleChanged += (s, args) =>
                {
                    if ((bool)args.NewValue) return;
                    BackupController.Initialize();
                    StatusLabel.Content = string.Format(Strings.BackupStatus,
                        Properties.Settings.Default.BackupChatLogAutomatically ? Strings.Enabled : Strings.Disabled);
                };
                backupSettings.Closed += (s, args) =>
                {
                    backupSettings = null;
                };
            }

            backupSettings.ShowDialog();
        }

        /// <summary>
        /// Opens the chat log filter window
        /// </summary>
        private static ChatLogFilterWindow chatLogFilter;
        private void FilterChatLogToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();

            if (chatLogFilter == null)
            {
                chatLogFilter = new ChatLogFilterWindow(this);
                chatLogFilter.Closed += (s, args) =>
                {
                    chatLogFilter = null;
                };
            }

            chatLogFilter.ShowDialog();
        }

        /// <summary>
        /// Displays some information about the application
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AboutToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            // ReSharper disable once UnreachableCode
#pragma warning disable 162
            MessageBox.Show(string.Format(Strings.About, AppController.Version, AppController.IsBetaVersion ? Strings.Beta : string.Empty, AppController.ResourceDirectory), Strings.Information, MessageBoxButton.OK, MessageBoxImage.Information);
#pragma warning restore 162
        }

        /// <summary>
        /// Quits the application from the tool strip
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ExitToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        /// <summary>
        /// Handles clicks on the logo
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Logo_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            //if (MessageBox.Show(Strings.OpenDocumentation, Strings.Information, MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            //    Process.Start(Strings.FeatureShowcaseLink);
        }

        /// <summary>
        /// Asks the user if they are sure they want to exit
        /// if automatic backup is enabled.
        /// Saves the settings before the main window closes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Main_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!isRestarting)
            {
                if (Properties.Settings.Default.BackupChatLogAutomatically && _trayIcon.Visible == false)
                {
                    MessageBoxResult result = MessageBoxResult.Yes;
                    if (!Properties.Settings.Default.AlwaysCloseToTray)
                        result = MessageBox.Show(Strings.MinimizeInsteadOfClose, Strings.Warning, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

                    // ReSharper disable once ConvertIfStatementToSwitchStatement
                    if (result == MessageBoxResult.Yes)
                    {
                        e.Cancel = true;

                        Hide();
                        _trayIcon.Visible = true;

                        return;
                    }

                    if (result == MessageBoxResult.Cancel)
                    {
                        e.Cancel = true;
                        return;
                    }
                }
            }

            StyleController.StopWatchers();
            BackupController.Quitting = true;
            SaveSettings();

            System.Windows.Application.Current.Shutdown();
        }

        /// <summary>
        /// Resumes and shows the main window by double clicking the tray icon
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TrayIcon_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            ResumeTrayStripMenuItem_Click(sender, EventArgs.Empty);
        }

        /// <summary>
        /// Resumes and shows the main window from the tray menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ResumeTrayStripMenuItem_Click(object sender, EventArgs e)
        {
            if (isRestarting)
                return;

            Show();
            _trayIcon.Visible = false;

            if (CheckForUpdatesOnStartup.IsChecked == true)
                TryCheckingForUpdates();
        }

        /// <summary>
        /// Quits the application from the tray
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ExitTrayToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BackupController.Quitting = true;
            StyleController.StopWatchers();

            _trayIcon.Visible = false;
            isRestarting = true;
            System.Windows.Application.Current.Shutdown();
        }

        /// <summary>
        /// Initializes the tray icon
        /// </summary>
        private void InitializeTrayIcon()
        {
            _trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Visible = false,
                Icon = Properties.Resources.AppIcon,
                Text= @"GTA World Chat Log Assistant"
            };

            _trayIcon.MouseDoubleClick += TrayIcon_MouseDoubleClick;

            _trayIcon.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            _trayIcon.ContextMenuStrip.Items.Add(@"Open", null, ResumeTrayStripMenuItem_Click);
            _trayIcon.ContextMenuStrip.Items.Add(@"Exit", null, ExitTrayToolStripMenuItem_Click);
        }

        /// <summary>
        /// Opens the program settings window
        /// </summary>
        private static ProgramSettingsWindow programSettings;
        private void OpenProgramSettings_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();

            if (programSettings == null)
            {
                programSettings = new ProgramSettingsWindow(this);
                programSettings.Closed += (s, args) =>
                {
                    _client = new GitHubClient(new ProductHeaderValue(AppController.ProductHeader));
                    _client.SetRequestTimeout(new TimeSpan(0, 0, 0, Properties.Settings.Default.UpdateCheckTimeout));

                    programSettings = null;
                };
            }

            programSettings.ShowDialog();
        }

        /// <summary>
        /// Opens the Github Project page in the default browser
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenGithubProject_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(Strings.ProjectLink);
        }

        /// <summary>
        /// Opens the Github Releases page in the default browser
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenGithubReleases_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(Strings.ReleasesLink);
        }

        /// <summary>
        /// Opens the UCP in the default browser
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenUCP_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(Strings.UCPLink);
        }

        /// <summary>
        /// Opens Facebrowser in the default browser
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenFacebrowser_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(Strings.FacebrowserLink);
        }

        /// <summary>
        /// Opens the Forums in the default browser
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenForums_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(Strings.ForumsLink);
        }
    }
}
