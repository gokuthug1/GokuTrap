using System.Reflection;
using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using GokuTrap;
using GokuTrap.Models.Persistable;
using GokuTrap.UI.Elements.Settings.Pages;
using GokuTrap.UI.Elements.Overlay;
using GokuTrap.UI.ViewModels.Settings;

internal static class Program
{
    private static int _checks;
    [STAThread]
    private static int Main(string[] args)
    {
        string scratch = Path.Combine(Path.GetTempPath(), "GokuTrap-FeatureChecks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            var paths = typeof(App).Assembly.GetType("GokuTrap.Paths")!;
            paths.GetMethod("Initialize")!.Invoke(null, new object[] { scratch });
            var app = new TestApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
            app.Resources["EnumNameConverter"] = Activator.CreateInstance(typeof(App).Assembly.GetType("GokuTrap.UI.Converters.EnumNameConverter")!);
            app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ThemeType.Dark });
            app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/GokuTrap;component/UI/Style/Dark.xaml") });
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/GokuTrap;component/UI/Style/Default.xaml") });
            App.Settings.Prop = new Settings { CheckForUpdates = false };
            App.State.Prop = new State();
            App.Settings.Save(); App.State.Save();
            Check(!JsonSerializer.Deserialize<Settings>("{}")!.SaiyanMode.Enabled, "legacy settings default performance off");
            Check(!JsonSerializer.Deserialize<Settings>("{}")!.Overlay.Enabled, "legacy settings default overlay off");
            var nullable = JsonSerializer.Deserialize<Settings>("{\"SaiyanMode\":null,\"Overlay\":null,\"Update\":null,\"SoundPacks\":null,\"AccountProfiles\":null,\"FastFlagCatalog\":null}")!;
            FeatureText.Normalize(nullable);
            Check(nullable.Update.CheckIntervalHours == 24 && nullable.SoundPacks.Count == 0, "explicit-null migration preserves safe defaults");
            string serialized = JsonSerializer.Serialize(new AccountProfile { RobloxUserId = 123 });
            var unsafeProfile = new Settings { AccountProfiles = new() { new AccountProfile { RobloxUserId = 5, ValidationStatus="Authenticated", AvatarThumbnailUrl="file:///C:/secret" } } };
            FeatureText.Normalize(unsafeProfile);
            Check(unsafeProfile.AccountProfiles[0].ValidationStatus=="NotValidated" && unsafeProfile.AccountProfiles[0].AvatarThumbnailUrl==string.Empty, "stale authentication and unsafe avatar paths cleared on load");
            Check(!serialized.Contains("Cookie", StringComparison.OrdinalIgnoreCase) && !serialized.Contains("Credential", StringComparison.OrdinalIgnoreCase), "account persistence has no secrets");
            var profile = new AccountProfile { RobloxUserId = 123, DisplayName = "Public only" };
            App.Settings.Prop.AccountProfiles.Add(profile);
            App.Accounts.Select(profile);
            Check(App.Accounts.ActiveProfile is null, "profile selection never authenticates");

