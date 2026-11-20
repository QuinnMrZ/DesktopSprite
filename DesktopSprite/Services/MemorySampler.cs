using System.Runtime.InteropServices;

namespace DesktopSprite.Services;

/// <summary>通过 GlobalMemoryStatusEx 读取物理内存使用情况。</summary>
internal static class MemorySampler
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    /// <summary>返回（已用 GB，总量 GB）。</summary>
    public static (double UsedGb, double TotalGb) Next()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status) || status.TotalPhys == 0)
            return (0, 0);

        const double gb = 1024.0 * 1024.0 * 1024.0;
        double total = status.TotalPhys / gb;
        double used = (status.TotalPhys - status.AvailPhys) / gb;
        return (Math.Round(used, 1), Math.Round(total, 1));
    }
}
