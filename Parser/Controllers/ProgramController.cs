using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Parser.Localization;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace Parser.Controllers
{
    public static class ProgramController
    {
        public const string AssemblyVersion = "4.1.7";
        public static readonly string Version = $"v{AssemblyVersion}";
        public const bool IsBetaVersion = false;
        public const string ParameterPrefix = "--";

        public static string ResourceDirectory;
        public static string LogLocation;

        /// <summary>
        /// Initializes the server IPs matching with the
        /// current server depending on the chosen locale
        /// and determines the newest log file if multiple
        /// server IPs are used to connect to the server
        /// </summary>
        public static void InitializeServerIp()
        {
            try
            {
                ResourceDirectory = "Not Found";
                LogLocation = $"client_resources\\{@"play.gta.world_22005"}\\.storage";

                // Get the directory path from user settings
                // Return if the user has not picked a RAGEMP directory path yet
                string directoryPath = Properties.Settings.Default.DirectoryPath;
                if (string.IsNullOrWhiteSpace(directoryPath)) return;

                // Get every directory in the client_resources directory found inside directoryPath
                string clientResourcesPath = Path.Combine(directoryPath, "client_resources");
                if (!Directory.Exists(clientResourcesPath)) return;

                // Get all ".storage" files in the client resources directory
                var potentialLogs = Directory.GetFiles(clientResourcesPath, "*.storage")
                                             // Filter files that contain the server version tag
                                             .Where(file => Regex.IsMatch(File.ReadAllText(file), "\\\"server_version\\\":\\\"GTA World[^\"]*\""))
                                             // Order files by last write time in descending order
                                             .OrderByDescending(file => new FileInfo(file).LastWriteTimeUtc)
                                             .ToList();

                if (potentialLogs.Count == 0) return;
                string latestLog = potentialLogs[0];

                // Find the index of the last directory separator
                int finalSeparator = latestLog.LastIndexOf(Path.DirectorySeparatorChar);
                if (finalSeparator == -1) return;

                // Finally, set the log location & extract the directory name from the latest log file path
                ResourceDirectory = latestLog.Substring(finalSeparator + 1);
                LogLocation = Path.Combine("client_resources", ResourceDirectory, ".storage");
            }
            catch
            {
                // Silent exception
            }
        }

        /// <summary>
        /// Parses the most recent chat log found at the
        /// selected RAGEMP directory path and returns it.
        /// Displays an error if a chat log does not
        /// exist or if it has an incorrect format
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <param name="removeTimestamps"></param>
        /// <returns></returns>
        public static string ParseChatLog(string directoryPath, bool removeTimestamps)
        {
            try
            {
                // Read the chat log
                string logFilePath = Path.Combine(directoryPath, LogLocation);
                if (!File.Exists(logFilePath))
                {
                    throw new FileNotFoundException($"Chat log file not found: {logFilePath}");
                }

                string log;
                using (StreamReader sr = new StreamReader(logFilePath))
                {
                    log = sr.ReadToEnd();
                }

                // Use REGEX to parse the chat_log section only.
                // Why REGEX? It's way faster than loading the massive JSON object in memory and then getting only the chat_log part.
                log = Regex.Match(log, "(?<=chat_log\\\":\\\")(.*?)(?=\\\\n\\\",\\\"rememberuser)").Value;

                if (string.IsNullOrWhiteSpace(log))
                {
                    throw new FormatException("Chat log is empty or in an incorrect format.");
                }

                log = WebUtility.HtmlDecode(log);
                log = log.Replace("\\n", "\n");

                if (removeTimestamps)
                {
                    log = Regex.Replace(log, @"\[\d{1,2}:\d{1,2}:\d{1,2}\] ", string.Empty);
                }

                return log;
            }
            catch (FileNotFoundException ex)
            {
                MessageBox.Show($"Error: {ex.Message}", Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (FormatException ex)
            {
                MessageBox.Show($"Error: {ex.Message}", Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error occurred: {ex.Message}", Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return string.Empty;
        }
    }
}
