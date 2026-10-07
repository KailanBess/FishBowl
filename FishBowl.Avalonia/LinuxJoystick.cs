using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace EmulatorHub
{
    public enum ControllerAction { Up, Down, Left, Right, Activate, Back, PreviousTab, NextTab, Details, Favorite, Menu }

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

    // Linux evdev gamepad events (struct input_event: type, code, value). Uses the kernel's standard gamepad codes,
    // so every controller with a gamepad driver (Xbox, PlayStation, Switch, 8BitDo...) maps the same way.
    public sealed class EvdevInput
    {
        public const int KeyEvent = 1, AbsoluteEvent = 3;
        public const int South = 0x130, East = 0x131, North = 0x133, West = 0x134, LeftShoulder = 0x136, RightShoulder = 0x137, Select = 0x13a, Start = 0x13b;
        public const int DpadUp = 0x220, DpadDown = 0x221, DpadLeft = 0x222, DpadRight = 0x223;
        public const int StickX = 0, StickY = 1, HatX = 0x10, HatY = 0x11;
        private readonly Dictionary<int, int[]> ranges = new Dictionary<int, int[]>();
        private readonly Dictionary<int, int> axes = new Dictionary<int, int>();
        private readonly HashSet<int> pads = new HashSet<int>();
        private int horizontal, vertical;
        private long nextHorizontal, nextVertical;

        // The device's reported minimum and maximum for an axis; sticks default to the signed 16-bit range.
        public void Range(int axis, int minimum, int maximum) { if (maximum > minimum) ranges[axis] = new[] { minimum, maximum }; }

        public List<ControllerAction> Read(int type, int code, int value, long now)
        {
            var result = new List<ControllerAction>();
            if (type == KeyEvent)
            {
                if (code >= DpadUp && code <= DpadRight)
                {
                    if (value != 0) pads.Add(code); else pads.Remove(code);
                    Steer(result, now); return result;
                }
                if (value != 1) return result; // 1 = press; 0 = release and 2 = autorepeat never activate.
                if (code == South) result.Add(ControllerAction.Activate);
                else if (code == East) result.Add(ControllerAction.Back);
                else if (code == North) result.Add(ControllerAction.Details);
                else if (code == West) result.Add(ControllerAction.Favorite);
                else if (code == LeftShoulder) result.Add(ControllerAction.PreviousTab);
                else if (code == RightShoulder) result.Add(ControllerAction.NextTab);
                else if (code == Start) result.Add(ControllerAction.Menu);
            }
            else if (type == AbsoluteEvent && (code == StickX || code == StickY || code == HatX || code == HatY))
            {
                int direction;
                if (code == HatX || code == HatY) direction = Math.Sign(value);
                else
                {
                    int[] range; if (!ranges.TryGetValue(code, out range)) range = new[] { -32768, 32767 };
                    double centre = (range[0] + range[1]) / 2.0, half = (range[1] - range[0]) / 2.0;
                    double position = half <= 0 ? 0 : (value - centre) / half;
                    direction = position > 0.45 ? 1 : position < -0.45 ? -1 : 0;
                }
                axes[code] = direction; Steer(result, now);
            }
            return result;
        }
        private int Axis(int code) { int value; return axes.TryGetValue(code, out value) ? value : 0; }
        private void Steer(List<ControllerAction> result, long now)
        {
            int x = pads.Contains(DpadLeft) ? -1 : pads.Contains(DpadRight) ? 1 : Axis(HatX) != 0 ? Axis(HatX) : Axis(StickX);
            int y = pads.Contains(DpadUp) ? -1 : pads.Contains(DpadDown) ? 1 : Axis(HatY) != 0 ? Axis(HatY) : Axis(StickY);
            if (x != horizontal) { horizontal = x; nextHorizontal = now + 350; if (x != 0) result.Add(x < 0 ? ControllerAction.Left : ControllerAction.Right); }
            if (y != vertical) { vertical = y; nextVertical = now + 350; if (y != 0) result.Add(y < 0 ? ControllerAction.Up : ControllerAction.Down); }
        }
        public List<ControllerAction> Repeat(long now)
        {
            var result = new List<ControllerAction>();
            if (horizontal != 0 && now >= nextHorizontal) { result.Add(horizontal < 0 ? ControllerAction.Left : ControllerAction.Right); nextHorizontal = now + 120; }
            if (vertical != 0 && now >= nextVertical) { result.Add(vertical < 0 ? ControllerAction.Up : ControllerAction.Down); nextVertical = now + 120; }
            return result;
        }
        public void Reset() { axes.Clear(); pads.Clear(); horizontal = vertical = 0; nextHorizontal = nextVertical = 0; }

        // True when a /sys/class/input/eventN/device/capabilities/key bitmap reports the standard gamepad button.
        public static bool IsGamepad(string keyCapabilities)
        {
            var words = (keyCapabilities ?? "").Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int index = words.Length - 1 - South / 64;
            ulong word;
            return index >= 0 && UInt64.TryParse(words[index], System.Globalization.NumberStyles.HexNumber, null, out word) && (word >> (South % 64) & 1) == 1;
        }
    }

    // Reads the first connected gamepad without blocking the UI thread: evdev (/dev/input/event*, standard gamepad
    // codes) first, then the older joystick interface (/dev/input/js*). No service, package or permission is changed;
    // desktops grant the signed-in user access to game controllers.
    public sealed class LinuxJoystick : IDisposable
    {
        [DllImport("libc", SetLastError = true, EntryPoint = "open")] private static extern int Open(string path, int flags);
        [DllImport("libc", SetLastError = true, EntryPoint = "read")] private static extern IntPtr ReadNative(int descriptor, [Out] byte[] buffer, UIntPtr count);
        [DllImport("libc", SetLastError = true, EntryPoint = "close")] private static extern int Close(int descriptor);
        [DllImport("libc", SetLastError = true, EntryPoint = "ioctl")] private static extern int Control(int descriptor, UIntPtr request, [Out] byte[] buffer);
        private const int ReadOnlyNonBlocking = 0x800, EventSize = 24, AccessDenied = 13, TryAgain = 11;
        private int descriptor = -1;
        private bool evdev;
        private long retryAt;
        private readonly JoystickInput input = new JoystickInput();
        private readonly EvdevInput events = new EvdevInput();
        public string Status { get; private set; } = "Controller navigation is off.";
        public string DeviceName { get; private set; }
        public bool Connected { get { return descriptor >= 0; } }

        public List<ControllerAction> Poll(bool enabled, bool active, long now)
        {
            var result = new List<ControllerAction>();
            if (!OperatingSystem.IsLinux()) { Status = "Kernel controller navigation is available on Linux."; return result; }
            if (!enabled) { Disconnect(); Status = "Controller navigation is off."; return result; }
            if (descriptor < 0 && now >= retryAt) { retryAt = now + 2000; Connect(); }
            if (descriptor < 0) return result;
            var frame = new byte[evdev ? EventSize : 8];
            for (int count = 0; count < 256; count++)
            {
                long read = ReadNative(descriptor, frame, (UIntPtr)(uint)frame.Length).ToInt64();
                if (read == -1 && Marshal.GetLastWin32Error() == TryAgain) break; // Never block the UI thread.
                if (read != frame.Length) { Disconnect(); Status = "Controller disconnected. Reconnect to continue."; break; }
                var actions = evdev ? events.Read(BitConverter.ToUInt16(frame, 16), BitConverter.ToUInt16(frame, 18), BitConverter.ToInt32(frame, 20), now) : input.Read(frame, now);
                if (active) result.AddRange(actions);
            }
            if (active) result.AddRange(evdev ? events.Repeat(now) : input.Repeat(now)); else { input.Reset(); events.Reset(); }
            return result;
        }

        private void Connect()
        {
            Status = "No controller found. Connect a controller; FishBowl checks again every two seconds.";
            bool denied = false;
            try
            {
                const string classes = "/sys/class/input";
                foreach (var node in Directory.Exists(classes) ? Directory.GetDirectories(classes, "event*").OrderBy(EventNumber) : Enumerable.Empty<string>())
                {
                    string capabilities = Path.Combine(node, "device", "capabilities", "key");
                    if (!File.Exists(capabilities) || !EvdevInput.IsGamepad(File.ReadAllText(capabilities))) continue;
                    int candidate = Open("/dev/input/" + Path.GetFileName(node), ReadOnlyNonBlocking);
                    if (candidate < 0) { denied |= Marshal.GetLastWin32Error() == AccessDenied; continue; }
                    descriptor = candidate; evdev = true; events.Reset();
                    foreach (int axis in new[] { EvdevInput.StickX, EvdevInput.StickY })
                    {
                        // EVIOCGABS(axis): struct input_absinfo { value, minimum, maximum, fuzz, flat, resolution }.
                        var info = new byte[24];
                        if (Control(descriptor, (UIntPtr)(0x80184540u + (uint)axis), info) == 0) events.Range(axis, BitConverter.ToInt32(info, 4), BitConverter.ToInt32(info, 8));
                    }
                    string nameFile = Path.Combine(node, "device", "name");
                    DeviceName = File.Exists(nameFile) ? File.ReadAllText(nameFile).Trim() : Path.GetFileName(node);
                    Status = "Connected: " + DeviceName + ". A selects, B goes back, shoulders switch tabs, Start opens the menu.";
                    return;
                }
                foreach (var file in Directory.Exists("/dev/input") ? Directory.GetFiles("/dev/input", "js*") : Array.Empty<string>())
                {
                    int candidate = Open(file, ReadOnlyNonBlocking);
                    if (candidate < 0) { denied |= Marshal.GetLastWin32Error() == AccessDenied; continue; }
                    descriptor = candidate; evdev = false; input.Reset(); DeviceName = Path.GetFileName(file);
                    Status = "Connected: " + DeviceName + ". A selects, B goes back, shoulders switch tabs."; return;
                }
                if (denied) Status = "Controller access denied. Use your desktop's input-device permissions.";
            }
            catch (IOException error) { Status = "Controller unavailable: " + error.Message; }
            catch (UnauthorizedAccessException) { Status = "Controller device permissions are required."; }
        }
        private static int EventNumber(string node) { int number; return Int32.TryParse(Path.GetFileName(node).Substring(5), out number) ? number : Int32.MaxValue; }
        private void Disconnect() { if (descriptor >= 0) { Close(descriptor); descriptor = -1; } DeviceName = null; input.Reset(); events.Reset(); }
        public void Dispose() { Disconnect(); }
    }
}
