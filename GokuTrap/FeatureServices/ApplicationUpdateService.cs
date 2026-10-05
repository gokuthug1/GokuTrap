using System.Security.Cryptography;

namespace GokuTrap
{
    /// <summary>Single-flight application updater that keeps the existing -upgrade handoff.</summary>
    public sealed class ApplicationUpdateService
    {
        private const string LOG_IDENT = "ApplicationUpdateService";
        private static readonly SemaphoreSlim OperationLock = new(1, 1);

        public string Status { get; private set; } = FeatureText.Get("UpdateIdle");
        private InterProcessLock? _handoffLock;
        public double Progress { get; private set; }
        public UpdateCheckResult? AvailableUpdate { get; private set; }
        public event EventHandler? StateChanged;

        public bool IsCheckDue()
        {
            if (App.State.Prop.LastUpdateCheckUtc is not DateTimeOffset lastCheck)
                return true;
            int hours = Math.Clamp(App.Settings.Prop.Update.CheckIntervalHours, 1, 168);
            return DateTimeOffset.UtcNow - lastCheck >= TimeSpan.FromHours(hours);
        }

        public async Task<UpdateCheckResult?> CheckAsync(bool manual = false, CancellationToken cancellationToken = default)
        {
            if (!manual && (!App.Settings.Prop.CheckForUpdates || !IsCheckDue()))
                return null;
            if (!await OperationLock.WaitAsync(0, cancellationToken))
            {
                Status = FeatureText.Get("UpdateBusy");
                RaiseStateChanged();
                return null;
            }

            try
            {
                Status = FeatureText.Get("UpdateChecking");
                Progress = 0;
                RaiseStateChanged();
                GithubRelease? release = await GetEligibleReleaseAsync(cancellationToken);
                App.State.Prop.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
                App.State.Save();
                cancellationToken.ThrowIfCancellationRequested();
                if (release is null || release.Draft || (!App.Settings.Prop.Update.IncludePrerelease && release.Prerelease) || !TryGetVersion(release.TagName, out Version? remoteVersion) || remoteVersion is null)
                {
                    AvailableUpdate = null;
                    Status = FeatureText.Get("UpdateInvalidRelease");
                    RaiseStateChanged();
                    return null;
                }

                if (remoteVersion <= Utilities.GetVersionFromString(App.Version) || VersionsMatch(remoteVersion, Utilities.GetVersionFromString(App.Version)))
                {
                    AvailableUpdate = null;
                    Status = string.Format(FeatureText.Get("UpdateCurrent"), App.Version);
                    RaiseStateChanged();
                    return null;
                }

                GithubReleaseAsset? asset = SelectWindowsX64Asset(release.Assets);
                if (asset is null)
                {
                    AvailableUpdate = null;
                    Status = FeatureText.Get("UpdateNoAsset");
                    RaiseStateChanged();
                    return null;
                }

                AvailableUpdate = new UpdateCheckResult(release, asset, remoteVersion);
                Status = string.Format(FeatureText.Get("UpdateAvailable"), release.TagName, FeatureText.Get(HasSha256(asset) ? "UpdateDigest" : "UpdateNoDigest"));
                RaiseStateChanged();
                return AvailableUpdate;
            }
            catch (OperationCanceledException)
            {
                Status = FeatureText.Get("Cancelled");
                RaiseStateChanged();
                return null;
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidDataException)
            {
                AvailableUpdate = null;
                Status = FeatureText.Get("UpdateCheckFailed");
                App.Logger.WriteException(LOG_IDENT, ex);
                RaiseStateChanged();
                return null;
            }
            finally
            {
                OperationLock.Release();
            }
        }

