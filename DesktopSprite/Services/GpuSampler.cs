using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace DesktopSprite.Services;

internal sealed class GpuSample
{
    public double Utilization { get; init; }
    public double MemUsedMb { get; init; }
    public double MemTotalMb { get; init; }
    public double Temperature { get; init; }
}

/// <summary>
/// NVIDIA GPU 采样。优先使用 NVML（随驱动安装，零进程开销），
/// 初始化失败时回退到 nvidia-smi 命令行。
/// </summary>
internal sealed class GpuSampler
{
    private const string NvmlDll = "nvml.dll";

    [StructLayout(LayoutKind.Sequential)]
    private struct NvmlUtilization
    {
        public uint Gpu;
        public uint Memory;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NvmlMemory
    {
        public ulong Total;
        public ulong Free;
        public ulong Used;
    }

    [DllImport(NvmlDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlInit_v2();

    [DllImport(NvmlDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlShutdown();

    [DllImport(NvmlDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetCount_v2(out uint count);

    [DllImport(NvmlDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out IntPtr device);

    [DllImport(NvmlDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetUtilizationRates(IntPtr device, out NvmlUtilization utilization);

    [DllImport(NvmlDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetMemoryInfo(IntPtr device, out NvmlMemory memory);

    [DllImport(NvmlDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetTemperature(IntPtr device, int sensorType, out uint temperature);

    private const int NvmlSuccess = 0;
    private const int NvmlTemperatureGpu = 0;

    private static readonly object Sync = new();
    private static bool _initTried;
    private static bool _nvmlOk;
    private static IntPtr _device = IntPtr.Zero;

    private DateTime _lastSmiQuery = DateTime.MinValue;
    private GpuSample? _lastSmiSample;

    public bool Available { get; private set; }

    public GpuSample? Next()
    {
        EnsureInit();

        if (_nvmlOk)
        {
            var sample = ReadNvml();
            if (sample != null)
            {
                Available = true;
                return sample;
            }
        }

        var fallback = ReadSmi();
        Available = fallback != null;
        return fallback;
    }

    private static void EnsureInit()
    {
        lock (Sync)
        {
            if (_initTried)
                return;

            _initTried = true;
            try
            {
                if (nvmlInit_v2() != NvmlSuccess)
                    return;
                if (nvmlDeviceGetCount_v2(out uint count) != NvmlSuccess || count == 0)
                    return;
                if (nvmlDeviceGetHandleByIndex_v2(0, out var handle) != NvmlSuccess || handle == IntPtr.Zero)
                    return;
                _device = handle;
                _nvmlOk = true;
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            {
                _nvmlOk = false;
            }
        }
    }

    private static GpuSample? ReadNvml()
    {
        try
        {
            if (nvmlDeviceGetUtilizationRates(_device, out var util) != NvmlSuccess)
                return null;

            double memUsed = 0;
            double memTotal = 0;
            if (nvmlDeviceGetMemoryInfo(_device, out var mem) == NvmlSuccess)
            {
                memUsed = mem.Used / 1024.0 / 1024.0;
                memTotal = mem.Total / 1024.0 / 1024.0;
            }

            double temp = 0;
            if (nvmlDeviceGetTemperature(_device, NvmlTemperatureGpu, out uint t) == NvmlSuccess)
                temp = t;

            return new GpuSample
            {
                Utilization = util.Gpu,
                MemUsedMb = Math.Round(memUsed),
                MemTotalMb = Math.Round(memTotal),
                Temperature = temp
            };
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            lock (Sync)
            {
                _nvmlOk = false;
            }
            return null;
        }
    }

    private GpuSample? ReadSmi()
    {
        // 进程启动约几十毫秒，1.5 秒内复用上次结果，避免频繁拉起进程。
        if ((DateTime.UtcNow - _lastSmiQuery).TotalMilliseconds < 1500)
            return _lastSmiSample;

        _lastSmiQuery = DateTime.UtcNow;
        _lastSmiSample = null;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=utilization.gpu,memory.used,memory.total,temperature.gpu --format=csv,noheader,nounits",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return null;

            string output = process.StandardOutput.ReadToEnd().Trim();
            if (!process.WaitForExit(1200) || process.ExitCode != 0 || output.Length == 0)
                return null;

            string line = output.Split('\n')[0];
            string[] parts = line.Split(',');
            if (parts.Length < 4)
                return null;

            static double Parse(string s)
                => double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0;

            _lastSmiSample = new GpuSample
            {
                Utilization = Parse(parts[0]),
                MemUsedMb = Parse(parts[1]),
                MemTotalMb = Parse(parts[2]),
                Temperature = Parse(parts[3])
            };
            return _lastSmiSample;
        }
        catch
        {
            return null;
        }
    }

    public static void Shutdown()
    {
        lock (Sync)
        {
            if (_nvmlOk)
            {
                try { nvmlShutdown(); } catch { /* ignore */ }
                _nvmlOk = false;
            }
        }
    }
}
