using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace EmulatorHub
{
    // A connected game controller as the kernel lists it: /sys/class/input/eventN with the standard gamepad button.
    public sealed class ControllerDevice
    {
        public string Name = "", Event = "", Device = "", Bus = "", VendorProduct = "", Joystick;
        public bool Readable;
        public string BusName
        {
            get { return Bus == "0003" ? "USB" : Bus == "0005" ? "Bluetooth" : Bus == "0006" ? "Virtual" : Bus.Length == 0 ? "Unknown" : "Bus " + Bus; }
        }
    }

    // Lists controllers without opening them: names and IDs from sysfs, access checked with access(2).
    public static class ControllerInventory
    {
        [DllImport("libc", SetLastError = true, EntryPoint = "access")] private static extern int Access(string path, int mode);
        public static bool CanRead(string path) { try { return OperatingSystem.IsLinux() && Access(path, 4) == 0; } catch (Exception) { return false; } }

        public static List<ControllerDevice> Scan() { return Scan("/sys/class/input", "/dev/input", CanRead); }
        public static List<ControllerDevice> Scan(string classes, string devices, Func<string, bool> readable)
        {
            var result = new List<ControllerDevice>();
            if (!Directory.Exists(classes)) return result;
            foreach (var node in Directory.GetDirectories(classes, "event*").OrderBy(EventNumber))
            {
                var device = Path.Combine(node, "device");
                var keys = Read(Path.Combine(device, "capabilities", "key"));
                if (!EvdevInput.IsGamepad(keys)) continue;
                var item = new ControllerDevice { Event = Path.GetFileName(node), Device = Path.Combine(devices, Path.GetFileName(node)) };
                item.Name = Read(Path.Combine(device, "name")).Trim(); if (item.Name.Length == 0) item.Name = item.Event;
                item.Bus = Read(Path.Combine(device, "id", "bustype")).Trim();
                var vendor = Read(Path.Combine(device, "id", "vendor")).Trim(); var product = Read(Path.Combine(device, "id", "product")).Trim();
                item.VendorProduct = vendor.Length > 0 ? vendor + ":" + product : "";
                try { item.Joystick = Directory.Exists(device) ? Directory.GetDirectories(device, "js*").Select(Path.GetFileName).FirstOrDefault() : null; } catch (Exception) { }
                item.Readable = readable(item.Device);
                result.Add(item);
            }
            return result;
        }
        static string Read(string file) { try { return File.Exists(file) ? File.ReadAllText(file) : ""; } catch (Exception) { return ""; } }
        static int EventNumber(string node) { int number; return Int32.TryParse(Path.GetFileName(node).Substring(5), out number) ? number : Int32.MaxValue; }
    }
}