        public async Task<bool> DownloadAndStartAsync(UpdateCheckResult update, LaunchMode launchMode, IEnumerable<string> originalArgs, CancellationToken cancellationToken = default)
        {
            if (!await OperationLock.WaitAsync(0, cancellationToken))
            {
                Status = FeatureText.Get("UpdateBusy");
                RaiseStateChanged();
                return false;
            }

            InterProcessLock? downloadLock = null;
            string? stagingPath = null;
            bool handoffStarted = false;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromMinutes(15));
            cancellationToken = deadline.Token;
            try
            {
                downloadLock = System.Windows.Application.Current.Dispatcher.Invoke(() => new InterProcessLock("UpdateDownload"));
                if (!downloadLock.IsAcquired) throw new IOException("Another update download is running");
                if (!Uri.TryCreate(update.Asset.BrowserDownloadUrl, UriKind.Absolute, out Uri? assetUri) || assetUri.Scheme != Uri.UriSchemeHttps || !IsGitHubDownloadHost(assetUri.Host))
                    throw new InvalidDataException("The release asset is not an approved HTTPS GitHub download.");
                if (!IsSafeWindowsAssetName(update.Asset.Name))
                    throw new InvalidDataException("The release asset name is not a supported Windows executable.");

                Directory.CreateDirectory(Paths.TempUpdates);
                string finalPath = Path.Combine(Paths.TempUpdates, Path.GetFileName(update.Asset.Name));
                stagingPath = finalPath + ".download";
                if (File.Exists(stagingPath))
                    File.Delete(stagingPath);

                Status = FeatureText.Get("UpdateDownloading");
                Progress = 0;
                RaiseStateChanged();
                using HttpResponseMessage response = await App.HttpClient.GetAsync(assetUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                Uri? finalUri = response.RequestMessage?.RequestUri;
                if (finalUri is null || finalUri.Scheme != "https" || !IsGitHubDownloadHost(finalUri.Host)) throw new InvalidDataException("Unexpected download redirect");
                long? contentLength = response.Content.Headers.ContentLength;
                if (contentLength is null or <= 0 or > 536870912 || update.Asset.Size <= 0 || contentLength != update.Asset.Size)
                    throw new InvalidDataException("The release asset length did not match the published metadata.");

                await using (Stream input = await response.Content.ReadAsStreamAsync(cancellationToken))
                await using (var output = new FileStream(stagingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] buffer = new byte[64 * 1024];
                    int read;
                    long total = 0;
                    while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                    {
                        await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                        total += read;
                        if (total > contentLength.Value) throw new InvalidDataException("Download exceeded its published length");
                        Progress = (double)total / contentLength.Value;
                        RaiseStateChanged();
                    }
                    if (total != contentLength.Value)
                        throw new InvalidDataException("The release asset download was incomplete.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                await Task.Run(() =>
                {
                    VerifyDigest(stagingPath, update.Asset.Digest);
                    VerifyWindowsX64Executable(stagingPath);
                    string? productVersion = FileVersionInfo.GetVersionInfo(stagingPath).ProductVersion;
                    if (productVersion is null || !VersionsMatch(Utilities.GetVersionFromString(productVersion), update.Version))
                        throw new InvalidDataException("Executable version does not match the release tag");
                }, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(stagingPath, finalPath, true);
                if (new FileInfo(finalPath).Length != contentLength.Value)
                    throw new InvalidDataException("The staged release asset failed its final length check.");

                Status = FeatureText.Get("UpdateStarting");
                Progress = 1;
                RaiseStateChanged();
                var startInfo = new ProcessStartInfo { FileName = finalPath, UseShellExecute = false };
                startInfo.ArgumentList.Add("-upgrade");
                foreach (string arg in originalArgs)
                    startInfo.ArgumentList.Add(arg);
                if (launchMode == LaunchMode.Player && !startInfo.ArgumentList.Contains("-player")) startInfo.ArgumentList.Add("-player");
                if (launchMode == LaunchMode.Studio && !startInfo.ArgumentList.Contains("-studio")) startInfo.ArgumentList.Add("-studio");
                App.Settings.Save();
                if (Process.GetProcessesByName(App.ProjectName).Length > 1) throw new IOException("Close other GokuTrap instances before updating");
                _handoffLock = System.Windows.Application.Current.Dispatcher.Invoke(() => new InterProcessLock("AutoUpdater"));
                if (!_handoffLock.IsAcquired) throw new IOException("Update handoff is already running");
                try { if (Process.Start(startInfo) is null) throw new IOException("Could not start upgrade"); }
                catch { System.Windows.Application.Current.Dispatcher.Invoke(_handoffLock.Dispose); _handoffLock = null; throw; }
                handoffStarted = true;
                return true;
            }
            catch (OperationCanceledException)
            {
                Status = FeatureText.Get("UpdateCancelled");
                RaiseStateChanged();
                return false;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or InvalidDataException or CryptographicException or System.ComponentModel.Win32Exception)
            {
                Status = FeatureText.Get("UpdateFailed");
                App.Logger.WriteException(LOG_IDENT, ex);
                RaiseStateChanged();
                return false;
            }
            finally
            {
                // The handoff mutex is held until exit only after a successful child launch.
                if (!handoffStarted && _handoffLock?.IsAcquired == true) { System.Windows.Application.Current.Dispatcher.Invoke(_handoffLock.Dispose); _handoffLock = null; }
                // Remove only this updater's incomplete staging file, never the installed executable.
                try { if (stagingPath is not null && File.Exists(stagingPath)) File.Delete(stagingPath); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { App.Logger.WriteLine(LOG_IDENT, "Staging cleanup deferred until retry"); }
                if (downloadLock is not null) System.Windows.Application.Current.Dispatcher.Invoke(downloadLock.Dispose);
                OperationLock.Release();
            }
        }

        private static async Task<GithubRelease?> GetEligibleReleaseAsync(CancellationToken cancellationToken)
        {
            if (!App.Settings.Prop.Update.IncludePrerelease)
                return await App.GetLatestRelease(cancellationToken);

            // The existing endpoint intentionally excludes prereleases. Only query GitHub's
            // official releases collection when the user explicitly opted into them.
            using HttpResponseMessage response = await App.HttpClient.GetAsync($"https://api.github.com/repos/{App.ProjectRepository}/releases?per_page=20", cancellationToken);
            response.EnsureSuccessStatusCode();
            List<GithubRelease>? releases = JsonSerializer.Deserialize<List<GithubRelease>>(await response.Content.ReadAsStringAsync(cancellationToken));
            return releases?.Where(x => x is not null && !x.Draft && TryGetVersion(x.TagName, out _)).OrderByDescending(x => Utilities.GetVersionFromString(x.TagName)).FirstOrDefault();
        }

        public static GithubReleaseAsset? SelectWindowsX64Asset(IEnumerable<GithubReleaseAsset>? assets)
        {
            return assets?
                .Where(x => x is not null && IsSafeWindowsAssetName(x.Name))
                .OrderByDescending(x => x.Name.Contains("win-x64", StringComparison.OrdinalIgnoreCase))
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private static bool IsSafeWindowsAssetName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name) || name != Path.GetFileName(name) || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return false;
            // The release workflow publishes GokuTrap.exe exclusively for win-x64.
            return Regex.IsMatch(name, @"^GokuTrap(?:[.-]win-x64)?\.exe$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static bool IsGitHubDownloadHost(string host) => host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);

        public static bool TryGetVersion(string tag, out Version? version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(tag) || !Regex.IsMatch(tag, "^v?\\d+(?:\\.\\d+){1,3}(?:[-+][A-Za-z0-9.-]+)?$"))
                return false;
            string numeric = tag.TrimStart('v').Split('-', '+')[0];
            return Version.TryParse(numeric, out version) && version > new Version(0, 0);
        }

        public static bool VersionsMatch(Version left, Version right) =>
            left.Major == right.Major && left.Minor == right.Minor && Math.Max(0, left.Build) == Math.Max(0, right.Build) &&
            Math.Max(0, left.Revision) == Math.Max(0, right.Revision);

        private static bool HasSha256(GithubReleaseAsset asset) => asset.Digest?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true && asset.Digest.Length == 71;

        public static void VerifyDigest(string path, string? digest)
        {
            if (string.IsNullOrWhiteSpace(digest))
                return; // Honest fallback: length checks and HTTPS only.
            if (!HasSha256(new GithubReleaseAsset { Digest = digest }))
                throw new InvalidDataException("The published release digest is malformed.");
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            string actual = Convert.ToHexString(sha.ComputeHash(stream));
            string expected = digest["sha256:".Length..];
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                throw new CryptographicException("The downloaded release asset did not match its SHA-256 digest.");
        }

        public static void ReplaceWithRollback(string replacement, string installed)
        {
            VerifyWindowsX64Executable(replacement);
            File.Replace(replacement, installed, installed + ".rollback", true);
        }

        public static void VerifyWindowsX64Executable(string path)
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            if (reader.BaseStream.Length < 256 || reader.ReadUInt16() != 0x5a4d) throw new InvalidDataException("Not a Windows executable");
            reader.BaseStream.Position = 0x3c;
            int offset = reader.ReadInt32();
            if (offset < 64 || offset > reader.BaseStream.Length - 26) throw new InvalidDataException("Invalid PE header");
            reader.BaseStream.Position = offset;
            if (reader.ReadUInt32() != 0x00004550 || reader.ReadUInt16() != 0x8664) throw new InvalidDataException("Not a Windows x64 executable");
            reader.BaseStream.Position = offset + 24;
            if (reader.ReadUInt16() != 0x20b) throw new InvalidDataException("Not PE32+");
        }

        private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public sealed record UpdateCheckResult(GithubRelease Release, GithubReleaseAsset Asset, Version Version);
}
