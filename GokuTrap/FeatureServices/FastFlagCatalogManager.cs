namespace GokuTrap
{
    public sealed class FastFlagCatalogManager
    {
        private const string LOG_IDENT = "FastFlagCatalogManager";
        private const int MaxCatalogBytes = 256 * 1024;
        private static readonly Uri CatalogUri = new($"https://raw.githubusercontent.com/{App.ProjectRepository}/main/GokuTrap/Resources/FastFlagCatalog.json");

        private static readonly IReadOnlyDictionary<string, FlagRule> Rules = new Dictionary<string, FlagRule>(StringComparer.Ordinal)
        {
            ["FIntDebugForceMSAASamples"] = FlagRule.Int(1, 4),
            ["DFIntDebugFRMQualityLevelOverride"] = FlagRule.Int(1, 21)
        };

        public CommunityFastFlagCatalog Catalog { get; private set; } = CreateBundledCatalog();
        public string Source { get; private set; } = FeatureText.Get("CatalogBundled");
        public string LastStatus { get; private set; } = FeatureText.Get("CatalogLoaded");

        public void LoadCachedCatalog()
        {
            try
            {
                if (File.Exists(Paths.FastFlagCatalog))
                {
                    if (new FileInfo(Paths.FastFlagCatalog).Length > MaxCatalogBytes) throw new InvalidDataException("Cached catalog too large");
                    string json = File.ReadAllText(Paths.FastFlagCatalog);
                    if (TryParseCatalog(json, out CommunityFastFlagCatalog? catalog))
                    {
                        Catalog = catalog!;
                        Source = FeatureText.Get("CatalogCached");
                        LastStatus = FeatureText.Get("CatalogCacheLoaded");
                        return;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }

            Catalog = CreateBundledCatalog();
            Source = FeatureText.Get("CatalogBundled");
        }

        public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
        {
            if (App.Settings.Prop.ForceLocalData)
            {
                LastStatus = FeatureText.Get("CatalogRemoteDisabled");
                return false;
            }

            try
            {
                using HttpResponseMessage response = await App.HttpClient.GetAsync(CatalogUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.RequestMessage?.RequestUri is not Uri finalUri || finalUri.Scheme != Uri.UriSchemeHttps || finalUri.Host != CatalogUri.Host) throw new InvalidDataException("Unexpected catalog redirect");
                if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength is long length && length > MaxCatalogBytes)
                    throw new InvalidDataException("The catalog response was unavailable or too large.");

                string json = await ReadLimitedAsync(response.Content, cancellationToken);
                if (!TryParseCatalog(json, out CommunityFastFlagCatalog? catalog))
                    throw new InvalidDataException("The catalog did not match GokuTrap's supported schema.");

                Directory.CreateDirectory(Path.GetDirectoryName(Paths.FastFlagCatalog)!);
                string temporary = Paths.FastFlagCatalog + ".download";
                File.WriteAllText(temporary, json);
                File.Move(temporary, Paths.FastFlagCatalog, true);

                Catalog = catalog!;
                Source = FeatureText.Get("CatalogRemote");
                App.Settings.Prop.FastFlagCatalog.LastCatalogVersion = Catalog.Version;
                App.Settings.Prop.FastFlagCatalog.LastRefreshedUtc = DateTimeOffset.UtcNow;
                App.Settings.Save();
                LastStatus = FeatureText.Get("CatalogRefreshed");
                return true;
            }
            catch (OperationCanceledException)
            {
                LastStatus = FeatureText.Get("CatalogCancelled");
                return false;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
            {
                LastStatus = FeatureText.Get("CatalogFailed");
                App.Logger.WriteException(LOG_IDENT, ex);
                return false;
            }
        }

        public IReadOnlyList<FastFlagChange> Preview(CommunityFastFlagPreset preset)
        {
            return preset.Flags.Select(pair => new FastFlagChange
            {
                Key = pair.Key,
                Before = App.FastFlags.GetValue(pair.Key),
                After = pair.Value,
                Explanation = FeatureText.Get(pair.Key == "FIntDebugForceMSAASamples" ? "FlagMsaa" : "FlagQuality")
            }).ToList();
        }

        public void Apply(CommunityFastFlagPreset preset)
        {
            if (!TryParseCatalog(JsonSerializer.Serialize(new CommunityFastFlagCatalog { SchemaVersion = 1, Version = "local", Presets = new() { preset } }), out _))
                throw new InvalidDataException(FeatureText.Get("CatalogFailed"));
            FastFlagCatalogSettings state = App.Settings.Prop.FastFlagCatalog;
            bool alreadyApplied = state.AppliedValues.Count == preset.Flags.Count &&
                preset.Flags.All(x => state.AppliedValues.TryGetValue(x.Key, out string? value) && value == x.Value && App.FastFlags.GetValue(x.Key) == x.Value);
            if (alreadyApplied)
            {
                LastStatus = FeatureText.Get("CatalogAlreadyApplied");
                return;
            }

            Revert();
            state.PreviousValues.Clear();
            state.AppliedValues.Clear();

            foreach (var pair in preset.Flags)
            {
                state.PreviousValues[pair.Key] = App.FastFlags.GetValue(pair.Key);
                state.AppliedValues[pair.Key] = pair.Value;
                App.FastFlags.SetValue(pair.Key, pair.Value);
            }

            LastStatus = string.Format(FeatureText.Get("CatalogApplied"), preset.Title);
        }

        public void Revert()
        {
            FastFlagCatalogSettings state = App.Settings.Prop.FastFlagCatalog;
            foreach (var applied in state.AppliedValues)
            {
                if (!Rules.TryGetValue(applied.Key, out var rule) || !rule.IsValid(applied.Value)) continue;
                // A manual change after applying a catalog must win over a stale revert.
                if (!string.Equals(App.FastFlags.GetValue(applied.Key), applied.Value, StringComparison.Ordinal))
                    continue;

                state.PreviousValues.TryGetValue(applied.Key, out string? previous);
                App.FastFlags.SetValue(applied.Key, previous);
            }

            if (state.AppliedValues.Count > 0)
                LastStatus = FeatureText.Get("CatalogRestored");
            state.AppliedValues.Clear();
            state.PreviousValues.Clear();
        }

        private static async Task<string> ReadLimitedAsync(HttpContent content, CancellationToken cancellationToken)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(15));
            cancellationToken = deadline.Token;
            await using Stream stream = await content.ReadAsStreamAsync(cancellationToken);
            using var memory = new MemoryStream();
            byte[] buffer = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                if (memory.Length + read > MaxCatalogBytes)
                    throw new InvalidDataException("The catalog response exceeded the size limit.");
                memory.Write(buffer, 0, read);
            }
            return Encoding.UTF8.GetString(memory.ToArray());
        }

        public static bool TryParseCatalog(string json, out CommunityFastFlagCatalog? catalog)
        {
            catalog = null;
            try { return ParseCatalog(json, out catalog); }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or OverflowException) { return false; }
        }

        private static bool HasUniqueProperties(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                return element.EnumerateObject().All(x => names.Add(x.Name) && HasUniqueProperties(x.Value));
            }
            return element.ValueKind != JsonValueKind.Array || element.EnumerateArray().All(HasUniqueProperties);
        }

        private static bool ParseCatalog(string json, out CommunityFastFlagCatalog? catalog)
        {
            catalog = null;
            if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaxCatalogBytes)
                return false;

            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
            JsonElement root = document.RootElement;
            if (!HasUniqueProperties(root) || root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schemaVersion", out JsonElement schema) || schema.GetInt32() != 1 ||
                !root.TryGetProperty("version", out JsonElement version) || version.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(version.GetString()) || version.GetString()!.Length > 64 ||
                !root.TryGetProperty("presets", out JsonElement presets) || presets.ValueKind != JsonValueKind.Array || presets.GetArrayLength() is 0 or > 20)
                return false;

            var presetIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement preset in presets.EnumerateArray())
            {
                if (preset.ValueKind != JsonValueKind.Object || !preset.TryGetProperty("id", out JsonElement id) || id.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(id.GetString()) || !presetIds.Add(id.GetString()!) ||
                    id.GetString()!.Length > 80 || !Regex.IsMatch(id.GetString()!, "^[a-z0-9-]+$") ||
                    !preset.TryGetProperty("flags", out JsonElement flags) || flags.ValueKind != JsonValueKind.Object || flags.EnumerateObject().Count() is 0 or > 2)
                    return false;

                var seenKeys = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonProperty flag in flags.EnumerateObject())
                {
                    if (!seenKeys.Add(flag.Name) || !Rules.TryGetValue(flag.Name, out FlagRule? rule) || flag.Value.ValueKind != JsonValueKind.String || !rule.IsValid(flag.Value.GetString()!))
                        return false;
                }
            }

            catalog = JsonSerializer.Deserialize<CommunityFastFlagCatalog>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = false });
            return catalog is not null && catalog.Presets.All(x => !string.IsNullOrWhiteSpace(x.Title) && x.Title.Length <= 100 && !string.IsNullOrWhiteSpace(x.Description) && x.Description.Length <= 2000 && !string.IsNullOrWhiteSpace(x.CompatibilityNotice) && x.CompatibilityNotice.Length <= 2000);
        }

        private static CommunityFastFlagCatalog CreateBundledCatalog() => new()
        {
            SchemaVersion = 1,
            Version = "bundled-1",
            Presets = new()
            {
                new CommunityFastFlagPreset
                {
                    Id = "competitive-low-latency", Title = FeatureText.Get("PresetCompetitive"),
                    Description = FeatureText.Get("PresetCompetitiveHelp"),
                    CompatibilityNotice = FeatureText.Get("PresetCompatibility"),
                    Flags = new() { ["FIntDebugForceMSAASamples"] = "1", ["DFIntDebugFRMQualityLevelOverride"] = "8" }
                },
                new CommunityFastFlagPreset
                {
                    Id = "cinematic-high-fidelity", Title = FeatureText.Get("PresetCinematic"),
                    Description = FeatureText.Get("PresetCinematicHelp"),
                    CompatibilityNotice = FeatureText.Get("PresetCompatibility"),
                    Flags = new() { ["FIntDebugForceMSAASamples"] = "4", ["DFIntDebugFRMQualityLevelOverride"] = "21" }
                },
                new CommunityFastFlagPreset
                {
                    Id = "potato-power-saver", Title = FeatureText.Get("PresetPotato"),
                    Description = FeatureText.Get("PresetPotatoHelp"),
                    CompatibilityNotice = FeatureText.Get("PresetCompatibility"),
                    Flags = new() { ["FIntDebugForceMSAASamples"] = "1", ["DFIntDebugFRMQualityLevelOverride"] = "1" }
                }
            }
        };

        private sealed class FlagRule
        {
            private readonly Func<string, bool> _validator;
            private FlagRule(Func<string, bool> validator) => _validator = validator;
            public bool IsValid(string value) => _validator(value);
            public static FlagRule Int(int min, int max) => new(value => int.TryParse(value, out int number) && number >= min && number <= max);
        }
    }

    public sealed class CommunityFastFlagCatalog
    {
        [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; }
        [JsonPropertyName("version")] public string Version { get; set; } = string.Empty;
        [JsonPropertyName("presets")] public List<CommunityFastFlagPreset> Presets { get; set; } = new();
    }

    public sealed class CommunityFastFlagPreset
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
        [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
        [JsonPropertyName("compatibilityNotice")] public string CompatibilityNotice { get; set; } = string.Empty;
        [JsonPropertyName("flags")] public Dictionary<string, string> Flags { get; set; } = new();
    }

    public sealed class FastFlagChange
    {
        public string Key { get; set; } = string.Empty;
        public string? Before { get; set; }
        public string After { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;
        public string Summary => $"{Key}: {Before ?? FeatureText.Get("Unset")} → {After}\n{Explanation}";
    }
}
