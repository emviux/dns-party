using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using DNSParty.Models;
using DNSParty.Services;

namespace DNSParty.UI
{
    public enum OverlayTab
    {
        Dns = 0,
        Test = 1,
        Settings = 2
    }

    public class OverlayForm : Form
    {
        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(
            int nLeftRect,
            int nTopRect,
            int nRightRect,
            int nBottomRect,
            int nWidthEllipse,
            int nHeightEllipse);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private readonly ConfigService _configService;
        private readonly DnsService _dnsService;
        private readonly SoundService _soundService;
        private readonly Action _onStateChangedCallback;

        private OverlayTab _activeTab = OverlayTab.Dns;
        private string _selectedProfileId = string.Empty;
        private DnsStatusSnapshot _currentSnapshot;
        private bool _isBusy;
        private string _statusFooterText = "Quickly switch your DNS without leaving the game.";
        private bool _statusIsError;

        private readonly Dictionary<string, DnsTestResult> _testResults =
            new Dictionary<string, DnsTestResult>(StringComparer.OrdinalIgnoreCase);

        private int _scrollOffset;
        private int _maxScroll;

        // Interactive Rectangles
        private Rectangle _headerRect;
        private Rectangle _tabDnsRect;
        private Rectangle _tabTestRect;
        private Rectangle _tabSettingsRect;
        private Rectangle _closeBtnRect;

        private Rectangle _qaFlushRect;
        private Rectangle _qaDisableRect;
        private Rectangle _qaTestCurrentRect;
        private Rectangle _enterKeyHintRect;
        private Rectangle _escKeyHintRect;

        private readonly List<OverlayCardHit> _cardHits = new List<OverlayCardHit>();
        private readonly Dictionary<string, Rectangle> _settingsHits = new Dictionary<string, Rectangle>();
        private Rectangle _btnTestAllRect;

        private string _hoveredElement = string.Empty;
        private int _hoveredCardIndex = -1;
        private string _hoveredCardPart = string.Empty; // "card", "star", "apply"

        private bool _isDragging;
        private Point _dragStart;

        private class OverlayCardHit
        {
            public DnsProfile Profile;
            public Rectangle CardRect;
            public Rectangle StarRect;
            public Rectangle ActionBtnRect;
        }

        public OverlayForm(
            ConfigService configService,
            DnsService dnsService,
            SoundService soundService,
            Action onStateChangedCallback = null)
        {
            _configService = configService;
            _dnsService = dnsService;
            _soundService = soundService;
            _onStateChangedCallback = onStateChangedCallback;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            TopMost = true; // In-game overlay sits above the active game when toggled open
            Opacity = 0.93;
            BackColor = Color.FromArgb(11, 13, 24);
            ForeColor = Theme.TextPrimary;
            Font = Theme.CreateFont(9.5f);
            Icon = IconRenderer.GetAppIcon();
            Text = "DNS Party Overlay";
            ClientSize = new Size(920, 560);
            KeyPreview = true;

            RefreshState();
            RecalculateLayout();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                cp.ExStyle |= WS_EX_TOOLWINDOW; // Keep overlay out of Alt+Tab
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyRoundedRegion();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyRoundedRegion();
            RecalculateLayout();
            Invalidate();
        }

