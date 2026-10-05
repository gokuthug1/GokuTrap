using GokuTrap.Models.APIs.RoValra;

namespace GokuTrap
{
    public sealed class DatacenterRadarService
    {
        public IReadOnlyList<RadarRegion> Regions { get; } = new List<RadarRegion>
        {
            new("US East", "US", false), new("US West", "US", false),
            new("Frankfurt", "DE", true), new("Singapore", "SG", true), new("Tokyo", "JP", true)
        };
        private DateTimeOffset _lastDiscovery;
        public string LastStatus { get; private set; } = FeatureText.Get("RadarHelp");
        public Task RefreshAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            LastStatus = FeatureText.Get("RadarHelp");
            return Task.CompletedTask;
        }

        public async Task<RegionJoinCandidate?> FindJoinableServerAsync(RadarRegion region, CancellationToken token = default)
        {
            if (!region.SupportsCountryDiscovery) { LastStatus = FeatureText.Get("RadarUsUnavailable"); return null; }
            PlayerActivitySnapshot? context;
            try { context = JsonSerializer.Deserialize<PlayerActivitySnapshot>(await File.ReadAllTextAsync(Paths.PlayerActivity, token)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            { LastStatus = FeatureText.Get("RadarNoContext"); return null; }
            // Read-only: a torn/unavailable watcher snapshot must never trigger recovery writes.
            if (context?.PlaceId is not long placeId || placeId <= 0 || context.ObservedUtc is not DateTimeOffset observed ||
                DateTimeOffset.UtcNow - observed > TimeSpan.FromMinutes(2))
            { LastStatus = FeatureText.Get("RadarNoContext"); return null; }
            if (DateTimeOffset.UtcNow - _lastDiscovery < TimeSpan.FromSeconds(30))
            { LastStatus = FeatureText.Get("RadarCooldown"); return null; }
            _lastDiscovery = DateTimeOffset.UtcNow;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                using var response = await App.HttpClient.GetAsync($"https://apis.rovalra.com/v1/servers/region?place_id={placeId}&region={region.Country}", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                response.EnsureSuccessStatusCode();
                string json = await ReadBoundedAsync(response.Content, timeout.Token);
                var discovered = JsonSerializer.Deserialize<RoValraServers>(json)?.Servers?.Select(x => x.ServerId).Where(IsSafeServerId).Take(100).ToHashSet();
                if (discovered is null || discovered.Count == 0) { LastStatus = FeatureText.Get("RadarNoServers"); return null; }
                // Only offer IDs that are also public and have capacity in Roblox's official API.
                using var publicResponse = await App.HttpClient.GetAsync($"https://games.roblox.com/v1/games/{placeId}/servers/Public?sortOrder=Asc&limit=100&excludeFullGames=true", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                publicResponse.EnsureSuccessStatusCode();
                using JsonDocument document = JsonDocument.Parse(await ReadBoundedAsync(publicResponse.Content, timeout.Token));
                foreach (JsonElement server in document.RootElement.GetProperty("data").EnumerateArray())
                {
                    string? id = server.GetProperty("id").GetString();
                    if (id is not null && discovered.Contains(id) && id != context.JobId && server.GetProperty("playing").GetInt32() < server.GetProperty("maxPlayers").GetInt32())
                    {
                        LastStatus = FeatureText.Get("RadarFound");
                        return new(placeId, id, region.Name, region.Country, DateTimeOffset.UtcNow);
                    }
                }
                LastStatus = FeatureText.Get("RadarNoServers");
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or KeyNotFoundException or InvalidDataException or FormatException or OverflowException)
            { LastStatus = FeatureText.Get("RadarUnavailable"); App.Logger.WriteLine("Radar", "Discovery unavailable: " + ex.GetType().Name); }
            return null;
        }

        public bool Hop(RegionJoinCandidate candidate)
        {
            if (candidate.PlaceId <= 0 || !IsSafeServerId(candidate.ServerId) || DateTimeOffset.UtcNow - candidate.VerifiedUtc > TimeSpan.FromMinutes(1)) return false;
            try
            {
                var context = JsonSerializer.Deserialize<PlayerActivitySnapshot>(File.ReadAllText(Paths.PlayerActivity));
                if (context?.PlaceId != candidate.PlaceId || context.ObservedUtc is not DateTimeOffset observed ||
                    DateTimeOffset.UtcNow - observed > TimeSpan.FromMinutes(2))
                { LastStatus = FeatureText.Get("RadarNoContext"); return false; }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            { LastStatus = FeatureText.Get("RadarNoContext"); return false; }
            // Reuse the official deep-link join mechanism used by ActivityData.RejoinServer.
            Utilities.ShellExecute($"roblox://experiences/start?placeId={candidate.PlaceId}&gameInstanceId={Uri.EscapeDataString(candidate.ServerId)}");
            LastStatus = FeatureText.Get("RadarHopRequested");
            return true;
        }
        private static bool IsSafeServerId(string? id) => Guid.TryParseExact(id, "D", out _);
        private static async Task<string> ReadBoundedAsync(HttpContent content, CancellationToken token)
        {
            using Stream stream = await content.ReadAsStreamAsync(token);
            using var memory = new MemoryStream();
            byte[] buffer = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(), token)) > 0)
            {
                if (memory.Length + read > 256 * 1024) throw new InvalidDataException("Response too large");
                memory.Write(buffer, 0, read);
            }
            return Encoding.UTF8.GetString(memory.ToArray());
        }
    }
    public sealed record RadarRegion(string Name, string Country, bool SupportsCountryDiscovery)
    {
        public string Summary => $"{Name} — {FeatureText.Get("RadarLatencyUnavailable")}";
    }
    public sealed record RegionJoinCandidate(long PlaceId, string ServerId, string RegionName, string Country, DateTimeOffset VerifiedUtc);
}
