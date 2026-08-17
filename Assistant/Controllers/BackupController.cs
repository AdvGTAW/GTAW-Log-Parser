using System;
using System.IO;
using System.Windows;
using System.Threading;
using System.Diagnostics.CodeAnalysis;
using Assistant.Utilities;
using Assistant.Localization;

namespace Assistant.Controllers
{
    public static class BackupController
    {
        private static Thread backupThread;
        private static Thread intervalThread;

        private static string backupPath;

        private static bool enableAutomaticBackup;
        private static bool enableIntervalBackup;

        private static bool runBackgroundBackup;
        private static bool runBackgroundInterval;
        public static bool Quitting = false;

        private const int GameClosedCheckTime = 10;
        private static bool isGameRunning;

        /// <summary>
        /// Displays a message box
        /// on the main UI thread
        /// </summary>
        /// <param name="text"></param>
        /// <param name="title"></param>
        /// <param name="buttons"></param>
        /// <param name="image"></param>
        private static void DisplayBackupResultMessage(string text, string title, MessageBoxButton buttons, MessageBoxImage image)
        {
            Application.Current.Dispatcher?.Invoke(() =>
            {
                MessageBox.Show(text, title, buttons, image);
            });
        }

        /// <summary>
        /// Starts the backup threads if they are enabled
        /// or resumes them if they are queued to stop
        /// </summary>
        [SuppressMessage("ReSharper", "InvertIf")]
        public static void Initialize()
        {
            backupPath = Properties.Settings.Default.BackupPath;

            enableAutomaticBackup = Properties.Settings.Default.BackupChatLogAutomatically;
            enableIntervalBackup = Properties.Settings.Default.EnableIntervalBackup;

            if (string.IsNullOrWhiteSpace(backupPath) || !Directory.Exists(backupPath))
                return;

            FiveMChatCaptureController.Initialize();

            ResumeIfQueuedToStop();

            if (enableAutomaticBackup && (backupThread == null || !backupThread.IsAlive))
            {
                runBackgroundBackup = true;

                backupThread = new Thread(BackupWorker);
                backupThread.Start();
            }

            if (enableIntervalBackup && (intervalThread == null || !intervalThread.IsAlive))
            {
                runBackgroundInterval = true;

                intervalThread = new Thread(IntervalWorker);
                intervalThread.Start();
            }
        }

        /// <summary>
        /// Gracefully stops the automatic backup thread
        /// </summary>
        private static void AbortAutomaticBackup()
        {
            if (backupThread != null && backupThread.IsAlive)
                runBackgroundBackup = false;
        }

        /// <summary>
        /// Gracefully stops the interval backup thread
        /// </summary>
        private static void AbortIntervalBackup()
        {
            if (intervalThread != null && intervalThread.IsAlive)
                runBackgroundInterval = false;
        }

        /// <summary>
        /// Resumes the threads if they are queued to stop
        /// </summary>
        private static void ResumeIfQueuedToStop()
        {
            if (backupThread != null && backupThread.IsAlive && !runBackgroundBackup && !Quitting)
                runBackgroundBackup = true;

            if (intervalThread != null && intervalThread.IsAlive && !runBackgroundInterval && !Quitting)
                runBackgroundInterval = true;
        }

        /// <summary>
        /// Gracefully stops all threads
        /// </summary>
        public static void AbortAll()
        {
            AbortIntervalBackup();
            AbortAutomaticBackup();
        }

        /// <summary>
        /// Runs the main backup thread
        /// </summary>
        private static void BackupWorker()
        {
            while (!Quitting && runBackgroundBackup)
            {
                bool fiveMRunning = AppController.IsFiveMRunning();

                if (!isGameRunning && fiveMRunning)
                    isGameRunning = true;
                else if (isGameRunning && !fiveMRunning)
                {
                    isGameRunning = false;
                    ParseThenSaveToFile(true);
                }

                Thread.Sleep(GameClosedCheckTime * 1000);
            }
        }

        /// <summary>
        /// Runs the interval backup thread
        /// </summary>
        private static void IntervalWorker()
        {
            while (!Quitting && runBackgroundInterval)
            {
                int intervalTime = Properties.Settings.Default.IntervalTime;

                if (isGameRunning && File.Exists(FiveMChatCaptureController.SessionFilePath))
                    ParseThenSaveToFile();

                for (int i = 0; i < intervalTime * 6; i++)
                {
                    if (Quitting || !runBackgroundInterval)
                        break;

                    Thread.Sleep(10 * 1000);
                }
            }
        }

        /// <summary>
        /// Parses the current chat log and saves it. This
        /// function is called from the backup threads
        /// </summary>
        /// <param name="gameClosed"></param>
        private static void ParseThenSaveToFile(bool gameClosed = false)
        {
            try
            {
                // Parse the chat log
                string parsed = AppController.ParseChatLog(Properties.Settings.Default.RemoveTimestampsFromBackup, gameClosed);
                if (string.IsNullOrWhiteSpace(parsed))
                    return;

                DateTime startedAt = FiveMChatCaptureController.SessionStartedAt;
                string fileName = startedAt.ToString("dd.MMM.yyyy-HH.mm.ss", System.Globalization.CultureInfo.InvariantCulture).ToUpperInvariant() + ".txt";
                string path = Path.Combine(backupPath, startedAt.ToString("yyyy", System.Globalization.CultureInfo.InvariantCulture), startedAt.ToString("MMM", System.Globalization.CultureInfo.InvariantCulture).ToUpperInvariant()) + Path.DirectorySeparatorChar;

                // Make sure directory exists
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                // There is no backup file with this name yet,
                // we are good to go
                if (!File.Exists(path + fileName))
                {
                    using (StreamWriter sw = new StreamWriter(path + fileName))
                    {
                        sw.Write(parsed);
                    }
                }
                else
                {
                    // If the file already exists (i.e. backed up from the interval worker)
                    // check if the current chat log is larger than the old one
                    
                    // Remove any temporary files that may
                    // exist for some reason
                    if (File.Exists(path + ".temp"))
                        File.Delete(path + ".temp");
                    
                    // Write a temporary file
                    using (StreamWriter sw = new StreamWriter(path + ".temp"))
                    {
                        sw.Write(parsed);
                    }

                    // Check to see which file is bigger
                    FileInfo oldFile = new FileInfo(path + fileName);
                    FileInfo newFile = new FileInfo(path + ".temp");

                    // New file larger, overwrite the old file
                    if (oldFile.Length < newFile.Length)
                    {
                        File.Delete(path + fileName);
                        File.Move(path + ".temp", path + fileName);
                    }
                    else // Old file larger, delete the temporary file
                        File.Delete(path + ".temp");
                }

                if (!gameClosed) return;
                if (!Properties.Settings.Default.SuppressNotifications)
                    DisplayBackupResultMessage(string.Format(Strings.SuccessfulBackup, path + fileName), Strings.Information, MessageBoxButton.OK, MessageBoxImage.Information);

                // Save the MD5 hash of the chat log
                if (Properties.Settings.Default.WarnOnSameHash)
                    HashGenerator.SaveParsedHash(parsed);
            }
            catch
            {
                if (gameClosed)
                    DisplayBackupResultMessage(Strings.BackupError, Strings.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
