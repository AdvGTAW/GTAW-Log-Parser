using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using Octokit;

namespace Assistant.Controllers
{
    internal static class UpdateController
    {
        private const string Owner = "AdvGTAW";
        private const string Repository = "GTAW-Log-Parser";
        private const string AssistantAssetName = "GTAWAssistant.exe";
        private const string MiniAssetName = "ParserMini.exe";

        private sealed class UpdateFile
        {
            public string TargetPath;
            public string BackupPath;
            public string DownloadPath;
        }

        public static bool HasRollback()
        {
            string assistantBackup = GetBackupPath(AppController.ExecutablePath);
            return File.Exists(assistantBackup);
        }

        public static bool TryInstall(Release release, out string error)
        {
            error = null;
            try
            {
                if (release == null)
                    throw new InvalidOperationException("No release was returned by GitHub.");

                string directory = AppController.StartupPath;
                if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                    throw new IOException("The application folder could not be found.");

                EnsureWritableDirectory(directory);
                EnsureWritableDirectory(GetRollbackDirectory());

                List<UpdateFile> files = new List<UpdateFile>();
                ReleaseAsset assistant = FindAsset(release, AssistantAssetName);
                if (assistant == null)
                    throw new IOException("The release does not include GTAWAssistant.exe.");

                files.Add(DownloadAsset(release, assistant, AppController.ExecutablePath));

                ReleaseAsset mini = FindAsset(release, MiniAssetName);
                string miniPath = Path.Combine(directory, MiniAssetName);
                if (mini != null && File.Exists(miniPath))
                    files.Add(DownloadAsset(release, mini, miniPath));

                StartReplacementScript(files, AppController.ExecutablePath, Process.GetCurrentProcess().Id, false);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public static bool TryRestorePreviousVersion(out string error)
        {
            error = null;
            try
            {
                string assistantBackup = GetBackupPath(AppController.ExecutablePath);
                if (!File.Exists(assistantBackup))
                    throw new IOException("No previous GTAWAssistant version is available.");

                EnsureWritableDirectory(GetRollbackDirectory());

                List<UpdateFile> files = new List<UpdateFile>
                {
                    new UpdateFile { TargetPath = AppController.ExecutablePath, BackupPath = assistantBackup }
                };

                string miniPath = Path.Combine(AppController.StartupPath, MiniAssetName);
                string miniBackup = GetBackupPath(miniPath);
                if (File.Exists(miniPath) && File.Exists(miniBackup))
                    files.Add(new UpdateFile { TargetPath = miniPath, BackupPath = miniBackup });

                StartReplacementScript(files, AppController.ExecutablePath, Process.GetCurrentProcess().Id, true);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static ReleaseAsset FindAsset(Release release, string name)
        {
            return release.Assets.FirstOrDefault(asset => string.Equals(asset.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static UpdateFile DownloadAsset(Release release, ReleaseAsset asset, string targetPath)
        {
            string expectedHash = GetAssetDigest(release.TagName, asset.Name);
            if (string.IsNullOrWhiteSpace(expectedHash))
                throw new IOException("GitHub did not provide a SHA-256 digest for " + asset.Name + ".");

            string directory = Path.Combine(Path.GetTempPath(), "GTAW-Log-Parser", "updates", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string downloadPath = Path.Combine(directory, asset.Name);

            using (WebClient client = new WebClient())
            {
                client.Headers[HttpRequestHeader.UserAgent] = AppController.ProductHeader;
                client.DownloadFile(asset.BrowserDownloadUrl, downloadPath);
            }

            string downloadedHash = ComputeSha256(downloadPath);
            if (!string.Equals(expectedHash, downloadedHash, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(downloadPath);
                throw new IOException("The downloaded " + asset.Name + " did not match GitHub's SHA-256 digest.");
            }

            return new UpdateFile
            {
                TargetPath = targetPath,
                BackupPath = GetBackupPath(targetPath),
                DownloadPath = downloadPath
            };
        }

        private static string GetAssetDigest(string tagName, string assetName)
        {
            string url = "https://api.github.com/repos/" + Owner + "/" + Repository + "/releases/tags/" + Uri.EscapeDataString(tagName);
            HttpWebRequest request = (HttpWebRequest) WebRequest.Create(url);
            request.UserAgent = AppController.ProductHeader;
            request.Timeout = 10000;

            using (HttpWebResponse response = (HttpWebResponse) request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                IDictionary<string, object> release = serializer.DeserializeObject(reader.ReadToEnd()) as IDictionary<string, object>;
                object[] assets = release != null && release.ContainsKey("assets") ? release["assets"] as object[] : null;
                if (assets == null)
                    return null;

                foreach (object value in assets)
                {
                    IDictionary<string, object> asset = value as IDictionary<string, object>;
                    if (asset == null || !asset.ContainsKey("name") || !string.Equals(asset["name"] as string, assetName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string digest = asset.ContainsKey("digest") ? asset["digest"] as string : null;
                    return string.IsNullOrWhiteSpace(digest) ? null : digest.Replace("sha256:", string.Empty);
                }
            }

            return null;
        }

        private static void EnsureWritableDirectory(string directory)
        {
            Directory.CreateDirectory(directory);
            string probePath = Path.Combine(directory, ".gtaw-update-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (FileStream stream = new FileStream(probePath, System.IO.FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    stream.WriteByte(0);
            }
            finally
            {
                if (File.Exists(probePath))
                    File.Delete(probePath);
            }
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string GetBackupPath(string targetPath)
        {
            string name = Path.GetFileNameWithoutExtension(targetPath);
            string extension = Path.GetExtension(targetPath);
            return Path.Combine(GetRollbackDirectory(), name + ".previous" + extension);
        }

        private static string GetRollbackDirectory()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "GTAW Log Parser", "Rollback");
        }

        private static void StartReplacementScript(IEnumerable<UpdateFile> files, string applicationPath, int processId, bool rollback)
        {
            string scriptPath = Path.Combine(Path.GetTempPath(), "GTAW-Log-Parser-update-" + Guid.NewGuid().ToString("N") + ".cmd");
            StringBuilder script = new StringBuilder();
            script.AppendLine("@echo off");
            script.AppendLine("chcp 65001 >nul");
            script.AppendLine("setlocal");
            script.AppendLine(":wait_for_app");
            script.AppendLine("tasklist /FI \"PID eq " + processId + "\" | find \"" + processId + "\" >nul");
            script.AppendLine("if not errorlevel 1 (");
            script.AppendLine("  timeout /t 1 /nobreak >nul");
            script.AppendLine("  goto wait_for_app");
            script.AppendLine(")");

            foreach (UpdateFile file in files)
            {
                if (rollback)
                {
                    string temporary = file.TargetPath + ".restore-temp";
                    script.AppendLine("copy /y \"" + file.TargetPath + "\" \"" + temporary + "\" >nul || goto failed");
                    script.AppendLine("copy /y \"" + file.BackupPath + "\" \"" + file.TargetPath + "\" >nul || goto failed");
                    script.AppendLine("copy /y \"" + temporary + "\" \"" + file.BackupPath + "\" >nul || goto failed");
                    script.AppendLine("del /q \"" + temporary + "\"");
                }
                else
                {
                    script.AppendLine("copy /y \"" + file.TargetPath + "\" \"" + file.BackupPath + "\" >nul || goto failed");
                    script.AppendLine("copy /y \"" + file.DownloadPath + "\" \"" + file.TargetPath + "\" >nul || goto failed");
                    script.AppendLine("del /q \"" + file.DownloadPath + "\"");
                }
            }

            script.AppendLine("start \"\" \"" + applicationPath + "\"");
            script.AppendLine("del \"%~f0\"");
            script.AppendLine("exit /b");
            script.AppendLine(":failed");
            script.AppendLine("start \"\" \"" + applicationPath + "\"");
            script.AppendLine("del \"%~f0\"");
            File.WriteAllText(scriptPath, script.ToString(), new UTF8Encoding(false));

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c \"" + scriptPath + "\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }
    }
}
