using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace DNSParty.UI
{
    public static class Theme
    {
        public static bool DarkMode { get; set; } = true;

        // Window & Layout Backgrounds
        public static Color WindowBackground
        {
            get { return DarkMode ? Color.FromArgb(12, 13, 24) : Color.FromArgb(245, 247, 252); } // #0C0D18
        }

        public static Color WindowBorder
        {
            get { return DarkMode ? Color.FromArgb(35, 39, 62) : Color.FromArgb(203, 213, 225); } // #23273E
        }

        public static Color SidebarBackground
        {
            get { return DarkMode ? Color.FromArgb(9, 10, 20) : Color.FromArgb(236, 240, 248); } // #090A14
        }

        public static Color SidebarBorder
        {
            get { return DarkMode ? Color.FromArgb(24, 27, 44) : Color.FromArgb(215, 222, 234); }
        }

        public static Color TitleBarBackground
        {
            get { return DarkMode ? Color.FromArgb(10, 11, 21) : Color.FromArgb(238, 242, 250); }
        }

        // Active Sidebar Pill Gradient
        public static Color SidebarActiveStart
        {
            get { return Color.FromArgb(59, 38, 126); } // #3B267E
        }

        public static Color SidebarActiveEnd
        {
            get { return Color.FromArgb(85, 50, 168); } // #5532A8
        }

        public static Color SidebarActiveBorder
        {
            get { return Color.FromArgb(124, 77, 255); } // #7C4DFF
        }

        public static Color SidebarHover
        {
            get { return DarkMode ? Color.FromArgb(22, 25, 42) : Color.FromArgb(224, 231, 245); }
        }

        // Cards (DNS Profile Cards, Quick Actions Container, etc.)
        public static Color CardBackground
        {
            get { return DarkMode ? Color.FromArgb(20, 23, 38) : Color.FromArgb(255, 255, 255); } // #141726
        }

        public static Color CardHoverBackground
        {
            get { return DarkMode ? Color.FromArgb(26, 30, 50) : Color.FromArgb(248, 250, 255); } // #1A1E32
        }

        public static Color CardBorder
        {
            get { return DarkMode ? Color.FromArgb(35, 39, 62) : Color.FromArgb(218, 224, 236); } // #23273E
        }

        public static Color CardHoverBorder
        {
            get { return DarkMode ? Color.FromArgb(56, 62, 96) : Color.FromArgb(180, 192, 216); }
        }

        // Active DNS Card
        public static Color ActiveCardStart
        {
            get { return DarkMode ? Color.FromArgb(29, 27, 56) : Color.FromArgb(237, 233, 254); } // #1D1B38
        }

        public static Color ActiveCardEnd
        {
            get { return DarkMode ? Color.FromArgb(37, 31, 71) : Color.FromArgb(221, 214, 254); } // #251F47
        }

        public static Color ActiveCardBorder
        {
            get { return Color.FromArgb(139, 92, 246); } // #8B5CF6
        }

        public static Color ActiveCardGlow
        {
            get { return Color.FromArgb(65, 139, 92, 246); }
        }

        // Right-hand "DNS Active" Status Card
        public static Color StatusHeaderActiveStart
        {
            get { return DarkMode ? Color.FromArgb(14, 41, 38) : Color.FromArgb(209, 250, 229); } // #0E2926
        }

        public static Color StatusHeaderActiveEnd
        {
            get { return DarkMode ? Color.FromArgb(18, 23, 40) : Color.FromArgb(236, 253, 245); } // #121728
        }

        public static Color StatusHeaderActiveBorder
        {
            get { return DarkMode ? Color.FromArgb(30, 78, 66) : Color.FromArgb(110, 231, 183); } // #1E4E42
        }

        public static Color StatusGreen
        {
            get { return Color.FromArgb(34, 197, 94); } // #22C55E
        }

        public static Color StatusEmerald
        {
            get { return Color.FromArgb(16, 185, 129); } // #10B981
        }

        public static Color StatusWarning
        {
            get { return Color.FromArgb(245, 158, 11); } // #F59E0B
        }

        // Primary Neon Purple Button
        public static Color PrimaryButtonStart
        {
            get { return Color.FromArgb(109, 56, 232); } // #6D38E8
        }

        public static Color PrimaryButtonEnd
        {
            get { return Color.FromArgb(124, 58, 237); } // #7C3AED
        }

        public static Color PrimaryButtonHover
        {
            get { return Color.FromArgb(139, 92, 246); } // #8B5CF6
        }

        public static Color PrimaryButtonBorder
        {
            get { return Color.FromArgb(167, 139, 250); } // #A78BFA
        }

        // Secondary Dark Pill Button
        public static Color SecondaryButtonBackground
        {
            get { return DarkMode ? Color.FromArgb(23, 26, 43) : Color.FromArgb(241, 245, 249); } // #171A2B
        }

        public static Color SecondaryButtonHover
        {
            get { return DarkMode ? Color.FromArgb(32, 37, 61) : Color.FromArgb(226, 232, 240); } // #20253D
        }

        public static Color SecondaryButtonBorder
        {
            get { return DarkMode ? Color.FromArgb(39, 43, 66) : Color.FromArgb(203, 213, 225); } // #272B42
        }

        // Input Box Colors
        public static Color InputBackground
        {
            get { return DarkMode ? Color.FromArgb(16, 18, 32) : Color.FromArgb(255, 255, 255); }
        }

        public static Color InputBorder
        {
            get { return DarkMode ? Color.FromArgb(42, 47, 74) : Color.FromArgb(203, 213, 225); }
        }

        public static Color InputFocusBorder
        {
            get { return Color.FromArgb(139, 92, 246); } // #8B5CF6
        }

        // Typography & Accents
        public static Color TextPrimary
        {
            get { return DarkMode ? Color.FromArgb(248, 250, 252) : Color.FromArgb(15, 23, 42); } // #F8FAFC
        }

        public static Color TextSecondary
        {
            get { return DarkMode ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139); } // #94A3B8
        }

        public static Color TextMuted
        {
            get { return DarkMode ? Color.FromArgb(100, 116, 139) : Color.FromArgb(148, 163, 184); } // #64748B
        }

        public static Color TextTagPurple
        {
            get { return DarkMode ? Color.FromArgb(167, 139, 250) : Color.FromArgb(109, 40, 217); } // #A78BFA
        }

        public static Color TextTagNeutral
        {
            get { return DarkMode ? Color.FromArgb(203, 213, 225) : Color.FromArgb(71, 85, 105); } // #CBD5E1
        }

        public static Color StarGold
        {
            get { return Color.FromArgb(251, 191, 36); } // #FBBF24
        }

        public static Color StarOutline
        {
            get { return DarkMode ? Color.FromArgb(148, 163, 184) : Color.FromArgb(148, 163, 184); } // #94A3B8
        }

        public static Color AccentViolet
        {
            get { return Color.FromArgb(167, 139, 250); } // #A78BFA
        }

        public static Color DangerRed
        {
            get { return Color.FromArgb(239, 68, 68); } // #EF4444
        }

        public static Color DangerBannerBackground
        {
            get { return DarkMode ? Color.FromArgb(45, 18, 28) : Color.FromArgb(254, 226, 226); }
        }

        public static Color DangerBannerBorder
        {
            get { return DarkMode ? Color.FromArgb(127, 29, 29) : Color.FromArgb(248, 113, 113); }
        }

        // High-DPI GDI+ Helpers
        public static void ConfigureHighQuality(Graphics g)
        {
            if (g == null) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }

        public static GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
        {
            return CreateRoundedRectPath(new RectangleF(rect.X, rect.Y, rect.Width, rect.Height), radius);
        }

        public static GraphicsPath CreateRoundedRectPath(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return path;
            }

            if (radius <= 0f)
            {
                path.AddRectangle(rect);
                path.CloseFigure();
                return path;
            }

            float diameter = radius * 2f;
            if (diameter > rect.Width) diameter = rect.Width;
            if (diameter > rect.Height) diameter = rect.Height;

            RectangleF arc = new RectangleF(rect.Location, new SizeF(diameter, diameter));

            // Top-left
            path.AddArc(arc, 180, 90);

            // Top-right
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);

            // Bottom-right
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // Bottom-left
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        public static void FillRoundedRect(Graphics g, Brush brush, Rectangle rect, int radius)
        {
            FillRoundedRect(g, brush, new RectangleF(rect.X, rect.Y, rect.Width, rect.Height), radius);
        }

        public static void FillRoundedRect(Graphics g, Brush brush, RectangleF rect, float radius)
        {
            if (g == null || brush == null || rect.Width <= 0 || rect.Height <= 0) return;
            using (GraphicsPath path = CreateRoundedRectPath(rect, radius))
            {
                SmoothingMode prevSmooth = g.SmoothingMode;
                PixelOffsetMode prevOffset = g.PixelOffsetMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.FillPath(brush, path);
                g.SmoothingMode = prevSmooth;
                g.PixelOffsetMode = prevOffset;
            }
        }

        public static void FillRoundedGradient(Graphics g, Rectangle rect, int radius, Color startColor, Color endColor, LinearGradientMode mode = LinearGradientMode.Horizontal)
        {
            if (g == null || rect.Width <= 0 || rect.Height <= 0) return;
            using (LinearGradientBrush brush = new LinearGradientBrush(rect, startColor, endColor, mode))
            {
                FillRoundedRect(g, brush, rect, radius);
            }
        }

        public static void DrawRoundedRect(Graphics g, Pen pen, Rectangle rect, int radius)
        {
            DrawRoundedRect(g, pen, new RectangleF(rect.X, rect.Y, rect.Width, rect.Height), radius);
        }

        public static void DrawRoundedRect(Graphics g, Pen pen, RectangleF rect, float radius)
        {
            if (g == null || pen == null || rect.Width <= 0 || rect.Height <= 0) return;
            using (GraphicsPath path = CreateRoundedRectPath(rect, radius))
            {
                SmoothingMode prevSmooth = g.SmoothingMode;
                PixelOffsetMode prevOffset = g.PixelOffsetMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawPath(pen, path);
                g.SmoothingMode = prevSmooth;
                g.PixelOffsetMode = prevOffset;
            }
        }

        // Font Helpers (DPI-aware Point fonts with fallback)
        public static Font CreateFont(float sizePt, FontStyle style = FontStyle.Regular)
        {
            try
            {
                if (style == FontStyle.Bold)
                {
                    return new Font("Segoe UI Semibold", sizePt, FontStyle.Bold, GraphicsUnit.Point);
                }
                return new Font("Segoe UI", sizePt, style, GraphicsUnit.Point);
            }
            catch
            {
                return new Font(SystemFonts.DefaultFont.FontFamily, sizePt, style, GraphicsUnit.Point);
            }
        }

        public static Font CreateSemiboldFont(float sizePt)
        {
            try
            {
                return new Font("Segoe UI Semibold", sizePt, FontStyle.Regular, GraphicsUnit.Point);
            }
            catch
            {
                return CreateFont(sizePt, FontStyle.Bold);
            }
        }

        public static Font CreateBoldFont(float sizePt)
        {
            try
            {
                return new Font("Segoe UI", sizePt, FontStyle.Bold, GraphicsUnit.Point);
            }
            catch
            {
                return new Font(SystemFonts.DefaultFont.FontFamily, sizePt, FontStyle.Bold, GraphicsUnit.Point);
            }
        }

        public static Font CreateMonoFont(float sizePt, FontStyle style = FontStyle.Regular)
        {
            try
            {
                return new Font("Consolas", sizePt, style, GraphicsUnit.Point);
            }
            catch
            {
                return CreateFont(sizePt, style);
            }
        }
    }
}