        private void ApplyRoundedRegion()
        {
            if (Width <= 0 || Height <= 0 || !IsHandleCreated) return;
            try
            {
                int pref = 2; // DWMWCP_ROUND
                int hr = DwmSetWindowAttribute(Handle, 33, ref pref, sizeof(int));
                if (hr != 0)
                {
                    IntPtr rgn = CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 22, 22);
                    Region = Region.FromHrgn(rgn);
                }
            }
            catch
            {
                try
                {
                    IntPtr rgn = CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 22, 22);
                    Region = Region.FromHrgn(rgn);
                }
                catch { }
            }
        }

        public void ShowOverlay()
        {
            RefreshState();
            RecalculateLayout();
            if (!Visible)
            {
                Screen screen = Screen.FromPoint(Cursor.Position);
                Location = new Point(
                    screen.WorkingArea.Left + (screen.WorkingArea.Width - Width) / 2,
                    screen.WorkingArea.Top + (screen.WorkingArea.Height - Height) / 2);
                Show();
            }
            Activate();
            BringToFront();
            Invalidate();

            // Trigger background ping update so latencies populate like the reference image
            _ = RefreshLatenciesQuietlyAsync();
        }

        public void HideOverlay()
        {
            if (Visible)
            {
                Hide();
            }
        }

        public void ToggleOverlay()
        {
            if (Visible)
            {
                HideOverlay();
            }
            else
            {
                ShowOverlay();
            }
        }

        public void RefreshState()
        {
            _currentSnapshot = _dnsService.GetActualDnsSnapshot(
                _configService.Profiles,
                _configService.Settings.SelectedAdapterId,
                _configService.Settings.AutoDetectAdapter,
                _configService.Settings.LastRequestedProfileId);

            if (!string.IsNullOrEmpty(_currentSnapshot.ActiveProfileId) &&
                _configService.Profiles.Any(p => string.Equals(p.Id, _currentSnapshot.ActiveProfileId, StringComparison.OrdinalIgnoreCase)))
            {
                if (string.IsNullOrEmpty(_selectedProfileId))
                {
                    _selectedProfileId = _currentSnapshot.ActiveProfileId;
                }
            }
            else if (string.IsNullOrEmpty(_selectedProfileId) && _configService.Profiles.Count > 0)
            {
                _selectedProfileId = _configService.Profiles[0].Id;
            }
        }

        private async Task RefreshLatenciesQuietlyAsync()
        {
            var profiles = _configService.Profiles.ToList();
            foreach (var p in profiles)
            {
                if (!Visible || IsDisposed) return;
                DnsTestResult res = await _dnsService.TestDnsServerAsync(p.PrimaryDns, 1400);
                _testResults[p.Id] = res;
                if (Visible && !IsDisposed) Invalidate();
            }
        }

        private void RecalculateLayout()
        {
            int w = ClientSize.Width;
            int h = ClientSize.Height;

            _headerRect = new Rectangle(0, 0, w, 72);

            // Top Navigation Tabs matching Transparent DNS Party Overlay.png
            _tabDnsRect = new Rectangle(276, 17, 102, 38);
            _tabTestRect = new Rectangle(390, 17, 94, 38);
            _tabSettingsRect = new Rectangle(496, 17, 112, 38);

            _closeBtnRect = new Rectangle(w - 48, 21, 30, 30);

            // Right Panel (Current DNS status + 3 quick action cards)
            int rightX = 594;
            int rightW = w - rightX - 24;

            _qaFlushRect = new Rectangle(rightX, 206, rightW, 56);
            _qaDisableRect = new Rectangle(rightX, 274, rightW, 56);
            _qaTestCurrentRect = new Rectangle(rightX, 342, rightW, 56);

            // Footer keyboard hint pills
            _enterKeyHintRect = new Rectangle(w - 280, h - 38, 138, 26);
            _escKeyHintRect = new Rectangle(w - 118, h - 38, 92, 26);

            // Left Panel (DNS Cards)
            int leftX = 24;
            int leftW = 548;
            int listTop = 92;
            int listBottom = h - 54;
            int listH = Math.Max(100, listBottom - listTop);

            _cardHits.Clear();
            var profiles = _configService.Profiles;
            int cardH = 62;
            int gap = 8;
            int totalH = profiles.Count * (cardH + gap);

            _maxScroll = Math.Max(0, totalH - listH);
            if (_scrollOffset > _maxScroll) _scrollOffset = _maxScroll;
            if (_scrollOffset < 0) _scrollOffset = 0;

            for (int i = 0; i < profiles.Count; i++)
            {
                int cy = listTop + i * (cardH + gap) - _scrollOffset;
                Rectangle cardRect = new Rectangle(leftX, cy, leftW, cardH);
                Rectangle starRect = new Rectangle(cardRect.Right - 94, cy + (cardH - 30) / 2, 30, 30);
                Rectangle actionRect = new Rectangle(cardRect.Right - 48, cy + (cardH - 34) / 2, 34, 34);

                _cardHits.Add(new OverlayCardHit
                {
                    Profile = profiles[i],
                    CardRect = cardRect,
                    StarRect = starRect,
                    ActionBtnRect = actionRect
                });
            }

            _btnTestAllRect = new Rectangle(leftX + leftW - 150, 92, 150, 34);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            Theme.ConfigureHighQuality(g);

            RecalculateLayout();

            int w = ClientSize.Width;
            int h = ClientSize.Height;

            // Glassy Dark Overlay Background with subtle vertical gradient
            using (LinearGradientBrush bgBrush = new LinearGradientBrush(
                ClientRectangle,
                Color.FromArgb(14, 16, 30),
                Color.FromArgb(9, 11, 21),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }

            // Subtle ambient purple glow at top center
            using (GraphicsPath glowPath = new GraphicsPath())
            {
                Rectangle glowRect = new Rectangle(w / 2 - 260, -90, 520, 200);
                glowPath.AddEllipse(glowRect);
                using (PathGradientBrush pgb = new PathGradientBrush(glowPath))
                {
                    pgb.CenterColor = Color.FromArgb(32, 139, 92, 246);
                    pgb.SurroundColors = new Color[] { Color.Transparent };
                    g.FillPath(pgb, glowPath);
                }
            }

            DrawHeaderBar(g, w);

            if (_activeTab == OverlayTab.Dns)
            {
                DrawDnsCardsPanel(g, h);
                DrawRightStatusPanel(g, w);
            }
            else if (_activeTab == OverlayTab.Test)
            {
                DrawTestPanel(g, h);
                DrawRightStatusPanel(g, w);
            }
            else if (_activeTab == OverlayTab.Settings)
            {
                DrawOverlaySettingsPanel(g, h);
                DrawRightStatusPanel(g, w);
            }

            DrawFooterBar(g, w, h);

            // Outer translucent neon border
            using (Pen borderPen = new Pen(Color.FromArgb(110, 90, 75, 160), 1.5f))
            {
                Theme.DrawRoundedRect(g, borderPen, new Rectangle(1, 1, w - 2, h - 2), 18);
            }
        }

        private void DrawHeaderBar(Graphics g, int w)
        {
            using (Pen sepPen = new Pen(Color.FromArgb(40, 148, 163, 184), 1f))
            {
                g.DrawLine(sepPen, 20, _headerRect.Bottom, w - 20, _headerRect.Bottom);
            }

            // Left: Overlay Badge Logo + "DNS Party" / "Quick DNS Switcher"
            IconRenderer.DrawAppLogo(g, new RectangleF(24, 12, 48, 48), true);

            using (Font titleFont = Theme.CreateBoldFont(13f))
            using (Font subFont = Theme.CreateFont(8.8f))
            using (SolidBrush titleBrush = new SolidBrush(Theme.TextPrimary))
            using (SolidBrush subBrush = new SolidBrush(Theme.TextSecondary))
            {
                g.DrawString("DNS Party", titleFont, titleBrush, 80, 15);
                g.DrawString("Quick DNS Switcher", subFont, subBrush, 81, 39);
            }

            // Center Tabs: [Gamepad DNS]  [Waveform Test]  [Gear Settings]
            DrawOverlayTabButton(g, _tabDnsRect, "gamepad", "DNS", _activeTab == OverlayTab.Dns, _hoveredElement == "tab_dns");
            DrawOverlayTabButton(g, _tabTestRect, "test", "Test", _activeTab == OverlayTab.Test, _hoveredElement == "tab_test");
            DrawOverlayTabButton(g, _tabSettingsRect, "settings", "Settings", _activeTab == OverlayTab.Settings, _hoveredElement == "tab_settings");

            // Right: Shortcut Key Badges (`[Shift] + [Tab] to close`) + Close `X`
            string shortcut = string.IsNullOrWhiteSpace(_configService.Settings.OverlayShortcut)
                ? "Shift+Tab"
                : _configService.Settings.OverlayShortcut;
            string[] keys = shortcut.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);

            int rightAnchor = _closeBtnRect.Left - 14;
            using (Font smallFont = Theme.CreateFont(8.8f))
            using (Font keyFont = Theme.CreateSemiboldFont(8.5f))
            using (SolidBrush mutedBrush = new SolidBrush(Theme.TextSecondary))
            {
                SizeF toCloseSz = g.MeasureString("to close", smallFont);
                float toCloseX = rightAnchor - toCloseSz.Width;
                g.DrawString("to close", smallFont, mutedBrush, toCloseX, 27);

                float curX = toCloseX - 8;
                for (int i = keys.Length - 1; i >= 0; i--)
                {
                    string k = keys[i].Trim();
                    SizeF ksz = g.MeasureString(k, keyFont);
                    int kw = Math.Max(32, (int)ksz.Width + 14);
                    curX -= kw;
                    Rectangle keyRect = new Rectangle((int)curX, 23, kw, 24);
                    DrawKeycapBadge(g, keyRect, k);

                    if (i > 0)
                    {
                        curX -= 14;
                        g.DrawString("+", smallFont, mutedBrush, curX + 2, 26);
                    }
                }
            }

            // Close Button
            if (_hoveredElement == "close")
            {
                using (SolidBrush hb = new SolidBrush(Color.FromArgb(80, 239, 68, 68)))
                {
                    Theme.FillRoundedRect(g, hb, _closeBtnRect, 7);
                }
            }
            IconRenderer.DrawIcon(
                g,
                "close",
                new RectangleF(_closeBtnRect.X + 7, _closeBtnRect.Y + 7, 16, 16),
                _hoveredElement == "close" ? Color.White : Theme.TextSecondary);
        }

        private void DrawOverlayTabButton(Graphics g, Rectangle rect, string iconName, string label, bool active, bool hovered)
        {
            if (active)
            {
                Theme.FillRoundedGradient(
                    g,
                    rect,
                    10,
                    Color.FromArgb(76, 42, 168),
                    Color.FromArgb(109, 56, 232),
                    LinearGradientMode.Horizontal);
                using (Pen p = new Pen(Color.FromArgb(167, 139, 250), 1.3f))
                {
                    Theme.DrawRoundedRect(g, p, rect, 10);
                }
            }
            else if (hovered)
            {
                using (SolidBrush hb = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                {
                    Theme.FillRoundedRect(g, hb, rect, 10);
                }
            }

            Color fg = active ? Color.White : (hovered ? Theme.TextPrimary : Theme.TextSecondary);
            IconRenderer.DrawIcon(g, iconName, new RectangleF(rect.X + 14, rect.Y + 9, 20, 20), fg);

            using (Font f = active ? Theme.CreateSemiboldFont(10f) : Theme.CreateFont(10f))
            using (SolidBrush b = new SolidBrush(fg))
            {
                g.DrawString(label, f, b, rect.X + 40, rect.Y + 9);
            }
        }

        private void DrawKeycapBadge(Graphics g, Rectangle rect, string text)
        {
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(26, 30, 50)))
            using (Pen border = new Pen(Color.FromArgb(65, 75, 110), 1f))
            using (Font f = Theme.CreateSemiboldFont(8.4f))
            using (SolidBrush tb = new SolidBrush(Theme.TextPrimary))
            {
                Theme.FillRoundedRect(g, bg, rect, 6);
                Theme.DrawRoundedRect(g, border, rect, 6);
                StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(text, f, tb, rect, sf);
            }
        }

        private void DrawDnsCardsPanel(Graphics g, int h)
        {
            int listTop = 88;
            int listBottom = h - 54;
            Rectangle clipRect = new Rectangle(18, listTop, 560, listBottom - listTop);
            Region oldClip = g.Clip;
            g.SetClip(clipRect, CombineMode.Intersect);

            string activeId = (_currentSnapshot != null && !_currentSnapshot.IsDhcpDns)
                ? _currentSnapshot.ActiveProfileId
                : string.Empty;

            for (int i = 0; i < _cardHits.Count; i++)
            {
                var box = _cardHits[i];
                if (box.CardRect.Bottom < listTop || box.CardRect.Top > listBottom) continue;

                bool isActive = !string.IsNullOrEmpty(activeId) &&
                                string.Equals(activeId, box.Profile.Id, StringComparison.OrdinalIgnoreCase);
                bool isSelected = string.Equals(_selectedProfileId, box.Profile.Id, StringComparison.OrdinalIgnoreCase);
                bool isHovered = _hoveredCardIndex == i;

                DrawOverlayDnsCard(g, box, isActive, isSelected, isHovered);
            }

            g.Clip = oldClip;
        }

        private void DrawOverlayDnsCard(Graphics g, OverlayCardHit box, bool isActive, bool isSelected, bool isHovered)
        {
            Rectangle r = box.CardRect;
            DnsProfile p = box.Profile;

            if (isActive)
            {
                using (LinearGradientBrush actGrad = new LinearGradientBrush(
                    r,
                    Color.FromArgb(215, 36, 26, 74),
                    Color.FromArgb(215, 48, 32, 96),
                    LinearGradientMode.Horizontal))
                {
                    Theme.FillRoundedRect(g, actGrad, r, 12);
                }
                using (Pen glowPen = new Pen(Color.FromArgb(148, 102, 255), 1.8f))
                {
                    Theme.DrawRoundedRect(g, glowPen, r, 12);
                }
            }
            else
            {
                Color bgColor = isHovered
                    ? Color.FromArgb(195, 26, 31, 52)
                    : Color.FromArgb(175, 18, 22, 38);
                Color borderColor = isSelected
                    ? Color.FromArgb(150, 139, 92, 246)
                    : (isHovered ? Color.FromArgb(90, 100, 140) : Color.FromArgb(45, 52, 82));

                using (SolidBrush bg = new SolidBrush(bgColor))
                using (Pen pen = new Pen(borderColor, isSelected ? 1.4f : 1f))
                {
                    Theme.FillRoundedRect(g, bg, r, 12);
                    Theme.DrawRoundedRect(g, pen, r, 12);
                }
            }

            // Provider Logo
            IconRenderer.DrawProviderIcon(g, p.IconKey, new RectangleF(r.X + 16, r.Y + 15, 32, 32));

            // Provider Name
            string displayName = (string.Equals(p.Id, "google", StringComparison.OrdinalIgnoreCase) && p.Name == "Google")
                ? "Google DNS"
                : p.Name;
            if (!p.IsDefaultProvider && !string.IsNullOrWhiteSpace(p.Tag) && !displayName.Contains("("))
            {
                displayName = displayName + " (" + p.Tag + ")";
            }

            using (Font nameFont = Theme.CreateBoldFont(10.5f))
            using (SolidBrush nameBrush = new SolidBrush(Theme.TextPrimary))
            {
                g.DrawString(displayName, nameFont, nameBrush, r.X + 62, r.Y + 10);
            }

            // Primary | Secondary IPs
            using (Font ipFont = Theme.CreateFont(9.2f))
            using (SolidBrush ipBrush = new SolidBrush(isActive ? Theme.TextPrimary : Theme.TextSecondary))
            using (Pen sepPen = new Pen(Theme.TextMuted, 1f))
            {
                float ipX = r.X + 62;
                float ipY = r.Y + 34;
                g.DrawString(p.PrimaryDns, ipFont, ipBrush, ipX, ipY);

                if (!string.IsNullOrWhiteSpace(p.SecondaryDns))
                {
                    SizeF p1Size = g.MeasureString(p.PrimaryDns, ipFont);
                    float sepX = ipX + p1Size.Width + 8;
                    g.DrawLine(sepPen, sepX, ipY + 3, sepX, ipY + 15);
                    g.DrawString(p.SecondaryDns, ipFont, ipBrush, sepX + 10, ipY);
                }
            }

            // Latency pill (`● 14 ms`) matching Transparent DNS Party Overlay.png
            if (_testResults.ContainsKey(p.Id))
            {
                DnsTestResult tr = _testResults[p.Id];
                string text = tr.IsReachable ? (tr.LatencyMs + " ms") : "Timeout";
                Color pillColor = !tr.IsReachable
                    ? Theme.DangerRed
                    : (tr.LatencyMs <= 60 ? Theme.StatusGreen : Theme.StatusWarning);

                Rectangle pillRect = new Rectangle(box.StarRect.Left - 88, r.Y + (r.Height - 26) / 2, 76, 26);
                using (SolidBrush pb = new SolidBrush(Color.FromArgb(35, pillColor)))
                using (SolidBrush dotB = new SolidBrush(pillColor))
                using (Font pf = Theme.CreateSemiboldFont(8.6f))
                using (SolidBrush pt = new SolidBrush(pillColor))
                {
                    Theme.FillRoundedRect(g, pb, pillRect, 12);
                    g.FillEllipse(dotB, pillRect.X + 10, pillRect.Y + 9, 7, 7);
                    g.DrawString(text, pf, pt, pillRect.X + 22, pillRect.Y + 4);
                }
            }

            // Favorite Star
            IconRenderer.DrawStar(
                g,
                new RectangleF(box.StarRect.X + 5, box.StarRect.Y + 5, 20, 20),
                p.IsFavorite ? Theme.StarGold : Theme.StarOutline,
                p.IsFavorite);

            // Right Action Button: Purple circle with Checkmark when Active, Dark rounded square with Play triangle `[▷]` when inactive
            Rectangle actRect = box.ActionBtnRect;
            if (isActive)
            {
                using (SolidBrush cb = new SolidBrush(Color.FromArgb(139, 92, 246)))
                {
                    g.FillEllipse(cb, actRect);
                }
                IconRenderer.DrawIcon(g, "check", new RectangleF(actRect.X + 8, actRect.Y + 8, 18, 18), Color.White);
            }
            else
            {
                bool btnHover = isHovered && _hoveredCardPart == "apply";
                Color btnBg = btnHover ? Color.FromArgb(52, 58, 92) : Color.FromArgb(26, 30, 50);
                Color btnBorder = btnHover ? Theme.AccentViolet : Color.FromArgb(58, 66, 102);

                using (SolidBrush bb = new SolidBrush(btnBg))
                using (Pen bp = new Pen(btnBorder, 1f))
                {
                    Theme.FillRoundedRect(g, bb, actRect, 8);
                    Theme.DrawRoundedRect(g, bp, actRect, 8);
                }
                IconRenderer.DrawIcon(g, "play", new RectangleF(actRect.X + 8, actRect.Y + 8, 18, 18), Color.White, false);
            }
        }

        private void DrawTestPanel(Graphics g, int h)
        {
            int leftX = 24;
            int leftW = 548;

            using (Font hFont = Theme.CreateBoldFont(13f))
            using (Font sFont = Theme.CreateFont(9.2f))
            using (SolidBrush hb = new SolidBrush(Theme.TextPrimary))
            using (SolidBrush sb = new SolidBrush(Theme.TextSecondary))
            {
                g.DrawString("DNS Latency Benchmark", hFont, hb, leftX, 90);
                g.DrawString("Direct RFC 1035 UDP port 53 response times across your DNS servers.", sFont, sb, leftX, 116);
            }

            // "Run Benchmark" button
            DrawPillActionButton(g, _btnTestAllRect, "test", _isBusy ? "Testing..." : "Benchmark All", true, _hoveredElement == "btn_test_all");

            var ordered = _configService.Profiles
                .OrderBy(p => _testResults.ContainsKey(p.Id) && _testResults[p.Id].IsReachable ? _testResults[p.Id].LatencyMs : 99999L)
                .ToList();

            int rowY = 148;
            int rowH = 52;
            for (int i = 0; i < Math.Min(6, ordered.Count); i++)
            {
                var p = ordered[i];
                Rectangle rowRect = new Rectangle(leftX, rowY + i * (rowH + 8), leftW, rowH);
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(180, 18, 22, 38)))
                using (Pen border = new Pen(Color.FromArgb(45, 52, 82), 1f))
                {
                    Theme.FillRoundedRect(g, bg, rowRect, 10);
                    Theme.DrawRoundedRect(g, border, rowRect, 10);
                }

                IconRenderer.DrawProviderIcon(g, p.IconKey, new RectangleF(rowRect.X + 14, rowRect.Y + 12, 28, 28));

                using (Font nf = Theme.CreateSemiboldFont(10f))
                using (Font ipf = Theme.CreateFont(8.8f))
                using (SolidBrush nb = new SolidBrush(Theme.TextPrimary))
                using (SolidBrush ib = new SolidBrush(Theme.TextSecondary))
                {
                    g.DrawString(p.Name, nf, nb, rowRect.X + 54, rowRect.Y + 8);
                    g.DrawString(p.PrimaryDns, ipf, ib, rowRect.X + 54, rowRect.Y + 28);
                }

                // Latency bar visualization
                if (_testResults.ContainsKey(p.Id))
                {
                    var tr = _testResults[p.Id];
                    Color barColor = !tr.IsReachable
                        ? Theme.DangerRed
                        : (tr.LatencyMs <= 50 ? Theme.StatusGreen : Theme.StatusWarning);

                    int barMaxW = 180;
                    int barW = tr.IsReachable
                        ? Math.Max(16, Math.Min(barMaxW, (int)(barMaxW * (1.0 - Math.Min(200.0, tr.LatencyMs) / 220.0))))
                        : 24;

                    Rectangle trackRect = new Rectangle(rowRect.Right - 270, rowRect.Y + 21, barMaxW, 10);
                    Rectangle fillRect = new Rectangle(trackRect.X, trackRect.Y, barW, 10);

                    using (SolidBrush tb = new SolidBrush(Color.FromArgb(28, 33, 54)))
                    using (SolidBrush fb = new SolidBrush(barColor))
                    using (Font lf = Theme.CreateSemiboldFont(9f))
                    {
                        Theme.FillRoundedRect(g, tb, trackRect, 5);
                        Theme.FillRoundedRect(g, fb, fillRect, 5);
                        string msLabel = tr.IsReachable ? (tr.LatencyMs + " ms") : "Unreachable";
                        g.DrawString(msLabel, lf, fb, trackRect.Right + 14, rowRect.Y + 16);
                    }
                }
            }
        }

        private void DrawOverlaySettingsPanel(Graphics g, int h)
        {
            _settingsHits.Clear();
            int leftX = 24;
            int leftW = 548;

            using (Font hFont = Theme.CreateBoldFont(13f))
            using (Font sFont = Theme.CreateFont(9.2f))
            using (SolidBrush hb = new SolidBrush(Theme.TextPrimary))
            using (SolidBrush sb = new SolidBrush(Theme.TextSecondary))
            {
                g.DrawString("In-Game Overlay Settings", hFont, hb, leftX, 90);
                g.DrawString("Customize overlay behavior, sound cues, and shortcut preferences.", sFont, sb, leftX, 116);
            }

            Rectangle card = new Rectangle(leftX, 150, leftW, 320);
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(185, 18, 22, 38)))
            using (Pen border = new Pen(Color.FromArgb(45, 52, 82), 1f))
            {
                Theme.FillRoundedRect(g, bg, card, 12);
                Theme.DrawRoundedRect(g, border, card, 12);
            }

            DrawOverlayToggleRow(g, "ov_sounds", card.X + 22, card.Y + 24, card.Width - 44, "Play audio chime when switching DNS in-game", _configService.Settings.EnableSounds);
            DrawOverlayToggleRow(g, "ov_notify", card.X + 22, card.Y + 74, card.Width - 44, "Show Windows notifications on DNS change", _configService.Settings.EnableNotifications);
            DrawOverlayToggleRow(g, "ov_auto_adapter", card.X + 22, card.Y + 124, card.Width - 44, "Automatically target active gaming network adapter", _configService.Settings.AutoDetectAdapter);

            // Overlay Shortcut selector row
            Rectangle scRow = new Rectangle(card.X + 22, card.Y + 184, card.Width - 44, 46);
            _settingsHits["ov_cycle_shortcut"] = scRow;
            bool scHover = _hoveredElement == "ov_cycle_shortcut";
            using (SolidBrush sb = new SolidBrush(scHover ? Color.FromArgb(32, 38, 64) : Color.FromArgb(23, 27, 45)))
            using (Pen sp = new Pen(scHover ? Theme.AccentViolet : Color.FromArgb(45, 52, 82), 1f))
            using (Font lf = Theme.CreateSemiboldFont(9.5f))
            using (SolidBrush lb = new SolidBrush(Theme.TextPrimary))
            {
                Theme.FillRoundedRect(g, sb, scRow, 9);
                Theme.DrawRoundedRect(g, sp, scRow, 9);
                g.DrawString("Overlay Toggle Shortcut (Click to cycle):", lf, lb, scRow.X + 14, scRow.Y + 13);

                string curSc = string.IsNullOrWhiteSpace(_configService.Settings.OverlayShortcut) ? "Shift+Tab" : _configService.Settings.OverlayShortcut;
                Rectangle badge = new Rectangle(scRow.Right - 130, scRow.Y + 9, 116, 28);
                DrawKeycapBadge(g, badge, curSc);
            }
        }

        private void DrawOverlayToggleRow(Graphics g, string key, int x, int y, int width, string label, bool isChecked)
        {
            Rectangle r = new Rectangle(x, y, width, 34);
            _settingsHits[key] = r;

            using (Font f = Theme.CreateFont(9.6f))
            using (SolidBrush b = new SolidBrush(Theme.TextPrimary))
            {
                g.DrawString(label, f, b, x, y + 6);
            }

            Rectangle sw = new Rectangle(x + width - 44, y + 5, 42, 22);
            using (SolidBrush sb = new SolidBrush(isChecked ? Theme.ActiveCardBorder : Color.FromArgb(52, 60, 90)))
            {
                Theme.FillRoundedRect(g, sb, sw, 11);
            }
            int kx = isChecked ? (sw.Right - 19) : (sw.X + 3);
            using (SolidBrush kb = new SolidBrush(Color.White))
            {
                g.FillEllipse(kb, kx, sw.Y + 3, 16, 16);
            }
        }

        private void DrawRightStatusPanel(Graphics g, int w)
        {
            int rightX = 594;
            int rightW = w - rightX - 24;

            // 1. Top "DNS Active" Green Status Box matching Transparent DNS Party Overlay.png
            Rectangle statusBox = new Rectangle(rightX, 92, rightW, 96);
            bool isCustomActive = _currentSnapshot != null && !_currentSnapshot.IsDhcpDns && _currentSnapshot.IsConnected;

            if (isCustomActive)
            {
                using (LinearGradientBrush grad = new LinearGradientBrush(
                    statusBox,
                    Color.FromArgb(210, 14, 46, 38),
                    Color.FromArgb(210, 14, 30, 34),
                    LinearGradientMode.Horizontal))
                {
                    Theme.FillRoundedRect(g, grad, statusBox, 14);
                }
                using (Pen borderPen = new Pen(Color.FromArgb(34, 197, 94), 1.2f))
                {
                    Theme.DrawRoundedRect(g, borderPen, statusBox, 14);
                }
            }
            else
            {
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(190, 20, 24, 42)))
                using (Pen borderPen = new Pen(Color.FromArgb(65, 75, 115), 1.2f))
                {
                    Theme.FillRoundedRect(g, bg, statusBox, 14);
                    Theme.DrawRoundedRect(g, borderPen, statusBox, 14);
                }
            }

            // Checkmark Circle
            Rectangle circleRect = new Rectangle(statusBox.X + 20, statusBox.Y + 24, 48, 48);
            Color circleColor = isCustomActive ? Theme.StatusGreen : Theme.AccentViolet;
            using (Pen cp = new Pen(circleColor, 2.4f))
            {
                g.DrawEllipse(cp, circleRect);
            }
            IconRenderer.DrawIcon(
                g,
                isCustomActive ? "check" : "wifi",
                new RectangleF(circleRect.X + 12, circleRect.Y + 12, 24, 24),
                circleColor);

            string title = isCustomActive ? "DNS Active" : "Automatic DNS";
            string sub = isCustomActive
                ? string.Format("{0} ({1})", _currentSnapshot.ActiveProfileName, _currentSnapshot.ActualPrimaryDns)
                : "System Default (DHCP)";

            using (Font tf = Theme.CreateBoldFont(14f))
            using (Font sf = Theme.CreateFont(9.8f))
            using (SolidBrush tb = new SolidBrush(Theme.TextPrimary))
            using (SolidBrush sb = new SolidBrush(isCustomActive ? Theme.StatusGreen : Theme.TextSecondary))
            {
                g.DrawString(title, tf, tb, statusBox.X + 82, statusBox.Y + 21);
                g.DrawString(sub, sf, sb, statusBox.X + 83, statusBox.Y + 51);
            }

            // 2. Three Action Cards with right chevrons (`>`):
            // - Flush DNS Cache
            // - Disable DNS (Use Automatic DNS)
            // - Test Current DNS
            DrawOverlayActionRow(g, _qaFlushRect, "flush", "Flush DNS Cache", null, _hoveredElement == "qa_flush");
            DrawOverlayActionRow(g, _qaDisableRect, "disable-circle", "Disable DNS", "(Use Automatic DNS)", _hoveredElement == "qa_disable");
            DrawOverlayActionRow(g, _qaTestCurrentRect, "test", "Test Current DNS", null, _hoveredElement == "qa_test");
        }

        private void DrawOverlayActionRow(Graphics g, Rectangle rect, string iconName, string title, string subtitle, bool hovered)
        {
            Color bg = hovered ? Color.FromArgb(215, 28, 33, 56) : Color.FromArgb(185, 20, 24, 42);
            Color border = hovered ? Color.FromArgb(110, 139, 92, 246) : Color.FromArgb(48, 55, 86);

            using (SolidBrush b = new SolidBrush(bg))
            using (Pen p = new Pen(border, 1f))
            {
                Theme.FillRoundedRect(g, b, rect, 12);
                Theme.DrawRoundedRect(g, p, rect, 12);
            }

            IconRenderer.DrawIcon(g, iconName, new RectangleF(rect.X + 18, rect.Y + (rect.Height - 24) / 2f, 24, 24), Theme.TextPrimary);

            if (string.IsNullOrEmpty(subtitle))
            {
                using (Font tf = Theme.CreateSemiboldFont(10f))
                using (SolidBrush tb = new SolidBrush(Theme.TextPrimary))
                {
                    g.DrawString(title, tf, tb, rect.X + 56, rect.Y + (rect.Height - 19) / 2f);
                }
            }
            else
            {
                using (Font tf = Theme.CreateSemiboldFont(9.8f))
                using (Font sf = Theme.CreateFont(8.5f))
                using (SolidBrush tb = new SolidBrush(Theme.TextPrimary))
                using (SolidBrush sb = new SolidBrush(Theme.TextSecondary))
                {
                    g.DrawString(title, tf, tb, rect.X + 56, rect.Y + 9);
                    g.DrawString(subtitle, sf, sb, rect.X + 56, rect.Y + 30);
                }
            }

            IconRenderer.DrawIcon(
                g,
                "chevron-right",
                new RectangleF(rect.Right - 28, rect.Y + (rect.Height - 16) / 2f, 16, 16),
                Theme.TextSecondary);
        }

        private void DrawPillActionButton(Graphics g, Rectangle rect, string iconName, string text, bool primary, bool hovered)
        {
            if (primary)
            {
                Theme.FillRoundedGradient(
                    g,
                    rect,
                    8,
                    hovered ? Theme.PrimaryButtonHover : Theme.PrimaryButtonStart,
                    Theme.PrimaryButtonEnd,
                    LinearGradientMode.Horizontal);
            }
            else
            {
                using (SolidBrush b = new SolidBrush(hovered ? Theme.SecondaryButtonHover : Theme.SecondaryButtonBackground))
                {
                    Theme.FillRoundedRect(g, b, rect, 8);
                }
            }
            IconRenderer.DrawIcon(g, iconName, new RectangleF(rect.X + 12, rect.Y + 8, 18, 18), Color.White);
            using (Font f = Theme.CreateSemiboldFont(9f))
            using (SolidBrush tb = new SolidBrush(Color.White))
            {
                g.DrawString(text, f, tb, rect.X + 36, rect.Y + 8);
            }
        }

        private void DrawFooterBar(Graphics g, int w, int h)
        {
            int footerTop = h - 46;
            using (Pen sepPen = new Pen(Color.FromArgb(35, 148, 163, 184), 1f))
            {
                g.DrawLine(sepPen, 20, footerTop, w - 20, footerTop);
            }

            // Left info icon + status text
            Color infoColor = _statusIsError ? Theme.DangerRed : Theme.TextSecondary;
            IconRenderer.DrawIcon(g, "info", new RectangleF(24, footerTop + 12, 18, 18), infoColor);

            using (Font f = Theme.CreateFont(9.2f))
            using (SolidBrush b = new SolidBrush(_statusIsError ? Theme.DangerRed : Theme.TextSecondary))
            {
                g.DrawString(_statusFooterText, f, b, 50, footerTop + 12);
            }

            // Right: `[Enter] Apply Selected`   `[Esc] Close`
            using (Font f = Theme.CreateFont(9f))
            using (SolidBrush b = new SolidBrush(Theme.TextSecondary))
            {
                Rectangle enterKey = new Rectangle(_enterKeyHintRect.X, footerTop + 10, 48, 24);
                DrawKeycapBadge(g, enterKey, "Enter");
                g.DrawString("Apply Selected", f, b, enterKey.Right + 8, footerTop + 13);

                Rectangle escKey = new Rectangle(_escKeyHintRect.X, footerTop + 10, 38, 24);
                DrawKeycapBadge(g, escKey, "Esc");
                g.DrawString("Close", f, b, escKey.Right + 8, footerTop + 13);
            }
        }

        protected override async void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                HideOverlay();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                var selected = GetSelectedProfile();
                if (selected != null)
                {
                    await ApplyProfileInOverlayAsync(selected);
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up)
            {
                MoveSelection(e.KeyCode == Keys.Down ? 1 : -1);
                e.Handled = true;
            }
        }

        private void MoveSelection(int delta)
        {
            var list = _configService.Profiles;
            if (list.Count == 0) return;

            int idx = list.FindIndex(p => string.Equals(p.Id, _selectedProfileId, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) idx = 0;
            idx = (idx + delta + list.Count) % list.Count;
            _selectedProfileId = list[idx].Id;
            Invalidate();
        }

        private DnsProfile GetSelectedProfile()
        {
            if (!string.IsNullOrEmpty(_selectedProfileId))
            {
                var match = _configService.Profiles.FirstOrDefault(p => string.Equals(p.Id, _selectedProfileId, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }
            return _configService.Profiles.FirstOrDefault();
        }

        private async Task ApplyProfileInOverlayAsync(DnsProfile profile)
        {
            if (profile == null || _dnsService.IsOperationInProgress) return;

            _selectedProfileId = profile.Id;
            _configService.Settings.LastRequestedProfileId = profile.Id;
            _isBusy = true;
            _statusIsError = false;
            _statusFooterText = "Switching DNS to " + profile.Name + " (" + profile.PrimaryDns + ")...";
            Invalidate();

            DnsOperationResult res = await _dnsService.ApplyDnsProfileAsync(
                profile,
                _configService.Settings.SelectedAdapterId,
                _configService.Settings.AutoDetectAdapter,
                _configService.Profiles);

            _isBusy = false;
            if (res.Snapshot != null)
            {
                _currentSnapshot = res.Snapshot;
            }
            else
            {
                RefreshState();
            }

            if (res.Success)
            {
                _configService.Settings.LastChangeText = "Today, " + DateTime.Now.ToString("HH:mm");
                _configService.Save();
                _soundService.PlayConnect(_configService.Settings.EnableSounds);
                _statusIsError = false;
                _statusFooterText = res.Message;
            }
            else
            {
                _statusIsError = true;
                _statusFooterText = res.Message;
            }

            if (_onStateChangedCallback != null)
            {
                _onStateChangedCallback();
            }
            Invalidate();
        }

        private async Task DisableDnsInOverlayAsync()
        {
            if (_dnsService.IsOperationInProgress) return;

            _isBusy = true;
            _statusIsError = false;
            _statusFooterText = "Restoring Automatic (DHCP) DNS...";
            Invalidate();

            DnsOperationResult res = await _dnsService.DisableDnsAsync(
                _configService.Settings.SelectedAdapterId,
                _configService.Settings.AutoDetectAdapter,
                _configService.Profiles);

            _isBusy = false;
            if (res.Snapshot != null)
            {
                _currentSnapshot = res.Snapshot;
            }
            else
            {
                RefreshState();
            }

            if (res.Success)
            {
                _configService.Settings.LastChangeText = "Today, " + DateTime.Now.ToString("HH:mm");
                _configService.Save();
                _soundService.PlayDisconnect(_configService.Settings.EnableSounds);
                _statusIsError = false;
                _statusFooterText = res.Message;
            }
            else
            {
                _statusIsError = true;
                _statusFooterText = res.Message;
            }

            if (_onStateChangedCallback != null)
            {
                _onStateChangedCallback();
            }
            Invalidate();
        }

        private async Task FlushDnsInOverlayAsync()
        {
            _isBusy = true;
            _statusIsError = false;
            _statusFooterText = "Flushing Windows DNS Resolver Cache...";
            Invalidate();

            DnsOperationResult res = await _dnsService.FlushDnsCacheAsync();
            _isBusy = false;
            _statusIsError = !res.Success;
            _statusFooterText = res.Message;
            Invalidate();
        }

        private async Task TestCurrentDnsInOverlayAsync()
        {
            string targetIp = (_currentSnapshot != null && !string.IsNullOrWhiteSpace(_currentSnapshot.ActualPrimaryDns))
                ? _currentSnapshot.ActualPrimaryDns
                : (GetSelectedProfile() != null ? GetSelectedProfile().PrimaryDns : "1.1.1.1");

            _isBusy = true;
            _statusIsError = false;
            _statusFooterText = "Testing DNS response for " + targetIp + "...";
            Invalidate();

            DnsTestResult tr = await _dnsService.TestDnsServerAsync(targetIp, 1800);
            _isBusy = false;

            if (tr.IsReachable)
            {
                _statusIsError = false;
                _statusFooterText = string.Format("DNS {0} responded in {1} ms.", targetIp, tr.LatencyMs);
            }
            else
            {
                _statusIsError = true;
                _statusFooterText = string.Format("DNS {0} unreachable ({1}).", targetIp, tr.Details);
            }
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_activeTab == OverlayTab.Dns && _maxScroll > 0)
            {
                _scrollOffset -= (e.Delta / 120) * 34;
                if (_scrollOffset < 0) _scrollOffset = 0;
                if (_scrollOffset > _maxScroll) _scrollOffset = _maxScroll;
                RecalculateLayout();
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && _headerRect.Contains(e.Location) &&
                !_tabDnsRect.Contains(e.Location) && !_tabTestRect.Contains(e.Location) &&
                !_tabSettingsRect.Contains(e.Location) && !_closeBtnRect.Contains(e.Location))
            {
                _isDragging = true;
                _dragStart = e.Location;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isDragging)
            {
                Point p = PointToScreen(e.Location);
                Location = new Point(p.X - _dragStart.X, p.Y - _dragStart.Y);
                return;
            }

            string prevElem = _hoveredElement;
            int prevCard = _hoveredCardIndex;
            string prevPart = _hoveredCardPart;

            _hoveredElement = string.Empty;
            _hoveredCardIndex = -1;
            _hoveredCardPart = string.Empty;

            if (_closeBtnRect.Contains(e.Location)) _hoveredElement = "close";
            else if (_tabDnsRect.Contains(e.Location)) _hoveredElement = "tab_dns";
            else if (_tabTestRect.Contains(e.Location)) _hoveredElement = "tab_test";
            else if (_tabSettingsRect.Contains(e.Location)) _hoveredElement = "tab_settings";
            else if (_qaFlushRect.Contains(e.Location)) _hoveredElement = "qa_flush";
            else if (_qaDisableRect.Contains(e.Location)) _hoveredElement = "qa_disable";
            else if (_qaTestCurrentRect.Contains(e.Location)) _hoveredElement = "qa_test";
            else if (_enterKeyHintRect.Contains(e.Location)) _hoveredElement = "hint_enter";
            else if (_escKeyHintRect.Contains(e.Location)) _hoveredElement = "hint_esc";
            else if (_activeTab == OverlayTab.Dns)
            {
                for (int i = 0; i < _cardHits.Count; i++)
                {
                    var box = _cardHits[i];
                    if (box.CardRect.Contains(e.Location))
                    {
                        _hoveredCardIndex = i;
                        if (box.StarRect.Contains(e.Location)) _hoveredCardPart = "star";
                        else if (box.ActionBtnRect.Contains(e.Location)) _hoveredCardPart = "apply";
                        else _hoveredCardPart = "card";
                        break;
                    }
                }
            }
            else if (_activeTab == OverlayTab.Test && _btnTestAllRect.Contains(e.Location))
            {
                _hoveredElement = "btn_test_all";
            }
            else if (_activeTab == OverlayTab.Settings)
            {
                foreach (var kvp in _settingsHits)
                {
                    if (kvp.Value.Contains(e.Location))
                    {
                        _hoveredElement = kvp.Key;
                        break;
                    }
                }
            }

            Cursor = (!string.IsNullOrEmpty(_hoveredElement) || _hoveredCardIndex >= 0) ? Cursors.Hand : Cursors.Default;

            if (prevElem != _hoveredElement || prevCard != _hoveredCardIndex || prevPart != _hoveredCardPart)
            {
                Invalidate();
            }
        }

        protected override async void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isDragging)
            {
                _isDragging = false;
                return;
            }

            if (e.Button != MouseButtons.Left) return;

            if (_closeBtnRect.Contains(e.Location) || _escKeyHintRect.Contains(e.Location))
            {
                HideOverlay();
                return;
            }

            if (_enterKeyHintRect.Contains(e.Location))
            {
                var sel = GetSelectedProfile();
                if (sel != null) await ApplyProfileInOverlayAsync(sel);
                return;
            }

            if (_tabDnsRect.Contains(e.Location)) { _activeTab = OverlayTab.Dns; Invalidate(); return; }
            if (_tabTestRect.Contains(e.Location)) { _activeTab = OverlayTab.Test; Invalidate(); return; }
            if (_tabSettingsRect.Contains(e.Location)) { _activeTab = OverlayTab.Settings; Invalidate(); return; }

            if (_qaFlushRect.Contains(e.Location)) { await FlushDnsInOverlayAsync(); return; }
            if (_qaDisableRect.Contains(e.Location)) { await DisableDnsInOverlayAsync(); return; }
            if (_qaTestCurrentRect.Contains(e.Location)) { await TestCurrentDnsInOverlayAsync(); return; }

            if (_activeTab == OverlayTab.Dns)
            {
                for (int i = 0; i < _cardHits.Count; i++)
                {
                    var box = _cardHits[i];
                    if (box.CardRect.Contains(e.Location))
                    {
                        if (box.StarRect.Contains(e.Location))
                        {
                            box.Profile.IsFavorite = !box.Profile.IsFavorite;
                            _configService.Save();
                            if (_onStateChangedCallback != null) _onStateChangedCallback();
                            Invalidate();
                            return;
                        }

                        _selectedProfileId = box.Profile.Id;
                        await ApplyProfileInOverlayAsync(box.Profile);
                        return;
                    }
                }
            }
            else if (_activeTab == OverlayTab.Test)
            {
                if (_btnTestAllRect.Contains(e.Location))
                {
                    await RefreshLatenciesQuietlyAsync();
                }
            }
            else if (_activeTab == OverlayTab.Settings)
            {
                foreach (var kvp in _settingsHits)
                {
                    if (!kvp.Value.Contains(e.Location)) continue;
                    var s = _configService.Settings;
                    switch (kvp.Key)
                    {
                        case "ov_sounds":
                            s.EnableSounds = !s.EnableSounds;
                            break;
                        case "ov_notify":
                            s.EnableNotifications = !s.EnableNotifications;
                            break;
                        case "ov_auto_adapter":
                            s.AutoDetectAdapter = !s.AutoDetectAdapter;
                            break;
                        case "ov_cycle_shortcut":
                            string[] presets = { "Shift+Tab", "Ctrl+Shift+D", "Alt+D", "Ctrl+Tab", "F8" };
                            int idx = Array.FindIndex(presets, p => string.Equals(p, s.OverlayShortcut, StringComparison.OrdinalIgnoreCase));
                            s.OverlayShortcut = presets[(idx + 1) % presets.Length];
                            break;
                    }
                    _configService.Save();
                    if (_onStateChangedCallback != null) _onStateChangedCallback();
                    Invalidate();
                    break;
                }
            }
        }
    }
}
