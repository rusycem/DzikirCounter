// FILENAME: GlobalKeyboardHook.cs
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;

namespace DzikirCounter
{
    public class GlobalKeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private LowLevelKeyboardProc _proc;
        private IntPtr _hookHandle = IntPtr.Zero;
        private DispatcherQueue _dispatcherQueue;
        private Action<int> _callback;
        private int _specificKeyCode = -1; // -1 means listen to all (Recording mode)

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        /// <summary>
        /// Initializes the hook.
        /// </summary>
        /// <param name="callback">Action to run. Receives KeyCode.</param>
        /// <param name="targetKeyCode">If set > 0, hook only fires for this key. If -1, fires for all (Recording).</param>
        public GlobalKeyboardHook(Action<int> callback, DispatcherQueue dispatcherQueue, int targetKeyCode = -1)
        {
            _callback = callback;
            _dispatcherQueue = dispatcherQueue;
            _specificKeyCode = targetKeyCode;
            _proc = HookCallback;
        }

        public void SetHook()
        {
            IntPtr hModule = GetModuleHandle(null);
            _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, hModule, 0);
            Debug.WriteLine($"[INFO] Keyboard Hook set. Target: {(_specificKeyCode == -1 ? "ALL" : _specificKeyCode.ToString())}");
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);

                // If we are targeting a specific key, check it. If -1, allow all (Recording mode).
                if (_specificKeyCode == -1 || vkCode == _specificKeyCode)
                {
                    _dispatcherQueue.TryEnqueue(() => _callback?.Invoke(vkCode));
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