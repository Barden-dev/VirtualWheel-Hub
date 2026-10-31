using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SimRacingHub.Core;
using SimRacingHub.Linux.Native;

namespace SimRacingHub.Linux
{
    public sealed record DiscoveredDevice(string Path, string Name, bool IsMouse, bool IsKeyboard, string? Identity = null);

    public sealed record DeviceResolutionResult(
        string? MousePath,
        string? KeyboardPath,
        bool IsUnambiguous,
        bool IsDisconnected,
        IReadOnlyList<DiscoveredDevice> AvailableMice,
        IReadOnlyList<DiscoveredDevice> AvailableKeyboards);

    public static class LinuxInputDeviceResolver
    {
        public static DeviceResolutionResult ResolveDevices(AppSettings? settings = null)
            => ResolveDevices(settings, "/dev/input", ReadCapabilities);

        internal static DeviceResolutionResult ResolveDevices(AppSettings? settings, string inputDirectory,
            Func<string, (bool IsMouse, bool IsKeyboard)?> readCapabilities)
        {
            var mice = new List<DiscoveredDevice>();
            var keyboards = new List<DiscoveredDevice>();

            // 1. Scan /dev/input/by-id
            string byIdDirectory = Path.Combine(inputDirectory, "by-id");
            if (Directory.Exists(byIdDirectory))
            {
                foreach (string file in Directory.GetFiles(byIdDirectory))
                {
                    string name = Path.GetFileName(file);
                    if (name.EndsWith("-event-mouse", StringComparison.OrdinalIgnoreCase))
                    {
                        mice.Add(new DiscoveredDevice(file, DeviceName(file), IsMouse: true, IsKeyboard: false, Identity: GetIdentity(file)));
                    }
                    else if (name.EndsWith("-event-kbd", StringComparison.OrdinalIgnoreCase) && readCapabilities(file)?.IsKeyboard != false)
                    {
                        keyboards.Add(new DiscoveredDevice(file, DeviceName(file), IsMouse: false, IsKeyboard: true, Identity: GetIdentity(file)));
                    }
                }
            }

            // Built-in devices often have no by-id alias. Always scan event nodes,
            // preserving stable by-id paths and deduplicating their canonical targets.
            foreach (string path in Directory.Exists(inputDirectory)
                ? Directory.GetFiles(inputDirectory, "event*") : Array.Empty<string>())
            {
                if (!File.Exists(path)) continue;
                var capabilities = readCapabilities(path);
                if (capabilities?.IsMouse == true && !mice.Any(m => SameDevice(m.Path, path)))
                    mice.Add(new DiscoveredDevice(path, DeviceName(path), true, false, GetIdentity(path)));
                if (capabilities?.IsKeyboard == true && !keyboards.Any(k => SameDevice(k.Path, path)))
                    keyboards.Add(new DiscoveredDevice(path, DeviceName(path), false, true, GetIdentity(path)));
            }

            return ResolveFromDiscovered(mice, keyboards, settings);
        }

        public static bool HasKeyboardKeys(ReadOnlySpan<byte> keys)
        {
            // A receiver's consumer/media-key endpoint can also be named event-kbd.
            // Require ordinary letter keys used for driving, not just the filename.
            foreach (int key in new[] { 16, 17, 30, 31 }) // Q, W, A, S
                if (keys.Length <= key / 8 || (keys[key / 8] & (1 << (key % 8))) == 0) return false;
            return true;
        }

        private static (bool IsMouse, bool IsKeyboard)? ReadCapabilities(string path)
        {
            if (DeviceName(path) == UInputPointerRelay.DeviceName) return (false, false);
            int fd = LinuxNative.Open(path, LinuxNative.O_RDONLY | LinuxNative.O_NONBLOCK);
            if (fd < 0) return null; // Keep inaccessible by-id nodes for permission repair.
            try
            {
                var rel = new byte[4];
                bool mouse = LinuxNative.Ioctl(fd, LinuxNative.EVIOCGBIT_REL_4, rel) >= 0 &&
                    (rel[0] & (1 << LinuxNative.REL_X)) != 0;
                var keys = new byte[96];
                bool keyboard = LinuxNative.Ioctl(fd, LinuxNative.EVIOCGBIT_KEY_96, keys) >= 0 && HasKeyboardKeys(keys);
                return (mouse, keyboard);
            }
            finally { LinuxNative.Close(fd); }
        }

