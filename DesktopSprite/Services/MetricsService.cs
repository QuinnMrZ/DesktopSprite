using DesktopSprite.Models;

namespace DesktopSprite.Services;

public sealed class MetricsEventArgs : EventArgs
{
    public required MetricSnapshot Snapshot { get; init; }
    public required double[] CpuHistory { get; init; }
    public required double[] GpuHistory { get; init; }
    public required double[] NetDownHistory { get; init; }
    public required double[] NetUpHistory { get; init; }
}

/// <summary>定时采样、平滑数值、维护历史曲线，并向界面推送。</summary>
public sealed class MetricsService : IDisposable
{
    private const int HistoryLength = 48;

    private readonly GpuSampler _gpu = new();
    private readonly NetworkSampler _net = new();
    private readonly System.Threading.Timer _timer;

    private readonly double[] _cpuHistory = new double[HistoryLength];
    private readonly double[] _gpuHistory = new double[HistoryLength];
    private readonly double[] _netDownHistory = new double[HistoryLength];
    private readonly double[] _netUpHistory = new double[HistoryLength];

    private double _cpuSmooth;
    private double _gpuSmooth;
    private double _downSmooth;
    private double _upSmooth;
    private double _memPercentSmooth;

    private int _busy;
    private bool _disposed;

    public event Action<MetricsEventArgs>? Updated;

    public MetricSnapshot Latest { get; private set; } = MetricSnapshot.Empty;

    public MetricsService(int intervalMs)
    {
        intervalMs = Math.Clamp(intervalMs, 500, 5000);

        // 预热 CPU / 网络计数器，第一次 tick 就有有效差值。
        CpuSampler.Next();
        _net.Next();

        _timer = new System.Threading.Timer(Tick, null, intervalMs, intervalMs);
    }

    private void Tick(object? state)
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1)
            return;

        try
        {
            double cpuRaw = CpuSampler.Next();
            _cpuSmooth = Ema(_cpuSmooth, cpuRaw, 0.45);

            var (memUsed, memTotal) = MemorySampler.Next();
            double memPercent = memTotal > 0.01 ? memUsed / memTotal * 100.0 : 0;
            if (_memPercentSmooth == 0)
                _memPercentSmooth = memPercent;
            else
                _memPercentSmooth = Ema(_memPercentSmooth, memPercent, 0.5);

            var gpuSample = _gpu.Next();
            bool gpuAvailable = gpuSample != null;
            if (gpuSample != null)
                _gpuSmooth = Ema(_gpuSmooth, gpuSample.Utilization, 0.45);

            var (down, up) = _net.Next();
            _downSmooth = Ema(_downSmooth, down, 0.55);
            _upSmooth = Ema(_upSmooth, up, 0.55);

            Push(_cpuHistory, _cpuSmooth);
            Push(_gpuHistory, _gpuSmooth);
            Push(_netDownHistory, _downSmooth);
            Push(_netUpHistory, _upSmooth);

            var snapshot = new MetricSnapshot
            {
                CpuPercent = Math.Round(_cpuSmooth, 0),
                MemUsedGb = memUsed,
                MemTotalGb = memTotal,
                GpuAvailable = gpuAvailable,
                GpuPercent = gpuAvailable ? Math.Round(_gpuSmooth, 0) : 0,
                VramUsedMb = gpuSample?.MemUsedMb ?? 0,
                VramTotalMb = gpuSample?.MemTotalMb ?? 0,
                GpuTemp = gpuSample?.Temperature ?? 0,
                NetDownBytesPerSec = _downSmooth,
                NetUpBytesPerSec = _upSmooth,
                Timestamp = DateTime.Now
            };

            Latest = snapshot;

            Updated?.Invoke(new MetricsEventArgs
            {
                Snapshot = snapshot,
                CpuHistory = (double[])_cpuHistory.Clone(),
                GpuHistory = (double[])_gpuHistory.Clone(),
                NetDownHistory = (double[])_netDownHistory.Clone(),
                NetUpHistory = (double[])_netUpHistory.Clone()
            });
        }
        catch
        {
            // 采样异常不能拖垮 UI 进程。
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    private static double Ema(double previous, double next, double alpha)
        => previous + (next - previous) * alpha;

    private static void Push(double[] buffer, double value)
    {
        Array.Copy(buffer, 1, buffer, 0, buffer.Length - 1);
        buffer[^1] = value;
    }

    public void Stop()
    {
        if (_disposed)
            return;
        _disposed = true;
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        _timer.Dispose();
    }

    public void Dispose() => Stop();
}
