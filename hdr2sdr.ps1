<#
.SYNOPSIS
    把 HDR（PQ / HLG）录屏转成 SDR，输出可直接导入剪映且颜色正常。

.DESCRIPTION
    做对的转换，而不是"改个标签"：
      - 用 libplacebo 做 BT.2390 色调映射。高光有层次、黑位不抬底，
        颜色也不会因为 BT.2020 -> BT.709 的裸转换而偏。
      - 自动识别 PQ(smpte2084) / HLG(arib-std-b67)，非 HDR 默认跳过。
      - 转换后清除容器里的 mdcv / clli 母版元数据（1000nit / MaxCLL）。
        这两个盒子会让剪映把已经是 SDR 的片子重新当成 HDR，等于白转。
      - 音轨 -c copy 直通，不重编。
      - 输出统一打 bt709 / bt709 / bt709 + tv 范围标签。

.PARAMETER Path
    可选。输入文件或目录，可写多个。目录会递归查找 mp4 / mkv / mov / ts / m4v。
    不填就弹出文件选择框，自己勾选要转的视频（支持多选）。
    输出默认写在源文件旁边，命名为 <原名>_SDR.mp4。

.PARAMETER OutDir
    把结果统一输出到这个目录。不填则跟随源文件目录。

.PARAMETER Crf
    质量，默认 16（x264 的 -crf / nvenc 的 -cq）。数字越小越清晰、体积越大。

.PARAMETER Preset
    x264 速度档，默认 medium。机器慢可以用 fast / veryfast。

.PARAMETER Encoder
    x264（默认，兼容性最好）或 nvenc。
    注意：本机 NVIDIA 走混合显卡输出时 nvenc 初始化会失败
    （报 -40 Function not implemented），所以默认不选它。

.PARAMETER NoStrip
    只转换、不清除 mdcv / clli 元数据。一般不需要。

.PARAMETER Force
    对非 HDR 文件也强制转换；并且覆盖已存在的输出文件。

.EXAMPLE
    .\hdr2sdr.ps1
    # 不带参数，直接弹框自己挑视频（最省事）

.EXAMPLE
    .\hdr2sdr.ps1 "D:\MrLin\Documents\jianying\视频\回放 2026-10-17 22-02-22.mp4"

.EXAMPLE
    .\hdr2sdr.ps1 "D:\MrLin\Documents\jianying\视频" -Preset fast -Crf 18

.EXAMPLE
    .\hdr2sdr.ps1 a.mp4 b.mp4 -OutDir "D:\MrLin\Documents\jianying\视频\SDR" -Force
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
    [string[]]$Path,

    [string]$OutDir,

    [int]$Crf = 16,

    [ValidateSet('ultrafast', 'veryfast', 'fast', 'medium', 'slow')]
    [string]$Preset = 'medium',

    [ValidateSet('x264', 'nvenc')]
    [string]$Encoder = 'x264',

    [switch]$NoStrip,

    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$VideoExt = @('.mp4', '.mkv', '.mov', '.ts', '.m4v', '.avi')
$Latin1 = [Text.Encoding]::GetEncoding(28591)

# ---------------------------------------------------------------- ffmpeg 定位
function Get-FfTools {
    $exe = (Get-Command ffmpeg.exe -ErrorAction SilentlyContinue).Source
    if (-not $exe) {
        $hit = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter 'ffmpeg.exe' -ErrorAction SilentlyContinue |
               Sort-Object FullName -Descending | Select-Object -First 1
        if ($hit) { $exe = $hit.FullName }
    }
    if (-not $exe) { throw "找不到 ffmpeg。先装：  winget install --id Gyan.FFmpeg -e" }
    return [pscustomobject]@{
        Ffmpeg  = $exe
        Ffprobe = (Join-Path (Split-Path $exe) 'ffprobe.exe')
    }
}

