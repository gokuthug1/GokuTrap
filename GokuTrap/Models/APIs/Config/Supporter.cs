namespace GokuTrap.Models.APIs.Config
{
    public class Supporter
    {
        [JsonPropertyName("imageAsset")]
        public string ImageAsset { get; set; } = null!;

        [JsonPropertyName("name")]
        public string Name { get; set; } = null!;

        public string Image => $"https://raw.githubusercontent.com/gokutraplabs/config/main/assets/{ImageAsset}";
    }
}
