using System.Diagnostics;
using System.Net.NetworkInformation;

namespace DesktopSprite.Services;

/// <summary>汇总所有活动物理网卡的收发字节数，换算成瞬时速率。</summary>
internal sealed class NetworkSampler
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private long _prevReceived;
    private long _prevSent;
    private TimeSpan _prevTime = TimeSpan.Zero;
    private bool _hasPrev;

    public (double DownBytesPerSec, double UpBytesPerSec) Next()
    {
        long received = 0;
        long sent = 0;

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                    continue;
                if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                    continue;
                if (!nic.Supports(NetworkInterfaceComponent.IPv4))
                    continue;

                try
                {
                    var stats = nic.GetIPv4Statistics();
                    received += stats.BytesReceived;
                    sent += stats.BytesSent;
                }
                catch
                {
                    // 某些虚拟网卡读取会抛异常，忽略即可。
                }
            }
        }
        catch
        {
            return (0, 0);
        }

        var now = _stopwatch.Elapsed;

        if (!_hasPrev)
        {
            _prevReceived = received;
            _prevSent = sent;
            _prevTime = now;
            _hasPrev = true;
            return (0, 0);
        }

        double seconds = (now - _prevTime).TotalSeconds;
        long rxDelta = received - _prevReceived;
        long txDelta = sent - _prevSent;

        _prevReceived = received;
        _prevSent = sent;
        _prevTime = now;

        if (seconds <= 0 || rxDelta < 0 || txDelta < 0)
            return (0, 0);

        return (rxDelta / seconds, txDelta / seconds);
    }
}
