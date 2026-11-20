using System.Runtime.InteropServices;

namespace DesktopSprite.Services;

/// <summary>基于 GetSystemTimes 的 CPU 总占用采样（两次差值，开销极低）。</summary>
internal static class CpuSampler
{
    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    private static ulong _prevIdle;
    private static ulong _prevKernel;
    private static ulong _prevUser;
    private static bool _hasPrev;

    private static ulong ToUInt64(FileTime ft) => ((ulong)ft.High << 32) | ft.Low;

    public static double Next()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
            return 0;

        ulong i = ToUInt64(idle);
        ulong k = ToUInt64(kernel);
        ulong u = ToUInt64(user);

        if (!_hasPrev)
        {
            _prevIdle = i;
            _prevKernel = k;
            _prevUser = u;
            _hasPrev = true;
            return 0;
        }

        ulong idleDelta = i - _prevIdle;
        ulong totalDelta = (k - _prevKernel) + (u - _prevUser);

        _prevIdle = i;
        _prevKernel = k;
        _prevUser = u;

        if (totalDelta == 0)
            return 0;

        double busy = (double)(totalDelta - idleDelta) / totalDelta * 100.0;
        return Math.Clamp(busy, 0, 100);
    }
}
