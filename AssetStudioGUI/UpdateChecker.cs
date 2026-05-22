using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AssetStudioGUI
{
    internal class UpdateChecker
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/CGWang/AssetStudio/releases/latest";
        private const string UpdateTempDir = "_update_temp";

        private readonly Action<string> _statusUpdate;

        public string LatestVersion { get; private set; }
        public string DownloadUrl { get; private set; }
        public string DownloadFileName { get; private set; }

        public UpdateChecker(Action<string> statusUpdate)
        {
            _statusUpdate = statusUpdate;
        }

        public async Task CheckAndPromptAsync()
        {
            try
            {
                var hasUpdate = await CheckForUpdateAsync();
                if (!hasUpdate) return;

                var result = MessageBox.Show(
                    $"发现新版本 {LatestVersion}，是否下载更新？\n\n当前版本: v{GetCurrentVersion()}",
                    "版本更新",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result != DialogResult.Yes) return;

                var zipPath = await DownloadUpdateAsync();
                if (zipPath == null) return;

                var applyResult = MessageBox.Show(
                    "下载完成，是否立即更新并重启？",
                    "版本更新",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (applyResult == DialogResult.Yes)
                {
                    ApplyUpdateAndRestart(zipPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Update check failed: {ex.Message}");
            }
        }

        private async Task<bool> CheckForUpdateAsync()
        {
            using (var client = CreateHttpClient())
            {
                var json = await client.GetStringAsync(GitHubApiUrl);
                var release = JObject.Parse(json);

                var tagName = release["tag_name"]?.ToString();
                if (string.IsNullOrEmpty(tagName)) return false;

                LatestVersion = tagName;
                var remoteVersion = ParseVersion(tagName);
                var localVersion = new Version(GetCurrentVersion());

                if (remoteVersion <= localVersion) return false;

                var assetName = GetTargetAssetName();
                var assets = release["assets"] as JArray;
                if (assets == null) return false;

                foreach (var asset in assets)
                {
                    var name = asset["name"]?.ToString();
                    if (string.Equals(name, assetName, StringComparison.OrdinalIgnoreCase))
                    {
                        DownloadUrl = asset["browser_download_url"]?.ToString();
                        DownloadFileName = name;
                        return true;
                    }
                }
            }
            return false;
        }

        private async Task<string> DownloadUpdateAsync()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var tempDir = Path.Combine(appDir, UpdateTempDir);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            var zipPath = Path.Combine(tempDir, DownloadFileName);

            using (var client = CreateHttpClient())
            {
                client.Timeout = TimeSpan.FromMinutes(10);
                _statusUpdate?.Invoke($"正在下载更新 {LatestVersion}...");

                using (var response = await client.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength ?? -1;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[81920];
                        long downloaded = 0;
                        int bytesRead;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            downloaded += bytesRead;

                            if (totalBytes > 0)
                            {
                                var pct = (int)(downloaded * 100 / totalBytes);
                                _statusUpdate?.Invoke($"正在下载更新 {LatestVersion}... {pct}%");
                            }
                        }
                    }
                }
            }

            _statusUpdate?.Invoke("下载完成");
            return zipPath;
        }

        private void ApplyUpdateAndRestart(string zipPath)
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var tempDir = Path.Combine(appDir, UpdateTempDir);
            var extractDir = Path.Combine(tempDir, "extracted");

            ZipFile.ExtractToDirectory(zipPath, extractDir);

            var sourceDir = ResolveExtractedRoot(extractDir);
            var exeName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
            var batPath = Path.Combine(tempDir, "update.bat");

            var batContent = $@"@echo off
chcp 65001 >nul
echo 正在更新，请稍候...
timeout /t 2 /nobreak >nul
xcopy /s /y /i ""{sourceDir}"" ""{appDir}"" >nul 2>&1
rd /s /q ""{tempDir}""
start """" ""{Path.Combine(appDir, exeName)}""
exit
";
            File.WriteAllText(batPath, batContent, System.Text.Encoding.UTF8);

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(psi);

            Application.Exit();
        }

        private static string ResolveExtractedRoot(string extractDir)
        {
            var dirs = Directory.GetDirectories(extractDir);
            var files = Directory.GetFiles(extractDir);
            if (dirs.Length == 1 && files.Length == 0)
                return dirs[0];
            return extractDir;
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "AssetStudio-AutoUpdater");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            client.Timeout = TimeSpan.FromSeconds(15);
            return client;
        }

        private static string GetCurrentVersion()
        {
            return Application.ProductVersion;
        }

        private static Version ParseVersion(string tag)
        {
            var v = tag.TrimStart('v', 'V');
            if (Version.TryParse(v, out var result))
                return result;
            return new Version(0, 0);
        }

        private static string GetTargetAssetName()
        {
#if NETFRAMEWORK
            return "AssetStudioGUI-net472.zip";
#else
            return "AssetStudioGUI-net6.zip";
#endif
        }
    }
}
