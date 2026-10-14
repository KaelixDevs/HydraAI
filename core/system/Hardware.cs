using System.Runtime.InteropServices;

namespace Hydra.Core.SystemInfo;

public record HardwareInfo(string OS, string Architecture, int LogicalCores, ulong RamBytes, string Profile, string Guidance);
public static class Hardware
{
    public static HardwareInfo Detect()
    {
        var memory = new Memory { Length = (uint)Marshal.SizeOf<Memory>() };
        var ram = OperatingSystem.IsWindows() && GlobalMemoryStatusEx(ref memory) ? memory.TotalPhysical : 0;
        if (OperatingSystem.IsLinux() && File.Exists("/proc/meminfo"))
        {
            var total = File.ReadLines("/proc/meminfo").FirstOrDefault(l => l.StartsWith("MemTotal:", StringComparison.Ordinal));
            if (total is not null && ulong.TryParse(total.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1], out var kib)) ram = kib * 1024;
        }
        var gib = ram / (1024UL * 1024 * 1024);
        var profile = gib switch { < 8 => "Low", < 24 => "Balanced", < 48 => "High", _ => "Ultra" };
        return new(Environment.OSVersion.VersionString, RuntimeInformation.OSArchitecture.ToString(), Environment.ProcessorCount, ram, profile,
            "Capacity estimate only. CPU inference is the default. GPU/VRAM detection is not implemented; use a vendor-compatible llama.cpp runtime in future GPU-enabled releases.");
    }
    [StructLayout(LayoutKind.Sequential)] private struct Memory
    {
        public uint Length, Load;
        public ulong TotalPhysical, AvailablePhysical, TotalPage, AvailablePage, TotalVirtual, AvailableVirtual, AvailableExtended;
    }
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref Memory status);
}
