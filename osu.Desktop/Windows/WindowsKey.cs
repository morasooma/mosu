// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.InteropServices;
using osu.Framework.Logging;

// ReSharper disable IdentifierTypo

namespace osu.Desktop.Windows
{
    internal static class WindowsKey
    {
        private delegate IntPtr LowLevelKeyboardProcDelegate(int nCode, IntPtr wParam, ref KbdLlHookStruct lParam);

        private const int wh_keyboard_ll = 13;
        private const int wm_keydown = 0x100;
        private const int wm_syskeyup = 0x105;

        // Keep the callback alive even if removing the hook fails.
        private static readonly LowLevelKeyboardProcDelegate keyboardHookDelegate = lowLevelKeyboardProc;
        private static IntPtr keyHook;
        private static bool blockingRequested;

        [StructLayout(LayoutKind.Sequential)]
        private struct KbdLlHookStruct
        {
            public uint VkCode;
            public uint ScanCode;
            public uint Flags;
            public uint Time;
            public UIntPtr ExtraInfo;
        }

        private static IntPtr lowLevelKeyboardProc(int nCode, IntPtr wParam, ref KbdLlHookStruct lParam)
        {
            if (nCode >= 0 && blockingRequested && wParam.ToInt64() >= wm_keydown && wParam.ToInt64() <= wm_syskeyup
                && (lParam.VkCode == 0x5B || lParam.VkCode == 0x5C))
            {
                // Focus notifications arrive asynchronously. Never swallow Win for another app
                // while waiting for the game/update thread to process a focus change.
                getWindowThreadProcessId(getForegroundWindow(), out uint processId);
                if (processId == (uint)Environment.ProcessId)
                    return new IntPtr(1);
            }

            return callNextHookEx(IntPtr.Zero, nCode, wParam, ref lParam);
        }

        internal static void Disable()
        {
            blockingRequested = true;
            if (keyHook != IntPtr.Zero)
                return;

            keyHook = setWindowsHookEx(wh_keyboard_ll, keyboardHookDelegate, getModuleHandle(null), 0);
            if (keyHook == IntPtr.Zero)
            {
                blockingRequested = false;
                Logger.Log($"Could not block the Windows key (Win32 error {Marshal.GetLastWin32Error()}).", LoggingTarget.Runtime, LogLevel.Important);
            }
        }

        internal static void Enable()
        {
            blockingRequested = false;
            if (keyHook == IntPtr.Zero)
                return;

            if (unhookWindowsHookEx(keyHook))
                keyHook = IntPtr.Zero;
            else
                Logger.Log($"Could not remove the Windows key hook (Win32 error {Marshal.GetLastWin32Error()}).", LoggingTarget.Runtime, LogLevel.Important);
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
        private static extern IntPtr setWindowsHookEx(int idHook, LowLevelKeyboardProcDelegate lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", EntryPoint = "UnhookWindowsHookEx", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool unhookWindowsHookEx(IntPtr hHook);

        [DllImport("user32.dll", EntryPoint = "CallNextHookEx")]
        private static extern IntPtr callNextHookEx(IntPtr hHook, int nCode, IntPtr wParam, ref KbdLlHookStruct lParam);

        [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
        private static extern IntPtr getModuleHandle(string? moduleName);

        [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")]
        private static extern IntPtr getForegroundWindow();

        [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId")]
        private static extern uint getWindowThreadProcessId(IntPtr window, out uint processId);
    }
}
