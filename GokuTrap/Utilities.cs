using GokuTrap.AppData;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace GokuTrap
{
    static class Utilities
    {
        public static void ShellExecute(string website)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = website,
                    UseShellExecute = true
                });
            }
            catch (Win32Exception ex)
            {
                // lmfao

                if (ex.NativeErrorCode != (int)ErrorCode.CO_E_APPNOTFOUND)
                    throw;

                Process.Start(new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = $"shell32,OpenAs_RunDLL {website}"
                });
            }
        }

        public static Version GetVersionFromString(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return new Version(0, 0);

            // Strip leading 'v' or 'V'
            version = version.Trim().TrimStart('v', 'V');

            // Strip commit info (+...)
            int idx = version.IndexOf('+');
            if (idx != -1)
                version = version[..idx];

            // Strip prerelease tag (-...)
            idx = version.IndexOf('-');
            if (idx != -1)
                version = version[..idx];

            // Extract numeric version pattern (\d+(\.\d+)*)
            var match = Regex.Match(version, @"^\d+(\.\d+)*");
            if (match.Success)
                version = match.Value;

            // System.Version requires at least 2 components: Major.Minor
            if (!version.Contains('.'))
                version += ".0";

            if (Version.TryParse(version, out var parsed))
                return parsed;

            return new Version(0, 0);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="versionStr1"></param>
        /// <param name="versionStr2"></param>
        /// <returns>
        /// Result of System.Version.CompareTo <br />
        /// -1: version1 &lt; version2 <br />
        ///  0: version1 == version2 <br />
        ///  1: version1 &gt; version2
        /// </returns>
        public static VersionComparison CompareVersions(string versionStr1, string versionStr2)
        {
            try
            {
                var version1 = GetVersionFromString(versionStr1);
                var version2 = GetVersionFromString(versionStr2);

                return (VersionComparison)version1.CompareTo(version2);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("Utilities::CompareVersions", "An exception occurred when comparing versions");
                App.Logger.WriteLine("Utilities::CompareVersions", $"versionStr1={versionStr1} versionStr2={versionStr2}");
                App.Logger.WriteException("Utilities::CompareVersions", ex);

                return VersionComparison.Equal;
            }
        }

        /// <summary>
        /// Parses the input version string and prints if fails
        /// </summary>
        public static Version? ParseVersionSafe(string versionStr)
        {
            const string LOG_IDENT = "Utilities::ParseVersionSafe";

            if (!Version.TryParse(versionStr, out Version? version))
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to convert {versionStr} to a valid Version type.");
                return version;
            }

            return version;
        }

        public static bool IsRobloxRunning()
        {
            Process[] processes = GetProcessesSafe();
            string processName = Path.GetFileNameWithoutExtension(App.RobloxPlayerAppName);

            return processes.Any(x => x.ProcessName == processName);
        }

        public static string GetRobloxVersionStr(IAppData data)
        {
            string playerLocation = data.ExecutablePath;

            if (!File.Exists(playerLocation))
                return "";

            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(playerLocation);

            if (versionInfo.ProductVersion is null)
                return "";

            return versionInfo.ProductVersion.Replace(", ", ".");
        }

        public static string GetRobloxVersionStr(bool studio)
        {
            IAppData data = studio ? new RobloxStudioData() : new RobloxPlayerData();

            return GetRobloxVersionStr(data);
        }

        public static Version? GetRobloxVersion(IAppData data)
        {
            string str = GetRobloxVersionStr(data);
            return ParseVersionSafe(str);
        }

        public static Process[] GetProcessesSafe()
        {
            const string LOG_IDENT = "Utilities::GetProcessesSafe";

            try
            {
                return Process.GetProcesses();
            }
            catch (ArithmeticException ex) // thanks microsoft
            {
                App.Logger.WriteLine(LOG_IDENT, $"Unable to fetch processes!");
                App.Logger.WriteException(LOG_IDENT, ex);
                return Array.Empty<Process>(); // can we retry?
            }
        }

        public static bool DoesMutexExist(string name)
        {
            try
            {
                Mutex.OpenExisting(name).Close();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void KillBackgroundUpdater()
        {
            using EventWaitHandle handle = new EventWaitHandle(false, EventResetMode.AutoReset, $"{App.ProjectName}-BackgroundUpdaterKillEvent");
            handle.Set();
        }
    }
}
