using System.Security.Cryptography;
using System.Windows.Media;

namespace GokuTrap
{
    /// <summary>
    /// Owns imported sound assets.  It never retains an arbitrary source path: assets are copied
    /// into the installation, hashed, and only the matching files are removed during restore.
    /// </summary>
    public sealed class SoundPackManager : IDisposable
    {
        private const string LOG_IDENT = "SoundPackManager";
        private const long MaxAssetBytes = 20 * 1024 * 1024;
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".ogg", ".m4a"
        };
        private MediaPlayer? _previewPlayer;
        public event EventHandler<string>? PlaybackStatusChanged;

        public static IReadOnlyCollection<SoundPackTarget> Targets { get; } = Enum.GetValues<SoundPackTarget>();

        public static string GetPresetDefinition(SoundPackTarget target) => target switch
        {
            SoundPackTarget.LauncherStartup => FeatureText.Get("SoundStartup"),
            SoundPackTarget.LauncherLaunchSucceeded => FeatureText.Get("SoundSuccess"),
            SoundPackTarget.CharacterJump => FeatureText.Get("SoundJump"),
            SoundPackTarget.CharacterFootsteps => FeatureText.Get("SoundFootsteps"),
            SoundPackTarget.CharacterGetUp => FeatureText.Get("SoundGetUp"),
            _ => ""
        };

        public SoundPackSlot Import(string sourcePath, SoundPackTarget target, string? displayName = null)
        {
            if (!File.Exists(sourcePath))
                throw new InvalidDataException(FeatureText.Get("SoundInvalid"));

            string extension = Path.GetExtension(sourcePath);
            if (!AllowedExtensions.Contains(extension) || (IsClientAsset(target) && !extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException(FeatureText.Get("SoundFormats"));
            if (App.Settings.Prop.SoundPacks.Any(x => x.Target == target))
                throw new InvalidDataException(FeatureText.Get("SoundSlotOccupied"));

            var info = new FileInfo(sourcePath);
            if (info.Length <= 0 || info.Length > MaxAssetBytes)
                throw new InvalidDataException(FeatureText.Get("SoundInvalid"));

            // Read through the whole source before copying, catching locked/corrupt sources early.
            // Hold the read-only source handle through copying so it cannot be replaced or
            // expanded between validation and import on Windows.
            using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (source.Length <= 0 || source.Length > MaxAssetBytes) throw new InvalidDataException(FeatureText.Get("SoundInvalid"));
            byte[] header = new byte[16];
            int read = source.Read(header, 0, header.Length);
            if (!HasAudioHeader(header.AsSpan(0, read), extension)) throw new InvalidDataException(FeatureText.Get("SoundInvalid"));
            source.Position = 0;
            using var sha = SHA256.Create();
            string sourceHash = Convert.ToHexString(sha.ComputeHash(source));

            Directory.CreateDirectory(Paths.SoundPacks);
            string assetFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            string destination = Path.Combine(Paths.SoundPacks, assetFileName);
            source.Position = 0;
            using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None)) source.CopyTo(output);
            if (new FileInfo(destination).Length > MaxAssetBytes || HashFile(destination) != sourceHash)
            { File.Delete(destination); throw new InvalidDataException(FeatureText.Get("SoundInvalid")); }

            var slot = new SoundPackSlot
            {
                Name = string.IsNullOrWhiteSpace(displayName) ? Path.GetFileNameWithoutExtension(sourcePath) : displayName.Trim(),
                Target = target,
                AssetFileName = assetFileName,
                SourceFileName = Path.GetFileName(sourcePath),
                Sha256 = sourceHash,
                Enabled = true
            };
            App.Settings.Prop.SoundPacks.Add(slot);
            return slot;
        }

        public bool Remove(SoundPackSlot slot)
        {
            if (!RestoreSlot(slot)) return false;
            try
            {
                string assetPath = GetAssetPath(slot);
                if (File.Exists(assetPath) && string.Equals(HashFile(assetPath), slot.Sha256, StringComparison.OrdinalIgnoreCase))
                    File.Delete(assetPath);
                App.Settings.Prop.SoundPacks.Remove(slot);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                return false;
            }
        }

        public int Apply()
        {
            int failed = 0;
            foreach (SoundPackSlot slot in App.Settings.Prop.SoundPacks)
            {
                if (IsClientAsset(slot.Target))
                {
                    if (!(slot.Enabled ? ApplySlot(slot) : RestoreSlot(slot))) failed++;
                }
            }
            return failed;
        }

