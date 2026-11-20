namespace DesktopSprite.Models;

/// <summary>一次采样得到的系统资源快照。</summary>
public sealed class MetricSnapshot
{
    public double CpuPercent { get; init; }

    public double MemUsedGb { get; init; }
    public double MemTotalGb { get; init; }
    public double MemPercent => MemTotalGb > 0.01 ? MemUsedGb / MemTotalGb * 100.0 : 0;

    public bool GpuAvailable { get; init; }
    public double GpuPercent { get; init; }
    public double VramUsedMb { get; init; }
    public double VramTotalMb { get; init; }
    public double VramPercent => VramTotalMb > 0.01 ? VramUsedMb / VramTotalMb * 100.0 : 0;
    public double GpuTemp { get; init; }

    public double NetDownBytesPerSec { get; init; }
    public double NetUpBytesPerSec { get; init; }

    public DateTime Timestamp { get; init; } = DateTime.Now;

    public static MetricSnapshot Empty => new();
}
