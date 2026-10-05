using System.Windows;
using System.Windows.Threading;
using GokuTrap.FeatureServices;
using GokuTrap.UI.Elements.Overlay;

namespace GokuTrap
{
    /// <summary>External WPF overlay only; it does not inject, hook graphics, or inspect game memory.</summary>
    public sealed class OverlayService : IDisposable
    {
        private CrosshairOverlayWindow? _window;
        private DispatcherTimer? _timer;
        private IntPtr _targetWindow;
        private int _targetPid;

        public string Status { get; private set; } = FeatureText.Get("Disabled");

        public void StartForRoblox(int processId, IntPtr windowHandle)
        {
            if (!App.Settings.Prop.Overlay.Enabled || windowHandle == IntPtr.Zero)
            {
                Status = FeatureText.Get("Disabled");
                return;
            }
            try
            {
                using Process process = Process.GetProcessById(processId);
                if (!SaiyanPerformanceManager.IsRobloxPlayer(process)) return;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { Status = FeatureText.Get("NoPlayer"); return; }
            Application.Current.Dispatcher.Invoke(() =>
            {
                StopCore();
                _targetPid = processId;
                _targetWindow = windowHandle;
                _window = new CrosshairOverlayWindow();
                _window.Configure(App.Settings.Prop.Overlay);
                _window.Show();
                _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(500) };
                _timer.Tick += (_, _) => Position();
                _timer.Start();
                Position();
            });
        }

        public void StopForRoblox(int processId)
        {
            if (_targetPid == processId)
                Application.Current?.Dispatcher.Invoke(StopCore);
        }

        private void Position()
        {
            if (_window is null || _targetWindow == IntPtr.Zero || !NativeOperations.GetWindowRect(_targetWindow, out NativeOperations.RECT rect))
            {
                StopCore();
                return;
            }
            if (!App.Settings.Prop.Overlay.Enabled) { StopCore(); return; }
            try
            {
                using Process target = Process.GetProcessById(_targetPid);
                if (!SaiyanPerformanceManager.IsRobloxPlayer(target) || target.MainWindowHandle != _targetWindow) { StopCore(); return; }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { StopCore(); return; }
            _window.Configure(App.Settings.Prop.Overlay);
            _window.SetBounds(rect, NativeOperations.GetDpiForWindow(_targetWindow));
            _window.SetTargetVisible(!NativeOperations.IsIconic(_targetWindow) && NativeOperations.GetForegroundWindow() == _targetWindow);
            Status = FeatureText.Get(_window.HotkeyRegistered ? "OverlayActive" : "OverlayHotkeyUnavailable");
        }

        private void StopCore()
        {
            _timer?.Stop();
            _timer = null;
            _window?.Close();
            _window = null;
            _targetWindow = IntPtr.Zero;
            _targetPid = 0;
            Status = FeatureText.Get("Disabled");
        }

        public void ApplyToRunningPlayer()
        {
            Dispose();
            if (!App.Settings.Prop.Overlay.Enabled) return;
            foreach (Process process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(App.RobloxPlayerAppName)))
                using (process)
                {
                    try
                    {
                        if (SaiyanPerformanceManager.IsRobloxPlayer(process) && process.MainWindowHandle != IntPtr.Zero)
                        { StartForRoblox(process.Id, process.MainWindowHandle); break; }
                    }
                    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { Status = FeatureText.Get("NoPlayer"); }
                }
        }

        public void Dispose()
        {
            if (Application.Current is not null)
                Application.Current.Dispatcher.Invoke(StopCore);
        }
    }
}