        private static string DeviceName(string path)
        {
            try
            {
                string node = File.ResolveLinkTarget(path, true)?.FullName ?? path;
                string name = "/sys/class/input/" + Path.GetFileName(node) + "/device/name";
                if (File.Exists(name) && File.ReadAllText(name).Trim() is { Length: > 0 } value) return value;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return Path.GetFileName(path);
        }

        public static string? GetIdentity(string path) => GetIdentity(path, "/sys/class/input");

        internal static string? GetIdentity(string path, string sysInputDirectory)
        {
            try
            {
                string node = File.ResolveLinkTarget(path, true)?.FullName ?? path;
                // Resolve the class alias before its relative device link. Resolving
                // only the final link can otherwise produce /sys/class/input17.
                string eventAlias = Path.Combine(sysInputDirectory, Path.GetFileName(node));
                string eventDirectory = Directory.ResolveLinkTarget(eventAlias, true)?.FullName ?? eventAlias;
                string device = Path.Combine(eventDirectory, "device");
                string physical = Directory.ResolveLinkTarget(device, true)?.FullName ?? device;
                physical = System.Text.RegularExpressions.Regex.Replace(physical, @"/input/input\d+$", "");
                // HID instance counters also change when devices are enumerated again.
                physical = System.Text.RegularExpressions.Regex.Replace(physical,
                    @"/([0-9a-fA-F]{4}:[0-9a-fA-F]{4}:[0-9a-fA-F]{4})\.[0-9a-fA-F]+(?=/|$)", "/$1");
                string Read(string name) => File.Exists(device + "/" + name) ? File.ReadAllText(device + "/" + name).Trim() : "";
                return physical + "|" + Read("name") + "|" + Read("phys") + "|" + Read("uniq");
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        private static bool SameDevice(string a, string b)
        {
            static string Canonical(string path)
            {
                try { return File.ResolveLinkTarget(path, true)?.FullName ?? path; }
                catch { return path; }
            }
            return Canonical(a) == Canonical(b);
        }

        public static DeviceResolutionResult ResolveFromDiscovered(
            IReadOnlyList<DiscoveredDevice> mice,
            IReadOnlyList<DiscoveredDevice> keyboards,
            AppSettings? settings = null,
            Func<string, bool>? fileExists = null)
        {
            var checkExists = fileExists ?? File.Exists;
            string? savedMouse = settings?.LinuxSelectedMousePath;
            string? savedKbd = settings?.LinuxSelectedKeyboardPath;

            bool disconnected = false;
            string? finalMouse = null;
            string? finalKbd = null;

            // Handle saved mouse
            if (!string.IsNullOrEmpty(savedMouse))
            {
                if (checkExists(savedMouse) && mice.Any(m => SameDevice(m.Path, savedMouse) && (string.IsNullOrEmpty(settings?.LinuxSelectedMouseIdentity) || m.Identity == settings.LinuxSelectedMouseIdentity)))
                {
                    finalMouse = savedMouse;
                }
                else
                {
                    // Saved mouse was disconnected; DO NOT silently fallback to another device!
                    disconnected = true;
                }
            }
            else if (mice.Count == 1)
            {
                finalMouse = mice[0].Path;
            }

            // Handle saved keyboard
            if (!string.IsNullOrEmpty(savedKbd))
            {
                if (checkExists(savedKbd) && keyboards.Any(k => SameDevice(k.Path, savedKbd) && (string.IsNullOrEmpty(settings?.LinuxSelectedKeyboardIdentity) || k.Identity == settings.LinuxSelectedKeyboardIdentity)))
                {
                    finalKbd = savedKbd;
                }
                else
                {
                    // Saved keyboard was disconnected; DO NOT silently fallback!
                    disconnected = true;
                }
            }
            else if (keyboards.Count == 1)
            {
                finalKbd = keyboards[0].Path;
            }

            bool isUnambiguous = finalMouse != null && finalKbd != null;

            return new DeviceResolutionResult(
                finalMouse,
                finalKbd,
                isUnambiguous,
                disconnected,
                mice,
                keyboards);
        }
    }
}
