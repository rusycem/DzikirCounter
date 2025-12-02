// FILENAME: GlobalMouseHook.cs
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Input;
using Microsoft.UI.Dispatching;
using System.Diagnostics;

namespace DzikirCounter
{
    // UPDATED: Added "Custom" for specific button listening and "Recording" to catch any click
    public enum MouseHookMode { XButtons, LButtonOnly, Custom, Recording }

    public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    public class GlobalMouseHook : IDisposable
    {
        // 1. Windows API Constants
        private const int WH_MOUSE_LL = 14;

        // Mouse Messages
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_XBUTTONDOWN = 0x020B;

        private const ushort XBUTTON1 = 0x0001;
        private const ushort XBUTTON2 = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        // 2. Windows API Imports
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        // 3. Hook Management
        private IntPtr _hookHandle = IntPtr.Zero;
        private LowLevelMouseProc _proc;

        private Action _increaseCallback;
        private Action? _decreaseCallback;

        // NEW: Callback that passes the raw integer code (for recording)
        private Action<int>? _recordingCallback;

        private readonly MouseHookMode _mode;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly int _targetCustomMessage; // Used if Mode == Custom

        /// <summary>
        /// Constructor for Standard or Recording modes
        /// </summary>
        public GlobalMouseHook(MouseHookMode mode, Action increaseCallback, Action? decreaseCallback, DispatcherQueue dispatcherQueue)
        {
            _mode = mode;
            _increaseCallback = increaseCallback;
            _decreaseCallback = decreaseCallback;
            _dispatcherQueue = dispatcherQueue;
            _proc = HookCallback;
        }

        /// <summary>
        /// Constructor specifically for Custom mode (listening for a specific message)
        /// </summary>
        public GlobalMouseHook(int targetMessage, Action increaseCallback, DispatcherQueue dispatcherQueue)
        {
            _mode = MouseHookMode.Custom;
            _targetCustomMessage = targetMessage;
            _increaseCallback = increaseCallback;
            _dispatcherQueue = dispatcherQueue;
            _proc = HookCallback;
        }

        /// <summary>
        /// Constructor specifically for Recording mode
        /// </summary>
        public GlobalMouseHook(Action<int> recordingCallback, DispatcherQueue dispatcherQueue)
        {
            _mode = MouseHookMode.Recording;
            _recordingCallback = recordingCallback;
            _dispatcherQueue = dispatcherQueue;
            _increaseCallback = () => { }; // dummy
            _proc = HookCallback;
        }

        public void SetHook()
        {
            IntPtr hModule = GetModuleHandle(null);
            _hookHandle = SetWindowsHookEx(WH_MOUSE_LL, _proc, hModule, 0);

            if (_hookHandle == IntPtr.Zero)
            {
                Debug.WriteLine($"[ERROR] Failed to set mouse hook. Mode: {_mode}");
            }
            else
            {
                Debug.WriteLine($"[INFO] Mouse Hook Set. Mode: {_mode}");
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                try
                {
                    int msg = (int)wParam;

                    // --- RECORDING MODE ---
                    if (_mode == MouseHookMode.Recording)
                    {
                        // If any button down message is detected
                        if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN ||
                            msg == WM_MBUTTONDOWN || msg == WM_XBUTTONDOWN)
                        {
                            int resultData = msg;

                            // If XButton, we need to distinguish X1 vs X2 to be specific
                            if (msg == WM_XBUTTONDOWN)
                            {
                                MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                                uint mouseData = hookStruct.mouseData;
                                ushort hiWord = (ushort)((mouseData >> 16) & 0xFFFF);
                                // We encode XButton details into the int if needed, 
                                // but for simplicity let's pass the base message + hiWord shift
                                // A simple trick: Standard messages are small. 
                                // Let's return a distinct ID for X1/X2.
                                if (hiWord == XBUTTON1) resultData = WM_XBUTTONDOWN + 1;
                                if (hiWord == XBUTTON2) resultData = WM_XBUTTONDOWN + 2;
                            }

                            _dispatcherQueue.TryEnqueue(() => _recordingCallback?.Invoke(resultData));
                        }
                    }
                    // --- CUSTOM MODE (User defined specific button) ---
                    else if (_mode == MouseHookMode.Custom)
                    {
                        bool match = false;

                        // Strict match for standard buttons
                        if (msg == _targetCustomMessage && msg != WM_XBUTTONDOWN)
                        {
                            match = true;
                        }
                        // Detailed match for XButtons
                        else if (msg == WM_XBUTTONDOWN)
                        {
                            MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                            ushort hiWord = (ushort)((hookStruct.mouseData >> 16) & 0xFFFF);

                            // Check if target matches X1 or X2 logic
                            // Target stored as WM_XBUTTONDOWN + 1 (X1) or + 2 (X2)
                            if (_targetCustomMessage == WM_XBUTTONDOWN + 1 && hiWord == XBUTTON1) match = true;
                            if (_targetCustomMessage == WM_XBUTTONDOWN + 2 && hiWord == XBUTTON2) match = true;
                        }

                        if (match)
                        {
                            _dispatcherQueue.TryEnqueue(() => _increaseCallback?.Invoke());
                        }
                    }
                    // --- STANDARD MODES ---
                    else if (_mode == MouseHookMode.XButtons && msg == WM_XBUTTONDOWN)
                    {
                        MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                        ushort hiWord = (ushort)((hookStruct.mouseData >> 16) & 0xFFFF);

                        if (hiWord == XBUTTON1) _dispatcherQueue.TryEnqueue(() => _decreaseCallback?.Invoke());
                        else if (hiWord == XBUTTON2) _dispatcherQueue.TryEnqueue(() => _increaseCallback?.Invoke());
                    }
                    else if (_mode == MouseHookMode.LButtonOnly && msg == WM_LBUTTONDOWN)
                    {
                        _dispatcherQueue.TryEnqueue(() => _increaseCallback?.Invoke());
                    }
                }
                catch (Exception)
                {
                    Debug.WriteLine($"[CRITICAL ERROR] Exception inside Mouse Hook.");
                }
            }
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }
        }
    }
}