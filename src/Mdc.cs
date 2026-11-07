// Mdc.cs —— Material Design 3 (MD3) 控件库
// 为 WinForms 提供：色板 / 圆角按钮(Filled·Tonal·Text) / 圆角进度条 / 圆角面板 / AppBar
// 依赖: System.Drawing, System.Windows.Forms, System.Drawing.Drawing2D

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Hdr2Sdr
{
    // ------------------------------------------------------------------ MD3 色板 (Baseline dynamic light)
    internal static class Mdc
    {
        // 主色
        public static readonly Color Primary = Color.FromArgb(0x67, 0x50, 0xA4);          // #6750A4
        public static readonly Color OnPrimary = Color.FromArgb(0xFF, 0xFF, 0xFF);
        public static readonly Color PrimaryContainer = Color.FromArgb(0xEA, 0xDD, 0xFF); // #EADDFF
        public static readonly Color OnPrimaryContainer = Color.FromArgb(0x21, 0x00, 0x5D);

        // 次级
        public static readonly Color Secondary = Color.FromArgb(0x62, 0x5B, 0x71);
        public static readonly Color SecondaryContainer = Color.FromArgb(0xE8, 0xDE, 0xF8);
        public static readonly Color OnSecondaryContainer = Color.FromArgb(0x1D, 0x19, 0x2B);

        // 表面 / 轮廓
        public static readonly Color Surface = Color.FromArgb(0xFE, 0xF7, 0xFF);         // #FEF7FF
        public static readonly Color SurfaceVariant = Color.FromArgb(0xE7, 0xE0, 0xEC);
        public static readonly Color OnSurface = Color.FromArgb(0x1D, 0x1B, 0x20);
        public static readonly Color OnSurfaceVariant = Color.FromArgb(0x49, 0x45, 0x4F);
        public static readonly Color Outline = Color.FromArgb(0x79, 0x74, 0x7E);

        // 错误 / 成功(自定义扩展)
        public static readonly Color Error = Color.FromArgb(0xB3, 0x26, 0x1E);
        public static readonly Color Success = Color.FromArgb(0x3C, 0x82, 0x5E);

        // hover / press 覆盖层
        public static readonly Color HoverOverlay = Color.FromArgb(20, 0xFF, 0xFF, 0xFF);
        public static readonly Color PressOverlay = Color.FromArgb(40, 0x00, 0x00, 0x00);

        private static GraphicsPath RoundRect(RectangleF r, float rad)
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

        public static GraphicsPath RoundRectP(Rectangle r, float rad)
        {
            return RoundRect(new RectangleF(r.X, r.Y, r.Width - 1, r.Height - 1), rad);
        }

        public static Color Blend(Color baseCol, Color overlay)
        {
            int a = overlay.A;
            if (a <= 0) return baseCol;
            if (a >= 255) return overlay;
            int r = (baseCol.R * (255 - a) + overlay.R * a) / 255;
            int g = (baseCol.G * (255 - a) + overlay.G * a) / 255;
            int b = (baseCol.B * (255 - a) + overlay.B * a) / 255;
            return Color.FromArgb(r, g, b);
        }

        // 禁用态: 原色叠加 60% 表面蓝灰，得到去饱和的灰
        public static Color Disabled(Color c)
        {
            int a = 145;
            int r = (c.R * (255 - a) + Mdc.OnSurfaceVariant.R * a) / 255;
            int g = (c.G * (255 - a) + Mdc.OnSurfaceVariant.G * a) / 255;
            int b = (c.B * (255 - a) + Mdc.OnSurfaceVariant.B * a) / 255;
            return Color.FromArgb(r, g, b);
        }
    }

    // ------------------------------------------------------------------ 圆角 Button (Filled / Tonal / Text)
    internal enum BtnVariant { Filled, Tonal, Text }

    internal class RButton : Button
    {
        public BtnVariant Variant = BtnVariant.Tonal;
        public int CornerRadius = 18;
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
            this.Height = 38;
            this.BackColor = Color.Transparent;
        }

        protected static Color Fg(BtnVariant v)
        {
            if (v == BtnVariant.Filled) return Mdc.OnPrimary;
            if (v == BtnVariant.Tonal) return Mdc.OnSecondaryContainer;
            return Mdc.Primary;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rc = this.ClientRectangle;
            rc.Inflate(-1, -1);

            Color bg;
            Color fg = Fg(this.Variant);

            if (this.Enabled)
            {
                if (this.Variant == BtnVariant.Filled) bg = Mdc.Primary;
                else if (this.Variant == BtnVariant.Tonal) bg = Mdc.SecondaryContainer;
                else bg = Color.Transparent; // Text
            }
            else
            {
                if (this.Variant == BtnVariant.Text) { bg = Color.Transparent; fg = Mdc.Disabled(Mdc.OnSurface); }
                else { bg = Mdc.SurfaceVariant; fg = Mdc.Disabled(Mdc.OnSurface); }
            }

            if (this.Enabled && this.Variant == BtnVariant.Text)
            {
                if (_press) bg = Mdc.Blend(Color.White, Color.FromArgb(24, 0, 0, 0));
                else if (_hover) bg = Mdc.Blend(Color.White, Color.FromArgb(12, 0, 0, 0));
            }

            if (bg != Color.Transparent)
            {
                using (GraphicsPath p = Mdc.RoundRectP(rc, this.CornerRadius))
                {
                    using (SolidBrush br = new SolidBrush(bg)) g.FillPath(br, p);
                }
            }
            if (this.Enabled && _press && this.Variant != BtnVariant.Text)
            {
                using (GraphicsPath p = Mdc.RoundRectP(rc, this.CornerRadius))
                using (SolidBrush br = new SolidBrush(Mdc.PressOverlay)) g.FillPath(br, p);
            }
            else if (this.Enabled && _hover && this.Variant != BtnVariant.Text)
            {
                using (GraphicsPath p = Mdc.RoundRectP(rc, this.CornerRadius))
                using (SolidBrush br = new SolidBrush(Mdc.HoverOverlay)) g.FillPath(br, p);
            }

            TextRenderer.DrawText(g, this.Text, this.Font, rc, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _press = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _press = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _press = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    }

    // ------------------------------------------------------------------ 圆角 Panel (卡片)
    internal class CardPanel : Panel
    {
        public int CornerRadius = 16;
        public Color Fill = Mdc.SurfaceVariant;

        public CardPanel()
        {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = Color.Transparent;
        }

        protected override void OnPaintBackground(PaintEventArgs e) { /* 由 OnPaint 绘制 */ }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rc = this.ClientRectangle;
            rc.Inflate(-1, -1);
            using (GraphicsPath p = Mdc.RoundRectP(rc, this.CornerRadius))
            using (SolidBrush br = new SolidBrush(this.Fill))
            {
                g.FillPath(br, p);
            }
        }
    }

    // ------------------------------------------------------------------ 圆角进度条 (MD3)
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
            Rectangle rc = new Rectangle(this.ClientRectangle.X, this.ClientRectangle.Y,
                this.ClientRectangle.Width, this.ClientRectangle.Height);
            float rad = rc.Height / 2f;

            using (GraphicsPath pt = Mdc.RoundRectP(rc, rad))
            using (SolidBrush tr = new SolidBrush(Mdc.Blend(Mdc.SurfaceVariant, Color.FromArgb(60, 60, 60))))
            {
                g.FillPath(tr, pt);
            }

            if (_value > 0)
            {
                float frac = (float)_value / (float)_max;
                Rectangle fill = new Rectangle(rc.X, rc.Y, (int)Math.Round(rc.Width * frac), rc.Height);
                if (fill.Width > 1)
                {
                    using (GraphicsPath pf = Mdc.RoundRectP(fill, rad))
                    using (SolidBrush fb = new SolidBrush(Mdc.Primary))
                    {
                        g.FillPath(fb, pf);
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------------ 顶部 AppBar (MD3 top app bar)
    internal class AppBar : Panel
    {
        public AppBar()
        {
            this.BackColor = Mdc.Primary;
            this.Height = 48;
        }
    }
}