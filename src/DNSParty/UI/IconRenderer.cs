using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;

namespace DNSParty.UI
{
    public static class IconRenderer
    {
        private static Icon _cachedAppIcon;
        private static Icon _cachedTrayIcon;
        private static Bitmap _cachedTransparentLogo;
        private static Bitmap _cachedOverlayBadge;
        private static readonly Dictionary<string, Icon> _trayStateIconCache =
            new Dictionary<string, Icon>(StringComparer.OrdinalIgnoreCase);

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        private static string[] GetLogoPackSearchDirs()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return new string[]
            {
                Path.Combine(baseDir, "assets", "new_logos", "DNS-Party-Logo-Pack"),
                Path.Combine(baseDir, "..", "..", "assets", "new_logos", "DNS-Party-Logo-Pack"),
                Path.Combine(baseDir, "..", "..", "..", "assets", "new_logos", "DNS-Party-Logo-Pack"),
                baseDir
            };
        }

        private static Bitmap LoadBitmapFromResourceOrDisk(string relativePath, string resourceSuffix)
        {
            try
            {
                foreach (string root in GetLogoPackSearchDirs())
                {
                    string candidate = Path.Combine(root, relativePath);
                    if (File.Exists(candidate))
                    {
                        using (FileStream fs = new FileStream(candidate, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (Image img = Image.FromStream(fs))
                        {
                            Bitmap bmp = new Bitmap(img.Width, img.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                            using (Graphics g = Graphics.FromImage(bmp))
                            {
                                g.Clear(Color.Transparent);
                                g.DrawImage(img, new Rectangle(0, 0, img.Width, img.Height));
                            }
                            return bmp;
                        }
                    }
                }

                Assembly asm = Assembly.GetExecutingAssembly();
                foreach (string resName in asm.GetManifestResourceNames())
                {
                    if (resName.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase))
                    {
                        using (Stream s = asm.GetManifestResourceStream(resName))
                        {
                            if (s != null)
                            {
                                using (Image img = Image.FromStream(s))
                                {
                                    Bitmap bmp = new Bitmap(img.Width, img.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                                    using (Graphics g = Graphics.FromImage(bmp))
                                    {
                                        g.Clear(Color.Transparent);
                                        g.DrawImage(img, new Rectangle(0, 0, img.Width, img.Height));
                                    }
                                    return bmp;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        /// <summary>
        /// Loads the primary multi-resolution Windows application Icon (Icons/app.ico)
        /// from the new DNS-Party-Logo-Pack on disk or embedded resource.
        /// </summary>
        public static Icon GetAppIcon()
        {
            if (_cachedAppIcon != null)
            {
                return _cachedAppIcon;
            }

            try
            {
                foreach (string root in GetLogoPackSearchDirs())
                {
                    string[] candidates = new string[]
                    {
                        Path.Combine(root, "Icons", "app.ico"),
                        Path.Combine(root, "app.ico")
                    };

                    foreach (string candidate in candidates)
                    {
                        if (File.Exists(candidate))
                        {
                            _cachedAppIcon = new Icon(candidate);
                            return _cachedAppIcon;
                        }
                    }
                }

                Assembly asm = Assembly.GetExecutingAssembly();
                foreach (string resName in asm.GetManifestResourceNames())
                {
                    if (resName.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                    {
                        using (Stream s = asm.GetManifestResourceStream(resName))
                        {
                            if (s != null)
                            {
                                _cachedAppIcon = new Icon(s);
                                return _cachedAppIcon;
                            }
                        }
                    }
                }

                Icon exeIcon = Icon.ExtractAssociatedIcon(asm.Location);
                if (exeIcon != null)
                {
                    _cachedAppIcon = exeIcon;
                    return _cachedAppIcon;
                }
            }
            catch
            {
                // Ignore and fall back below
            }

            return SystemIcons.Application;
        }

        /// <summary>
        /// Extracts the System Tray icon from the new DNS-Party-Logo-Pack Tray state assets or app.ico.
        /// </summary>
        public static Icon GetTrayIcon(string state = "default")
        {
            string normalizedState = string.IsNullOrWhiteSpace(state) ? "default" : state.Trim().ToLowerInvariant();
            if (_trayStateIconCache.ContainsKey(normalizedState) && _trayStateIconCache[normalizedState] != null)
            {
                return _trayStateIconCache[normalizedState];
            }

            if (normalizedState == "default" && _cachedTrayIcon != null)
            {
                return _cachedTrayIcon;
            }

            try
            {
                Size smallSize = System.Windows.Forms.SystemInformation.SmallIconSize;
                int targetPx = smallSize.Width > 0 ? smallSize.Width : 16;
                int[] trayTiers = new int[] { 16, 20, 24, 32, 48 };
                int chosenTier = 16;
                foreach (int t in trayTiers)
                {
                    if (t >= targetPx)
                    {
                        chosenTier = t;
                        break;
                    }
                    chosenTier = t;
                }

                string trayFileName = string.Format("tray-{0}-{1}.png", normalizedState, chosenTier);
                Bitmap trayBmp = LoadBitmapFromResourceOrDisk(Path.Combine("Tray", trayFileName), trayFileName)
                              ?? LoadBitmapFromResourceOrDisk(Path.Combine("Tray", string.Format("tray-{0}-32.png", normalizedState)), string.Format("tray-{0}-32.png", normalizedState));

                if (trayBmp != null)
                {
                    IntPtr hIcon = trayBmp.GetHicon();
                    using (Icon tempIcon = Icon.FromHandle(hIcon))
                    {
                        Icon cloned = (Icon)tempIcon.Clone();
                        DestroyIcon(hIcon);
                        _trayStateIconCache[normalizedState] = cloned;
                        if (normalizedState == "default")
                        {
                            _cachedTrayIcon = cloned;
                        }
                        return cloned;
                    }
                }

                foreach (string root in GetLogoPackSearchDirs())
                {
                    string icoCandidate = Path.Combine(root, "Icons", "app.ico");
                    if (File.Exists(icoCandidate))
                    {
                        Icon sizedIco = new Icon(icoCandidate, smallSize);
                        _trayStateIconCache[normalizedState] = sizedIco;
                        if (normalizedState == "default") _cachedTrayIcon = sizedIco;
                        return sizedIco;
                    }
                }

                Assembly asm = Assembly.GetExecutingAssembly();
                foreach (string resName in asm.GetManifestResourceNames())
                {
                    if (resName.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                    {
                        using (Stream s = asm.GetManifestResourceStream(resName))
                        {
                            if (s != null)
                            {
                                Icon sizedIco = new Icon(s, smallSize);
                                _trayStateIconCache[normalizedState] = sizedIco;
                                if (normalizedState == "default") _cachedTrayIcon = sizedIco;
                                return sizedIco;
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return GetAppIcon();
        }

        private static Bitmap GetMasterTransparentLogo()
        {
            if (_cachedTransparentLogo != null)
            {
                return _cachedTransparentLogo;
            }

            _cachedTransparentLogo =
                LoadBitmapFromResourceOrDisk(Path.Combine("Overlay", "overlay-badge-256.png"), "overlay-badge-256.png") ??
                LoadBitmapFromResourceOrDisk(Path.Combine("Installer", "installer-logo-512.png"), "installer-logo-512.png") ??
                LoadBitmapFromResourceOrDisk(Path.Combine("Branding", "logo-transparent.png"), "logo-transparent.png");

            return _cachedTransparentLogo;
        }

        public static Bitmap GetOverlayBadgeBitmap()
        {
            if (_cachedOverlayBadge != null)
            {
                return _cachedOverlayBadge;
            }

            _cachedOverlayBadge =
                LoadBitmapFromResourceOrDisk(Path.Combine("Overlay", "overlay-badge-256.png"), "overlay-badge-256.png") ??
                GetMasterTransparentLogo();

            return _cachedOverlayBadge;
        }

        /// <summary>
        /// Returns a Bitmap of the new DNS Party transparent PNG logo at the requested size.
        /// Preserves transparent corners and full center artwork.
        /// </summary>
        public static Bitmap GetAppLogoBitmap(int size = 32)
        {
            if (size <= 0) size = 32;
            Bitmap source = GetMasterTransparentLogo();

            Bitmap scaled = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(scaled))
            {
                Theme.ConfigureHighQuality(g);
                g.Clear(Color.Transparent);
                if (source != null)
                {
                    g.DrawImage(source, new Rectangle(0, 0, size, size));
                }
                else
                {
                    Icon ico = GetAppIcon();
                    if (ico != null)
                    {
                        using (Bitmap icoBmp = ico.ToBitmap())
                        {
                            g.DrawImage(icoBmp, new Rectangle(2, 2, size - 4, size - 4));
                        }
                    }
                }
            }
            return scaled;
        }

        // Coordinate helper mapping 0..24 SVG viewBox into target RectangleF
        private static PointF Map(RectangleF bounds, float vx, float vy)
        {
            return new PointF(
                bounds.X + (vx / 24f) * bounds.Width,
                bounds.Y + (vy / 24f) * bounds.Height);
        }

        private static RectangleF MapRect(RectangleF bounds, float vx, float vy, float vw, float vh)
        {
            return new RectangleF(
                bounds.X + (vx / 24f) * bounds.Width,
                bounds.Y + (vy / 24f) * bounds.Height,
                (vw / 24f) * bounds.Width,
                (vh / 24f) * bounds.Height);
        }

        private static Pen CreateVectorPen(Color color, RectangleF bounds, float strokeWidth = 1.8f)
        {
            float scale = Math.Min(bounds.Width, bounds.Height) / 24f;
            float width = Math.Max(1.35f, strokeWidth * scale);
            Pen pen = new Pen(color, width);
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            return pen;
        }

        /// <summary>
        /// Renders the new DNS Party transparent PNG artwork at the appropriate resolution
        /// while preserving aspect ratio and transparency.
        /// </summary>
        public static void DrawAppLogo(Graphics g, RectangleF bounds, bool filled = true)
        {
            if (g == null || bounds.Width <= 0f || bounds.Height <= 0f) return;

            float side = Math.Min(bounds.Width, bounds.Height);
            RectangleF destRect = new RectangleF(
                bounds.X + (bounds.Width - side) / 2f,
                bounds.Y + (bounds.Height - side) / 2f,
                side,
                side);

            Bitmap logoBmp = GetMasterTransparentLogo();
            if (logoBmp != null)
            {
                SmoothingMode prevSmooth = g.SmoothingMode;
                InterpolationMode prevInterp = g.InterpolationMode;
                PixelOffsetMode prevOffset = g.PixelOffsetMode;

                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                // Trim outer empty padding (~8% per side) so the globe/bolt/controller emblem fills bounds crisply
                float insetX = logoBmp.Width * 0.08f;
                float insetY = logoBmp.Height * 0.08f;
                RectangleF srcRect = new RectangleF(
                    insetX,
                    insetY,
                    logoBmp.Width - insetX * 2f,
                    logoBmp.Height - insetY * 2f);

                g.DrawImage(logoBmp, destRect, srcRect, GraphicsUnit.Pixel);

                g.SmoothingMode = prevSmooth;
                g.InterpolationMode = prevInterp;
                g.PixelOffsetMode = prevOffset;
            }
        }

        /// <summary>
        /// Renders the bottom-left audio wave / party pulse icon (`|i|`) shown in ui.png footer.
        /// </summary>
        public static void DrawAudioWaveIcon(Graphics g, RectangleF bounds, Color color)
        {
            Theme.ConfigureHighQuality(g);
            using (Pen pen = CreateVectorPen(color, bounds, 2.0f))
            using (SolidBrush brush = new SolidBrush(color))
            {
                float dotR = Math.Max(1.2f, bounds.Width * 0.06f);

                // Far-left tiny dot
                PointF p1 = Map(bounds, 3.5f, 12f);
                g.FillEllipse(brush, p1.X - dotR, p1.Y - dotR, dotR * 2f, dotR * 2f);

                // Left short bar
                g.DrawLine(pen, Map(bounds, 7.5f, 8.5f), Map(bounds, 7.5f, 15.5f));

                // Center tall bar
                g.DrawLine(pen, Map(bounds, 12f, 4.5f), Map(bounds, 12f, 19.5f));

                // Right short bar
                g.DrawLine(pen, Map(bounds, 16.5f, 8.5f), Map(bounds, 16.5f, 15.5f));

                // Far-right tiny dot
                PointF p5 = Map(bounds, 20.5f, 12f);
                g.FillEllipse(brush, p5.X - dotR, p5.Y - dotR, dotR * 2f, dotR * 2f);
            }
        }

        /// <summary>
        /// Renders any named icon from the DNS Party icon pack (`assets/icon_pack/`) plus overlay glyphs.
        /// </summary>
        public static void DrawIcon(Graphics g, string iconName, RectangleF bounds, Color color, bool filled = false)
        {
            if (g == null || string.IsNullOrEmpty(iconName)) return;
            Theme.ConfigureHighQuality(g);

            string key = iconName.Trim().ToLowerInvariant();
            switch (key)
            {
                case "app-logo":
                case "logo":
                    DrawAppLogo(g, bounds, filled);
                    return;

                case "wave":
                case "party":
                case "audio-wave":
                    DrawAudioWaveIcon(g, bounds, color);
                    return;

                case "star":
                    DrawStar(g, bounds, color, filled);
                    return;
            }

            using (Pen pen = CreateVectorPen(color, bounds, 1.85f))
            using (SolidBrush brush = new SolidBrush(color))
            {
                switch (key)
                {
                    case "home":
                        g.DrawLines(pen, new PointF[]
                        {
                            Map(bounds, 3f, 10f),
                            Map(bounds, 12f, 3f),
                            Map(bounds, 21f, 10f)
                        });
                        g.DrawLines(pen, new PointF[]
                        {
                            Map(bounds, 5f, 9.2f),
                            Map(bounds, 5f, 21f),
                            Map(bounds, 19f, 21f),
                            Map(bounds, 19f, 9.2f)
                        });
                        g.DrawLines(pen, new PointF[]
                        {
                            Map(bounds, 9.2f, 21f),
                            Map(bounds, 9.2f, 14f),
                            Map(bounds, 14.8f, 14f),
                            Map(bounds, 14.8f, 21f)
                        });
                        break;

                    case "gamepad":
                    case "controller":
                        DrawGamepadIcon(g, bounds, pen, brush);
                        break;

                    case "play":
                        PointF[] playPts = new PointF[]
                        {
                            Map(bounds, 8.2f, 5.5f),
                            Map(bounds, 18.5f, 12.0f),
                            Map(bounds, 8.2f, 18.5f)
                        };
                        if (filled)
                        {
                            g.FillPolygon(brush, playPts);
                        }
                        g.DrawPolygon(pen, playPts);
                        break;

                    case "plus":
                        g.DrawLine(pen, Map(bounds, 12f, 5f), Map(bounds, 12f, 19f));
                        g.DrawLine(pen, Map(bounds, 5f, 12f), Map(bounds, 19f, 12f));
                        break;

                    case "check":
                        g.DrawLines(pen, new PointF[]
                        {
                            Map(bounds, 5.5f, 12.2f),
                            Map(bounds, 9.8f, 16.5f),
                            Map(bounds, 18.8f, 6.8f)
                        });
                        break;

                    case "cloud":
                        DrawCloudOutline(g, bounds, pen, null);
                        break;

                    case "disable":
                        g.DrawPolygon(pen, new PointF[]
                        {
                            Map(bounds, 8f, 5f),
                            Map(bounds, 19f, 12f),
                            Map(bounds, 8f, 19f)
                        });
                        g.DrawLine(pen, Map(bounds, 4.5f, 4.5f), Map(bounds, 19.5f, 19.5f));
                        break;

                    case "disable-circle":
                        RectangleF dcCircle = MapRect(bounds, 2.5f, 2.5f, 19f, 19f);
                        g.DrawEllipse(pen, dcCircle);
                        g.DrawPolygon(pen, new PointF[]
                        {
                            Map(bounds, 9.5f, 7.5f),
                            Map(bounds, 16.8f, 12f),
                            Map(bounds, 9.5f, 16.5f)
                        });
                        g.DrawLine(pen, Map(bounds, 5.5f, 5.5f), Map(bounds, 18.5f, 18.5f));
                        break;

                    case "edit":
                        g.DrawPolygon(pen, new PointF[]
                        {
                            Map(bounds, 4.2f, 15.8f),
                            Map(bounds, 3.3f, 20.7f),
                            Map(bounds, 8.2f, 19.8f),
                            Map(bounds, 19.2f, 8.8f),
                            Map(bounds, 15.2f, 4.8f)
                        });
                        g.DrawLine(pen, Map(bounds, 13.2f, 6.8f), Map(bounds, 17.2f, 10.8f));
                        break;

                    case "flush":
                        RectangleF arcRect = MapRect(bounds, 4f, 4f, 16f, 16f);
                        g.DrawArc(pen, arcRect, 15f, 300f);
                        g.DrawLines(pen, new PointF[]
                        {
                            Map(bounds, 20f, 4.8f),
                            Map(bounds, 20f, 9.6f),
                            Map(bounds, 15.2f, 9.6f)
                        });
                        break;

                    case "info":
                    case "about":
                        RectangleF infoCircle = MapRect(bounds, 3f, 3f, 18f, 18f);
                        g.DrawEllipse(pen, infoCircle);
                        g.DrawLine(pen, Map(bounds, 12f, 11.2f), Map(bounds, 12f, 16.5f));
                        PointF dotPt = Map(bounds, 12f, 7.6f);
                        float rDot = Math.Max(1.2f, pen.Width * 0.7f);
                        g.FillEllipse(brush, dotPt.X - rDot, dotPt.Y - rDot, rDot * 2f, rDot * 2f);
                        break;

                    case "clock":
                    case "status-clock":
                        RectangleF clockCircle = MapRect(bounds, 3f, 3f, 18f, 18f);
                        g.DrawEllipse(pen, clockCircle);
                        g.DrawLines(pen, new PointF[]
                        {
                            Map(bounds, 12f, 7.5f),
                            Map(bounds, 12f, 12.2f),
                            Map(bounds, 15.2f, 14.0f)
                        });
                        break;

                    case "more-vertical":
                        float dotRadius = Math.Max(1.3f, bounds.Width * 0.06f);
                        float[] cyList = new float[] { 5.5f, 12f, 18.5f };
                        foreach (float cy in cyList)
                        {
                            PointF pt = Map(bounds, 12f, cy);
                            g.FillEllipse(brush, pt.X - dotRadius, pt.Y - dotRadius, dotRadius * 2f, dotRadius * 2f);
                        }
                        break;

                    case "power":
                        g.DrawLine(pen, Map(bounds, 12f, 2.8f), Map(bounds, 12f, 11.8f));
                        RectangleF pwrArc = MapRect(bounds, 4f, 4.5f, 16f, 16f);
                        g.DrawArc(pen, pwrArc, -50f, 280f);
                        break;

                    case "server":
                        DrawServerRackIcon(g, bounds, pen, brush);
                        break;

                    case "settings":
                        DrawSettingsGear(g, bounds, pen);
                        break;

                    case "test":
                        g.DrawLines(pen, new PointF[]
                        {
                            Map(bounds, 2.5f, 12f),
                            Map(bounds, 6.2f, 12f),
                            Map(bounds, 8.5f, 5.5f),
                            Map(bounds, 12.2f, 18.5f),
                            Map(bounds, 15.2f, 9.5f),
                            Map(bounds, 17.2f, 12f),
                            Map(bounds, 21.5f, 12f)
                        });
                        break;

                    case "trash":
                    case "delete":
                        g.DrawLine(pen, Map(bounds, 4f, 6.8f), Map(bounds, 20f, 6.8f));
                        g.DrawLines(pen, new PointF[]
                        {
                            Map(bounds, 9f, 6.8f),
                            Map(bounds, 9f, 4f),
                            Map(bounds, 15f, 4f),
                            Map(bounds, 15f, 6.8f)
                        });
                        g.DrawLines(pen, new PointF[]
                        {
                            Map(bounds, 6.2f, 6.8f),
                            Map(bounds, 7.2f, 20f),
                            Map(bounds, 16.8f, 20f),
                            Map(bounds, 17.8f, 6.8f)
                        });
                        g.DrawLine(pen, Map(bounds, 10.2f, 10.8f), Map(bounds, 10.2f, 16.5f));
                        g.DrawLine(pen, Map(bounds, 13.8f, 10.8f), Map(bounds, 13.8f, 16.5f));
                        break;

                    case "volume":
                        g.DrawPolygon(pen, new PointF[]
                        {
                            Map(bounds, 4f, 9.8f),
                            Map(bounds, 4f, 14.2f),
                            Map(bounds, 8f, 14.2f),
                            Map(bounds, 13f, 18.2f),
                            Map(bounds, 13f, 5.8f),
                            Map(bounds, 8f, 9.8f)
                        });
                        g.DrawArc(pen, MapRect(bounds, 12.5f, 8.2f, 6f, 7.6f), -55f, 110f);
                        g.DrawArc(pen, MapRect(bounds, 13.5f, 5.2f, 9f, 13.6f), -55f, 110f);
                        break;

                    case "wifi":
                        g.DrawArc(pen, MapRect(bounds, 2f, 5.5f, 20f, 16f), 215f, 110f);
                        g.DrawArc(pen, MapRect(bounds, 5.2f, 9f, 13.6f, 12f), 215f, 110f);
                        g.DrawArc(pen, MapRect(bounds, 8.4f, 12.5f, 7.2f, 8f), 215f, 110f);
                        PointF wifiDot = Map(bounds, 12f, 18.5f);
                        float wr = Math.Max(1.3f, pen.Width * 0.75f);
                        g.FillEllipse(brush, wifiDot.X - wr, wifiDot.Y - wr, wr * 2f, wr * 2f);
                        break;

                    case "chevron-right":
                        g.DrawLines(pen, new PointF[]
                        {
                            Map(bounds, 9f, 6f),
                            Map(bounds, 15f, 12f),
                            Map(bounds, 9f, 18f)
                        });
                        break;

                    case "close":
                        g.DrawLine(pen, Map(bounds, 6f, 6f), Map(bounds, 18f, 18f));
                        g.DrawLine(pen, Map(bounds, 18f, 6f), Map(bounds, 6f, 18f));
                        break;

                    case "minimize":
                        g.DrawLine(pen, Map(bounds, 5f, 12f), Map(bounds, 19f, 12f));
                        break;
                }
            }
        }

        private static void DrawGamepadIcon(Graphics g, RectangleF bounds, Pen pen, Brush dotBrush)
        {
            RectangleF body = MapRect(bounds, 2.5f, 6.2f, 19f, 11.6f);
            Theme.DrawRoundedRect(g, pen, body, body.Height * 0.45f);

            // D-pad on left
            g.DrawLine(pen, Map(bounds, 6.5f, 12f), Map(bounds, 10.5f, 12f));
            g.DrawLine(pen, Map(bounds, 8.5f, 10f), Map(bounds, 8.5f, 14f));

            // Action dots on right
            float r = Math.Max(1.1f, pen.Width * 0.58f);
            PointF b1 = Map(bounds, 15.2f, 10.8f);
            PointF b2 = Map(bounds, 17.5f, 13.0f);
            g.FillEllipse(dotBrush, b1.X - r, b1.Y - r, r * 2f, r * 2f);
            g.FillEllipse(dotBrush, b2.X - r, b2.Y - r, r * 2f, r * 2f);
        }

        public static void DrawStar(Graphics g, RectangleF bounds, Color color, bool filled)
        {
            Theme.ConfigureHighQuality(g);
            PointF[] pts = new PointF[]
            {
                Map(bounds, 12.0f, 3.0f),
                Map(bounds, 14.8f, 8.7f),
                Map(bounds, 21.0f, 9.6f),
                Map(bounds, 16.5f, 14.0f),
                Map(bounds, 17.6f, 20.2f),
                Map(bounds, 12.0f, 17.2f),
                Map(bounds, 6.4f, 20.2f),
                Map(bounds, 7.5f, 14.0f),
                Map(bounds, 3.0f, 9.6f),
                Map(bounds, 9.2f, 8.7f)
            };

            if (filled)
            {
                using (SolidBrush b = new SolidBrush(color))
                {
                    g.FillPolygon(b, pts);
                }
            }

            using (Pen pen = CreateVectorPen(color, bounds, filled ? 1.4f : 1.8f))
            {
                g.DrawPolygon(pen, pts);
            }
        }

        private static void DrawServerRackIcon(Graphics g, RectangleF bounds, Pen pen, Brush dotBrush)
        {
            float r = Math.Max(1.5f, bounds.Width * 0.08f);
            RectangleF topBox = MapRect(bounds, 3f, 4f, 18f, 6.8f);
            RectangleF botBox = MapRect(bounds, 3f, 13.2f, 18f, 6.8f);

            Theme.DrawRoundedRect(g, pen, topBox, r);
            Theme.DrawRoundedRect(g, pen, botBox, r);

            float dotR = Math.Max(1.1f, pen.Width * 0.65f);
            PointF d1 = Map(bounds, 6.8f, 7.4f);
            PointF d2 = Map(bounds, 6.8f, 16.6f);
            g.FillEllipse(dotBrush, d1.X - dotR, d1.Y - dotR, dotR * 2f, dotR * 2f);
            g.FillEllipse(dotBrush, d2.X - dotR, d2.Y - dotR, dotR * 2f, dotR * 2f);

            g.DrawLine(pen, Map(bounds, 11f, 7.4f), Map(bounds, 17.2f, 7.4f));
            g.DrawLine(pen, Map(bounds, 11f, 16.6f), Map(bounds, 17.2f, 16.6f));
        }

        private static void DrawSettingsGear(Graphics g, RectangleF bounds, Pen pen)
        {
            PointF center = Map(bounds, 12f, 12f);
            float outerR = bounds.Width * (7.2f / 24f);
            float innerR = bounds.Width * (3.1f / 24f);

            int teeth = 8;
            PointF[] gearPts = new PointF[teeth * 4];
            for (int i = 0; i < teeth; i++)
            {
                double baseAngle = (i * 2.0 * Math.PI) / teeth;
                double step = (2.0 * Math.PI) / (teeth * 4.0);

                double a0 = baseAngle - step * 1.1;
                double a1 = baseAngle - step * 0.65;
                double a2 = baseAngle + step * 0.65;
                double a3 = baseAngle + step * 1.1;

                float rValley = outerR * 0.78f;
                float rTip = outerR * 1.06f;

                gearPts[i * 4 + 0] = new PointF(center.X + (float)(Math.Cos(a0) * rValley), center.Y + (float)(Math.Sin(a0) * rValley));
                gearPts[i * 4 + 1] = new PointF(center.X + (float)(Math.Cos(a1) * rTip), center.Y + (float)(Math.Sin(a1) * rTip));
                gearPts[i * 4 + 2] = new PointF(center.X + (float)(Math.Cos(a2) * rTip), center.Y + (float)(Math.Sin(a2) * rTip));
                gearPts[i * 4 + 3] = new PointF(center.X + (float)(Math.Cos(a3) * rValley), center.Y + (float)(Math.Sin(a3) * rValley));
            }

            g.DrawPolygon(pen, gearPts);
            g.DrawEllipse(pen, center.X - innerR, center.Y - innerR, innerR * 2f, innerR * 2f);
        }

        private static void DrawCloudOutline(Graphics g, RectangleF bounds, Pen pen, Brush fillBrush)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                RectangleF leftPuff = MapRect(bounds, 2.2f, 10.2f, 7.2f, 7.2f);
                RectangleF midLeftPuff = MapRect(bounds, 5.5f, 7.2f, 7.8f, 8.0f);
                RectangleF mainPuff = MapRect(bounds, 9.4f, 5.4f, 9.6f, 9.8f);
                RectangleF rightPuff = MapRect(bounds, 15.2f, 10.0f, 6.8f, 7.4f);

                path.AddArc(leftPuff, 90f, 165f);
                path.AddArc(midLeftPuff, 195f, 115f);
                path.AddArc(mainPuff, 200f, 145f);
                path.AddArc(rightPuff, 285f, 165f);
                path.CloseFigure();

                if (fillBrush != null)
                {
                    g.FillPath(fillBrush, path);
                }
                if (pen != null)
                {
                    g.DrawPath(pen, path);
                }
            }
        }

        /// <summary>
        /// Renders the distinct DNS Provider Icons:
        /// cloudflare, google, quad9, adguard, opendns, custom.
        /// </summary>
        public static void DrawProviderIcon(Graphics g, string iconKey, RectangleF bounds)
        {
            if (g == null) return;
            Theme.ConfigureHighQuality(g);

            string key = string.IsNullOrWhiteSpace(iconKey) ? "custom" : iconKey.Trim().ToLowerInvariant();

            switch (key)
            {
                case "cloudflare":
                    DrawCloudflareLogo(g, bounds);
                    break;

                case "google":
                    DrawGoogleLogo(g, bounds);
                    break;

                case "quad9":
                    DrawQuad9Logo(g, bounds);
                    break;

                case "adguard":
                    DrawAdGuardLogo(g, bounds);
                    break;

                case "opendns":
                    DrawOpenDnsLogo(g, bounds);
                    break;

                default:
                    DrawCustomDnsLogo(g, bounds);
                    break;
            }
        }

        private static void DrawCloudflareLogo(Graphics g, RectangleF bounds)
        {
            using (LinearGradientBrush orangeGrad = new LinearGradientBrush(
                bounds,
                Color.FromArgb(251, 146, 60), // #FB923C
                Color.FromArgb(249, 115, 22), // #F97316
                LinearGradientMode.Vertical))
            {
                DrawCloudOutline(g, bounds, null, orangeGrad);
            }
        }

        private static void DrawGoogleLogo(Graphics g, RectangleF bounds)
        {
            float pad = bounds.Width * 0.14f;
            RectangleF ringRect = new RectangleF(
                bounds.X + pad,
                bounds.Y + pad,
                bounds.Width - pad * 2f,
                bounds.Height - pad * 2f);

            float stroke = Math.Max(3.2f, bounds.Width * 0.18f);

            using (Pen redPen = new Pen(Color.FromArgb(234, 67, 53), stroke))      // #EA4335 Top
            using (Pen yellowPen = new Pen(Color.FromArgb(251, 188, 5), stroke))   // #FBBC05 Left
            using (Pen greenPen = new Pen(Color.FromArgb(52, 168, 83), stroke))    // #34A853 Bottom
            using (Pen bluePen = new Pen(Color.FromArgb(66, 133, 244), stroke))    // #4285F4 Right & crossbar
            {
                g.DrawArc(redPen, ringRect, 200f, 115f);
                g.DrawArc(yellowPen, ringRect, 140f, 65f);
                g.DrawArc(greenPen, ringRect, 45f, 100f);
                g.DrawArc(bluePen, ringRect, -12f, 62f);

                float cy = bounds.Y + bounds.Height * 0.5f;
                float cx = bounds.X + bounds.Width * 0.52f;
                float rightX = ringRect.Right + stroke * 0.36f;
                g.DrawLine(bluePen, new PointF(cx, cy), new PointF(rightX, cy));
            }
        }

        private static void DrawQuad9Logo(Graphics g, RectangleF bounds)
        {
            PointF center = Map(bounds, 12f, 12f);
            float radius = bounds.Width * 0.40f;

            PointF[] hex = new PointF[6];
            for (int i = 0; i < 6; i++)
            {
                double angle = (i * 60.0 - 90.0) * Math.PI / 180.0;
                hex[i] = new PointF(
                    center.X + (float)(Math.Cos(angle) * radius),
                    center.Y + (float)(Math.Sin(angle) * radius));
            }

            Color nodeColor = Color.FromArgb(241, 245, 249); // #F1F5F9
            Color lineColor = Color.FromArgb(200, 226, 232, 240);

            using (Pen linePen = CreateVectorPen(lineColor, bounds, 1.35f))
            using (SolidBrush nodeBrush = new SolidBrush(nodeColor))
            {
                g.DrawPolygon(linePen, hex);

                for (int i = 0; i < 6; i++)
                {
                    g.DrawLine(linePen, center, hex[i]);
                    g.DrawLine(linePen, hex[i], hex[(i + 2) % 6]);
                }

                float rNode = Math.Max(1.6f, bounds.Width * 0.065f);
                g.FillEllipse(nodeBrush, center.X - rNode, center.Y - rNode, rNode * 2f, rNode * 2f);
                for (int i = 0; i < 6; i++)
                {
                    g.FillEllipse(nodeBrush, hex[i].X - rNode, hex[i].Y - rNode, rNode * 2f, rNode * 2f);
                }
            }
        }

        private static void DrawAdGuardLogo(Graphics g, RectangleF bounds)
        {
            using (GraphicsPath shield = new GraphicsPath())
            {
                PointF top = Map(bounds, 12f, 2.2f);
                PointF rightTop = Map(bounds, 20.2f, 5.4f);
                PointF bottom = Map(bounds, 12f, 21.6f);
                PointF leftTop = Map(bounds, 3.8f, 5.4f);

                shield.AddLine(top, rightTop);
                shield.AddBezier(
                    rightTop,
                    Map(bounds, 20.2f, 11.2f),
                    Map(bounds, 17.5f, 17.5f),
                    bottom);
                shield.AddBezier(
                    bottom,
                    Map(bounds, 6.5f, 17.5f),
                    Map(bounds, 3.8f, 11.2f),
                    leftTop);
                shield.CloseFigure();

                using (LinearGradientBrush greenBrush = new LinearGradientBrush(
                    bounds,
                    Color.FromArgb(52, 211, 153),
                    Color.FromArgb(22, 163, 74),
                    LinearGradientMode.Vertical))
                {
                    g.FillPath(greenBrush, shield);
                }
            }

            using (Pen checkPen = CreateVectorPen(Color.White, bounds, 2.3f))
            {
                g.DrawLines(checkPen, new PointF[]
                {
                    Map(bounds, 8.2f, 12.2f),
                    Map(bounds, 11.0f, 15.0f),
                    Map(bounds, 16.2f, 9.2f)
                });
            }
        }

        private static void DrawOpenDnsLogo(Graphics g, RectangleF bounds)
        {
            RectangleF box = MapRect(bounds, 2.5f, 2.5f, 19f, 19f);
            float radius = bounds.Width * 0.22f;

            using (LinearGradientBrush orangeBrush = new LinearGradientBrush(
                box,
                Color.FromArgb(249, 115, 22),
                Color.FromArgb(217, 70, 15),
                LinearGradientMode.Vertical))
            {
                Theme.FillRoundedRect(g, orangeBrush, box, radius);
            }

            RectangleF zeroRect = MapRect(bounds, 8.4f, 6.4f, 7.2f, 11.2f);
            using (Pen whitePen = CreateVectorPen(Color.White, bounds, 2.6f))
            {
                Theme.DrawRoundedRect(g, whitePen, zeroRect, zeroRect.Width * 0.48f);
            }
        }

        private static void DrawCustomDnsLogo(Graphics g, RectangleF bounds)
        {
            RectangleF topRack = MapRect(bounds, 2.5f, 4.0f, 19f, 6.8f);
            RectangleF botRack = MapRect(bounds, 2.5f, 13.2f, 19f, 6.8f);
            float r = Math.Max(2f, bounds.Width * 0.10f);

            using (LinearGradientBrush blueBrush = new LinearGradientBrush(
                bounds,
                Color.FromArgb(59, 130, 246), // #3B82F6
                Color.FromArgb(37, 99, 235),  // #2563EB
                LinearGradientMode.Vertical))
            {
                Theme.FillRoundedRect(g, blueBrush, topRack, r);
                Theme.FillRoundedRect(g, blueBrush, botRack, r);
            }

            Color slotColor = Color.FromArgb(15, 23, 42);
            using (SolidBrush slotBrush = new SolidBrush(slotColor))
            using (Pen slotPen = CreateVectorPen(slotColor, bounds, 1.9f))
            {
                float dotR = Math.Max(1.3f, bounds.Width * 0.065f);
                PointF d1 = Map(bounds, 6.4f, 7.4f);
                PointF d2 = Map(bounds, 9.6f, 7.4f);
                PointF d3 = Map(bounds, 6.4f, 16.6f);
                PointF d4 = Map(bounds, 9.6f, 16.6f);

                g.FillEllipse(slotBrush, d1.X - dotR, d1.Y - dotR, dotR * 2f, dotR * 2f);
                g.FillEllipse(slotBrush, d2.X - dotR, d2.Y - dotR, dotR * 2f, dotR * 2f);
                g.FillEllipse(slotBrush, d3.X - dotR, d3.Y - dotR, dotR * 2f, dotR * 2f);
                g.FillEllipse(slotBrush, d4.X - dotR, d4.Y - dotR, dotR * 2f, dotR * 2f);

                g.DrawLine(slotPen, Map(bounds, 13.5f, 7.4f), Map(bounds, 18.2f, 7.4f));
                g.DrawLine(slotPen, Map(bounds, 13.5f, 16.6f), Map(bounds, 18.2f, 16.6f));
            }
        }
    }
}
