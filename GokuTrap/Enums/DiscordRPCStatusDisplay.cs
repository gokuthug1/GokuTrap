using DiscordRPC;

namespace GokuTrap.Enums
{
    public enum DiscordRPCStatusDisplay
    {
        [EnumName(FromTranslation = "Enums.DiscordRPCStatusDisplay.Name")]
        Name = StatusDisplayType.Name,

        [EnumName(FromTranslation = "Enums.DiscordRPCStatusDisplay.Details")]
        Details = StatusDisplayType.Details,
    }
}