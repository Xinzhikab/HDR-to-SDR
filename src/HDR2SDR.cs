// HDR2SDR —— HDR(PQ/HLG) 视频转 SDR 图形工具
// 编译: csc /target:winexe /optimize+ /codepage:65001 /win32manifest:app.manifest
//        /out:HDR转SDR.exe HDR2SDR.cs
// 依赖: .NET Framework 4.x (Windows 10/11 自带) + ffmpeg(含 libplacebo)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Hdr2Sdr
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length > 0 && (args[0] == "--cli" || args[0] == "-cli"))
            {
                try { Console.OutputEncoding = Encoding.UTF8; Console.InputEncoding = Encoding.UTF8; } catch { }
                return CliMain.Run(args);
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
            {
                MessageBox.Show(e.Exception.Message, "程序异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            MainForm form = new MainForm(args);
            Application.Run(form);
            return 0;
        }
    }

    // ------------------------------------------------------------------ 视频信息
    internal class VideoItem
    {
        public string FullPath = "";
        public string FileName = "";
        public bool Probed;
        public bool IsHdr;
        public bool MaybeHdr;
        public string VCodec = "";
        public string ACodec = "";
        public string PixFmt = "";
        public string Transfer = "";
        public string Primaries = "";
        public string Range = "";
        public int Width;
        public int Height;
        public double Duration;
        public long SizeBytes;
        public string Status = "待转换";
        public ListViewItem Row;
    }

    // ------------------------------------------------------------------ ffmpeg 定位与调用
    internal static class Ff
    {
        public static string Exe;
        public static string ProbeExe;
        public static string Where = "";

        public static string AppDir
        {
            get { return Path.GetDirectoryName(Application.ExecutablePath); }
        }

        public static string IniPath
        {
            get { return Path.Combine(AppDir, "hdr2sdr.ini"); }
        }

        public static bool Locate()
        {
            List<string> cands = new List<string>();

            // 1) 配置文件里记住的路径
            try
            {
                if (File.Exists(IniPath))
                {
                    foreach (string raw in File.ReadAllLines(IniPath))
                    {
                        string line = raw.Trim();
                        if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        string k = line.Substring(0, eq).Trim().ToLowerInvariant();
                        string v = line.Substring(eq + 1).Trim().Trim('"');
                        if (k == "ffmpeg" && v.Length > 0) cands.Add(v);
                    }
                }
            }
            catch { }

            // 2) 与 exe 同目录的几种常见摆放
            cands.Add(Path.Combine(AppDir, "ffmpeg.exe"));
            cands.Add(Path.Combine(AppDir, "bin", "ffmpeg.exe"));
            cands.Add(Path.Combine(AppDir, "ffmpeg", "bin", "ffmpeg.exe"));
            cands.Add(Path.Combine(AppDir, "tools", "ffmpeg.exe"));

            // 3) PATH
            try
            {
                string[] envs = Environment.GetEnvironmentVariable("PATH").Split(';');
                foreach (string p in envs)
                {
                    if (p == null) continue;
                    string t = p.Trim().Trim('"');
                    if (t.Length == 0) continue;
                    try { cands.Add(Path.Combine(t, "ffmpeg.exe")); }
                    catch { }
                }
            }
            catch { }

            // 4) winget 安装目录
            try
            {
                string winget = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft", "WinGet", "Packages");
                if (Directory.Exists(winget))
                {
                    string[] hits = Directory.GetFiles(winget, "ffmpeg.exe", SearchOption.AllDirectories);
                    Array.Sort(hits);
                    for (int i = hits.Length - 1; i >= 0; i--) cands.Add(hits[i]);
                }
            }
            catch { }

            // 5) 常见手工安装位置
            cands.Add(@"C:\ffmpeg\bin\ffmpeg.exe");
            cands.Add(@"C:\ffmpeg\ffmpeg.exe");
            cands.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ffmpeg", "bin", "ffmpeg.exe"));

            foreach (string c in cands)
            {
                try
                {
                    if (c != null && c.Length > 0 && File.Exists(c))
                    {
                        string dir = Path.GetDirectoryName(c);
                        string probe = Path.Combine(dir, "ffprobe.exe");
                        if (!File.Exists(probe)) continue;
                        Exe = c;
                        ProbeExe = probe;
                        Where = c;
                        return true;
                    }
                }
                catch { }
            }
            Exe = null;
            ProbeExe = null;
            return false;
        }

        public static void SaveHint(string ffmpegPath)
        {
            try
            {
                File.WriteAllText(IniPath, "ffmpeg=" + ffmpegPath + "\r\n", new UTF8Encoding(false));
            }
            catch { }
        }

        public static string RunCapture(string exe, string args, int timeoutMs)
        {
            StringBuilder so = new StringBuilder();
            StringBuilder se = new StringBuilder();
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe, args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;
                using (Process p = new Process())
                {
                    p.StartInfo = psi;
                    p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                    {
                        if (e.Data != null) so.AppendLine(e.Data);
                    };
                    p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                    {
                        if (e.Data != null) se.AppendLine(e.Data);
                    };
                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(timeoutMs))
                    {
                        try { p.Kill(); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                return "ERROR:" + ex.Message;
            }
            return so.ToString() + se.ToString();
        }

        private static Dictionary<string, string> ParseKeyValues(string text)
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (text == null) return d;
            string[] lines = text.Replace("\r", "").Split('\n');
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string k = line.Substring(0, eq).Trim();
                string v = line.Substring(eq + 1).Trim();
                if (!d.ContainsKey(k)) d[k] = v;
            }
            return d;
        }

        public static void Probe(VideoItem v)
        {
            v.Probed = false;
            if (ProbeExe == null) return;

            string q = "\"" + v.FullPath + "\"";
            string outp = RunCapture(ProbeExe,
                "-v error -select_streams v:0 -show_entries stream=codec_name,width,height,pix_fmt,color_range,color_space,color_transfer,color_primaries,r_frame_rate " +
                "-show_entries format=duration -of default=nw=1 " + q, 30000);
            Dictionary<string, string> d = ParseKeyValues(outp);
            if (!d.ContainsKey("width")) return;

            int w, h;
            int.TryParse(Get(d, "width"), out w);
            int.TryParse(Get(d, "height"), out h);
            double dur;
            double.TryParse(Get(d, "duration"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out dur);

            v.Width = w;
            v.Height = h;
            v.Duration = dur;
            v.VCodec = Get(d, "codec_name");
            v.PixFmt = Get(d, "pix_fmt");
            v.Range = Get(d, "color_range");
            v.Transfer = Get(d, "color_transfer");
            v.Primaries = Get(d, "color_primaries");

            string aout = RunCapture(ProbeExe,
                "-v error -select_streams a:0 -show_entries stream=codec_name -of default=nw=1 " + q, 30000);
            Dictionary<string, string> ad = ParseKeyValues(aout);
            v.ACodec = Get(ad, "codec_name");

            v.IsHdr = (v.Transfer == "smpte2084" || v.Transfer == "arib-std-b67");
            v.MaybeHdr = (v.Transfer.Length == 0 && v.PixFmt != null && v.PixFmt.EndsWith("10le", StringComparison.OrdinalIgnoreCase)
                           && (v.Primaries == "bt2020" || v.Primaries == "bt2020nc" || v.Primaries.Length == 0));
            v.Probed = true;
        }

        private static string Get(Dictionary<string, string> d, string k)
        {
            string v;
            return d.TryGetValue(k, out v) ? v : "";
        }

        // 输出文件里是否还留着 HDR 母版/亮度 side data
        public static bool HasHdrSideData(string file)
        {
            string outp = RunCapture(ProbeExe,
                "-v error -select_streams v:0 -show_entries stream_side_data -of default=nw=1 \"" + file + "\"", 30000);
            return outp.IndexOf("Mastering display metadata", StringComparison.OrdinalIgnoreCase) >= 0
                || outp.IndexOf("Content light level", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    // ------------------------------------------------------------------ mp4 盒子清理
    internal static class Mp4Fix
    {
        private static readonly Encoding Latin1 = Encoding.GetEncoding(28591);

        private class Seg
        {
            public long Base;
            public byte[] Bytes;
        }

        private class Tgt
        {
            public string Type;
            public long Start;
            public int Size;
        }

        private static int BE32(byte[] a, int o)
        {
            return (a[o] << 24) | (a[o + 1] << 16) | (a[o + 2] << 8) | a[o + 3];
        }

        private static void SetBE32(byte[] a, int o, int v)
        {
            a[o] = (byte)((v >> 24) & 0xFF);
            a[o + 1] = (byte)((v >> 16) & 0xFF);
            a[o + 2] = (byte)((v >> 8) & 0xFF);
            a[o + 3] = (byte)(v & 0xFF);
        }

        private static byte[] ReadRange(string path, long off, int len)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                fs.Seek(off, SeekOrigin.Begin);
                byte[] b = new byte[len];
                int got = 0;
                while (got < len)
                {
                    int r = fs.Read(b, got, len - got);
                    if (r <= 0) break;
                    got += r;
                }
                if (got < len)
                {
                    byte[] t = new byte[got];
                    Array.Copy(b, t, got);
                    return t;
                }
                return b;
            }
        }

        // 把 mdcv / clli 盒子原地改写成等长 free 盒子，不改上层尺寸、不重编
        public static string Strip(string file)
        {
            FileInfo fi = new FileInfo(file);
            if (fi.Length < 64) return "文件过小，跳过";

            int chunk = 4 * 1024 * 1024;
            List<Seg> segs = new List<Seg>();
            Seg s0 = new Seg();
            s0.Base = 0;
            s0.Bytes = ReadRange(file, 0, (int)Math.Min((long)chunk, fi.Length));
            segs.Add(s0);
            if (fi.Length > chunk)
            {
                long ts = fi.Length - chunk;
                Seg s1 = new Seg();
                s1.Base = ts;
                s1.Bytes = ReadRange(file, ts, chunk);
                segs.Add(s1);
            }

            List<Tgt> targets = new List<Tgt>();
            string[] types = new string[] { "clli", "mdcv" };
            foreach (string t in types)
            {
                long bs = -1;
                foreach (Seg sg in segs)
                {
                    int i = Latin1.GetString(sg.Bytes).IndexOf(t, StringComparison.Ordinal);
                    if (i >= 0)
                    {
                        bs = sg.Base + i - 4;
                        break;
                    }
                }
                if (bs < 0) continue;

                byte[] head = ReadRange(file, bs, 8);
                if (head.Length < 8) continue;
                if (Latin1.GetString(head, 4, 4) != t) continue;   // 确认真是盒子类型
                int sz = BE32(head, 0);
                if (sz <= 8 || sz > 8192) continue;
                if (bs + sz > fi.Length) continue;

                Tgt tg = new Tgt();
                tg.Type = t;
                tg.Start = bs;
                tg.Size = sz;
                targets.Add(tg);
            }

            if (targets.Count == 0) return "无 mdcv/clli（本来就没有）";

            targets.Sort(delegate(Tgt a, Tgt b) { return a.Start.CompareTo(b.Start); });
            long first = targets[0].Start;
            Tgt last = targets[targets.Count - 1];
            long spanL = last.Start + last.Size - first;
            if (spanL <= 0 || spanL > 8192) return "盒子跨度异常，跳过";
            int span = (int)spanL;

            for (int k = 1; k < targets.Count; k++)
            {
                if (targets[k - 1].Start + targets[k - 1].Size != targets[k].Start)
                    return "mdcv/clli 不连续，跳过改写";
            }

            byte[] patch = new byte[span];
            SetBE32(patch, 0, span);
            patch[4] = (byte)'f';
            patch[5] = (byte)'r';
            patch[6] = (byte)'e';
            patch[7] = (byte)'e';

            using (FileStream fw = new FileStream(file, FileMode.Open, FileAccess.Write, FileShare.Read))
            {
                fw.Seek(first, SeekOrigin.Begin);
                fw.Write(patch, 0, patch.Length);
                fw.Flush();
            }
            return "已清除 " + span + " 字节";
        }
    }

    // ------------------------------------------------------------------ 主窗体
    internal class BufferedListView : ListView
    {
        public BufferedListView()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }
    }

    internal class MainForm : Form
    {
        private const string LpFilter =
            "libplacebo=tonemapping=bt.2390:colorspace=bt709:color_primaries=bt709:color_trc=bt709:range=tv:format=yuv420p";
        private const string ZsFilter =
            "zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,tonemap=tonemap=hable:desat=0,zscale=t=bt709:m=bt709:r=tv,format=yuv420p";

        private static readonly string[] VideoExt = new string[]
        {
            ".mp4", ".mkv", ".mov", ".ts", ".m4v", ".avi", ".wmv", ".webm", ".mts", ".m2ts", ".mpg", ".mpeg", ".flv"
        };

        private readonly string[] _startArgs;

        private BufferedListView lv;
        private RichTextBox txtLog;
        private TextBox txtOutDir;
        private NumericUpDown numCrf;
        private ComboBox cbPreset;
        private ComboBox cbEncoder;
        private CheckBox chkStrip;
        private CheckBox chkOverwrite;
        private CheckBox chkForce;
        private CheckBox chkOpenDone;
        private Button btnStart;
        private Button btnStop;
        private Button btnOpenOut;
        private Button btnAddFiles;
        private Button btnAddFolder;
        private Button btnRemove;
        private Button btnClear;
        private Button btnBrowseOut;
        private Button btnFfmpeg;
        private ProgressBar bar;
        private Label lblStatus;
        private Label lblFfmpeg;

        private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Thread _worker;
        private volatile bool _stop;
        private Process _proc;
        private string _lastOutDir = "";
        private DateTime _jobStart;

        public MainForm(string[] args)
        {
            _startArgs = args;
            BuildUi();
            this.Load += delegate
            {
                InitFfmpeg(true);
                if (_startArgs != null && _startArgs.Length > 0) AddPaths(_startArgs);
            };
        }

        // ---------------------------------------------------------- 界面
        private void BuildUi()
        {
            this.Text = "HDR 转 SDR";
            this.Font = new Font("Microsoft YaHei UI", 9f);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(1060, 730);
            this.MinimumSize = new Size(980, 640);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.AllowDrop = true;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 5;
            root.Padding = new Padding(8);
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132f));
            this.Controls.Add(root);

            // ---- 工具栏
            Panel bar1 = new Panel();
            bar1.Dock = DockStyle.Fill;
            root.Controls.Add(bar1, 0, 0);

            btnAddFiles = MakeButton("添加文件", 96);
            btnAddFiles.Location = new Point(0, 4);
            btnAddFiles.Click += delegate { PickFiles(); };
            bar1.Controls.Add(btnAddFiles);

            btnAddFolder = MakeButton("添加文件夹", 104);
            btnAddFolder.Location = new Point(104, 4);
            btnAddFolder.Click += delegate { PickFolder(); };
            bar1.Controls.Add(btnAddFolder);

            btnRemove = MakeButton("移除所选", 96);
            btnRemove.Location = new Point(216, 4);
            btnRemove.Click += delegate { RemoveSelected(); };
            bar1.Controls.Add(btnRemove);

            btnClear = MakeButton("清空", 72);
            btnClear.Location = new Point(320, 4);
            btnClear.Click += delegate { ClearAll(); };
            bar1.Controls.Add(btnClear);

            Label tip = new Label();
            tip.Text = "把视频直接拖进窗口也可以";
            tip.AutoSize = true;
            tip.ForeColor = Color.Gray;
            tip.Location = new Point(404, 12);
            bar1.Controls.Add(tip);

            // ---- 列表
            lv = new BufferedListView();
            lv.Dock = DockStyle.Fill;
            lv.View = View.Details;
            lv.FullRowSelect = true;
            lv.GridLines = true;
            lv.HideSelection = false;
            lv.AllowDrop = true;
            lv.Columns.Add("文件", 380);
            lv.Columns.Add("状态", 92);
            lv.Columns.Add("分辨率", 104);
            lv.Columns.Add("色彩", 210);
            lv.Columns.Add("时长", 72);
            lv.Columns.Add("大小", 86);
            lv.DoubleClick += delegate { OpenSelectedFolder(); };
            lv.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Delete) RemoveSelected();
            };
            lv.DragEnter += OnDragEnter;
            lv.DragDrop += OnDragDrop;
            root.Controls.Add(lv, 0, 1);

            // ---- 设置
            GroupBox gb = new GroupBox();
            gb.Text = "转换设置";
            gb.Dock = DockStyle.Fill;
            root.Controls.Add(gb, 0, 2);

            Label l1 = MakeLabel("质量 CRF", 14, 27);
            gb.Controls.Add(l1);
            numCrf = new NumericUpDown();
            numCrf.Minimum = 10;
            numCrf.Maximum = 32;
            numCrf.Value = 18;
            numCrf.Location = new Point(80, 24);
            numCrf.Width = 58;
            gb.Controls.Add(numCrf);

            Label l2 = MakeLabel("速度", 152, 27);
            gb.Controls.Add(l2);
            cbPreset = new ComboBox();
            cbPreset.DropDownStyle = ComboBoxStyle.DropDownList;
            cbPreset.Items.AddRange(new object[] { "ultrafast", "veryfast", "fast", "medium", "slow" });
            cbPreset.SelectedIndex = 3;
            cbPreset.Location = new Point(190, 24);
            cbPreset.Width = 100;
            gb.Controls.Add(cbPreset);

            Label l3 = MakeLabel("编码器", 304, 27);
            gb.Controls.Add(l3);
            cbEncoder = new ComboBox();
            cbEncoder.DropDownStyle = ComboBoxStyle.DropDownList;
            cbEncoder.Items.AddRange(new object[] { "x264（CPU，兼容最好）", "NVENC（N卡硬件，快）" });
            cbEncoder.SelectedIndex = 0;
            cbEncoder.Location = new Point(352, 24);
            cbEncoder.Width = 190;
            gb.Controls.Add(cbEncoder);

            chkStrip = new CheckBox();
            chkStrip.Text = "清除 HDR 元数据";
            chkStrip.Checked = true;
            chkStrip.AutoSize = true;
            chkStrip.Location = new Point(560, 26);
            gb.Controls.Add(chkStrip);

            chkOverwrite = new CheckBox();
            chkOverwrite.Text = "覆盖已有";
            chkOverwrite.AutoSize = true;
            chkOverwrite.Location = new Point(712, 26);
            gb.Controls.Add(chkOverwrite);

            chkForce = new CheckBox();
            chkForce.Text = "强制非 HDR";
            chkForce.AutoSize = true;
            chkForce.Location = new Point(812, 26);
            gb.Controls.Add(chkForce);

            Label l4 = MakeLabel("输出目录", 14, 60);
            gb.Controls.Add(l4);
            txtOutDir = new TextBox();
            txtOutDir.Location = new Point(80, 57);
            txtOutDir.Width = 700;
            txtOutDir.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            gb.Controls.Add(txtOutDir);

            btnBrowseOut = MakeButton("浏览…", 78);
            btnBrowseOut.Location = new Point(788, 55);
            btnBrowseOut.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnBrowseOut.Click += delegate { PickOutDir(); };
            gb.Controls.Add(btnBrowseOut);

            Label hint = MakeLabel("（留空 = 输出到源文件旁边，命名 <原名>_SDR.mp4）", 872, 60);
            hint.ForeColor = Color.Gray;
            hint.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            gb.Controls.Add(hint);

            // ---- 执行
            Panel bar2 = new Panel();
            bar2.Dock = DockStyle.Fill;
            root.Controls.Add(bar2, 0, 3);

            btnStart = MakeButton("开始转换", 132);
            btnStart.Font = new Font(this.Font, FontStyle.Bold);
            btnStart.Location = new Point(0, 4);
            btnStart.Height = 34;
            btnStart.BackColor = Color.FromArgb(232, 246, 233);
            btnStart.Click += delegate { StartConvert(); };
            bar2.Controls.Add(btnStart);

            btnStop = MakeButton("停止", 84);
            btnStop.Location = new Point(140, 4);
            btnStop.Height = 34;
            btnStop.Enabled = false;
            btnStop.Click += delegate { StopConvert(); };
            bar2.Controls.Add(btnStop);

            btnOpenOut = MakeButton("打开输出目录", 116);
            btnOpenOut.Location = new Point(232, 4);
            btnOpenOut.Height = 34;
            btnOpenOut.Click += delegate { OpenOutputFolder(); };
            bar2.Controls.Add(btnOpenOut);

            btnFfmpeg = MakeButton("指定 ffmpeg…", 112);
            btnFfmpeg.Location = new Point(356, 4);
            btnFfmpeg.Height = 34;
            btnFfmpeg.Click += delegate { PickFfmpeg(); };
            bar2.Controls.Add(btnFfmpeg);

            chkOpenDone = new CheckBox();
            chkOpenDone.Text = "完成后打开目录";
            chkOpenDone.AutoSize = true;
            chkOpenDone.Location = new Point(486, 12);
            bar2.Controls.Add(chkOpenDone);

            bar = new ProgressBar();
            bar.Location = new Point(0, 48);
            bar.Width = 900;
            bar.Height = 16;
            bar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            bar2.Controls.Add(bar);

            lblStatus = new Label();
            lblStatus.Text = "就绪";
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(0, 68);
            bar2.Controls.Add(lblStatus);

            lblFfmpeg = new Label();
            lblFfmpeg.Text = "";
            lblFfmpeg.AutoSize = true;
            lblFfmpeg.ForeColor = Color.Gray;
            lblFfmpeg.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblFfmpeg.Location = new Point(520, 68);
            bar2.Controls.Add(lblFfmpeg);

            // ---- 日志
            GroupBox gl = new GroupBox();
            gl.Text = "日志";
            gl.Dock = DockStyle.Fill;
            root.Controls.Add(gl, 0, 4);

            txtLog = new RichTextBox();
            txtLog.Dock = DockStyle.Fill;
            txtLog.ReadOnly = true;
            txtLog.BackColor = Color.White;
            txtLog.BorderStyle = BorderStyle.None;
            txtLog.Font = new Font("Consolas", 9f);
            txtLog.WordWrap = false;
            txtLog.HideSelection = false;
            gl.Controls.Add(txtLog);

            this.DragEnter += OnDragEnter;
            this.DragDrop += OnDragDrop;
            this.FormClosing += OnClosing;
        }

        private static Button MakeButton(string text, int width)
        {
            Button b = new Button();
            b.Text = text;
            b.Width = width;
            b.Height = 28;
            b.FlatStyle = FlatStyle.System;
            return b;
        }

        private static Label MakeLabel(string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Location = new Point(x, y);
            return l;
        }

        // ---------------------------------------------------------- 拖拽
        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
            else e.Effect = DragDropEffects.None;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0) AddPaths(files);
        }

        // ---------------------------------------------------------- 文件收集
        private void PickFiles()
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Title = "选择要转成 SDR 的视频（可多选）";
            d.Multiselect = true;
            d.Filter = "视频文件|*.mp4;*.mkv;*.mov;*.ts;*.m4v;*.avi;*.wmv;*.webm;*.mts;*.m2ts;*.mpg|所有文件|*.*";
            if (!string.IsNullOrEmpty(txtOutDir.Text) && Directory.Exists(txtOutDir.Text))
                d.InitialDirectory = txtOutDir.Text;
            else
            {
                string pref = Path.Combine(Ff.AppDir, "视频");
                d.InitialDirectory = Directory.Exists(pref) ? pref : Ff.AppDir;
            }
            if (d.ShowDialog(this) == DialogResult.OK) AddPaths(d.FileNames);
        }

        private void PickFolder()
        {
            FolderBrowserDialog d = new FolderBrowserDialog();
            d.Description = "选择包含视频的文件夹（会递归查找）";
            if (d.ShowDialog(this) == DialogResult.OK)
            {
                AddPaths(new string[] { d.SelectedPath });
            }
        }

        private void PickOutDir()
        {
            FolderBrowserDialog d = new FolderBrowserDialog();
            d.Description = "选择统一输出目录";
            if (Directory.Exists(txtOutDir.Text)) d.SelectedPath = txtOutDir.Text;
            if (d.ShowDialog(this) == DialogResult.OK) txtOutDir.Text = d.SelectedPath;
        }

        private void PickFfmpeg()
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Title = "选择 ffmpeg.exe";
            d.Filter = "ffmpeg.exe|ffmpeg.exe|可执行文件|*.exe";
            if (d.ShowDialog(this) == DialogResult.OK)
            {
                string dir = Path.GetDirectoryName(d.FileName);
                if (!File.Exists(Path.Combine(dir, "ffprobe.exe")))
                {
                    MessageBox.Show("同目录下没有 ffprobe.exe，ffmpeg 完整包两个文件都要有。",
                        "缺 ffprobe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                Ff.Exe = d.FileName;
                Ff.ProbeExe = Path.Combine(dir, "ffprobe.exe");
                Ff.SaveHint(d.FileName);
                ShowFfmpeg();
                Log("ffmpeg 已指定：" + d.FileName, Color.Gray);
            }
        }

        private void InitFfmpeg(bool verbose)
        {
            bool ok = Ff.Locate();
            ShowFfmpeg();
            if (!ok && verbose)
            {
                Log("没有自动找到 ffmpeg。点「指定 ffmpeg…」选一下，或者 winget install --id Gyan.FFmpeg -e", Color.Firebrick);
            }
        }

        private void ShowFfmpeg()
        {
            if (Ff.Exe == null)
            {
                lblFfmpeg.Text = "ffmpeg：未找到";
                lblFfmpeg.ForeColor = Color.Firebrick;
            }
            else
            {
                lblFfmpeg.Text = "ffmpeg：" + Ff.Exe;
                lblFfmpeg.ForeColor = Color.Gray;
            }
        }

        private void AddPaths(IEnumerable<string> paths)
        {
            if (Ff.Exe == null) InitFfmpeg(false);

            List<string> files = new List<string>();
            foreach (string p in paths)
            {
                if (p == null) continue;
                try
                {
                    if (Directory.Exists(p))
                    {
                        foreach (string f in Directory.GetFiles(p, "*", SearchOption.AllDirectories))
                        {
                            string ext = Path.GetExtension(f).ToLowerInvariant();
                            if (Array.IndexOf(VideoExt, ext) < 0) continue;
                            if (Path.GetFileNameWithoutExtension(f).EndsWith("_SDR", StringComparison.OrdinalIgnoreCase)) continue;
                            files.Add(f);
                        }
                    }
                    else if (File.Exists(p))
                    {
                        files.Add(Path.GetFullPath(p));
                    }
                }
                catch (Exception ex)
                {
                    Log("跳过 " + p + "：" + ex.Message, Color.Firebrick);
                }
            }

            if (files.Count == 0) return;

            Cursor old = this.Cursor;
            this.Cursor = Cursors.WaitCursor;
            int added = 0;
            foreach (string f in files)
            {
                if (_seen.Contains(f)) continue;
                VideoItem v = new VideoItem();
                v.FullPath = f;
                v.FileName = Path.GetFileName(f);
                try { v.SizeBytes = new FileInfo(f).Length; } catch { }

                ListViewItem it = new ListViewItem(v.FileName);
                it.SubItems.Add("读取中…");
                it.SubItems.Add("");
                it.SubItems.Add("");
                it.SubItems.Add("");
                it.SubItems.Add(FormatSize(v.SizeBytes));
                it.Tag = v;
                v.Row = it;
                lv.Items.Add(it);
                lv.EnsureVisible(lv.Items.Count - 1);
                Application.DoEvents();

                Ff.Probe(v);
                FillRow(v);
                _seen.Add(f);
                added++;
            }
            this.Cursor = old;
            Log("已加入 " + added + " 个文件。", Color.DimGray);
            UpdateStatus();
        }

        private void FillRow(VideoItem v)
        {
            if (v.Row == null) return;
            ListViewItem it = v.Row;
            it.SubItems[1].Text = v.Probed ? (v.IsHdr ? "HDR" : (v.MaybeHdr ? "疑似HDR" : "非HDR")) : "读取失败";
            it.SubItems[2].Text = v.Width > 0 ? (v.Width + "x" + v.Height) : "";
            it.SubItems[3].Text = ColorLabel(v);
            it.SubItems[4].Text = v.Duration > 0 ? FormatTime(v.Duration) : "";
            it.SubItems[5].Text = FormatSize(v.SizeBytes);
            it.ForeColor = v.Probed ? (v.IsHdr ? Color.Black : Color.DimGray) : Color.Firebrick;
            it.ToolTipText = v.FullPath;
        }

        private static string ColorLabel(VideoItem v)
        {
            if (!v.Probed) return "";
            string hdr;
            if (v.Transfer == "smpte2084") hdr = "HDR10 (PQ)";
            else if (v.Transfer == "arib-std-b67") hdr = "HLG";
            else if (v.Transfer == "smpte428") hdr = "HDR (428)";
            else if (v.Transfer == "bt2020-10" || v.Transfer == "bt2020-12") hdr = "BT.2020";
            else if (v.Transfer.Length == 0) hdr = v.MaybeHdr ? "疑似 HDR" : "未标注";
            else hdr = v.Transfer;

            string prim = v.Primaries.Length > 0 ? v.Primaries : "?";
            string bits = v.PixFmt.Length > 0 ? v.PixFmt : "?";
            return hdr + " · " + bits + " · " + prim;
        }

        private void RemoveSelected()
        {
            if (_worker != null && _worker.IsAlive) return;
            foreach (ListViewItem it in new List<ListViewItem>(GetSelected()))
            {
                VideoItem v = it.Tag as VideoItem;
                if (v != null) _seen.Remove(v.FullPath);
                lv.Items.Remove(it);
            }
            UpdateStatus();
        }

        private List<ListViewItem> GetSelected()
        {
            List<ListViewItem> r = new List<ListViewItem>();
            foreach (ListViewItem it in lv.SelectedItems) r.Add(it);
            return r;
        }

        private void ClearAll()
        {
            if (_worker != null && _worker.IsAlive) return;
            lv.Items.Clear();
            _seen.Clear();
            UpdateStatus();
        }

        private void OpenSelectedFolder()
        {
            if (lv.SelectedItems.Count == 0) return;
            VideoItem v = lv.SelectedItems[0].Tag as VideoItem;
            if (v == null) return;
            try { Process.Start("explorer.exe", "/select,\"" + v.FullPath + "\""); }
            catch { }
        }

        private void OpenOutputFolder()
        {
            string dir = _lastOutDir;
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                dir = txtOutDir.Text;
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                {
                    if (lv.Items.Count > 0)
                    {
                        VideoItem v = lv.Items[0].Tag as VideoItem;
                        if (v != null) dir = Path.GetDirectoryName(v.FullPath);
                    }
                }
            }
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                MessageBox.Show("还没有可打开的输出目录。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try { Process.Start("explorer.exe", "\"" + dir + "\""); }
            catch { }
        }

        // ---------------------------------------------------------- 转换
        private string BuildVideoArgs()
        {
            int crf = (int)numCrf.Value;
            string preset = (string)cbPreset.SelectedItem;
            if (cbEncoder.SelectedIndex == 1)
            {
                string p = "p5";
                if (preset == "ultrafast") p = "p1";
                else if (preset == "veryfast") p = "p3";
                else if (preset == "fast") p = "p4";
                else if (preset == "slow") p = "p6";
                return "-c:v h264_nvenc -preset " + p + " -rc vbr -cq " + crf + " -pix_fmt yuv420p";
            }
            return "-c:v libx264 -preset " + preset + " -crf " + crf + " -pix_fmt yuv420p";
        }

        private static string BuildAudioArgs(string codec)
        {
            string c = (codec == null) ? "" : codec.ToLowerInvariant();
            if (c.Length == 0) return "-an";
            if (c == "aac" || c == "mp3" || c == "ac3" || c == "eac3" || c == "alac" || c == "mp2")
                return "-c:a copy";
            return "-c:a aac -b:a 192k";
        }

        private void StartConvert()
        {
            if (_worker != null && _worker.IsAlive) return;
            if (lv.Items.Count == 0)
            {
                MessageBox.Show("列表是空的，先添加视频。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (Ff.Exe == null)
            {
                InitFfmpeg(true);
                if (Ff.Exe == null)
                {
                    MessageBox.Show("还没找到 ffmpeg.exe，点「指定 ffmpeg…」选一下。", "需要 ffmpeg",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            string outDir = txtOutDir.Text.Trim();
            if (outDir.Length > 0)
            {
                try { Directory.CreateDirectory(outDir); }
                catch (Exception ex)
                {
                    MessageBox.Show("输出目录建不了：" + ex.Message, "出错了", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            List<VideoItem> items = new List<VideoItem>();
            foreach (ListViewItem it in lv.Items)
            {
                VideoItem v = it.Tag as VideoItem;
                if (v != null) items.Add(v);
            }

            _stop = false;
            _jobStart = DateTime.Now;
            SetRunning(true);
            bar.Value = 0;
            txtLog.Clear();
            Log("开始转换 · " + items.Count + " 个文件 · ffmpeg " + Ff.Exe, Color.DimGray);

            _worker = new Thread(delegate() { Worker(items, outDir); });
            _worker.IsBackground = true;
            _worker.Start();
        }

        private void StopConvert()
        {
            _stop = true;
            Process p = _proc;
            if (p != null)
            {
                try { p.Kill(); } catch { }
            }
            Log("已请求停止…", Color.Firebrick);
        }

        private void Worker(List<VideoItem> items, string outDir)
        {
            int ok = 0, skip = 0, fail = 0;
            bool strip = chkStrip.Checked;
            bool overwrite = chkOverwrite.Checked;
            bool force = chkForce.Checked;

            for (int i = 0; i < items.Count; i++)
            {
                if (_stop) break;
                VideoItem v = items[i];

                if (!v.Probed)
                {
                    try { Ff.Probe(v); }
                    catch { }
                }
                SetRow(v, "检查中…", Color.Black);
                SetStatus("正在检查 " + (i + 1) + "/" + items.Count + " · " + v.FileName);

                if (!v.Probed)
                {
                    SetRow(v, "读取失败", Color.Firebrick);
                    Log("[" + (i + 1) + "/" + items.Count + "] " + v.FileName + " —— ffprobe 读不出来，跳过", Color.Firebrick);
                    fail++;
                    continue;
                }

                if (!v.IsHdr && !force)
                {
                    SetRow(v, "跳过(非HDR)", Color.DimGray);
                    Log("[" + (i + 1) + "/" + items.Count + "] " + v.FileName + " —— 不是 HDR，跳过"
                        + (v.MaybeHdr ? "（疑似 HDR 可勾「强制非 HDR」）" : ""), Color.DarkGoldenrod);
                    skip++;
                    continue;
                }

                string dir = outDir.Length > 0 ? outDir : Path.GetDirectoryName(v.FullPath);
                string baseName = Path.GetFileNameWithoutExtension(v.FullPath);
                string outPath = Path.Combine(dir, baseName + "_SDR.mp4");

                if (File.Exists(outPath) && !overwrite)
                {
                    SetRow(v, "跳过(已存在)", Color.DimGray);
                    Log("[" + (i + 1) + "/" + items.Count + "] " + v.FileName + " —— 输出已存在，跳过", Color.DarkGoldenrod);
                    skip++;
                    continue;
                }

                _lastOutDir = dir;
                SetRow(v, "转换中…", Color.MediumBlue);
                Log("", Color.Black);
                Log("[" + (i + 1) + "/" + items.Count + "] " + v.FileName, Color.SteelBlue);
                Log("    " + v.Width + "x" + v.Height + " · " + ColorLabel(v) + " · " + FormatTime(v.Duration)
                    + " · " + FormatSize(v.SizeBytes), Color.Gray);
                Log("    -> " + outPath, Color.Gray);

                Stopwatch sw = Stopwatch.StartNew();
                bool success = EncodeOne(v, outPath, i, items.Count);

                if (_stop && !success)
                {
                    TryDelete(outPath);
                    SetRow(v, "已停止", Color.DimGray);
                    break;
                }
                if (!success)
                {
                    TryDelete(outPath);
                    SetRow(v, "失败", Color.Firebrick);
                    fail++;
                    continue;
                }

                string meta;
                if (strip)
                {
                    try { meta = Mp4Fix.Strip(outPath); }
                    catch (Exception ex) { meta = "清理失败：" + ex.Message; }
                }
                else meta = "未处理";

                // 复核
                VideoItem chk = new VideoItem();
                chk.FullPath = outPath;
                try { Ff.Probe(chk); }
                catch { }
                bool sideLeft = false;
                try { sideLeft = Ff.HasHdrSideData(outPath); }
                catch { }

                bool tagOk = (chk.Transfer == "bt709" && chk.Primaries == "bt709");
                long outSize = 0;
                try { outSize = new FileInfo(outPath).Length; } catch { }
                sw.Stop();

                bool good = tagOk && !sideLeft && outSize > 0;
                if (good)
                {
                    ok++;
                    SetRow(v, "完成", Color.Green);
                    Log("    完成 · 用时 " + FormatTime(sw.Elapsed.TotalSeconds) + " · " + FormatSize(outSize)
                        + " · 标签 " + chk.Primaries + "/" + chk.Transfer
                        + " · 元数据 " + (sideLeft ? "有残留" : meta), Color.Green);
                }
                else
                {
                    fail++;
                    SetRow(v, "需检查", Color.DarkOrange);
                    Log("    完成但有疑点 · 标签 " + chk.Primaries + "/" + chk.Transfer
                        + " · 元数据 " + (sideLeft ? "有残留" : meta), Color.DarkOrange);
                }
            }

            int doneOk = ok, doneSkip = skip, doneFail = fail;
            bool stopped = _stop;
            this.BeginInvoke(new Action(delegate
            {
                SetRunning(false);
                bar.Value = 0;
                double secs = (DateTime.Now - _jobStart).TotalSeconds;
                string msg = "结束 · 成功 " + doneOk + " · 跳过 " + doneSkip + " · 失败 " + doneFail
                             + " · 总用时 " + FormatTime(secs);
                if (stopped) msg += "（已停止）";
                Log("", Color.Black);
                Log(msg, doneFail > 0 ? Color.Firebrick : Color.Green);
                SetStatus(msg);
                if (chkOpenDone.Checked && doneOk > 0 && !string.IsNullOrEmpty(_lastOutDir)) OpenOutputFolder();
            }));
        }

        private bool EncodeOne(VideoItem v, string outPath, int index, int total)
        {
            bool ok = RunEncode(v, outPath, LpFilter, index, total, false);
            if (!ok && !_stop)
            {
                Log("    libplacebo 失败，回退 zscale + tonemap（慢一些）…", Color.DarkGoldenrod);
                TryDelete(outPath);
                ok = RunEncode(v, outPath, ZsFilter, index, total, true);
            }
            return ok;
        }

        private bool RunEncode(VideoItem v, string outPath, string vf, int index, int total, bool fallback)
        {
            string args = "-hide_banner -y -nostats -loglevel error -progress pipe:1 "
                        + "-i \"" + v.FullPath + "\" "
                        + "-map 0:v:0 -map 0:a? -sn "
                        + "-vf \"" + vf + "\" "
                        + BuildVideoArgs() + " "
                        + BuildAudioArgs(v.ACodec) + " "
                        + "-color_primaries bt709 -color_trc bt709 -colorspace bt709 -color_range tv "
                        + "-movflags +faststart "
                        + "\"" + outPath + "\"";

            StringBuilder err = new StringBuilder();
            double duration = v.Duration;
            double lastPct = 0;
            double speed = 0;
            DateTime lastUi = DateTime.MinValue;

            ProcessStartInfo psi = new ProcessStartInfo(Ff.Exe, args);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;

            int exit = -1;
            try
            {
                using (Process p = new Process())
                {
                    p.StartInfo = psi;
                    p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                    {
                        if (e.Data == null) return;
                        string line = e.Data;
                        if (line.StartsWith("out_time_us=", StringComparison.Ordinal))
                        {
                            string val = line.Substring(12).Trim();
                            double us;
                            if (double.TryParse(val, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out us) && duration > 0.1)
                            {
                                lastPct = Math.Max(0, Math.Min(100, us / 1e6 / duration * 100.0));
                                if ((DateTime.Now - lastUi).TotalMilliseconds > 120)
                                {
                                    lastUi = DateTime.Now;
                                    ReportProgress(index, total, lastPct, speed, duration, us / 1e6);
                                }
                            }
                        }
                        else if (line.StartsWith("speed=", StringComparison.Ordinal))
                        {
                            string val = line.Substring(6).Trim().TrimEnd('x');
                            double sp;
                            if (double.TryParse(val, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out sp)) speed = sp;
                        }
                    };
                    p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                    {
                        if (e.Data == null) return;
                        if (err.Length < 4000) err.AppendLine(e.Data);
                    };
                    p.Start();
                    _proc = p;
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    p.WaitForExit();
                    exit = p.ExitCode;
                    _proc = null;
                }
            }
            catch (Exception ex)
            {
                Log("    启动 ffmpeg 失败：" + ex.Message, Color.Firebrick);
                return false;
            }

            if (_stop) return false;
            if (exit != 0 || !File.Exists(outPath))
            {
                string e = err.ToString().Trim();
                Log("    ffmpeg 退出码 " + exit + (e.Length > 0 ? "：" + e : ""), Color.Firebrick);
                return false;
            }
            return true;
        }

        private void ReportProgress(int index, int total, double pct, double speed, double duration, double done)
        {
            int overall = (int)Math.Round(((index + pct / 100.0) / total) * 100.0);
            string eta = "";
            if (speed > 0.05 && duration > 0)
            {
                double remain = (duration - done) / speed;
                if (remain > 0) eta = " · 剩余 " + FormatTime(remain);
            }
            string text = "转换中 " + (index + 1) + "/" + total + " · " + pct.ToString("0") + "%"
                          + (speed > 0 ? " · " + speed.ToString("0.0") + "x" : "") + eta;
            this.BeginInvoke(new Action(delegate
            {
                bar.Value = Math.Max(0, Math.Min(100, overall));
                SetStatus(text);
            }));
        }

        private void SetRow(VideoItem v, string status, Color color)
        {
            this.BeginInvoke(new Action(delegate
            {
                if (v.Row == null) return;
                v.Status = status;
                v.Row.SubItems[1].Text = status;
                v.Row.ForeColor = color;
                try { v.Row.EnsureVisible(); } catch { }
            }));
        }

        private void SetStatus(string text)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string>(SetStatus), text);
                return;
            }
            lblStatus.Text = text;
        }

        private void UpdateStatus()
        {
            int hdr = 0;
            foreach (ListViewItem it in lv.Items)
            {
                VideoItem v = it.Tag as VideoItem;
                if (v != null && v.IsHdr) hdr++;
            }
            SetStatus("共 " + lv.Items.Count + " 个文件，其中 HDR " + hdr + " 个");
        }

        private void SetRunning(bool running)
        {
            btnStart.Enabled = !running;
            btnStop.Enabled = running;
            btnAddFiles.Enabled = !running;
            btnAddFolder.Enabled = !running;
            btnRemove.Enabled = !running;
            btnClear.Enabled = !running;
            btnFfmpeg.Enabled = !running;
            numCrf.Enabled = !running;
            cbPreset.Enabled = !running;
            cbEncoder.Enabled = !running;
            chkStrip.Enabled = !running;
            chkOverwrite.Enabled = !running;
            chkForce.Enabled = !running;
            txtOutDir.Enabled = !running;
            btnBrowseOut.Enabled = !running;
            if (running) SetStatus("准备中…");
        }

        private void Log(string text, Color color)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string, Color>(Log), text, color);
                return;
            }
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.SelectionLength = 0;
            txtLog.SelectionColor = color;
            txtLog.AppendText(text + "\r\n");
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.ScrollToCaret();
        }

        private void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }

        private static string FormatTime(double seconds)
        {
            if (seconds < 0 || double.IsNaN(seconds)) seconds = 0;
            int s = (int)Math.Round(seconds);
            int h = s / 3600;
            int m = (s % 3600) / 60;
            int ss = s % 60;
            if (h > 0) return h + ":" + m.ToString("00") + ":" + ss.ToString("00");
            return m.ToString("00") + ":" + ss.ToString("00");
        }

        private static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "";
            double mb = bytes / 1024.0 / 1024.0;
            if (mb >= 1024) return (mb / 1024.0).ToString("0.00") + " GB";
            return mb.ToString("0") + " MB";
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (_worker != null && _worker.IsAlive)
            {
                DialogResult r = MessageBox.Show("还在转换中，确定退出？", "确认",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
                _stop = true;
                Process p = _proc;
                if (p != null) { try { p.Kill(); } catch { } }
            }
        }
    }

    // ------------------------------------------------------------------ CLI 模式
    // HDR转SDR.exe --cli <input> [-o <outdir>] [--crf 18] [--preset medium] [--nvenc] [--keepmeta] [--force] [--overwrite]
    internal static class CliMain
    {
        public static int Run(string[] args)
        {
            if (!Ff.Locate())
            {
                Console.Error.WriteLine("找不到 ffmpeg.exe / ffprobe.exe");
                return 2;
            }
            string input = null, outDir = null;
            int crf = 18;
            string preset = "medium";
            bool nvenc = false, keepmeta = false, force = false, overwrite = false;

            for (int i = 1; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "-o" && i + 1 < args.Length) { outDir = args[++i]; }
                else if (a == "--crf" && i + 1 < args.Length) { int.TryParse(args[++i], out crf); }
                else if (a == "--preset" && i + 1 < args.Length) { preset = args[++i]; }
                else if (a == "--nvenc") nvenc = true;
                else if (a == "--keepmeta") keepmeta = true;
                else if (a == "--force") force = true;
                else if (a == "--overwrite") overwrite = true;
                else if (a.StartsWith("-") && a != "--cli" && a != "-cli") { }
                else input = a;
            }

            if (input == null || !File.Exists(input))
            {
                Console.Error.WriteLine("用法: HDR转SDR.exe --cli <视频> [-o 输出目录] [--crf 18] [--preset medium] [--nvenc] [--keepmeta] [--force] [--overwrite]");
                return 2;
            }

            Console.WriteLine("ffmpeg: " + Ff.Exe);
            string full = Path.GetFullPath(input);

            // 队列里只有一个，直接复用 MainForm 的私有逻辑会困难；这里走等价的内联实现
            return ConvertOne(full, outDir, crf, preset, nvenc, keepmeta, force, overwrite);
        }

        private static int ConvertOne(string input, string outDir, int crf, string preset,
            bool nvenc, bool keepmeta, bool force, bool overwrite)
        {
            VideoItem v = new VideoItem();
            v.FullPath = input;
            v.FileName = Path.GetFileName(input);
            Ff.Probe(v);
            if (!v.Probed)
            {
                Console.Error.WriteLine("无法读取视频信息: " + input);
                return 1;
            }
            Console.WriteLine("输入: {0}x{1} · transfer={2} · primaries={3} · pix_fmt={4} · {5:N1}s",
                v.Width, v.Height, v.Transfer, v.Primaries, v.PixFmt, v.Duration);

            if (!v.IsHdr && !force)
            {
                Console.WriteLine("跳过: 不是 HDR" + (v.MaybeHdr ? "（可加 --force）" : ""));
                return 0;
            }

            string dir = (!string.IsNullOrEmpty(outDir)) ? outDir : Path.GetDirectoryName(input);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string outPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(input) + "_SDR.mp4");
            if (File.Exists(outPath) && !overwrite)
            {
                Console.WriteLine("输出已存在，跳过（可加 --overwrite）: " + outPath);
                return 0;
            }

            const string Lp = "libplacebo=tonemapping=bt.2390:colorspace=bt709:color_primaries=bt709:color_trc=bt709:range=tv:format=yuv420p";
            const string Zs = "zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,tonemap=tonemap=hable:desat=0,zscale=t=bt709:m=bt709:r=tv,format=yuv420p";

            string vcodec;
            if (nvenc)
                vcodec = "-c:v h264_nvenc -preset p5 -rc vbr -cq " + crf + " -pix_fmt yuv420p";
            else
                vcodec = "-c:v libx264 -preset " + preset + " -crf " + crf + " -pix_fmt yuv420p";

            string acodec = v.ACodec.Length == 0 ? "-an"
                : (v.ACodec == "aac" || v.ACodec == "mp3" || v.ACodec == "ac3" || v.ACodec == "eac3" || v.ACodec == "alac" || v.ACodec == "mp2"
                    ? "-c:a copy" : "-c:a aac -b:a 192k");

            if (!RunFfmpeg(Lp, vcodec, acodec, input, outPath, v.Duration))
            {
                Console.WriteLine("libplacebo 失败，回退 zscale + tonemap …");
                TryDelete(outPath);
                if (!RunFfmpeg(Zs, vcodec, acodec, input, outPath, v.Duration)) return 1;
            }

            string meta;
            if (keepmeta) meta = "未处理(--keepmeta)";
            else
            {
                try { meta = Mp4Fix.Strip(outPath); }
                catch (Exception ex) { meta = "清理失败:" + ex.Message; }
            }

            VideoItem chk = new VideoItem();
            chk.FullPath = outPath;
            Ff.Probe(chk);
            bool side = Ff.HasHdrSideData(outPath);
            bool ok = chk.Transfer == "bt709" && chk.Primaries == "bt709" && !side;
            long size = 0;
            try { size = new FileInfo(outPath).Length; } catch { }
            Console.WriteLine("输出: {0} · transfer={1} · primaries={2} · 元数据={3} · {4}MB",
                Path.GetFileName(outPath), chk.Transfer, chk.Primaries,
                side ? "有残留!" : meta, Math.Round(size / 1048576.0));
            return ok ? 0 : 3;
        }

        private static bool RunFfmpeg(string vf, string vcodec, string acodec, string input, string outPath, double duration)
        {
            string args = "-hide_banner -y -nostats -loglevel error -progress pipe:1 "
                        + "-i \"" + input + "\" "
                        + "-map 0:v:0 -map 0:a? -sn "
                        + "-vf \"" + vf + "\" " + vcodec + " " + acodec + " "
                        + "-color_primaries bt709 -color_trc bt709 -colorspace bt709 -color_range tv "
                        + "-movflags +faststart \"" + outPath + "\"";
            Console.WriteLine("运行 ffmpeg …");
            StringBuilder err = new StringBuilder();
            DateTime lastUi = DateTime.MinValue;
            ProcessStartInfo psi = new ProcessStartInfo(Ff.Exe, args);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
            try
            {
                using (Process p = new Process())
                {
                    p.StartInfo = psi;
                    p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                    {
                        if (e.Data == null) return;
                        if (e.Data.StartsWith("out_time_us=") && duration > 0.1)
                        {
                            double us;
                            if (double.TryParse(e.Data.Substring(12).Trim(),
                                System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out us))
                            {
                                double pct = Math.Max(0, Math.Min(100, us / 1e6 / duration * 100.0));
                                if ((DateTime.Now - lastUi).TotalMilliseconds > 300)
                                {
                                    lastUi = DateTime.Now;
                                    Console.WriteLine("  进度 {0}%  ({1}s / {2}s)", pct.ToString("0"), (us / 1e6).ToString("0"), duration.ToString("0"));
                                }
                            }
                        }
                    };
                    p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                    {
                        if (e.Data != null && err.Length < 4000) err.AppendLine(e.Data);
                    };
                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    p.WaitForExit();
                    // 进度最后一段可能没进回调，用 WaitForExit 后再等一帧
                    if (p.ExitCode != 0 || !File.Exists(outPath))
                    {
                        string e = err.ToString().Trim();
                        Console.WriteLine("ffmpeg 退出码 " + p.ExitCode + (e.Length > 0 ? ": " + e : ""));
                        return false;
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("启动 ffmpeg 失败: " + ex.Message);
                return false;
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }
    }
}
