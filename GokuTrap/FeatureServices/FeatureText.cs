namespace GokuTrap
{
    public static class FeatureText
    {
        public static string ToolsTitle => Get("ToolsTitle");
        public static string SoundsTitle => Get("SoundsTitle");
        public static string SoundsHelp => Get("SoundsHelp");
        public static string TelemetryUnavailable => Get("TelemetryUnavailable");
        public static string Get(string key) => Strings.ResourceManager.GetString("Features_" + key, Strings.Culture) ?? key;

        public static void Normalize(Settings settings)
        {
            settings.SaiyanMode ??= new();
            settings.Overlay ??= new();
            settings.SoundPacks ??= new();
            settings.AccountProfiles ??= new();
            settings.FastFlagCatalog ??= new();
            settings.FastFlagCatalog.PreviousValues ??= new();
            settings.FastFlagCatalog.AppliedValues ??= new();
            settings.Update ??= new();
            if (!Enum.IsDefined(typeof(RobloxPriorityPolicy), settings.SaiyanMode.Priority)) settings.SaiyanMode.Priority = RobloxPriorityPolicy.WindowsDefault;
            if (!Enum.IsDefined(typeof(OverlayStyle), settings.Overlay.Style)) settings.Overlay.Style = OverlayStyle.Cross;
            settings.Overlay.Size = double.IsFinite(settings.Overlay.Size) ? Math.Clamp(settings.Overlay.Size, 6, 96) : 18;
            settings.Overlay.Opacity = double.IsFinite(settings.Overlay.Opacity) ? Math.Clamp(settings.Overlay.Opacity, .1, 1) : .85;
            settings.Overlay.OffsetX = double.IsFinite(settings.Overlay.OffsetX) ? Math.Clamp(settings.Overlay.OffsetX, -2000, 2000) : 0;
            settings.Overlay.OffsetY = double.IsFinite(settings.Overlay.OffsetY) ? Math.Clamp(settings.Overlay.OffsetY, -2000, 2000) : 0;
            settings.Overlay.ToggleHotkey ??= "Ctrl+Shift+X";
            settings.Overlay.Color ??= "#FFFFFFFF";
            settings.Update.CheckIntervalHours = Math.Clamp(settings.Update.CheckIntervalHours, 1, 168);
            settings.SoundPacks.RemoveAll(x => x is null || !Enum.IsDefined(typeof(SoundPackTarget), x.Target));
            settings.AccountProfiles.RemoveAll(x => x is null || x.RobloxUserId <= 0);
            foreach (var profile in settings.AccountProfiles)
            {
                // A saved validation label must never be trusted after application restart.
                profile.ValidationStatus = "NotValidated";
                if (!Uri.TryCreate(profile.AvatarThumbnailUrl, UriKind.Absolute, out var avatar) || avatar.Scheme != "https" ||
                    !avatar.Host.EndsWith(".rbxcdn.com", StringComparison.OrdinalIgnoreCase)) profile.AvatarThumbnailUrl = string.Empty;
            }
        }
    }
}
