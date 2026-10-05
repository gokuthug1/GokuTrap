public class GithubReleaseAsset
{
    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = null!;

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    // GitHub returns a value such as "sha256:..." when a release asset digest is available.
    [JsonPropertyName("digest")]
    public string? Digest { get; set; }
}
