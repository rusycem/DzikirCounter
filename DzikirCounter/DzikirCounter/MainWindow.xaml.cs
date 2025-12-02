// FILENAME: MainWindow.xaml.cs
using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using Windows.Graphics;
using WinRT.Interop;
using System.ComponentModel;
using DzikirCounter;
using Microsoft.UI.Windowing;
using System.Diagnostics;
using Microsoft.UI.Dispatching;
using System.Runtime.InteropServices;
using System.IO; // Added for Path.Combine

namespace DzikirCounter
{
    public sealed partial class MainWindow : Window
    {
        // Win32 P/Invoke Definitions (Min Size)
        private const int WM_GETMINMAXINFO = 0x0024;
        private const int WM_DESTROY = 0x0002; // NEW: DETECT CLOSE
        private const int MIN_WIDTH = 300;
        private const int MIN_HEIGHT = 75;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        public delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData);

        [DllImport("ComCtl32.dll", SetLastError = true)]
        public static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, uint uIdSubclass, IntPtr dwRefData);

        // NEW: REQUIRED TO FIX CRASH
        [DllImport("ComCtl32.dll", SetLastError = true)]
        public static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, uint uIdSubclass);

        [DllImport("ComCtl32.dll", SetLastError = true)]
        public static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        private static IntPtr WindowSubclass(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData)
        {
            if (uMsg == WM_GETMINMAXINFO)
            {
                try
                {
                    MINMAXINFO minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(lParam)!;
                    minMaxInfo.ptMinTrackSize.x = MIN_WIDTH;
                    minMaxInfo.ptMinTrackSize.y = MIN_HEIGHT;
                    Marshal.StructureToPtr(minMaxInfo, lParam, false);
                }
                catch { }
                return IntPtr.Zero;
            }

            // CRITICAL FIX: Unhook on Destroy
            if (uMsg == WM_DESTROY)
            {
                RemoveWindowSubclass(hWnd, s_windowSubclass, (uint)uIdSubclass.ToInt32());
            }

            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        // -------------------------------------------------------------
        // Core Logic
        // -------------------------------------------------------------

        public DzikirCounterViewModel ViewModel { get; } = new DzikirCounterViewModel();

        private GlobalMouseHook? _xButtonHook;
        private GlobalMouseHook? _lButtonHook;
        private GlobalMouseHook? _customMouseHook;
        private GlobalKeyboardHook? _customKeyboardHook;
        private GlobalMouseHook? _recordingMouseHook;
        private GlobalKeyboardHook? _recordingKeyboardHook;
        private bool _isRecording = false;

        private AppWindow? _appWindow;
        private DispatcherQueue _uiDispatcher;

        private static readonly SubclassProc s_windowSubclass = WindowSubclass;
        private Brush _defaultButtonBackground;

        public MainWindow()
        {
            Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);

            this.InitializeComponent();
            this.Title = "Dzikir Counter Utility";

            // Fix for Theme Sync at startup:
            // Ensure the toggle matches the system theme (which the app defaults to).
            // This triggers the Toggled event to set the RequestedTheme explicitly.
            ThemeToggle.IsOn = Application.Current.RequestedTheme == ApplicationTheme.Dark;

            _uiDispatcher = DispatcherQueue.GetForCurrentThread();
            _defaultButtonBackground = RecordButton.Background;

            InitializeAppWindow(380, 580); // Increased Height slightly for new UI

            if (ViewModel.IsHookEnabled) EnableXButtonHook();
            if (ViewModel.IsLButtonHookEnabled) EnableLButtonHook();
            if (ViewModel.IsCustomInputEnabled) EnableCustomHook();

            if (_appWindow != null)
            {
                _appWindow.Closing += AppWindow_Closing;
            }
        }

        private void EnableXButtonHook()
        {
            if (_xButtonHook != null) return;
            _xButtonHook = new GlobalMouseHook(MouseHookMode.XButtons, ViewModel.Increment, ViewModel.Decrement, _uiDispatcher);
            _xButtonHook.SetHook();
        }

        private void DisableXButtonHook()
        {
            if (_xButtonHook != null) { _xButtonHook.Dispose(); _xButtonHook = null; }
        }

        private void EnableLButtonHook()
        {
            if (_lButtonHook != null) return;
            _lButtonHook = new GlobalMouseHook(MouseHookMode.LButtonOnly, ViewModel.Increment, null, _uiDispatcher);
            _lButtonHook.SetHook();
        }

        private void DisableLButtonHook()
        {
            if (_lButtonHook != null) { _lButtonHook.Dispose(); _lButtonHook = null; }
        }

        private void EnableCustomHook()
        {
            if (ViewModel.CustomInputType == InputType.None) return;

            if (ViewModel.CustomInputType == InputType.Mouse)
            {
                if (_customMouseHook != null) return;
                _customMouseHook = new GlobalMouseHook(ViewModel.CustomInputCode, ViewModel.Increment, _uiDispatcher);
                _customMouseHook.SetHook();
            }
            else if (ViewModel.CustomInputType == InputType.Keyboard)
            {
                if (_customKeyboardHook != null) return;
                _customKeyboardHook = new GlobalKeyboardHook((code) => ViewModel.Increment(), _uiDispatcher, ViewModel.CustomInputCode);
                _customKeyboardHook.SetHook();
            }
        }

        private void DisableCustomHook()
        {
            if (_customMouseHook != null) { _customMouseHook.Dispose(); _customMouseHook = null; }
            if (_customKeyboardHook != null) { _customKeyboardHook.Dispose(); _customKeyboardHook = null; }
        }

        private void RecordCustomInput_Click(object sender, RoutedEventArgs e)
        {
            if (_isRecording) return;
            _isRecording = true;

            DisableXButtonHook();
            DisableLButtonHook();
            DisableCustomHook();

            BoundKeyText.Visibility = Visibility.Collapsed;
            InstructionText.Visibility = Visibility.Visible;
            RecordButton.Background = new SolidColorBrush(Colors.PaleGoldenrod);

            StartRecordingListeners();
        }

        private void StartRecordingListeners()
        {
            _recordingMouseHook = new GlobalMouseHook((msgCode) => { FinishRecording(InputType.Mouse, msgCode); }, _uiDispatcher);
            _recordingMouseHook.SetHook();

            _recordingKeyboardHook = new GlobalKeyboardHook((keyCode) => { FinishRecording(InputType.Keyboard, keyCode); }, _uiDispatcher, -1);
            _recordingKeyboardHook.SetHook();
        }

        private void FinishRecording(InputType type, int code)
        {
            if (!_isRecording) return;

            if (_recordingMouseHook != null) { _recordingMouseHook.Dispose(); _recordingMouseHook = null; }
            if (_recordingKeyboardHook != null) { _recordingKeyboardHook.Dispose(); _recordingKeyboardHook = null; }

            ViewModel.CustomInputType = type;
            ViewModel.CustomInputCode = code;

            string name = "Unknown";
            if (type == InputType.Keyboard) name = ((Windows.System.VirtualKey)code).ToString();
            if (type == InputType.Mouse)
            {
                if (code == 0x0201) name = "Left Click";
                else if (code == 0x0204) name = "Right Click";
                else if (code == 0x0207) name = "Middle Click";
                else if (code == 0x020B + 1) name = "XButton 1";
                else if (code == 0x020B + 2) name = "XButton 2";
                else name = "Mouse Button";
            }
            ViewModel.CustomInputName = name;

            _isRecording = false;

            BoundKeyText.Visibility = Visibility.Visible;
            InstructionText.Visibility = Visibility.Collapsed;
            RecordButton.Background = _defaultButtonBackground;

            if (ViewModel.IsHookEnabled) EnableXButtonHook();
            if (ViewModel.IsLButtonHookEnabled) EnableLButtonHook();
            if (ViewModel.IsCustomInputEnabled) EnableCustomHook();
        }

        private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            args.Cancel = true;
            _appWindow?.Hide();
        }

        private void XButtonHookToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle) { if (toggle.IsOn) EnableXButtonHook(); else DisableXButtonHook(); }
        }

        private void LButtonHookToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle) { if (toggle.IsOn) EnableLButtonHook(); else DisableLButtonHook(); }
        }

        private void CustomHookToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle) { if (toggle.IsOn) EnableCustomHook(); else DisableCustomHook(); }
        }

        // NEW: Theme Toggle Handler
        private void ThemeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                if (Content is FrameworkElement rootElement)
                {
                    rootElement.RequestedTheme = toggle.IsOn ? ElementTheme.Dark : ElementTheme.Light;
                }
            }
        }

        private void InitializeAppWindow(int width, int height)
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hWnd));
            _appWindow.Resize(new SizeInt32(width, height));

            // NEW: Set Window Icon Manually
            string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
            if (File.Exists(iconPath))
            {
                _appWindow.SetIcon(iconPath);
            }

            SetWindowSubclass(hWnd, s_windowSubclass, 0, IntPtr.Zero);
            CenterWindow(_appWindow);
        }

        private void CenterWindow(AppWindow appWindow)
        {
            DisplayArea displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
            if (displayArea != null)
            {
                var CenteredPosition = appWindow.Position;
                CenteredPosition.X = ((displayArea.WorkArea.Width - appWindow.Size.Width) / 2);
                CenteredPosition.Y = ((displayArea.WorkArea.Height - appWindow.Size.Height) / 2);
                appWindow.Move(CenteredPosition);
            }
        }

        private void DecreaseButton_Click(object sender, RoutedEventArgs e) => ViewModel.Decrement();
        private void IncreaseButton_Click(object sender, RoutedEventArgs e) => ViewModel.Increment();
        private void ResetButton_Click(object sender, RoutedEventArgs e) => ViewModel.Reset();
        private void SaveButton_Click(object sender, RoutedEventArgs e) { ViewModel.SaveState(); _appWindow?.Hide(); }
    }
}