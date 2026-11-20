# 桌面精灵 DesktopSprite

一个悬浮在桌面上的霓虹 HUD 小组件，实时显示本机资源占用：CPU、GPU、显存、内存、网络上下行。

## 功能

- **收起态**：只有中央圆环，图标大小，拖到哪里都方便；每 ~2.8 秒轮播 CPU / GPU / 显存 / 内存 / 网络。
- **展开态**：点击中央圆环，四角的 GPU / 显存 / 内存 / 网络圆环从中央展开；再点击收起。
- **霓虹仪表**：外圈刻度（中央）+ 渐变进度弧 + 发光端点，颜色随占用从绿→黄→红。
- **拖动**：按住拖动，松开保存位置。
- **滚轮缩放**：0.65x ~ 1.8x。
- **右键菜单**：始终置顶、点击穿透、锁定位置、开机启动、重置位置、退出。
- **系统托盘**：显示/隐藏、全部开关、退出（开启"点击穿透"后请用托盘菜单关闭）。
- **配置持久化**：位置、大小、开关等保存在 `%APPDATA%\\DesktopSprite\\settings.json`。

## 使用

直接双击 `dist\\DesktopSprite.exe`（需要 .NET 8 桌面运行时）或 `dist-standalone\\DesktopSprite.exe`（自带运行时，免安装）。

| 操作 | 效果 |
|---|---|
| 左键单击 | 展开 / 收起四角仪表 |
| 按住左键拖动 | 移动位置 |
| 鼠标滚轮 | 缩放大小 |
| 右键 | 打开菜单 |
| 托盘双击 | 从托盘恢复窗口 |
| 托盘右键 | 全部开关 + 退出 |

## 数据来源

| 指标 | 实现 |
|---|---|
| CPU | `GetSystemTimes` 两次采样差值（几乎零开销） |
| 内存 | `GlobalMemoryStatusEx` |
| GPU / 显存 / 温度 | NVML (`nvml.dll`) 直读，失败自动回退 `nvidia-smi` |
| 网络 | 汇总活动网卡的 IPv4 收发字�节数，按采样间隔换算 |

所有数值做指数滑动平均，避免数字乱跳。采样间隔默认 1 秒。

## 构建

需要 .NET 8 SDK（本机已安装在 `D:\\opencode_work\\.dotnet`）。

```powershell
$dotnet = 'D:\\opencode_work\\.dotnet\\dotnet.exe'

# 调试运行
& $dotnet run --project D:\\opencode_work\\DesktopSprite

# 发布：体积小，依赖已安装的 .NET 8 桌面运行时
& $dotnet publish D:\\opencode_work\\DesktopSprite -c Release -r win-x64 `
    --self-contained false -p:PublishSingleFile=true -o D:\\opencode_work\\dist

# 发布：独立单文件，可拷贝到其它 Windows 电脑直接跑
& $dotnet publish D:\\opencode_work\\DesktopSprite -c Release -r win-x64 `
    --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true -o D:\\opencode_work\\dist-standalone
```

## 项目结构

```
DesktopSprite/
  App.xaml(.cs)              启动、单实例、全局异常日志
  MainWindow.xaml(.cs)       透明置顶窗口、拖动/缩放/菜单、DPI 定位
  Views/HudView.xaml(.cs)    霓虹 HUD：中央 + 四角圆环、展开/收起动画、轮播
  Views/RingGauge.xaml(.cs)  环形仪表盘（刻度 + 渐变弧 + 发光端点）
  Services/CpuSampler.cs     GetSystemTimes
  Services/MemorySampler.cs  GlobalMemoryStatusEx
  Services/GpuSampler.cs     NVML + nvidia-smi 回退
  Services/NetworkSampler.cs 网卡流量差分
  Services/MetricsService.cs 定时采样、平滑、历史曲线
  Services/TrayService.cs    托盘图标与菜单
  Services/SettingsService.cs 配置读写、开机自启
  Themes/Styles.xaml         配色与控件样式
  Assets/app.ico             程序图标
```

## 关于

- **产品名称**：桌面精灵 DesktopSprite
- **版本**：V1.0
- **作者**：QuinnZ
- **AI 工具**：opencode
- **制作地点**：中国，上海
- **版权**：Copyright (c) 2026 QuinnZ

右键菜单 → 「关于」可查看版本信息。

- 只采集独显（NVIDIA）。Intel 核显不单独显示，独显被禁用时 GPU 显示 `N/A`。
- 透明窗口依赖 WPF 软件渲染，所有动画限制在 24fps，实测常驻 CPU < 0.2%（整机）。
- 显存显示 `0.0/8.0G` 是驱动在显卡省电状态下的真实上报值。
