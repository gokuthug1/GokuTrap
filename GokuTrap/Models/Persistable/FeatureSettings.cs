namespace GokuTrap.Models.Persistable
{
    public enum RobloxPriorityPolicy
    {
        [EnumName(FromTranslation = "Features_PriorityDefault")] WindowsDefault,
        [EnumName(FromTranslation = "Features_PriorityAbove")] AboveNormal,
        [EnumName(FromTranslation = "Features_PriorityHigh")] High
    }

    public class SaiyanModeSettings
    {
        public bool Enabled { get; set; }
        public RobloxPriorityPolicy Priority { get; set; } = RobloxPriorityPolicy.WindowsDefault;
        // A zero mask means automatic/default scheduling.  Affinity is only exposed on
        // machines where every selectable logical processor fits the Win32 affinity mask.
        public ulong? AffinityMask { get; set; }
        public bool AutomaticEfficiencySelection { get; set; }
    }

    public enum OverlayStyle
    {
        [EnumName(FromTranslation = "Features_StyleCross")] Cross,
        [EnumName(FromTranslation = "Features_StyleDot")] Dot,
        [EnumName(FromTranslation = "Features_StyleCircle")] Circle
    }

    public class OverlaySettings
    {
        public bool Enabled { get; set; }
        public OverlayStyle Style { get; set; } = OverlayStyle.Cross;
        public string Color { get; set; } = "#FFFFFFFF";
        public double Size { get; set; } = 18;
        public double Opacity { get; set; } = 0.85;
        // Ctrl+Shift+X in Win32 modifier/key notation.  Empty disables the hotkey.
        public string ToggleHotkey { get; set; } = "Ctrl+Shift+X";
        public bool ShowTelemetry { get; set; }
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
    }

    public enum SoundPackTarget
    {
        [EnumName(FromTranslation = "Features_TargetStartup")] LauncherStartup,
        [EnumName(FromTranslation = "Features_TargetSuccess")] LauncherLaunchSucceeded,
        [EnumName(FromTranslation = "Features_TargetJump")] CharacterJump,
        [EnumName(FromTranslation = "Features_TargetFootsteps")] CharacterFootsteps,
        [EnumName(FromTranslation = "Features_TargetGetUp")] CharacterGetUp
    }

    public class SoundPackSlot
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public SoundPackTarget Target { get; set; }
        public string AssetFileName { get; set; } = string.Empty;
        public string SourceFileName { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
        public bool Enabled { get; set; }
    }

    public class AccountProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public long RobloxUserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Nickname { get; set; } = string.Empty;
        [JsonIgnore] public string StatusLabel => FeatureText.Get("Account" + ValidationStatus);
        public string DisplayName { get; set; } = string.Empty;
        public string AvatarThumbnailUrl { get; set; } = string.Empty;
        public DateTimeOffset? LastValidatedUtc { get; set; }
        public string ValidationStatus { get; set; } = "NotValidated";
        // Credentials are intentionally never represented in a profile.
    }

    public class FastFlagCatalogSettings
    {
        public string LastCatalogVersion { get; set; } = "bundled-1";
        public DateTimeOffset? LastRefreshedUtc { get; set; }
        public Dictionary<string, string?> PreviousValues { get; set; } = new();
        public Dictionary<string, string> AppliedValues { get; set; } = new();
    }

    public class UpdateSettings
    {
        public bool IncludePrerelease { get; set; }
        public int CheckIntervalHours { get; set; } = 24;
    }
}
