// FILENAME: GlobalMouseHook.cs
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Input;
using Microsoft.UI.Dispatching;
using System.Diagnostics;

namespace DzikirCounter
{
    public enum MouseHookMode { XButtons, LButtonOnly, Custom, Recording }

    public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    public class GlobalMouseHook : IDisposable
    {
        private const int WH_MOUSE_LL = 14;
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

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        private IntPtr _hookHandle = IntPtr.Zero;
        private LowLevelMouseProc _proc;

        private Action _increaseCallback;
        private Action? _decreaseCallback;
        private Action<int>? _recordingCallback;

        private readonly MouseHookMode _mode;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly int _targetCustomMessage;

        public GlobalMouseHook(MouseHookMode mode, Action increaseCallback, Action? decreaseCallback, DispatcherQueue dispatcherQueue)
        {
            _mode = mode;
            _increaseCallback = increaseCallback;
            _decreaseCallback = decreaseCallback;
            _dispatcherQueue = dispatcherQueue;
            _proc = HookCallback;
        }

        public GlobalMouseHook(int targetMessage, Action increaseCallback, DispatcherQueue dispatcherQueue)
        {
            _mode = MouseHookMode.Custom;
            _targetCustomMessage = targetMessage;
            _increaseCallback = increaseCallback;
            _dispatcherQueue = dispatcherQueue;
            _proc = HookCallback;
        }

        public GlobalMouseHook(Action<int> recordingCallback, DispatcherQueue dispatcherQueue)
        {
            _mode = MouseHookMode.Recording;
            _recordingCallback = recordingCallback;
            _dispatcherQueue = dispatcherQueue;
            _increaseCallback = () => { };
            _proc = HookCallback;
        }

        public void SetHook()
        {
            IntPtr hModule = GetModuleHandle(null);
            _hookHandle = SetWindowsHookEx(WH_MOUSE_LL, _proc, hModule, 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                try
                {
                    int msg = (int)wParam;

                    if (_mode == MouseHookMode.Recording)
                    {
                        if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN || msg == WM_MBUTTONDOWN || msg == WM_XBUTTONDOWN)
                        {
                            int resultData = msg;
                            if (msg == WM_XBUTTONDOWN)
                            {
                                MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                                ushort hiWord = (ushort)((hookStruct.mouseData >> 16) & 0xFFFF);
                                if (hiWord == XBUTTON1) resultData = WM_XBUTTONDOWN + 1;
                                if (hiWord == XBUTTON2) resultData = WM_XBUTTONDOWN + 2;
                            }
                            _dispatcherQueue.TryEnqueue(() => _recordingCallback?.Invoke(resultData));
                        }
                    }
                    else if (_mode == MouseHookMode.Custom)
                    {
                        bool match = false;
                        if (msg == _targetCustomMessage && msg != WM_XBUTTONDOWN) match = true;
                        else if (msg == WM_XBUTTONDOWN)
                        {
                            MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                            ushort hiWord = (ushort)((hookStruct.mouseData >> 16) & 0xFFFF);
                            if (_targetCustomMessage == WM_XBUTTONDOWN + 1 && hiWord == XBUTTON1) match = true;
                            if (_targetCustomMessage == WM_XBUTTONDOWN + 2 && hiWord == XBUTTON2) match = true;
                        }

                        if (match) _dispatcherQueue.TryEnqueue(() => _increaseCallback?.Invoke());
                    }
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
                catch (Exception) { }
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