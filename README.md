# HDR 转 SDR 工具

把 HDR（HDR10/PQ 或 HLG）视频转成 SDR，输出可直接导入剪映且颜色正常。

## 使用

双击 **HDR转SDR.exe**，把视频拖进窗口（或点「添加文件 / 添加文件夹」），设置好质量后点「开始转换」。

- 输出默认在源文件旁边：`<原名>_SDR.mp4`
- 转换用 libplacebo 的 BT.2390 色调映射，高光有层次、不抬黑位
- 自动识别 PQ / HLG，非 HDR 默认跳过（可勾「强制非 HDR」）
- 转换后自动把容器里残留的 mdcv / clli 母版元数据清掉，避免剪映又把 SDR 认成 HDR

## 命令行（可选，备用/批处理）

```
HDR转SDR.exe --cli <视频> [-o 输出目录] [--crf 18] [--preset medium] [--nvenc] [--keepmeta] [--force] [--overwrite]
```

退出码：0=成功/跳过，1=失败，2=参数/ffmpeg 错误，3=输出有疑点。

## 依赖

需要 ffmpeg（含 libplacebo）。程序会自动查找：

1. 与 exe 同目录的 `ffmpeg.exe`
2. exe 同目录的 `bin\` 或 `ffmpeg\bin\`
3. 系统 PATH
4. winget 安装的 ffmpeg（`GameLoop`/`Gyan.FFmpeg`，本机默认走这条）
5. 常见安装位置（`C:\ffmpeg\bin\` 等）

找不到时可以点界面上的「指定 ffmpeg…」手动选择（要 ffmpeg.exe 和 ffprobe.exe 同目录），会记进 `hdr2sdr.ini`。

没装的话：`winget install --id Gyan.FFmpeg -e`

## 参数说明（界面）

| 项 | 说明 |
|----|------|
| 质量 CRF | 10–32，越小越清晰越占空间，默认 18。原来脚本用 16，我没动太多，默认 18 比较平衡 |
| 速度 | x264 档位 ultrafast…slow，慢的压缩率更好 |
| 编码器 | x264（CPU，兼容最好）/ NVENC（N 卡硬件加速，快但本机混合显卡可能初始化失败需回退 x264） |
| 清除 HDR 元数据 | 默认勾选，转完后删 mdcv/clli，防止剪映误判 |
| 覆盖已有 | 覆盖已存在的 `_SDR.mp4` |
| 强制非 HDR | 未打 transfer 标签但看着像 HDR 的片也转 |

## 源码

在 `src\` 文件夹，用 .NET Framework 4.x 的 `csc.exe` 编译：

```
csc /nologo /target:winexe /optimize+ /codepage:65001 /win32manifest:app.manifest ^
    /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
    /out:HDR转SDR.exe HDR2SDR.cs
```

原先的 PowerShell 版 `hdr2sdr.ps1` / `HDR转SDR.cmd` 保留下作为回退，功能已被此 exe 取代。