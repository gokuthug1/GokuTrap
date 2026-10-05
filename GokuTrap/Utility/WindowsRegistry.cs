using Microsoft.Win32;
using System.CodeDom;

namespace GokuTrap.Utility
{
    static class WindowsRegistry
    {
        private const string RobloxPlaceKey = "Roblox.Place";
        
        public static readonly List<RegistryKey> Roots = new() { Registry.CurrentUser, Registry.LocalMachine };

        public static void RegisterProtocol(string key, string name, string handler, string handlerParam = "%1")
        {
            string handlerArgs = $"\"{handler}\" {handlerParam}";

            using var uriKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{key}");
            using var uriIconKey = uriKey.CreateSubKey("DefaultIcon");
            using var uriCommandKey = uriKey.CreateSubKey(@"shell\open\command");

            if (uriKey.GetValue("") is null)
            {
                uriKey.SetValueSafe("", $"URL: {name} Protocol");
                uriKey.SetValueSafe("URL Protocol", "");
            }

            if (uriCommandKey.GetValue("") as string != handlerArgs)
            {
                uriIconKey.SetValueSafe("", handler);
                uriCommandKey.SetValueSafe("", handlerArgs);
            }
        }

        /// <summary>
        /// Registers Roblox Player protocols for GokuTrap
        /// </summary>
        public static void RegisterPlayer() => RegisterPlayer(Paths.Application, "-player \"%1\"");

        public static void RegisterPlayer(string handler, string handlerParam)
        {
            RegisterProtocol("roblox", "Roblox", handler, handlerParam);
            RegisterProtocol("roblox-player", "Roblox", handler, handlerParam);
        }

        /// <summary>
        /// Registers all Roblox Studio classes for GokuTrap
        /// </summary>
        public static void RegisterStudio()
        {
            RegisterStudioProtocol(Paths.Application, "-studio \"%1\"");
            RegisterStudioFileClass(Paths.Application, "-studio \"%1\"");
            RegisterStudioFileTypes();
        }

        /// <summary>
        /// Registers roblox-studio and roblox-studio-auth protocols
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="handlerParam"></param>
        public static void RegisterStudioProtocol(string handler, string handlerParam)
        {
            RegisterProtocol("roblox-studio", "Roblox", handler, handlerParam);
            RegisterProtocol("roblox-studio-auth", "Roblox", handler, handlerParam);
        }

        /// <summary>
        /// Registers file associations for Roblox.Place class
        /// </summary>
        public static void RegisterStudioFileTypes()
        {
            RegisterStudioFileType(".rbxl");
            RegisterStudioFileType(".rbxlx");
        }

        /// <summary>
        /// Registers Roblox.Place class
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="handlerParam"></param>
        public static void RegisterStudioFileClass(string handler, string handlerParam)
        {
            const string keyValue = "Roblox Place";
            string handlerArgs = $"\"{handler}\" {handlerParam}";
            string iconValue = $"{handler},0";

            using RegistryKey uriKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + RobloxPlaceKey);
            using RegistryKey uriIconKey = uriKey.CreateSubKey("DefaultIcon");
            using RegistryKey uriOpenKey = uriKey.CreateSubKey(@"shell\Open");
            using RegistryKey uriCommandKey = uriOpenKey.CreateSubKey(@"command");

            if (uriKey.GetValue("") as string != keyValue)
                uriKey.SetValueSafe("", keyValue);

            if (uriCommandKey.GetValue("") as string != handlerArgs)
                uriCommandKey.SetValueSafe("", handlerArgs);

            if (uriOpenKey.GetValue("") as string != "Open")
                uriOpenKey.SetValueSafe("", "Open");

            if (uriIconKey.GetValue("") as string != iconValue)
                uriIconKey.SetValueSafe("", iconValue);
        }

        public static void RegisterStudioFileType(string key)
        {
            using RegistryKey uriKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{key}");
            uriKey.CreateSubKey(RobloxPlaceKey + @"\ShellNew");

            if (uriKey.GetValue("") as string != RobloxPlaceKey)
                uriKey.SetValueSafe("", RobloxPlaceKey);
        }

        public static void RegisterApis()
        {
            try
            {
                using var apisKey = Registry.CurrentUser.CreateSubKey(App.ApisKey);
                apisKey.SetValueSafe("ApplicationPath", Paths.Application);
                apisKey.SetValueSafe("InstallationPath", Paths.Base);

                // Backward compatibility: mirror to legacy Bloxstrap key so third-party shaders/tools (e.g. Bloxshade) work out of the box
                using var legacyKey = Registry.CurrentUser.CreateSubKey(@"Software\Bloxstrap");
                legacyKey.SetValueSafe("ApplicationPath", Paths.Application);
                legacyKey.SetValueSafe("InstallationPath", Paths.Base);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("WindowsRegistry::RegisterApis", ex);
            }
        }

        public static void RegisterClientLocation(bool isStudio, string? clientPath)
        {
            string keyName = isStudio ? "StudioPath" : "PlayerPath";
            clientPath ??= "";

            try
            {
                using var apisKey = Registry.CurrentUser.CreateSubKey(App.ApisKey);
                apisKey.SetValueSafe(keyName, clientPath);

                // Backward compatibility mirror
                using var legacyKey = Registry.CurrentUser.CreateSubKey(@"Software\Bloxstrap");
                legacyKey.SetValueSafe(keyName, clientPath);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("WindowsRegistry::RegisterClientLocation", ex);
            }
        }

        /// <summary>
        /// Removes the autostart entry the Roblox player registers for itself when LaunchAtStartup is on
        /// </summary>
        public static void RemoveRobloxStartupEntry()
        {
            const string LOG_IDENT = "WindowsRegistry::RemoveRobloxStartupEntry";
            const string valueName = "RobloxPlayerBeta";

            try
            {
                using var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);

                if (runKey?.GetValue(valueName) is not string entry)
                    return;

                // make sure we only ever delete roblox's own entry
                if (!entry.Contains(App.RobloxPlayerAppName, StringComparison.OrdinalIgnoreCase))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Leaving '{valueName}' alone, it doesn't point at Roblox");
                    return;
                }

                App.Logger.WriteLine(LOG_IDENT, $"Removing startup entry '{entry}'");
                runKey.DeleteValue(valueName);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to remove startup entry");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public static void Unregister(string key)
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{key}");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("Protocol::Unregister", $"Failed to unregister {key}: {ex}");
            }
        }
    }
}