        public bool ApplySlot(SoundPackSlot slot)
        {
            if (!IsClientAsset(slot.Target))
                return false;

            string destination = GetClientAssetPath(slot.Target);
            try
            {
                string source = GetAssetPath(slot);
                if (!File.Exists(source) || !string.Equals(HashFile(source), slot.Sha256, StringComparison.OrdinalIgnoreCase)) return false;
                if (File.Exists(destination) && !string.Equals(HashFile(destination), slot.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    // Do not overwrite a file that GokuTrap cannot prove it owns.
                    App.Logger.WriteLine(LOG_IDENT, $"Skipped {slot.Target}; another modification owns the target.");
                    return false;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                Filesystem.AssertReadOnly(destination);
                File.Copy(source, destination, true);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                return false;
            }
        }

        public bool RestoreSlot(SoundPackSlot slot)
        {
            if (!IsClientAsset(slot.Target))
                return true;

            string destination = GetClientAssetPath(slot.Target);
            try
            {
                if (File.Exists(destination) && string.Equals(HashFile(destination), slot.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    Filesystem.AssertReadOnly(destination);
                    File.Delete(destination);
                }
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                return false;
            }
        }

        public void PlayEvent(SoundPackTarget target)
        {
            SoundPackSlot? slot = App.Settings.Prop.SoundPacks.FirstOrDefault(x => x.Enabled && x.Target == target);
            if (slot is null || IsClientAsset(target))
                return;
            Play(slot);
        }

        public void Play(SoundPackSlot slot)
        {
            string assetPath;
            try { assetPath = GetAssetPath(slot); }
            catch (InvalidDataException) { return; }
            if (!File.Exists(assetPath)) return;

            // MediaPlayer is a WPF object, so always create and use it on the UI dispatcher.
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    _previewPlayer?.Stop();
                    _previewPlayer?.Close();
                    _previewPlayer = new MediaPlayer();
                    _previewPlayer.MediaFailed += (_, _) =>
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Audio preview unavailable for this format/codec.");
                        PlaybackStatusChanged?.Invoke(this, FeatureText.Get("SoundPlaybackFailed"));
                    };
                    _previewPlayer.MediaOpened += (_, _) => PlaybackStatusChanged?.Invoke(this, FeatureText.Get("SoundPlaying"));
                    _previewPlayer.Open(new Uri(assetPath));
                    _previewPlayer.Play();
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }));
        }

        public string GetAssetPath(SoundPackSlot slot)
        {
            if (string.IsNullOrWhiteSpace(slot.AssetFileName) || !Regex.IsMatch(slot.AssetFileName, "^[a-f0-9]{32}\\.(mp3|wav|ogg|m4a)$"))
                throw new InvalidDataException(FeatureText.Get("SoundInvalid"));
            return Path.Combine(Paths.SoundPacks, slot.AssetFileName);
        }

        public static bool HasAudioHeader(ReadOnlySpan<byte> header, string extension)
        {
            if (header.Length < 12) return false;
            string prefix = Encoding.ASCII.GetString(header);
            return extension.ToLowerInvariant() switch
            {
                ".mp3" => prefix.StartsWith("ID3", StringComparison.Ordinal) || (header[0] == 0xff && (header[1] & 0xe0) == 0xe0),
                ".wav" => prefix.StartsWith("RIFF", StringComparison.Ordinal) && prefix.Substring(8, 4) == "WAVE",
                ".ogg" => prefix.StartsWith("OggS", StringComparison.Ordinal),
                ".m4a" => prefix.Substring(4, 4) == "ftyp",
                _ => false
            };
        }

        public static bool IsClientAsset(SoundPackTarget target) => target is SoundPackTarget.CharacterJump or SoundPackTarget.CharacterFootsteps or SoundPackTarget.CharacterGetUp;

        public static string GetClientAssetPath(SoundPackTarget target) => Path.Combine(Paths.Modifications, target switch
        {
            SoundPackTarget.CharacterJump => @"content\sounds\action_jump.mp3",
            SoundPackTarget.CharacterFootsteps => @"content\sounds\action_footsteps_plastic.mp3",
            SoundPackTarget.CharacterGetUp => @"content\sounds\action_get_up.mp3",
            _ => throw new ArgumentOutOfRangeException(nameof(target))
        });

        private static string HashFile(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(stream));
        }

        public void Dispose()
        {
            _previewPlayer?.Stop();
            _previewPlayer?.Close();
        }
    }
}