            string root = args.FirstOrDefault() ?? Directory.GetCurrentDirectory();
            string catalogJson = File.ReadAllText(Path.Combine(root, "GokuTrap", "Resources", "FastFlagCatalog.json"));
            Check(FastFlagCatalogManager.TryParseCatalog(catalogJson, out var catalog) && catalog!.Presets.Count == 3, "repository catalog has three valid profiles");
            Check(!FastFlagCatalogManager.TryParseCatalog("{", out _), "malformed JSON rejected without exception");
            Check(!FastFlagCatalogManager.TryParseCatalog(catalogJson.Replace("\"schemaVersion\": 1", "\"schemaVersion\": \"bad\""), out _), "wrong schema type rejected");
            Check(!FastFlagCatalogManager.TryParseCatalog(catalogJson.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"schemaVersion\": 1"), out _), "duplicate root property rejected");
            Check(!FastFlagCatalogManager.TryParseCatalog(catalogJson.Replace("FIntDebugForceMSAASamples", "FStringArbitraryCode"), out _), "unknown flags rejected");
            Check(!FastFlagCatalogManager.TryParseCatalog(catalogJson.Replace("\"id\": \"cinematic-high-fidelity\"", "\"id\": \"competitive-low-latency\""), out _), "duplicate preset IDs rejected");
            Check(!FastFlagCatalogManager.TryParseCatalog(catalogJson.Replace("\"FIntDebugForceMSAASamples\": \"1\"", "\"FIntDebugForceMSAASamples\": \"1\", \"FIntDebugForceMSAASamples\": \"2\""), out _), "duplicate flag keys rejected");
            Check(!FastFlagCatalogManager.TryParseCatalog(catalogJson.Replace("\"21\"", "\"999\""), out _), "out-of-range flags rejected");
            Check(!FastFlagCatalogManager.TryParseCatalog(new string('x', 262145), out _), "oversize catalog rejected");
            App.FastFlags.Prop.Clear();
            App.FastFlags.SetValue("Unrelated", "keep");
            App.FastFlags.SetValue("FIntDebugForceMSAASamples", "2");
            var preset = catalog!.Presets[0];
            Check(App.FastFlagCatalog.Preview(preset).First().Before == "2", "preview shows exact previous value");
            App.FastFlagCatalog.Apply(preset);
            var previous = JsonSerializer.Serialize(App.Settings.Prop.FastFlagCatalog.PreviousValues);
            App.FastFlagCatalog.Apply(preset);
            Check(previous == JsonSerializer.Serialize(App.Settings.Prop.FastFlagCatalog.PreviousValues), "repeated apply is idempotent");
            App.FastFlags.SetValue("DFIntDebugFRMQualityLevelOverride", "3");
            App.FastFlagCatalog.Revert();
            Check(App.FastFlags.GetValue("FIntDebugForceMSAASamples") == "2" && App.FastFlags.GetValue("DFIntDebugFRMQualityLevelOverride") == "3" && App.FastFlags.GetValue("Unrelated") == "keep", "undo restores previous values and preserves manual/unrelated edits");

            Check(ApplicationUpdateService.TryGetVersion("v3.2.0-beta.1", out var version) && version == new Version(3,2,0), "release tags parsed");
            Check(ApplicationUpdateService.VersionsMatch(new Version(3,2,0,0), new Version(3,2,0)), "release and executable versions ignore omitted trailing zeros");
            Check(!ApplicationUpdateService.VersionsMatch(new Version(3,2,0,1), new Version(3,2,0)), "nonzero executable revision mismatch rejected");
            Check(!ApplicationUpdateService.TryGetVersion("release-script", out _), "invalid tag rejected");
            Check(!ApplicationUpdateService.TryGetVersion("v999999999999999999999.0", out _), "overflow version rejected");
            Check(ApplicationUpdateService.SelectWindowsX64Asset(new[] { new GithubReleaseAsset { Name = "GokuTrap-arm64.exe" }, new GithubReleaseAsset { Name = "GokuTrap-x86.exe" }, new GithubReleaseAsset { Name = "GokuTrap-win-x64.exe" }, new GithubReleaseAsset { Name = "GokuTrap.exe" } })!.Name == "GokuTrap-win-x64.exe", "deterministic x64 selection excludes other platforms");
            string bytesFile = Path.Combine(scratch, "payload"); File.WriteAllText(bytesFile, "hello");
            string digest = "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("hello")));
            ApplicationUpdateService.VerifyDigest(bytesFile, digest); Check(true, "published SHA-256 verified");
            Throws(() => ApplicationUpdateService.VerifyDigest(bytesFile, "sha256:" + new string('0',64)), "digest mismatch rejected");
            Throws(() => ApplicationUpdateService.VerifyDigest(bytesFile, "sha256:bad"), "malformed digest rejected");
            Throws(() => ApplicationUpdateService.VerifyWindowsX64Executable(bytesFile), "non-executable update rejected");
            byte[] pe = new byte[256]; pe[0]=0x4d; pe[1]=0x5a; pe[0x3c]=0x80; pe[0x80]=0x50; pe[0x81]=0x45; pe[0x84]=0x64; pe[0x85]=0x86; pe[0x98]=0x0b; pe[0x99]=0x02;
            File.WriteAllBytes(bytesFile, pe); ApplicationUpdateService.VerifyWindowsX64Executable(bytesFile); Check(true, "x64 PE32+ accepted");
            string installed = Path.Combine(scratch, "installed.exe"); File.WriteAllText(installed, "old executable");
            ApplicationUpdateService.ReplaceWithRollback(bytesFile, installed);
            Check(File.ReadAllText(installed + ".rollback") == "old executable" && File.ReadAllBytes(installed).SequenceEqual(pe), "atomic replacement retains exact rollback bytes");
            File.WriteAllText(bytesFile,"bad replacement");
            Throws(() => ApplicationUpdateService.ReplaceWithRollback(bytesFile, installed), "invalid replacement leaves installation intact");
            Check(File.ReadAllBytes(installed).SequenceEqual(pe), "failed replacement preserves current installation");
            pe[0x84]=0x4c; pe[0x85]=0x01; File.WriteAllBytes(bytesFile, pe); Throws(() => ApplicationUpdateService.VerifyWindowsX64Executable(bytesFile), "x86 PE rejected");

            byte[] audio = new byte[32]; Encoding.ASCII.GetBytes("ID3").CopyTo(audio,0);
            string audioFile = Path.Combine(scratch, "local.mp3"); File.WriteAllBytes(audioFile,audio);
            Check(SoundPackManager.HasAudioHeader(audio,".mp3"), "MP3 format header accepted");
            Check(!SoundPackManager.HasAudioHeader(audio,".wav"), "extension spoofing rejected");
            var slot = App.SoundPacks.Import(audioFile,SoundPackTarget.CharacterJump);
            Check(File.Exists(App.SoundPacks.GetAssetPath(slot)), "audio copied into managed storage");
            Throws(() => App.SoundPacks.Import(audioFile,SoundPackTarget.CharacterJump), "duplicate sound slot rejected");
            string mod = SoundPackManager.GetClientAssetPath(slot.Target); Directory.CreateDirectory(Path.GetDirectoryName(mod)!); File.WriteAllText(mod,"unrelated");
            Check(!App.SoundPacks.ApplySlot(slot) && File.ReadAllText(mod)=="unrelated", "unrelated mod never overwritten");
            File.Delete(mod); Check(App.SoundPacks.ApplySlot(slot), "managed sound applies to real slot mapping");
            Check(App.SoundPacks.RestoreSlot(slot) && !File.Exists(mod), "owned sound restore removes managed mod");
            Throws(() => App.SoundPacks.GetAssetPath(new SoundPackSlot { AssetFileName="../secret" }), "managed path traversal rejected");
            string activityFile = (string)paths.GetProperty("PlayerActivity")!.GetValue(null)!;
            File.WriteAllText(activityFile, "{invalid snapshot");
            string stateBeforeRadar = File.ReadAllText(App.State.FileLocation);
            Task<RegionJoinCandidate?> discovery = App.DatacenterRadar.FindJoinableServerAsync(App.DatacenterRadar.Regions[2]);
            var discoveryWatch = Stopwatch.StartNew();
            while (!discovery.IsCompleted && discoveryWatch.Elapsed < TimeSpan.FromSeconds(5)) { Pump(); Thread.Sleep(10); }
            Check(discovery.IsCompletedSuccessfully && discovery.Result is null &&
                File.ReadAllText(activityFile) == "{invalid snapshot" && File.ReadAllText(App.State.FileLocation) == stateBeforeRadar,
                "invalid activity snapshot fails closed without recovery writes to user state");
            Check(App.DatacenterRadar.Regions.Count==5 && App.DatacenterRadar.Regions.All(x=>x.Summary.Contains("not measured")), "radar never fabricates latency samples");
            Check(!App.DatacenterRadar.Hop(new RegionJoinCandidate(1,"bad server id","Tokyo","JP",DateTimeOffset.UtcNow)), "invalid server handoff rejected");
            Check(!App.DatacenterRadar.Hop(new RegionJoinCandidate(1,Guid.NewGuid().ToString("D"),"Tokyo","JP",DateTimeOffset.UtcNow.AddMinutes(-2))), "stale server handoff rejected");
            File.WriteAllText(activityFile, JsonSerializer.Serialize(new { PlaceId=2L, JobId="", ObservedUtc=DateTimeOffset.UtcNow }));
            Check(!App.DatacenterRadar.Hop(new RegionJoinCandidate(1,Guid.NewGuid().ToString("D"),"Tokyo","JP",DateTimeOffset.UtcNow)), "changed place context rejects a previously verified server hop");
            Check(App.Updates.CheckAsync().GetAwaiter().GetResult() is null, "disabled background update check makes no request");
            Check(!App.Updates.DownloadAndStartAsync(new UpdateCheckResult(new GokuTrap.Models.APIs.GitHub.GithubRelease(), new GithubReleaseAsset { Name="GokuTrap.exe", BrowserDownloadUrl="http://evil.invalid/payload", Size=1 }, new Version(9,0)), GokuTrap.Enums.LaunchMode.None, Array.Empty<string>()).GetAwaiter().GetResult(), "unsafe update URL fails closed before network/download");
            var runtime = typeof(App).Assembly.GetType("GokuTrap.FeatureRuntime")!;
            bool unrelatedBefore = App.Settings.Prop.EnableBetterMatchmaking;
            App.Settings.Prop.EnableBetterMatchmaking = !unrelatedBefore;
            var persistedRuntime = JsonSerializer.Deserialize<Settings>(File.ReadAllText(App.Settings.FileLocation))!;
            persistedRuntime.Overlay.Size = 27;
            File.WriteAllText(App.Settings.FileLocation, JsonSerializer.Serialize(persistedRuntime));
            Check((bool)runtime.GetMethod("TryReloadSettings")!.Invoke(null, null)! && App.Settings.Prop.Overlay.Size == 27 &&
                App.Settings.Prop.EnableBetterMatchmaking == !unrelatedBefore, "watcher reload changes runtime controls without replacing unrelated settings");
            Check(!(bool)runtime.GetMethod("TryReloadSettings")!.Invoke(null, null)!, "unchanged runtime settings are not reapplied");
            Check(!App.SaiyanMode.ApplyForProcess(Environment.ProcessId), "disabled policy does not touch test process");
            App.Settings.Prop.SaiyanMode.Enabled=true;
            Check(!App.SaiyanMode.ApplyForProcess(Environment.ProcessId), "non-Player process rejected"); App.Settings.Prop.SaiyanMode.Enabled=false;
            Console.WriteLine("TOPOLOGY " + App.SaiyanMode.TopologyDescription);

            // Test delivered controls, not a mock: bind the actual page and invoke its measure button.
            var bindingErrors = new BindingErrorListener(); PresentationTraceSources.DataBindingSource.Listeners.Add(bindingErrors);
            var page = new FeatureToolsPage();
            var host = new Window { Content=page, Width=1000, Height=900, ShowInTaskbar=false, Title="GokuTrap feature verification" };
            host.Show(); Pump();
            var vm = (FeatureToolsViewModel)page.DataContext;
            var tabs = Find<TabControl>(page).Single();
            Check(tabs.Items.Count==7, "all seven tabs present in actual WPF UI");
            foreach (TabItem tab in tabs.Items) { tabs.SelectedItem=tab; host.UpdateLayout(); Pump(); Check(!string.IsNullOrWhiteSpace(tab.Header?.ToString()), "tab has localized accessible title"); }
            tabs.SelectedIndex=0; Pump();
            var measureButton = Find<Button>(page).First(x => ReferenceEquals(x.Command,vm.MeasureCommand));
            var buttonPeer = new ButtonAutomationPeer(measureButton);
            ((IInvokeProvider)buttonPeer.GetPattern(PatternInterface.Invoke)).Invoke(); Pump();
            Task measurement = ((IAsyncRelayCommand)measureButton.Command).ExecutionTask ?? throw new Exception("Button did not invoke command");
            var watch = Stopwatch.StartNew();
            while (!measurement.IsCompleted && watch.Elapsed < TimeSpan.FromSeconds(10)) { Pump(); Thread.Sleep(10); }
            Check(measurement.IsCompletedSuccessfully, "memory command completes asynchronously"); Pump();
            Check(vm.Status==FeatureText.Get("NoPlayer"), "memory command reports honest empty state");
            vm.SelectedPreset=vm.Presets[0]; Check(vm.Changes.Count==2, "UI preset selection generates diff");
            tabs.SelectedIndex=3; Pump();
            Check(Find<ListBox>(page).Any(x=>ReferenceEquals(x.ItemsSource,vm.Changes)), "diff is connected to actual list control");
            Check(bindingErrors.Errors.Count==0, "no WPF binding errors: " + string.Join(" | ",bindingErrors.Errors));
            if (args.Contains("--capture"))
            {
                tabs.SelectedIndex=0; Pump();
                var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)host.ActualWidth,(int)host.ActualHeight,96,96,PixelFormats.Pbgra32); bitmap.Render(host);
                var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using var output=File.Create(Path.Combine(root,"docs","feature-ui-verification.png")); encoder.Save(output);
            }
            host.Close(); Pump();
            using (var vm2 = new FeatureToolsViewModel())
            {
                var overlay = new CrosshairOverlayWindow(); overlay.Configure(App.Settings.Prop.Overlay); overlay.Show(); Pump();
                IntPtr handle=new WindowInteropHelper(overlay).Handle;
                int style=GetWindowLong(handle,-20);
                Check((style & 0x20)!=0 && (style & 0x08000000)!=0, "actual overlay window is click-through and non-activating"); overlay.Close();
            }
            Check(CrosshairOverlayWindow.TryParseHotkey("Ctrl+Shift+X",out _,out _), "modifier toggle hotkey accepted");
            Check(!CrosshairOverlayWindow.TryParseHotkey("X",out _,out _) && !CrosshairOverlayWindow.TryParseHotkey("Ctrl+LeftCtrl",out _,out _), "gameplay single-key/modifier-only hotkeys rejected");
            Check(App.Logger.AsDocument.IndexOf(".ROBLOSECURITY",StringComparison.OrdinalIgnoreCase)<0, "test diagnostics contain no credential material");
            app.Shutdown();
            Console.WriteLine($"PASS {_checks} checks"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { Directory.Delete(scratch,true); }
    }
    private sealed class TestApp : App
    {
        protected override void OnStartup(StartupEventArgs e) { /* Isolated UI harness: never run launcher startup. */ }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd,int index);
    private static void Check(bool condition,string name) { if (!condition) throw new Exception("FAIL " + name); _checks++; Console.WriteLine("PASS " + name); }
    private static void Throws(Action action,string name) { try { action(); } catch { Check(true,name); return; } throw new Exception("FAIL " + name); }
    private static void Pump() { var frame=new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>frame.Continue=false)); Dispatcher.PushFrame(frame); }
    private static IEnumerable<T> Find<T>(DependencyObject parent) where T:DependencyObject
    { for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++) { var child=VisualTreeHelper.GetChild(parent,i); if(child is T match) yield return match; foreach(var nested in Find<T>(child)) yield return nested; } }
    private sealed class BindingErrorListener : System.Diagnostics.TraceListener
    {
        public List<string> Errors { get; }=new();
        public override void Write(string? message) { if(!string.IsNullOrEmpty(message)) Errors.Add(message); }
        public override void WriteLine(string? message) => Write(message);
    }
}
