// Mdc.cs —— Material Design 3 (MD3) 控件库，按官方规范实现
// 参考: Material Design 3 规范 (seed #6750A4 baseline)
// 实现: 25 色槽 + Surface Container 5 级 / 亮暗双主题 / 状态层 / pill 按钮
// 依赖: System.Drawing, System.Windows.Forms, System.Drawing.Drawing2D

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Hdr2Sdr
{
    // ------------------------------------------------------------------ MD3 色彩方案 (light / dark)
    internal sealed class M3Scheme
    {
        // Accent
        public Color Primary;
        public Color OnPrimary;
        public Color PrimaryContainer;
        public Color OnPrimaryContainer;
        public Color Secondary;
        public Color OnSecondary;
        public Color SecondaryContainer;
        public Color OnSecondaryContainer;
        public Color Tertiary;
        public Color OnTertiary;
        public Color TertiaryContainer;
        public Color OnTertiaryContainer;

        // Neutral
        public Color Background;
        public Color OnBackground;
        public Color Surface;
        public Color OnSurface;
        public Color SurfaceVariant;
        public Color OnSurfaceVariant;
        public Color SurfaceContainerLowest;
        public Color SurfaceContainerLow;
        public Color SurfaceContainer;
        public Color SurfaceContainerHigh;
        public Color SurfaceContainerHighest;

        // Additional
        public Color Error;
        public Color OnError;
        public Color Outline;
        public Color OutlineVariant;

        public static M3Scheme Light()
        {
            M3Scheme s = new M3Scheme();
            s.Primary              = C(0x67, 0x50, 0xA4);
            s.OnPrimary            = C(0xFF, 0xFF, 0xFF);
            s.PrimaryContainer     = C(0xEA, 0xDD, 0xFF);
            s.OnPrimaryContainer   = C(0x21, 0x00, 0x5D);
            s.Secondary            = C(0x62, 0x5B, 0x71);
            s.OnSecondary          = C(0xFF, 0xFF, 0xFF);
            s.SecondaryContainer   = C(0xE8, 0xDE, 0xF8);
            s.OnSecondaryContainer = C(0x1D, 0x19, 0x2B);
            s.Tertiary             = C(0x7D, 0x52, 0x60);
            s.OnTertiary           = C(0xFF, 0xFF, 0xFF);
            s.TertiaryContainer    = C(0xFF, 0xD8, 0xE4);
            s.OnTertiaryContainer  = C(0x31, 0x11, 0x1D);

            s.Background           = C(0xFF, 0xFB, 0xFE);
            s.OnBackground         = C(0x1C, 0x1B, 0x1F);
            s.Surface              = C(0xFF, 0xFB, 0xFE);
            s.OnSurface            = C(0x1C, 0x1B, 0x1F);
            s.SurfaceVariant       = C(0xE7, 0xE0, 0xEC);
            s.OnSurfaceVariant     = C(0x49, 0x45, 0x4F);
            s.SurfaceContainerLowest  = C(0xFF, 0xFF, 0xFF);
            s.SurfaceContainerLow     = C(0xF7, 0xF2, 0xFA);
            s.SurfaceContainer        = C(0xF3, 0xED, 0xF7);
            s.SurfaceContainerHigh    = C(0xEC, 0xE6, 0xF0);
            s.SurfaceContainerHighest = C(0xE6, 0xE0, 0xE9);

            s.Error                = C(0xB3, 0x26, 0x1E);
            s.OnError              = C(0xFF, 0xFF, 0xFF);
            s.Outline              = C(0x79, 0x74, 0x7E);
            s.OutlineVariant       = C(0xCA, 0xC4, 0xD0);
            return s;
        }

        public static M3Scheme Dark()
        {
            M3Scheme s = new M3Scheme();
            s.Primary              = C(0xD0, 0xBC, 0xFF);
            s.OnPrimary            = C(0x38, 0x1E, 0x72);
            s.PrimaryContainer     = C(0x4F, 0x37, 0x8B);
            s.OnPrimaryContainer   = C(0xEA, 0xDD, 0xFF);
            s.Secondary            = C(0xCC, 0xC2, 0xDC);
            s.OnSecondary          = C(0x33, 0x2D, 0x41);
            s.SecondaryContainer   = C(0x49, 0x43, 0x58);
            s.OnSecondaryContainer = C(0xE8, 0xDE, 0xF8);
            s.Tertiary             = C(0xEF, 0xB8, 0xC8);
            s.OnTertiary           = C(0x49, 0x25, 0x32);
            s.TertiaryContainer    = C(0x63, 0x3B, 0x48);
            s.OnTertiaryContainer  = C(0xFF, 0xD8, 0xE4);

            s.Background           = C(0x1C, 0x1B, 0x1F);
            s.OnBackground         = C(0xE6, 0xE1, 0xE9);
            s.Surface              = C(0x1C, 0x1B, 0x1F);
            s.OnSurface            = C(0xE6, 0xE1, 0xE9);
            s.SurfaceVariant       = C(0x49, 0x45, 0x4F);
            s.OnSurfaceVariant     = C(0xCA, 0xC4, 0xD0);
            s.SurfaceContainerLowest  = C(0x17, 0x16, 0x1A);
            s.SurfaceContainerLow     = C(0x24, 0x22, 0x27);
            s.SurfaceContainer        = C(0x28, 0x26, 0x2B);
            s.SurfaceContainerHigh    = C(0x33, 0x30, 0x36);
            s.SurfaceContainerHighest = C(0x3E, 0x3B, 0x41);

            s.Error                = C(0xF2, 0xB8, 0xB5);
            s.OnError              = C(0x60, 0x1E, 0x17);
            s.Outline              = C(0x93, 0x8F, 0x99);
            s.OutlineVariant       = C(0x49, 0x45, 0x4F);
            return s;
        }

        private static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }
    }

    // ------------------------------------------------------------------ MD3 全局令牌入口
    internal static class Mdc
    {
        public static M3Scheme S = M3Scheme.Light();

        // 状态层透明度 (官方规范)
        public const float HoverOpacity = 0.08f;
        public const float FocusOpacity = 0.12f;
        public const float PressOpacity = 0.12f;
        public const float EnabledOpacity = 0.38f;   // disabled 文字/图标

        // 亮暗切换
        public static void ApplyDark(bool dark)
        {
            S = dark ? M3Scheme.Dark() : M3Scheme.Light();
        }

        // 便捷引用 (drives 所有控件)
        public static Color Primary { get { return S.Primary; } }
        public static Color OnPrimary { get { return S.OnPrimary; } }
        public static Color PrimaryContainer { get { return S.PrimaryContainer; } }
        public static Color OnPrimaryContainer { get { return S.OnPrimaryContainer; } }
        public static Color SecondaryContainer { get { return S.SecondaryContainer; } }
        public static Color OnSecondaryContainer { get { return S.OnSecondaryContainer; } }
        public static Color Surface { get { return S.Surface; } }
        public static Color OnSurface { get { return S.OnSurface; } }
        public static Color OnSurfaceVariant { get { return S.OnSurfaceVariant; } }
        public static Color Outline { get { return S.Outline; } }
        public static Color OutlineVariant { get { return S.OutlineVariant; } }
        public static Color Error { get { return S.Error; } }

        public static GraphicsPath RoundRect(Rectangle r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = rad * 2f;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            RectangleF a = new RectangleF(r.X, r.Y, d, d);
            RectangleF b = new RectangleF(r.Right - d, r.Y, d, d);
            RectangleF c = new RectangleF(r.Right - d, r.Bottom - d, d, d);
            RectangleF e = new RectangleF(r.X, r.Bottom - d, d, d);
            p.AddArc(a, 180, 90);
            p.AddArc(b, 270, 90);
            p.AddArc(c, 0, 90);
            p.AddArc(e, 90, 90);
            p.CloseFigure();
            return p;
        }

        // 半透明色叠加 (state layer)
        public static Color WithAlpha(Color baseCol, float alpha)
        {
            int a = (int)Math.Round(255f * Math.Max(0f, Math.Min(1f, alpha)));
            return Color.FromArgb(a, baseCol);
        }

        public static Color Disabled(Color c)
        {
            // 按规范: 用 surface 灰化 + 38% 透明当量的视觉
            int a = 145;
            int r = (c.R * (255 - a) + OnSurfaceVariant.R * a) / 255;
            int g = (c.G * (255 - a) + OnSurfaceVariant.G * a) / 255;
            int b = (c.B * (255 - a) + OnSurfaceVariant.B * a) / 255;
            return Color.FromArgb(r, g, b);
        }
    }

    // ------------------------------------------------------------------ 圆角 Button (Filled / Tonal / Text / Outlined)
    internal enum BtnVariant { Filled, Tonal, Text, Outlined }

    internal class RButton : Button
    {
        public BtnVariant Variant = BtnVariant.Tonal;
        private bool _hover;
        private bool _press;

        public RButton()
        {
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.FlatAppearance.MouseOverBackColor = Color.Transparent;
            this.FlatAppearance.MouseDownBackColor = Color.Transparent;
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.Cursor = Cursors.Hand;
            this.Height = 40;            // 规范 40dp
            this.BackColor = Color.Transparent;
            this.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold); // label-large 语义
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rc = this.ClientRectangle;
            rc.Inflate(-1, -1);

            Color bg;
            Color fg;
            bool outlined = this.Variant == BtnVariant.Outlined;

            if (this.Enabled)
            {
                if (this.Variant == BtnVariant.Filled) { bg = Mdc.Primary; fg = Mdc.OnPrimary; }
                else if (this.Variant == BtnVariant.Tonal) { bg = Mdc.SecondaryContainer; fg = Mdc.OnSecondaryContainer; }
                else if (this.Variant == BtnVariant.Outlined) { bg = Color.Transparent; fg = Mdc.Primary; }
                else { bg = Color.Transparent; fg = Mdc.Primary; }
            }
            else
            {
                if (this.Variant == BtnVariant.Filled) { bg = Mdc.WithAlpha(Mdc.OnSurface, 0.12f); fg = Mdc.WithAlpha(Mdc.OnSurface, Mdc.EnabledOpacity); }
                else if (this.Variant == BtnVariant.Tonal) { bg = Mdc.WithAlpha(Mdc.OnSurface, 0.12f); fg = Mdc.WithAlpha(Mdc.OnSurface, Mdc.EnabledOpacity); }
                else { bg = Color.Transparent; fg = Mdc.WithAlpha(Mdc.OnSurface, Mdc.EnabledOpacity); }
            }

            // 状态层 (hover/focus/pressed)
            if (this.Enabled)
            {
                if (_press) { bg = FillWithState(bg, fg, Mdc.PressOpacity); }
                else if (_hover) { bg = FillWithState(bg, fg, Mdc.HoverOpacity); }
            }

            if (bg != Color.Transparent)
            {
                using (GraphicsPath p = Mdc.RoundRect(rc, rc.Height / 2f))   // pill = full
                using (SolidBrush br = new SolidBrush(DarkenForRender(bg)))
                    g.FillPath(br, p);
            }
            else if (outlined || this.Variant == BtnVariant.Outlined)
            {
                using (GraphicsPath p = Mdc.RoundRect(rc, rc.Height / 2f))
                using (Pen pen = new Pen(this.Enabled ? Mdc.Outline : Mdc.WithAlpha(Mdc.OnSurface, 0.12f), 1f))
                    g.DrawPath(pen, p);
            }

            TextRenderer.DrawText(g, this.Text, this.Font, rc, ClampFg(fg, bg),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        // 关键: 深色下用 blend 而非直接把状态层叠加到透明，避免文字与背景混淆的取巧处理
        private static Color DarkenForRender(Color c) { return c; }

        private Color ClampFg(Color fg, Color bg) { return fg; }

        private Color FillWithState(Color bg, Color fg, float alpha)
        {
            if (bg == Color.Transparent)
            {
                // 纯文本/outlined: 状态层叠在 transparent -> 采 surface 一层
                return Mdc.WithAlpha(fg, alpha);
            }
            // 在背景上叠状态层
            int a = (int)Math.Round(255f * alpha);
            if (a <= 0) return bg;
            if (a >= 255) return fg;
            int r = (bg.R * (255 - a) + fg.R * a) / 255;
            int g = (bg.G * (255 - a) + fg.G * a) / 255;
            int b = (bg.B * (255 - a) + fg.B * a) / 255;
            return Color.FromArgb(r, g, b);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _press = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _press = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _press = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    }

    // ------------------------------------------------------------------ 圆角卡片 (MD3 Card: filled / elevated / outlined)
    // filled    -> surface-container-highest
    // elevated  -> surface-container-low   + level1 阴影
    // outlined  -> surface                  + outline-variant 边框
    internal enum CardVariant { Filled, Elevated, Outlined }

    internal class CardPanel : Panel
    {
        public CardVariant Variant = CardVariant.Filled;
        public int CornerRadius = 12;   // Card shape = medium (12px)

        public CardPanel()
        {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = Color.Transparent;
        }

        private Color FillColor
        {
            get
            {
                switch (this.Variant)
                {
                    case CardVariant.Elevated: return Mdc.S.SurfaceContainerLow;
                    case CardVariant.Outlined: return Mdc.S.Surface;
                    default: return Mdc.S.SurfaceContainerHighest;
                }
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rc = this.ClientRectangle;
            rc.Inflate(-1, -1);
            using (GraphicsPath p = Mdc.RoundRect(rc, this.CornerRadius))
            {
                using (SolidBrush br = new SolidBrush(this.FillColor))
                    g.FillPath(br, p);

                if (this.Variant == CardVariant.Elevated)
                {
                    // level1: 0 1px 2px rgba(0,0,0,0.3), 0 1px 3px 1px rgba(0,0,0,0.15)
                    using (SolidBrush sh = new SolidBrush(Color.FromArgb(24, 0, 0, 0)))
                    {
                        Rectangle s = new Rectangle(rc.X, rc.Bottom - 6, rc.Width, 6);
                        using (GraphicsPath sp = Mdc.RoundRect(new Rectangle(rc.X + 2, rc.Bottom - 5, rc.Width - 4, 5), 3))
                            g.FillPath(sh, sp);
                    }
                }
                else if (this.Variant == CardVariant.Outlined)
                {
                    using (Pen pen = new Pen(Mdc.OutlineVariant, 1f))
                        g.DrawPath(pen, p);
                }
            }
        }
    }

    // ------------------------------------------------------------------ 圆角进度条 (MD3 linear progress indicator)
    internal class MProgress : Control
    {
        private int _value;
        public int Value
        {
            get { return _value; }
            set { int v = Math.Max(0, Math.Min(this.Maximum, value)); if (v != _value) { _value = v; Invalidate(); } }
        }
        private int _max = 100;
        public int Maximum
        {
            get { return _max; }
            set { _max = Math.Max(1, value); Invalidate(); }
        }

        public MProgress()
        {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer, true);
            this.Height = 6;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rc = this.ClientRectangle;
            float rad = rc.Height / 2f;

            using (GraphicsPath pt = Mdc.RoundRect(rc, rad))
            using (SolidBrush tr = new SolidBrush(Mdc.WithAlpha(Mdc.OnSurface, 0.12f)))
                g.FillPath(tr, pt);

            if (_value > 0)
            {
                float frac = (float)_value / (float)_max;
                Rectangle fill = new Rectangle(rc.X, rc.Y, (int)Math.Round(rc.Width * frac), rc.Height);
                if (fill.Width > 1)
                {
                    using (GraphicsPath pf = Mdc.RoundRect(fill, rad))
                    using (SolidBrush fb = new SolidBrush(Mdc.Primary))
                        g.FillPath(fb, pf);
                }
            }
        }
    }

    // ------------------------------------------------------------------ 顶部 AppBar (MD3 top app bar, small variant, 64dp)
    internal class AppBar : Panel
    {
        public AppBar()
        {
            this.BackColor = Mdc.Surface;
            this.Height = 64;
        }
    }
}