# ---------------------------------------------------------------- 探测
function Get-VideoInfo($tools, [string]$file) {
    $json = & $tools.Ffprobe -v error -print_format json `
        -select_streams v:0 `
        -show_entries 'stream=codec_name,width,height,pix_fmt,color_primaries,color_transfer,color_matrix,color_range' `
        -show_entries 'format=duration' $file 2>$null
    $j = $json | ConvertFrom-Json
    $v = $j.streams[0]
    $trc = if ($v.PSObject.Properties['color_transfer']) { [string]$v.color_transfer } else { '' }
    $pri = if ($v.PSObject.Properties['color_primaries']) { [string]$v.color_primaries } else { '' }
    $pf  = if ($v.PSObject.Properties['pix_fmt']) { [string]$v.pix_fmt } else { '' }
    return [pscustomobject]@{
        Transfer = $trc
        Primaries = $pri
        PixFmt   = $pf
        Width    = $v.width
        Height   = $v.height
        Duration = if ($j.format.PSObject.Properties['duration']) { [double]$j.format.duration } else { 0 }
        IsHdr    = ($trc -eq 'smpte2084' -or $trc -eq 'arib-std-b67')
        MaybeHdr = ($trc -eq '' -and $pf -like '*10le*' -and $pri -eq 'bt2020')
    }
}

# ---------------------------------------------------------------- 盒子剥离
function Get-BE32([byte[]]$a, [int]$o) {
    ([int]$a[$o] -shl 24) -bor ([int]$a[$o + 1] -shl 16) -bor ([int]$a[$o + 2] -shl 8) -bor [int]$a[$o + 3]
}
function Set-BE32([byte[]]$a, [int]$o, [int]$v) {
    $a[$o]     = [byte](($v -shr 24) -band 0xFF)
    $a[$o + 1] = [byte](($v -shr 16) -band 0xFF)
    $a[$o + 2] = [byte](($v -shr 8) -band 0xFF)
    $a[$o + 3] = [byte]($v -band 0xFF)
}
function Read-Range([string]$p, [int64]$off, [int]$len) {
    $fs = [IO.File]::OpenRead($p)
    [void]$fs.Seek($off, [IO.SeekOrigin]::Begin)
    $b = New-Object byte[] $len
    $got = 0
    while ($got -lt $len) {
        $r = $fs.Read($b, $got, $len - $got)
        if ($r -le 0) { break }
        $got += $r
    }
    $fs.Close()
    if ($got -lt $len) { return , $b[0..($got - 1)] }
    return , $b
}

function Remove-HdrBoxes([string]$file) {
    $fi = Get-Item -LiteralPath $file
    if ($fi.Length -lt 64) { return '文件过小，跳过' }

    $chunk = 4194304
    $segs = @()
    $segs += [pscustomobject]@{ Base = [int64]0; Bytes = (Read-Range $file 0 ([int][Math]::Min($chunk, $fi.Length))) }
    if ($fi.Length -gt $chunk) {
        $tStart = [int64]($fi.Length - $chunk)
        $segs += [pscustomobject]@{ Base = $tStart; Bytes = (Read-Range $file $tStart $chunk) }
    }

    $targets = @()
    foreach ($t in 'clli', 'mdcv') {
        $bs = [int64](-1)
        foreach ($sg in $segs) {
            $i = $Latin1.GetString($sg.Bytes).IndexOf($t)
            if ($i -ge 0) { $bs = [int64]$sg.Base + [int64]$i - 4; break }
        }
        if ($bs -lt 0) { continue }
        $sz = Get-BE32 (Read-Range $file $bs 8) 0
        $targets += [pscustomobject]@{ Type = $t; BoxStart = $bs; Size = $sz }
    }
    if ($targets.Count -eq 0) { return '无 mdcv/clli（本来就没有）' }

    $targets = @($targets | Sort-Object BoxStart)
    $first = $targets[0].BoxStart
    $last = $targets[-1]
    $span = ($last.BoxStart + $last.Size) - $first

    for ($k = 1; $k -lt $targets.Count; $k++) {
        if (($targets[$k - 1].BoxStart + $targets[$k - 1].Size) -ne $targets[$k].BoxStart) {
            throw "mdcv/clli 盒子不连续，放弃改写以免破坏文件"
        }
    }

    # 原地改写成等长的 free 盒子：不改任何上层盒子尺寸，不动时间戳，无需重编
    $patch = New-Object byte[] $span
    Set-BE32 $patch 0 $span
    $patch[4] = [byte][char]'f'; $patch[5] = [byte][char]'r'
    $patch[6] = [byte][char]'e'; $patch[7] = [byte][char]'e'

    $fw = [IO.File]::Open($file, [IO.FileMode]::Open, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    [void]$fw.Seek($first, [IO.SeekOrigin]::Begin)
    $fw.Write($patch, 0, $span)
    $fw.Flush()
    $fw.Close()

    return "已清除 $span 字节（clli + mdcv -> free）"
}

# ---------------------------------------------------------------- 文件选择框
function Select-Videos {
    if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') {
        throw "当前不是 STA 线程，弹不出选择框。请用 powershell.exe 控制台或 ISE 运行，或改用 -Path 传路径。"
    }
    Add-Type -AssemblyName System.Windows.Forms | Out-Null

    # 用一个隐藏的置顶宿主窗口做 owner，免得对话框被终端/编辑器挡在后面
    $owner = New-Object System.Windows.Forms.Form
    $owner.TopMost = $true
    $owner.ShowInTaskbar = $false
    $owner.WindowState = [System.Windows.Forms.FormWindowState]::Minimized

    $dlg = New-Object System.Windows.Forms.OpenFileDialog
    $dlg.Title = '选择要转成 SDR 的 HDR 视频（可多选）'
    $dlg.Multiselect = $true
    $dlg.Filter = '视频文件|*.mp4;*.mkv;*.mov;*.ts;*.m4v;*.avi|所有文件|*.*'
    $dlg.FilterIndex = 1

    if ($PSScriptRoot) {
        $prefer = Join-Path $PSScriptRoot '视频'
        if (Test-Path -LiteralPath $prefer) { $start = $prefer } else { $start = $PSScriptRoot }
    }
    else {
        $start = [Environment]::GetFolderPath('MyVideos')
    }
    $dlg.InitialDirectory = $start

    $picked = @()
    if ($dlg.ShowDialog($owner) -eq [System.Windows.Forms.DialogResult]::OK) {
        $picked = @($dlg.FileNames)
    }
    $dlg.Dispose()
    $owner.Dispose()
    return $picked
}

# ================================================================ 主流程
if (-not $Path) {
    Write-Host "未传入路径，打开文件选择框 ..." -ForegroundColor DarkGray
    $Path = Select-Videos
    if (-not $Path) { Write-Host "没有选择任何文件，已退出。" -ForegroundColor Yellow; return }
}

$tools = Get-FfTools

$inputs = New-Object System.Collections.Generic.List[string]
foreach ($p in $Path) {
    if (-not (Test-Path -LiteralPath $p)) { Write-Warning "找不到：$p"; continue }
    $item = Get-Item -LiteralPath $p
    if ($item.PSIsContainer) {
        Get-ChildItem -LiteralPath $item.FullName -Recurse -File |
            Where-Object { $VideoExt -contains $_.Extension.ToLower() } |
            Where-Object { $_.Name -notmatch '_SDR' } |
            ForEach-Object { $inputs.Add($_.FullName) }
    }
    else { $inputs.Add($item.FullName) }
}

if ($inputs.Count -eq 0) { Write-Warning "没有找到可处理的视频文件"; return }

Write-Host ("ffmpeg : {0}" -f $tools.Ffmpeg) -ForegroundColor DarkGray
Write-Host ("待处理 : {0} 个文件" -f $inputs.Count) -ForegroundColor DarkGray

if ($OutDir -and -not (Test-Path -LiteralPath $OutDir)) {
    New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
}

$results = @()

foreach ($in in $inputs) {
    Write-Host ""
    Write-Host ("==> {0}" -f (Split-Path -Leaf $in)) -ForegroundColor Cyan

    $info = Get-VideoInfo $tools $in
    Write-Host ("    {0}x{1}  {2}  transfer={3}  {4:N1}s" -f $info.Width, $info.Height, $info.PixFmt, $info.Transfer, $info.Duration) -ForegroundColor DarkGray

    if (-not $info.IsHdr -and -not $Force) {
        $hint = if ($info.MaybeHdr) { '（未打 transfer 标签但看着像 HDR，可加 -Force）' } else { '' }
        Write-Host ("    跳过：不是 HDR {0}" -f $hint) -ForegroundColor Yellow
        $results += [pscustomobject]@{ 文件 = Split-Path -Leaf $in; 状态 = '跳过(非HDR)'; 耗时 = ''; 体积 = ''; 元数据 = '' }
        continue
    }

    $dir = if ($OutDir) { $OutDir } else { Split-Path -Parent $in }
    $base = [IO.Path]::GetFileNameWithoutExtension($in)
    $out = Join-Path $dir ($base + '_SDR.mp4')

    if ((Test-Path -LiteralPath $out) -and -not $Force) {
        Write-Host ("    跳过：输出已存在 -> {0}" -f (Split-Path -Leaf $out)) -ForegroundColor Yellow
        $results += [pscustomobject]@{ 文件 = Split-Path -Leaf $in; 状态 = '跳过(已存在)'; 耗时 = ''; 体积 = ''; 元数据 = '' }
        continue
    }

    $vArgs = if ($Encoder -eq 'nvenc') {
        @('-c:v', 'h264_nvenc', '-preset', 'p5', '-rc', 'vbr', '-cq', "$Crf", '-pix_fmt', 'yuv420p')
    }
    else {
        @('-c:v', 'libx264', '-preset', $Preset, '-crf', "$Crf", '-pix_fmt', 'yuv420p')
    }

    $ffArgs = @(
        '-hide_banner', '-y', '-loglevel', 'error', '-stats'
        '-i', $in
        '-map', '0:v:0', '-map', '0:a?'
        '-vf', 'libplacebo=colorspace=bt709:color_primaries=bt709:color_trc=bt709:range=tv:format=yuv420p'
    ) + $vArgs + @(
        '-c:a', 'copy'
        '-color_primaries', 'bt709', '-color_trc', 'bt709', '-colorspace', 'bt709'
        '-movflags', '+faststart'
        $out
    )

    $sw = [Diagnostics.Stopwatch]::StartNew()
    Write-Host ("    色调映射中（libplacebo BT.2390 + {0}）..." -f $(if ($Encoder -eq 'nvenc') { 'nvenc' } else { "x264 $Preset" }))
    & $tools.Ffmpeg @ffArgs
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $out)) {
        $sw.Stop()
        Write-Host ("    失败：ffmpeg 退出码 {0}" -f $LASTEXITCODE) -ForegroundColor Red
        $results += [pscustomobject]@{ 文件 = Split-Path -Leaf $in; 状态 = '失败'; 耗时 = ''; 体积 = ''; 元数据 = '' }
        continue
    }
    $sw.Stop()

    $meta = if ($NoStrip) { '未处理(-NoStrip)' } else { Remove-HdrBoxes $out }

    # 复核成品
    $outInfo = Get-VideoInfo $tools $out
    $sideJson = & $tools.Ffprobe -v error -select_streams v:0 -show_entries 'stream_side_data' -print_format json $out 2>$null
    $hasSide = ($sideJson -match 'Mastering display metadata') -or ($sideJson -match 'Content light level')

    $tagOk = ($outInfo.Transfer -eq 'bt709' -and $outInfo.Primaries -eq 'bt709')
    $ok = $tagOk -and (-not $hasSide)
    $mb = [Math]::Round((Get-Item -LiteralPath $out).Length / 1MB)

    Write-Host ("    完成：{0:N0}s  {1} MB  标签={2}/{3}  残留元数据={4}" -f `
        $sw.Elapsed.TotalSeconds, $mb, $outInfo.Primaries, $outInfo.Transfer, $(if ($hasSide) { '有' } else { '无' })) `
        -ForegroundColor $(if ($ok) { 'Green' } else { 'Red' })
    Write-Host ("    -> {0}" -f $out) -ForegroundColor DarkGray

    $results += [pscustomobject]@{
        文件   = Split-Path -Leaf $in
        状态   = if ($ok) { 'OK' } else { '需检查' }
        耗时   = "{0:N0}s" -f $sw.Elapsed.TotalSeconds
        体积   = "$mb MB"
        元数据 = $(if ($hasSide) { '有残留' } else { $meta })
    }
}

Write-Host ""
Write-Host "================ 汇总 ================" -ForegroundColor Cyan
$results | Format-Table -AutoSize
