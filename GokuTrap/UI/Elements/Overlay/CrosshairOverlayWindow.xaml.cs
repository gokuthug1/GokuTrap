using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Windows.Input;
using GokuTrap.FeatureServices;

namespace GokuTrap.UI.Elements.Overlay
{
    public partial class CrosshairOverlayWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WM_HOTKEY = 0x0312;
        private const int HotkeyId = 0x474B;
        private HwndSource? _source;
        private bool _hotkeyRegistered;
        private string? _hotkey;
        private double _offsetX, _offsetY;
        public bool UserHidden { get; private set; }
        public bool HotkeyRegistered => _hotkeyRegistered;

        public CrosshairOverlayWindow()
        {
            InitializeComponent();
            SourceInitialized += OnSourceInitialized;
            Closed += (_, _) => { UnregisterHotkey(); _source?.RemoveHook(WindowProc); };
        }

        public void Configure(OverlaySettings settings)
        {
            double size = Math.Clamp(settings.Size, 6, 96);
            double opacity = Math.Clamp(settings.Opacity, 0.1, 1);
            Color color = ParseColor(settings.Color);
            var brush = new SolidColorBrush(Color.FromArgb((byte)(color.A * opacity), color.R, color.G, color.B));

            Crosshair.Width = size;
            Crosshair.Height = size;
            Horizontal.X1 = 0; Horizontal.X2 = size; Horizontal.Y1 = Horizontal.Y2 = size / 2;
            Vertical.Y1 = 0; Vertical.Y2 = size; Vertical.X1 = Vertical.X2 = size / 2;
            Horizontal.Stroke = Vertical.Stroke = brush;
            Horizontal.StrokeThickness = Vertical.StrokeThickness = Math.Max(1, size / 12);
            Circle.Width = Circle.Height = size * .7;
            Circle.Stroke = brush;
            Circle.StrokeThickness = Math.Max(1, size / 12);
            Circle.Margin = new Thickness(size * .15);
            Dot.Width = Dot.Height = Math.Max(2, size / 6);
            Dot.Fill = brush;
            Dot.Margin = new Thickness((size - Dot.Width) / 2, (size - Dot.Height) / 2, 0, 0);
            Horizontal.Visibility = Vertical.Visibility = settings.Style == OverlayStyle.Cross ? Visibility.Visible : Visibility.Collapsed;
            Circle.Visibility = settings.Style == OverlayStyle.Circle ? Visibility.Visible : Visibility.Collapsed;
            Dot.Visibility = settings.Style == OverlayStyle.Dot ? Visibility.Visible : Visibility.Collapsed;
            Telemetry.Visibility = settings.ShowTelemetry ? Visibility.Visible : Visibility.Collapsed;
            Canvas.SetLeft(Telemetry, 12);
            Canvas.SetTop(Telemetry, 12);
            _offsetX = settings.OffsetX;
            _offsetY = settings.OffsetY;
            RegisterHotkey(settings.ToggleHotkey);
        }

        internal void SetBounds(NativeOperations.RECT rect, uint dpi)
        {
            if (_source is null) return;
            // Native screen coordinates avoid incorrectly scaling negative/mixed-DPI monitor origins.
            NativeOperations.SetWindowPos(_source.Handle, new IntPtr(-1), rect.Left, rect.Top,
                Math.Max(1, rect.Right - rect.Left), Math.Max(1, rect.Bottom - rect.Top), 0x0010);
            var transform = _source.CompositionTarget.TransformFromDevice;
            var size = transform.Transform(new Point(rect.Right - rect.Left, rect.Bottom - rect.Top));
            Canvas.SetLeft(Crosshair, Math.Clamp((size.X - Crosshair.Width) / 2 + _offsetX, 0, Math.Max(0, size.X - Crosshair.Width)));
            Canvas.SetTop(Crosshair, Math.Clamp((size.Y - Crosshair.Height) / 2 + _offsetY, 0, Math.Max(0, size.Y - Crosshair.Height)));
        }

        public void SetTargetVisible(bool visible) => Visibility = visible && !UserHidden ? Visibility.Visible : Visibility.Hidden;

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            _source = PresentationSource.FromVisual(this) as HwndSource;
            if (_source is null)
                return;
            int extended = NativeOperations.GetWindowLong(_source.Handle, GWL_EXSTYLE);
            NativeOperations.SetWindowLong(_source.Handle, GWL_EXSTYLE, extended | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);
            _source.AddHook(WindowProc);
            _hotkey = null;
            RegisterHotkey(App.Settings.Prop.Overlay.ToggleHotkey);
        }

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
            {
                UserHidden = !UserHidden;
                if (UserHidden) Hide();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void RegisterHotkey(string hotkey)
        {
            if (_hotkey == hotkey && _source is not null) return;
            _hotkey = hotkey;
            UnregisterHotkey();
            if (_source is null || !TryParseHotkey(hotkey, out uint modifiers, out uint key))
                return;
            _hotkeyRegistered = NativeOperations.RegisterHotKey(_source.Handle, HotkeyId, modifiers | 0x4000, key);
            if (!_hotkeyRegistered)
                App.Logger.WriteLine("CrosshairOverlayWindow", "Overlay hotkey is unavailable or already used by another application.");
        }

        private void UnregisterHotkey()
        {
            if (_hotkeyRegistered && _source is not null)
                NativeOperations.UnregisterHotKey(_source.Handle, HotkeyId);
            _hotkeyRegistered = false;
        }

        public static bool TryParseHotkey(string value, out uint modifiers, out uint key)
        {
            modifiers = 0;
            key = 0;
            if (string.IsNullOrWhiteSpace(value))
                return false;
            string[] parts = value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
                return false;
            foreach (string part in parts[..^1])
            {
                if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase)) modifiers |= 0x2;
                else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= 0x1;
                else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= 0x4;
                else return false;
            }
            if (!Enum.TryParse(parts[^1], true, out Key parsed) || !Enum.IsDefined(typeof(Key), parsed) ||
                parsed is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return false;
            key = (uint)KeyInterop.VirtualKeyFromKey(parsed);
            return modifiers != 0 && key != 0;
        }

        private static Color ParseColor(string value)
        {
            try { return (Color)ColorConverter.ConvertFromString(value)!; }
            catch { return Colors.White; }
        }
    }
}
