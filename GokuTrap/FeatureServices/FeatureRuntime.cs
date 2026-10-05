namespace GokuTrap
{
    internal static class FeatureRuntime
    {
        public static bool WatcherOwnsRuntime()
        {
            using var probe = new InterProcessLock("Watcher");
            return !probe.IsAcquired;
        }

        public static void PublishActivity(long? placeId, string? jobId)
        {
            try
            {
                var snapshot = new PlayerActivitySnapshot(placeId, jobId, placeId is null ? null : DateTimeOffset.UtcNow);
                string temporary = Paths.PlayerActivity + ".write";
                File.WriteAllText(temporary, JsonSerializer.Serialize(snapshot));
                File.Move(temporary, Paths.PlayerActivity, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { App.Logger.WriteLine("FeatureRuntime", "Activity snapshot unavailable; radar freshness will expire"); }
        }

        private static string? _lastReadHash;
        public static bool TryReloadSettings()
        {
            try
            {
                string file = App.Settings.FileLocation;
                string contents = File.ReadAllText(file);
                string hash = MD5Hash.FromString(contents);
                if (hash == (_lastReadHash ?? App.Settings.LastFileHash)) return false;
                var settings = JsonSerializer.Deserialize<Settings>(contents);
                if (settings is null) return false;
                FeatureText.Normalize(settings);
                // Refresh runtime controls only. Replacing the whole settings object would
                // discard unrelated changes made by the watcher/integrations in memory.
                App.Settings.Prop.SaiyanMode = settings.SaiyanMode;
                App.Settings.Prop.Overlay = settings.Overlay;
                App.Settings.Prop.AllowCookieAccess = settings.AllowCookieAccess;
                _lastReadHash = hash;
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            { App.Logger.WriteLine("FeatureRuntime", "Settings reload deferred; last policy retained"); return false; }
        }
    }
    internal sealed record PlayerActivitySnapshot(long? PlaceId, string? JobId, DateTimeOffset? ObservedUtc);
}
