using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace EmulatorHub
{
    public enum ControllerAction { Up, Down, Left, Right, Activate, Back, PreviousTab, NextTab }

    // Linux's existing joystick interface supplies eight-byte events. No service or package is installed.
    public sealed class JoystickInput
    {
        public const int DeadZone = 12000;
        private readonly Dictionary<int, bool> buttons = new Dictionary<int, bool>();
        private readonly Dictionary<int, int> axes = new Dictionary<int, int>();
        private int horizontal, vertical;
        private long nextHorizontal, nextVertical;

        public List<ControllerAction> Read(byte[] frame, long now)
        {
            var result = new List<ControllerAction>();
            if (frame == null || frame.Length != 8) return result;
            int value = unchecked((short)(frame[4] | frame[5] << 8)), kind = frame[6] & 0x7f, number = frame[7];
            bool initial = (frame[6] & 0x80) != 0;
            if (kind == 1)
            {
                bool before = buttons.TryGetValue(number, out var pressed) && pressed; buttons[number] = value != 0;
                if (!initial && value != 0 && !before)
                {
                    if (number == 0) result.Add(ControllerAction.Activate);
                    else if (number == 1) result.Add(ControllerAction.Back);
                    else if (number == 4) result.Add(ControllerAction.PreviousTab);
                    else if (number == 5) result.Add(ControllerAction.NextTab);
                }
            }
            else if (kind == 2 && (number == 0 || number == 1 || number == 6 || number == 7) && !initial)
            {
                axes[number] = value > DeadZone ? 1 : value < -DeadZone ? -1 : 0;
                int x = Axis(6) != 0 ? Axis(6) : Axis(0), y = Axis(7) != 0 ? Axis(7) : Axis(1);
                if (x != horizontal) { horizontal = x; nextHorizontal = now + 350; if (x != 0) result.Add(x < 0 ? ControllerAction.Left : ControllerAction.Right); }
                if (y != vertical) { vertical = y; nextVertical = now + 350; if (y != 0) result.Add(y < 0 ? ControllerAction.Up : ControllerAction.Down); }
            }
            return result;
        }
        private int Axis(int number) { return axes.TryGetValue(number, out var value) ? value : 0; }
        public List<ControllerAction> Repeat(long now)
        {
            var result = new List<ControllerAction>();
            if (horizontal != 0 && now >= nextHorizontal) { result.Add(horizontal < 0 ? ControllerAction.Left : ControllerAction.Right); nextHorizontal = now + 120; }
            if (vertical != 0 && now >= nextVertical) { result.Add(vertical < 0 ? ControllerAction.Up : ControllerAction.Down); nextVertical = now + 120; }
            return result;
        }
        public void Reset() { buttons.Clear(); axes.Clear(); horizontal = vertical = 0; nextHorizontal = nextVertical = 0; }
    }

    public sealed class LinuxJoystick : IDisposable
    {
        [DllImport("libc", SetLastError = true, EntryPoint = "open")] private static extern int Open(string path, int flags);
        [DllImport("libc", SetLastError = true, EntryPoint = "read")] private static extern IntPtr ReadNative(int descriptor, [Out] byte[] buffer, UIntPtr count);
        [DllImport("libc", SetLastError = true, EntryPoint = "close")] private static extern int Close(int descriptor);
        private int descriptor = -1;
        private long retryAt;
        private readonly JoystickInput input = new JoystickInput();
        public string Status { get; private set; } = "Controller navigation is off.";
        public List<ControllerAction> Poll(bool enabled, bool active, long now)
        {
            var result = new List<ControllerAction>();
            if (!OperatingSystem.IsLinux()) { Status = "Kernel controller navigation is available on Linux."; return result; }
            if (!enabled) { Disconnect(); Status = "Controller navigation is off."; return result; }
            if (descriptor < 0 && now >= retryAt)
            {
                retryAt = now + 2000; Status = "No readable joystick found. Connect a controller with /dev/input/js access.";
                try
                {
                    foreach (var file in Directory.Exists("/dev/input") ? Directory.GetFiles("/dev/input", "js*") : Array.Empty<string>())
                    {
                        int candidate = Open(file, 0x800); // O_RDONLY | O_NONBLOCK
                        if (candidate < 0) { if (Marshal.GetLastWin32Error() == 13) Status = "Controller access denied. Use your desktop's input-device permissions."; continue; }
                        descriptor = candidate; input.Reset(); Status = "Connected: " + Path.GetFileName(file) + ". A selects, B goes back, shoulders switch tabs."; break;
                    }
                }
                catch (IOException error) { Status = "Controller unavailable: " + error.Message; }
                catch (UnauthorizedAccessException) { Status = "Controller device permissions are required."; }
            }
            if (descriptor < 0) return result;
            var frame = new byte[8];
            for (int count = 0; count < 128; count++)
            {
                long read = ReadNative(descriptor, frame, (UIntPtr)8).ToInt64();
                if (read == -1 && Marshal.GetLastWin32Error() == 11) break; // EAGAIN: never block the UI thread.
                if (read != 8) { Disconnect(); Status = "Controller disconnected. Reconnect to continue."; break; }
                var actions = input.Read(frame, now); if (active) result.AddRange(actions);
            }
            if (active) result.AddRange(input.Repeat(now)); else input.Reset();
            return result;
        }
        private void Disconnect() { if (descriptor >= 0) { Close(descriptor); descriptor = -1; } input.Reset(); }
        public void Dispose() { Disconnect(); }
    }
}
