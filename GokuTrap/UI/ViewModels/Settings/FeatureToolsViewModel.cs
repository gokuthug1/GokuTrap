using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace GokuTrap.UI.ViewModels.Settings
{
    public sealed class FeatureLabels { public string this[string key] => FeatureText.Get(key); }

    public sealed class FeatureToolsViewModel : NotifyPropertyChangedViewModel, IDisposable
    {
        public FeatureLabels Text { get; } = new();
        public Models.Persistable.Settings Settings => App.Settings.Prop;
        public bool CookieAccess
        {
            get => Settings.AllowCookieAccess;
            set { Settings.AllowCookieAccess = value; if (!value) { App.Cookies.Clear(); App.Accounts.InvalidateSession(); Settings.Overlay.Enabled = false; App.Overlay.Dispose(); AccountChanged(); } App.Settings.Save(); OnPropertyChanged(nameof(CookieAccess)); }
        }
        public IEnumerable<RobloxPriorityPolicy> Priorities => Enum.GetValues<RobloxPriorityPolicy>();
        public IEnumerable<OverlayStyle> OverlayStyles => Enum.GetValues<OverlayStyle>();
        public IEnumerable<SoundPackTarget> SoundTargets => Enum.GetValues<SoundPackTarget>();
        public ObservableCollection<PlayerMeasurement> Players { get; } = new();
        public ObservableCollection<SoundPackSlot> Sounds { get; } = new();
        public ObservableCollection<AccountProfile> Accounts { get; } = new();
        public ObservableCollection<CommunityFastFlagPreset> Presets { get; } = new();
        public ObservableCollection<FastFlagChange> Changes { get; } = new();
        public IReadOnlyList<RadarRegion> Regions => App.DatacenterRadar.Regions;
        public string Topology => App.SaiyanMode.TopologyDescription;
        public bool AutomaticAffinityAvailable => App.SaiyanMode.Topology.FasterMask != 0;
        public PlayerMeasurement? SelectedPlayer { get; set; }
        public SoundPackSlot? SelectedSound { get; set; }
        public SoundPackTarget SoundTarget { get; set; }
        public AccountProfile? SelectedAccount { get; set; }
        public string AccountNickname { get; set; } = string.Empty;
        public string AccountUserId { get; set; } = string.Empty;
        public string Affinity { get; set; } = App.Settings.Prop.SaiyanMode.AffinityMask?.ToString("X") ?? string.Empty;
        public RadarRegion? SelectedRegion { get; set; }
        private CommunityFastFlagPreset? _preset;
        public CommunityFastFlagPreset? SelectedPreset
        {
            get => _preset;
            set { _preset = value; Changes.Clear(); if (value is not null) foreach (var change in App.FastFlagCatalog.Preview(value)) Changes.Add(change); OnPropertyChanged(nameof(SelectedPreset)); }
        }
        private string _status = FeatureText.Get("Ready");
        public string Status { get => _status; private set { _status = value; OnPropertyChanged(nameof(Status)); } }
        private bool _busy;
        public bool IsBusy { get => _busy; private set { _busy = value; OnPropertyChanged(nameof(IsBusy)); OnPropertyChanged(nameof(CanAct)); } }
        public bool CanAct => !IsBusy;
        public string CatalogMetadata => $"{App.FastFlagCatalog.Source} • {App.FastFlagCatalog.Catalog.Version} • {App.Settings.Prop.FastFlagCatalog.LastRefreshedUtc?.ToLocalTime().ToString("g") ?? FeatureText.Get("NeverRefreshed")}";
        public string AccountStatus => App.Accounts.Status;
        public string ActiveAccount => App.Accounts.ActiveProfile is { } active ? $"{FeatureText.Get("ActiveAccount")}: {active.DisplayName} ({active.RobloxUserId})" : FeatureText.Get("AccountNotValidated");
        public string SelectedProfile => App.Settings.Prop.SelectedAccountProfileId is string id && App.Settings.Prop.AccountProfiles.FirstOrDefault(x => x.Id == id) is { } profile
            ? $"{FeatureText.Get("SelectedProfile")}: {profile.Username} ({profile.RobloxUserId})" : FeatureText.Get("NoSelection");
        public string UpdateStatus => App.Updates.Status;
        public string CurrentVersion => App.Version;
        public string AvailableVersion => App.Updates.AvailableUpdate?.Release.TagName ?? FeatureText.Get("NoUpdate");
        public string ReleaseNotes => App.Updates.AvailableUpdate?.Release.Body ?? string.Empty;
        public double UpdateProgress => App.Updates.Progress * 100;
        private CancellationTokenSource? _operation;
        private bool _disposed;
        public ICommand ApplyPolicyCommand { get; }
        public ICommand ResetPolicyCommand { get; }
        public ICommand MeasureCommand { get; }
        public ICommand TrimCommand { get; }
        public ICommand ImportSoundCommand { get; }
        public ICommand PreviewSoundCommand { get; }
        public ICommand SaveSoundsCommand { get; }
        public ICommand RemoveSoundCommand { get; }
        public ICommand AddAccountCommand { get; }
        public ICommand AddPublicAccountCommand { get; }
        public ICommand RefreshAccountsCommand { get; }
        public ICommand SelectAccountCommand { get; }
        public ICommand RenameAccountCommand { get; }
        public ICommand RemoveAccountCommand { get; }
        public ICommand SignInCommand { get; }
        public ICommand RefreshCatalogCommand { get; }
        public ICommand ApplyPresetCommand { get; }
        public ICommand RevertPresetCommand { get; }
        public ICommand RefreshRadarCommand { get; }
        public ICommand HopCommand { get; }
        public ICommand ApplyOverlayCommand { get; }
        public ICommand HideOverlayCommand { get; }
        public ICommand CheckUpdatesCommand { get; }
        public ICommand InstallUpdateCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand SaveUpdateOptionsCommand { get; }
        public ICommand RestoreUpdateCommand { get; }

        public FeatureToolsViewModel()
        {
            ReloadLists();
            ApplyPolicyCommand = Command(async token =>
            {
                if (!string.IsNullOrWhiteSpace(Affinity))
                {
                    if (!ulong.TryParse(Affinity, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong mask) || mask == 0 || (mask & ~App.SaiyanMode.Topology.ValidMask) != 0)
                    { Status = FeatureText.Get("AffinityInvalid"); return; }
                    Settings.SaiyanMode.AffinityMask = mask;
                }
                else Settings.SaiyanMode.AffinityMask = null;
                if (Settings.SaiyanMode.Priority == RobloxPriorityPolicy.High && !Confirm("HighWarning")) return;
                App.Settings.Save();
                await Task.Run(() => { App.SaiyanMode.RestoreAll(); if (Settings.SaiyanMode.Enabled && !FeatureRuntime.WatcherOwnsRuntime()) App.SaiyanMode.ApplyToRunningRobloxPlayers(); }, token);
                Status = FeatureRuntime.WatcherOwnsRuntime() ? FeatureText.Get("RuntimeSaved") : App.SaiyanMode.LastStatus;
            });
            ResetPolicyCommand = Command(async token =>
            {
                Settings.SaiyanMode.Enabled = false; Settings.SaiyanMode.Priority = RobloxPriorityPolicy.WindowsDefault;
                Settings.SaiyanMode.AffinityMask = null; Settings.SaiyanMode.AutomaticEfficiencySelection = false; Affinity = string.Empty;
                App.Settings.Save(); await Task.Run(App.SaiyanMode.RestoreAll, token); Status = App.SaiyanMode.LastStatus;
                OnPropertyChanged(nameof(Settings)); OnPropertyChanged(nameof(Affinity));
            });
            MeasureCommand = Command(async token => { var samples = await Task.Run(App.SaiyanMode.Measure, token); Players.Clear(); foreach (var sample in samples) Players.Add(sample); Status = FeatureText.Get(samples.Count == 0 ? "NoPlayer" : "MemoryMeasured"); });
            TrimCommand = Command(async token => { if (SelectedPlayer is null || !Confirm("TrimWarning")) return; await Task.Run(() => App.SaiyanMode.TrimWorkingSet(SelectedPlayer), token); Status = App.SaiyanMode.LastStatus; });
            ImportSoundCommand = Command(async token =>
            {
                var dialog = new OpenFileDialog { Filter = FeatureText.Get("AudioFilter"), CheckFileExists = true };
                if (dialog.ShowDialog() != true) return;
                var slot = await Task.Run(() => App.SoundPacks.Import(dialog.FileName, SoundTarget), token);
                App.Settings.Save(); ReloadLists(); SelectedSound = slot; OnPropertyChanged(nameof(SelectedSound)); Status = FeatureText.Get("SoundImported");
            });
            PreviewSoundCommand = Command(token => { if (SelectedSound is not null) App.SoundPacks.Play(SelectedSound); Status = FeatureText.Get("SoundPreview"); return Task.CompletedTask; });
            SaveSoundsCommand = Command(async token => { App.Settings.Save(); int failures = await Task.Run(App.SoundPacks.Apply, token); Status = failures == 0 ? FeatureText.Get("SoundsSaved") : string.Format(FeatureText.Get("SoundConflicts"), failures); });
            RemoveSoundCommand = Command(async token =>
            {
                if (SelectedSound is null) return;
                bool removed = await Task.Run(() => App.SoundPacks.Remove(SelectedSound), token);
                Status = FeatureText.Get(removed ? "SoundRestored" : "ActionFailed"); App.Settings.Save(); ReloadLists();
            });
            AddAccountCommand = Command(async token => { await App.Accounts.AddCurrentAuthenticatedAsync(); App.Settings.Save(); ReloadLists(); AccountChanged(); });
            AddPublicAccountCommand = Command(async token =>
            {
                if (!long.TryParse(AccountUserId, out long id) || id <= 0) { Status = FeatureText.Get("AccountIdInvalid"); return; }
                var profile = Settings.AccountProfiles.FirstOrDefault(x => x.RobloxUserId == id) ?? new AccountProfile { RobloxUserId = id };
                if (await App.Accounts.RefreshAsync(profile) && !Settings.AccountProfiles.Contains(profile)) Settings.AccountProfiles.Add(profile);
                App.Settings.Save(); ReloadLists(); AccountChanged();
            });
            RefreshAccountsCommand = Command(async token => { await App.Accounts.ValidateSessionAsync(); foreach (var profile in Settings.AccountProfiles.ToArray()) { token.ThrowIfCancellationRequested(); await App.Accounts.RefreshAsync(profile); } App.Settings.Save(); ReloadLists(); AccountChanged(); });
            SelectAccountCommand = Command(token => { App.Accounts.Select(SelectedAccount); App.Settings.Save(); AccountChanged(); Status = FeatureText.Get("SelectionNotAuthentication"); return Task.CompletedTask; });
            RenameAccountCommand = Command(token => { if (SelectedAccount is not null && !string.IsNullOrWhiteSpace(AccountNickname) && AccountNickname.Length <= 80) SelectedAccount.Nickname = AccountNickname.Trim(); App.Settings.Save(); ReloadLists(); return Task.CompletedTask; });
            RemoveAccountCommand = Command(token => { if (SelectedAccount is not null) App.Accounts.Remove(SelectedAccount); App.Settings.Save(); ReloadLists(); AccountChanged(); return Task.CompletedTask; });
            SignInCommand = Command(token => { if (Confirm("SignInConfirm")) App.Accounts.BeginOfficialSignIn(); return Task.CompletedTask; });
            RefreshCatalogCommand = Command(async token => { await App.FastFlagCatalog.RefreshAsync(token); ReloadLists(); OnPropertyChanged(nameof(CatalogMetadata)); Status = App.FastFlagCatalog.LastStatus; });
            ApplyPresetCommand = Command(token => { if (SelectedPreset is null || !Confirm("PresetConfirm")) return Task.CompletedTask; App.FastFlagCatalog.Apply(SelectedPreset); App.FastFlags.Save(); App.Settings.Save(); Status = App.FastFlagCatalog.LastStatus; SelectedPreset = SelectedPreset; return Task.CompletedTask; });
            RevertPresetCommand = Command(token => { App.FastFlagCatalog.Revert(); App.FastFlags.Save(); App.Settings.Save(); Status = App.FastFlagCatalog.LastStatus; SelectedPreset = SelectedPreset; return Task.CompletedTask; });
            RefreshRadarCommand = Command(async token => { await App.DatacenterRadar.RefreshAsync(token); Status = App.DatacenterRadar.LastStatus; });
            HopCommand = Command(async token =>
            {
                if (SelectedRegion is null) return;
                var candidate = await App.DatacenterRadar.FindJoinableServerAsync(SelectedRegion, token);
                Status = App.DatacenterRadar.LastStatus;
                if (candidate is not null && Frontend.ShowMessageBox(string.Format(FeatureText.Get("HopConfirm"), candidate.RegionName, candidate.Country, candidate.PlaceId), MessageBoxImage.Question, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                { App.DatacenterRadar.Hop(candidate); Status = App.DatacenterRadar.LastStatus; }
            });
            ApplyOverlayCommand = Command(token =>
            {
                if (Settings.Overlay.Enabled && !Confirm("OverlayWarning")) return Task.CompletedTask;
                if (!string.IsNullOrWhiteSpace(Settings.Overlay.ToggleHotkey) && !UI.Elements.Overlay.CrosshairOverlayWindow.TryParseHotkey(Settings.Overlay.ToggleHotkey, out _, out _))
                { Status = FeatureText.Get("OverlayHotkeyInvalid"); return Task.CompletedTask; }
                if (!Regex.IsMatch(Settings.Overlay.Color ?? string.Empty, "^#(?:[A-Fa-f0-9]{6}|[A-Fa-f0-9]{8})$"))
                { Status = FeatureText.Get("OverlayColorInvalid"); return Task.CompletedTask; }
                FeatureText.Normalize(Settings); App.Settings.Save(); if (!FeatureRuntime.WatcherOwnsRuntime()) App.Overlay.ApplyToRunningPlayer(); Status = FeatureRuntime.WatcherOwnsRuntime() ? FeatureText.Get("RuntimeSaved") : App.Overlay.Status; return Task.CompletedTask;
            });
            HideOverlayCommand = Command(token => { Settings.Overlay.Enabled = false; App.Settings.Save(); App.Overlay.Dispose(); OnPropertyChanged(nameof(Settings)); Status = FeatureText.Get("Disabled"); return Task.CompletedTask; });
            CheckUpdatesCommand = Command(async token => { await App.Updates.CheckAsync(true, token); UpdateChanged(null, EventArgs.Empty); Status = App.Updates.Status; });
            InstallUpdateCommand = Command(async token =>
            {
                if (App.Updates.AvailableUpdate is not { } update || !Confirm("UpdateInstallConfirm")) return;
                if (await App.Updates.DownloadAndStartAsync(update, LaunchMode.None, new[] { "-settings" }, token)) App.SoftTerminate();
                else Status = App.Updates.Status;
            });
            SaveUpdateOptionsCommand = Command(token => { FeatureText.Normalize(Settings); App.Settings.Save(); Status = FeatureText.Get("Saved"); return Task.CompletedTask; });
            RestoreUpdateCommand = Command(token => { Status = FeatureText.Get("RollbackHelp"); return Task.CompletedTask; });
            CancelCommand = new RelayCommand(() => _operation?.Cancel());
            App.Updates.StateChanged += UpdateChanged;
            App.SoundPacks.PlaybackStatusChanged += PlaybackChanged;
        }
        private ICommand Command(Func<CancellationToken, Task> action) => new AsyncRelayCommand(async () =>
        {
            if (IsBusy || _disposed) return;
            IsBusy = true; _operation = new(); Status = FeatureText.Get("Working");
            try { await action(_operation.Token); }
            catch (InvalidDataException ex) { Status = ex.Message; App.Logger.WriteLine("FeatureTools", "Input validation rejected"); }
            catch (OperationCanceledException) { Status = FeatureText.Get("Cancelled"); }
            catch (Exception ex) { Status = FeatureText.Get("ActionFailed") + " " + ex.GetType().Name; App.Logger.WriteLine("FeatureTools", "Action failed: " + ex.GetType().Name); }
            finally { _operation.Dispose(); _operation = null; IsBusy = false; }
        });
        private static bool Confirm(string key) => Frontend.ShowMessageBox(FeatureText.Get(key), MessageBoxImage.Warning, MessageBoxButton.YesNo) == MessageBoxResult.Yes;
        private void ReloadLists()
        {
            Sounds.Clear(); foreach (var slot in Settings.SoundPacks) Sounds.Add(slot);
            Accounts.Clear(); foreach (var profile in Settings.AccountProfiles) Accounts.Add(profile);
            Presets.Clear(); foreach (var preset in App.FastFlagCatalog.Catalog.Presets) Presets.Add(preset);
        }
        private void AccountChanged() { OnPropertyChanged(nameof(Settings)); OnPropertyChanged(nameof(AccountStatus)); OnPropertyChanged(nameof(ActiveAccount)); OnPropertyChanged(nameof(SelectedProfile)); Status = App.Accounts.Status; }
        private void UpdateChanged(object? sender, EventArgs args) => Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        { if (_disposed) return; foreach (string name in new[] { nameof(UpdateStatus), nameof(AvailableVersion), nameof(ReleaseNotes), nameof(UpdateProgress) }) OnPropertyChanged(name); }));
        private void PlaybackChanged(object? sender, string status) { if (!_disposed) Status = status; }
        public async Task BackgroundCheckAsync() { if (!_disposed && !IsBusy) await App.Updates.CheckAsync(); }
        public void Dispose() { _disposed = true; _operation?.Cancel(); App.Accounts.InvalidateSession(); App.Updates.StateChanged -= UpdateChanged; App.SoundPacks.PlaybackStatusChanged -= PlaybackChanged; }
    }
}
