using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DNSParty.Models;

namespace DNSParty.UI
{
    public class AddEditDnsDialog : Form
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

        private readonly bool _isEditMode;
        private readonly DnsProfile _workingProfile;

        private TextBox _txtName;
        private TextBox _txtTag;
        private TextBox _txtPrimary;
        private TextBox _txtSecondary;

        private bool _isIPv6;
        private bool _isFavorite;
        private string _errorMessage = string.Empty;

        // Interactive hit rectangles (scaled in OnLayout/RecalcRects)
        private Rectangle _closeButtonRect;
        private Rectangle _ipv4ToggleRect;
        private Rectangle _ipv6ToggleRect;
        private Rectangle _favoriteToggleRect;
        private Rectangle _saveButtonRect;
        private Rectangle _cancelButtonRect;

        private Rectangle _nameBoxBounds;
        private Rectangle _tagBoxBounds;
        private Rectangle _primaryBoxBounds;
        private Rectangle _secondaryBoxBounds;
        private Rectangle _errorBannerBounds;

        private bool _hoverClose;
        private bool _hoverIPv4;
        private bool _hoverIPv6;
        private bool _hoverFavorite;
        private bool _hoverSave;
        private bool _hoverCancel;

        private Point _dragStartPoint;
        private bool _isDraggingTitleBar;

        public DnsProfile ResultProfile { get; private set; }

        public AddEditDnsDialog(DnsProfile existingProfile = null)
        {
            _isEditMode = existingProfile != null;
            _workingProfile = existingProfile != null ? existingProfile.Clone() : new DnsProfile
            {
                Name = string.Empty,
                Tag = "Custom",
                PrimaryDns = string.Empty,
                SecondaryDns = string.Empty,
                IsIPv6 = false,
                IsFavorite = false,
                IsDefaultProvider = false,
                IconKey = "custom"
            };

            _isIPv6 = _workingProfile.IsIPv6;
            _isFavorite = _workingProfile.IsFavorite;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            BackColor = Theme.WindowBackground;
            ForeColor = Theme.TextPrimary;
            Font = Theme.CreateFont(9.5f);
            Icon = IconRenderer.GetAppIcon();
            Text = _isEditMode ? "Edit Custom DNS" : "Add Custom DNS";
            float scale = GetDpiScale();
            ClientSize = new Size(S(460, scale), S(610, scale));
            KeyPreview = true;

            InitializeInputs();
            UpdateLayoutRects();
        }

        private float GetDpiScale()
        {
            using (Graphics g = CreateGraphics())
            {
                return g.DpiX / 96f;
            }
        }

        private int S(int value, float scale)
        {
            return (int)Math.Round(value * scale);
        }

        private void InitializeInputs()
        {
            _txtName = CreateStyledTextBox(_workingProfile.Name);
            _txtTag = CreateStyledTextBox(_workingProfile.Tag);
            _txtPrimary = CreateStyledTextBox(_workingProfile.PrimaryDns, true);
            _txtSecondary = CreateStyledTextBox(_workingProfile.SecondaryDns, true);

            _txtName.TextChanged += OnInputTextChanged;
            _txtTag.TextChanged += OnInputTextChanged;
            _txtPrimary.TextChanged += OnInputTextChanged;
            _txtSecondary.TextChanged += OnInputTextChanged;

            Controls.Add(_txtName);
            Controls.Add(_txtTag);
            Controls.Add(_txtPrimary);
            Controls.Add(_txtSecondary);
        }

        private TextBox CreateStyledTextBox(string initialValue, bool monospace = false)
        {
            TextBox tb = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Theme.InputBackground,
                ForeColor = Theme.TextPrimary,
                Font = monospace ? Theme.CreateMonoFont(10.5f) : Theme.CreateFont(10f),
                Text = initialValue ?? string.Empty
            };

            tb.GotFocus += (s, e) => Invalidate();
            tb.LostFocus += (s, e) =>
            {
                ValidateInputs(false);
                Invalidate();
            };

            return tb;
        }

        private void OnInputTextChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_errorMessage))
            {
                ValidateInputs(false);
            }
            Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyRoundedRegionAndDwm();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateLayoutRects();
            ApplyRoundedRegionAndDwm();
        }

        private void ApplyRoundedRegionAndDwm()
        {
            try
            {
                // Try Windows 11 native DWM rounded corners (DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2)
                int preference = 2;
                int hr = DwmSetWindowAttribute(Handle, 33, ref preference, sizeof(int));
                if (hr != 0)
                {
                    IntPtr rgn = CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 20, 20);
                    Region = Region.FromHrgn(rgn);
                }
            }
            catch
            {
                try
                {
                    IntPtr rgn = CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 20, 20);
                    Region = Region.FromHrgn(rgn);
                }
                catch
                {
                }
            }
        }

        private void UpdateLayoutRects()
        {
            float scale = GetDpiScale();
            int w = ClientSize.Width;
            int h = ClientSize.Height;

            int padX = S(26, scale);
            int contentW = w - padX * 2;

            _closeButtonRect = new Rectangle(w - S(42, scale), S(12, scale), S(30, scale), S(30, scale));

            // Name & Tag row (side by side)
            int row1BoxY = S(92, scale);
            int boxH = S(38, scale);
            int nameW = (int)(contentW * 0.60f);
            int gap = S(14, scale);
            int tagW = contentW - nameW - gap;

            _nameBoxBounds = new Rectangle(padX, row1BoxY, nameW, boxH);
            _tagBoxBounds = new Rectangle(padX + nameW + gap, row1BoxY, tagW, boxH);

            // IPv4 / IPv6 toggle pills
            int toggleY = S(162, scale);
            int toggleW = S(96, scale);
            int toggleH = S(32, scale);
            _ipv4ToggleRect = new Rectangle(padX, toggleY, toggleW, toggleH);
            _ipv6ToggleRect = new Rectangle(padX + toggleW + S(10, scale), toggleY, toggleW, toggleH);

            // Primary DNS
            int priBoxY = S(230, scale);
            _primaryBoxBounds = new Rectangle(padX, priBoxY, contentW, boxH);

            // Secondary DNS
            int secBoxY = S(304, scale);
            _secondaryBoxBounds = new Rectangle(padX, secBoxY, contentW, boxH);

            // Favorite toggle row
            int favY = S(366, scale);
            _favoriteToggleRect = new Rectangle(padX, favY, S(210, scale), S(30, scale));

            // Error banner
            _errorBannerBounds = new Rectangle(padX, S(408, scale), contentW, S(34, scale));

            // Bottom buttons (Footer area is bottom 80px)
            int footerH = S(80, scale);
            int btnW = S(116, scale);
            int btnH = S(38, scale);
            int btnY = h - footerH + (footerH - btnH) / 2;
            _saveButtonRect = new Rectangle(w - padX - btnW, btnY, btnW, btnH);
            _cancelButtonRect = new Rectangle(_saveButtonRect.Left - S(12, scale) - btnW, btnY, btnW, btnH);

            // Position child TextBoxes vertically centered inside their custom-drawn frames
            PositionTextBox(_txtName, _nameBoxBounds, scale);
            PositionTextBox(_txtTag, _tagBoxBounds, scale);
            PositionTextBox(_txtPrimary, _primaryBoxBounds, scale);
            PositionTextBox(_txtSecondary, _secondaryBoxBounds, scale);
        }

        private void PositionTextBox(TextBox tb, Rectangle frame, float scale)
        {
            if (tb == null) return;
            int insetX = S(12, scale);
            int textHeight = tb.PreferredHeight;
            int insetY = Math.Max(4, (frame.Height - textHeight) / 2);
            tb.BackColor = Theme.InputBackground;
            tb.ForeColor = Theme.TextPrimary;
            tb.Location = new Point(frame.X + insetX, frame.Y + insetY);
            tb.Width = Math.Max(40, frame.Width - insetX * 2);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                TrySaveAndClose();
                e.Handled = true;
            }
        }

        private bool ValidateInputs(bool requireAll)
        {
            string name = (_txtName.Text ?? string.Empty).Trim();
            string primary = (_txtPrimary.Text ?? string.Empty).Trim();
            string secondary = (_txtSecondary.Text ?? string.Empty).Trim();

            if (requireAll && string.IsNullOrWhiteSpace(name))
            {
                _errorMessage = "Please enter a profile name (e.g., My DNS).";
                return false;
            }

            if (requireAll || !string.IsNullOrWhiteSpace(primary))
            {
                string priErr;
                if (!DnsProfile.ValidateDnsAddress(primary, _isIPv6, out priErr))
                {
                    _errorMessage = "Primary DNS: " + priErr;
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(secondary))
            {
                string secErr;
                if (!DnsProfile.ValidateDnsAddress(secondary, _isIPv6, out secErr))
                {
                    _errorMessage = "Secondary DNS: " + secErr;
                    return false;
                }

                if (string.Equals(primary, secondary, StringComparison.OrdinalIgnoreCase))
                {
                    _errorMessage = "Secondary DNS cannot be identical to Primary DNS.";
                    return false;
                }
            }

            _errorMessage = string.Empty;
            return true;
        }

        private void TrySaveAndClose()
        {
            if (!ValidateInputs(true))
            {
                Invalidate();
                return;
            }

            _workingProfile.Name = _txtName.Text.Trim();
            _workingProfile.Tag = (_txtTag.Text ?? string.Empty).Trim();
            _workingProfile.PrimaryDns = _txtPrimary.Text.Trim();
            _workingProfile.SecondaryDns = (_txtSecondary.Text ?? string.Empty).Trim();
            _workingProfile.IsIPv6 = _isIPv6;
            _workingProfile.IsFavorite = _isFavorite;
            if (string.IsNullOrWhiteSpace(_workingProfile.IconKey))
            {
                _workingProfile.IconKey = "custom";
            }

            ResultProfile = _workingProfile;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            Theme.ConfigureHighQuality(g);
            float scale = GetDpiScale();

            Rectangle winRect = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);

            // Window background & subtle glowing border
            using (SolidBrush bgBrush = new SolidBrush(Theme.WindowBackground))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }

            // Footer background
            int footerH = S(80, scale);
            Rectangle footerRect = new Rectangle(1, ClientSize.Height - footerH - 1, ClientSize.Width - 2, footerH);
            using (SolidBrush footerBrush = new SolidBrush(Theme.SidebarBackground))
            {
                g.FillRectangle(footerBrush, footerRect);
            }
            using (Pen sepPen = new Pen(Theme.CardBorder, 1f))
            {
                g.DrawLine(sepPen, 0, footerRect.Top, ClientSize.Width, footerRect.Top);
            }

            // Titlebar header area
            Rectangle headerRect = new Rectangle(1, 1, ClientSize.Width - 2, S(52, scale));
            using (SolidBrush headerBrush = new SolidBrush(Theme.SidebarBackground))
            {
                g.FillRectangle(headerBrush, headerRect);
            }
            using (Pen sepPen = new Pen(Theme.CardBorder, 1f))
            {
                g.DrawLine(sepPen, 0, headerRect.Bottom, ClientSize.Width, headerRect.Bottom);
            }

            // App icon & dialog title
            RectangleF logoRect = new RectangleF(S(24, scale), S(15, scale), S(22, scale), S(22, scale));
            IconRenderer.DrawAppLogo(g, logoRect, true);

            using (Font titleFont = Theme.CreateSemiboldFont(11.5f))
            using (SolidBrush titleBrush = new SolidBrush(Theme.TextPrimary))
            {
                string titleText = _isEditMode ? "Edit Custom DNS" : "Add Custom DNS";
                g.DrawString(titleText, titleFont, titleBrush, S(54, scale), S(16, scale));
            }

            // Close button
            if (_hoverClose)
            {
                using (SolidBrush closeHover = new SolidBrush(Color.FromArgb(60, 239, 68, 68)))
                {
                    Theme.FillRoundedRect(g, closeHover, _closeButtonRect, S(6, scale));
                }
            }
            RectangleF closeIconRect = new RectangleF(
                _closeButtonRect.X + _closeButtonRect.Width * 0.25f,
                _closeButtonRect.Y + _closeButtonRect.Height * 0.25f,
                _closeButtonRect.Width * 0.5f,
                _closeButtonRect.Height * 0.5f);
            IconRenderer.DrawIcon(g, "close", closeIconRect, _hoverClose ? Color.White : Theme.TextSecondary);

            // Field Labels & Input Frames
            using (Font labelFont = Theme.CreateSemiboldFont(8.75f))
            using (Font hintFont = Theme.CreateFont(8.25f))
            using (SolidBrush labelBrush = new SolidBrush(Theme.TextSecondary))
            using (SolidBrush hintBrush = new SolidBrush(Theme.TextMuted))
            {
                // Row 1: Profile Name & Tag
                g.DrawString("Profile Name", labelFont, labelBrush, _nameBoxBounds.X, _nameBoxBounds.Y - S(22, scale));
                g.DrawString("Tag (Optional)", labelFont, labelBrush, _tagBoxBounds.X, _tagBoxBounds.Y - S(22, scale));

                DrawInputBoxFrame(g, _nameBoxBounds, _txtName.Focused, scale);
                DrawInputBoxFrame(g, _tagBoxBounds, _txtTag.Focused, scale);

                // IPv4 / IPv6 Address Family Selector
                g.DrawString("Address Family:", labelFont, labelBrush, _ipv4ToggleRect.X, _ipv4ToggleRect.Y - S(20, scale));
                DrawTogglePill(g, _ipv4ToggleRect, "IPv4", !_isIPv6, _hoverIPv4, scale);
                DrawTogglePill(g, _ipv6ToggleRect, "IPv6", _isIPv6, _hoverIPv6, scale);

                // Primary DNS
                string priHint = _isIPv6 ? "e.g., 2606:4700:4700::1111" : "e.g., 1.1.1.1";
                g.DrawString("Primary DNS Server", labelFont, labelBrush, _primaryBoxBounds.X, _primaryBoxBounds.Y - S(22, scale));
                SizeF hintSize1 = g.MeasureString(priHint, hintFont);
                g.DrawString(priHint, hintFont, hintBrush, _primaryBoxBounds.Right - hintSize1.Width, _primaryBoxBounds.Y - S(21, scale));
                DrawInputBoxFrame(g, _primaryBoxBounds, _txtPrimary.Focused, scale);

                // Secondary DNS
                string secHint = _isIPv6 ? "Optional (e.g., 2606:4700:4700::1001)" : "Optional (e.g., 1.0.0.1)";
                g.DrawString("Secondary DNS Server", labelFont, labelBrush, _secondaryBoxBounds.X, _secondaryBoxBounds.Y - S(22, scale));
                SizeF hintSize2 = g.MeasureString(secHint, hintFont);
                g.DrawString(secHint, hintFont, hintBrush, _secondaryBoxBounds.Right - hintSize2.Width, _secondaryBoxBounds.Y - S(21, scale));
                DrawInputBoxFrame(g, _secondaryBoxBounds, _txtSecondary.Focused, scale);
            }

            // Favorite Star Toggle
            RectangleF starRect = new RectangleF(
                _favoriteToggleRect.X + S(2, scale),
                _favoriteToggleRect.Y + S(5, scale),
                S(20, scale),
                S(20, scale));
            IconRenderer.DrawStar(
                g,
                starRect,
                _isFavorite ? Theme.StarGold : (_hoverFavorite ? Theme.AccentViolet : Theme.StarOutline),
                _isFavorite);

            using (Font favFont = Theme.CreateSemiboldFont(9.5f))
            using (SolidBrush favBrush = new SolidBrush(_isFavorite ? Theme.TextPrimary : Theme.TextSecondary))
            {
                g.DrawString(
                    "Mark as Favorite DNS",
                    favFont,
                    favBrush,
                    starRect.Right + S(10, scale),
                    _favoriteToggleRect.Y + S(5, scale));
            }

            // Validation Error Banner
            if (!string.IsNullOrEmpty(_errorMessage))
            {
                using (SolidBrush errBg = new SolidBrush(Theme.DangerBannerBackground))
                using (Pen errPen = new Pen(Theme.DangerBannerBorder, 1f))
                using (Font errFont = Theme.CreateSemiboldFont(8.5f))
                using (SolidBrush errText = new SolidBrush(Theme.DangerRed))
                {
                    Theme.FillRoundedRect(g, errBg, _errorBannerBounds, S(7, scale));
                    Theme.DrawRoundedRect(g, errPen, _errorBannerBounds, S(7, scale));

                    StringFormat sf = new StringFormat
                    {
                        Alignment = StringAlignment.Near,
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter,
                        FormatFlags = StringFormatFlags.NoWrap
                    };
                    Rectangle textRect = new Rectangle(
                        _errorBannerBounds.X + S(12, scale),
                        _errorBannerBounds.Y,
                        _errorBannerBounds.Width - S(24, scale),
                        _errorBannerBounds.Height);
                    g.DrawString(_errorMessage, errFont, errText, textRect, sf);
                }
            }

            // Bottom Cancel & Save Buttons
            DrawSecondaryButton(g, _cancelButtonRect, "Cancel", _hoverCancel, scale);
            DrawPrimaryButton(g, _saveButtonRect, _isEditMode ? "Save DNS" : "Add DNS", _hoverSave, scale);

            // Outer modal border
            using (Pen borderPen = new Pen(Theme.ActiveCardBorder, 1.5f))
            {
                Theme.DrawRoundedRect(g, borderPen, winRect, S(12, scale));
            }
        }

        private void DrawInputBoxFrame(Graphics g, Rectangle rect, bool focused, float scale)
        {
            int radius = S(8, scale);
            using (SolidBrush bg = new SolidBrush(Theme.InputBackground))
            {
                Theme.FillRoundedRect(g, bg, rect, radius);
            }

            Color borderColor = focused ? Theme.InputFocusBorder : Theme.InputBorder;
            float borderWidth = focused ? 1.6f : 1.0f;
            using (Pen pen = new Pen(borderColor, borderWidth))
            {
                Theme.DrawRoundedRect(g, pen, rect, radius);
            }
        }

        private void DrawTogglePill(Graphics g, Rectangle rect, string text, bool active, bool hovered, float scale)
        {
            int radius = S(8, scale);
            if (active)
            {
                Theme.FillRoundedGradient(g, rect, radius, Theme.SidebarActiveStart, Theme.SidebarActiveEnd);
                using (Pen pen = new Pen(Theme.ActiveCardBorder, 1.5f))
                {
                    Theme.DrawRoundedRect(g, pen, rect, radius);
                }
            }
            else
            {
                Color bg = hovered ? Theme.SecondaryButtonHover : Theme.SecondaryButtonBackground;
                using (SolidBrush b = new SolidBrush(bg))
                using (Pen pen = new Pen(Theme.SecondaryButtonBorder, 1f))
                {
                    Theme.FillRoundedRect(g, b, rect, radius);
                    Theme.DrawRoundedRect(g, pen, rect, radius);
                }
            }

            using (Font f = Theme.CreateSemiboldFont(9f))
            using (SolidBrush tb = new SolidBrush(active ? Color.White : Theme.TextSecondary))
            {
                StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(text, f, tb, rect, sf);
            }
        }

        private void DrawSecondaryButton(Graphics g, Rectangle rect, string text, bool hovered, float scale)
        {
            int radius = S(10, scale);
            Color bg = hovered ? Theme.SecondaryButtonHover : Theme.SecondaryButtonBackground;
            using (SolidBrush b = new SolidBrush(bg))
            using (Pen pen = new Pen(Theme.SecondaryButtonBorder, 1f))
            {
                Theme.FillRoundedRect(g, b, rect, radius);
                Theme.DrawRoundedRect(g, pen, rect, radius);
            }

            using (Font f = Theme.CreateSemiboldFont(9.5f))
            using (SolidBrush tb = new SolidBrush(Theme.TextPrimary))
            {
                StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(text, f, tb, rect, sf);
            }
        }

        private void DrawPrimaryButton(Graphics g, Rectangle rect, string text, bool hovered, float scale)
        {
            int radius = S(10, scale);
            Color c1 = hovered ? Theme.PrimaryButtonHover : Theme.PrimaryButtonStart;
            Color c2 = hovered ? Theme.PrimaryButtonEnd : Theme.PrimaryButtonEnd;
            Theme.FillRoundedGradient(g, rect, radius, c1, c2);

            using (Pen pen = new Pen(Theme.PrimaryButtonBorder, 1f))
            {
                Theme.DrawRoundedRect(g, pen, rect, radius);
            }

            using (Font f = Theme.CreateSemiboldFont(9.5f))
            using (SolidBrush tb = new SolidBrush(Color.White))
            {
                StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(text, f, tb, rect, sf);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            float scale = GetDpiScale();
            if (_closeButtonRect.Contains(e.Location))
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            if (_ipv4ToggleRect.Contains(e.Location))
            {
                if (_isIPv6)
                {
                    _isIPv6 = false;
                    ValidateInputs(false);
                    Invalidate();
                }
                return;
            }

            if (_ipv6ToggleRect.Contains(e.Location))
            {
                if (!_isIPv6)
                {
                    _isIPv6 = true;
                    ValidateInputs(false);
                    Invalidate();
                }
                return;
            }

            if (_favoriteToggleRect.Contains(e.Location))
            {
                _isFavorite = !_isFavorite;
                Invalidate();
                return;
            }

            if (_cancelButtonRect.Contains(e.Location))
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            if (_saveButtonRect.Contains(e.Location))
            {
                TrySaveAndClose();
                return;
            }

            if (_nameBoxBounds.Contains(e.Location))
            {
                _txtName.Focus();
                return;
            }
            if (_tagBoxBounds.Contains(e.Location))
            {
                _txtTag.Focus();
                return;
            }
            if (_primaryBoxBounds.Contains(e.Location))
            {
                _txtPrimary.Focus();
                return;
            }
            if (_secondaryBoxBounds.Contains(e.Location))
            {
                _txtSecondary.Focus();
                return;
            }

            if (e.Y <= S(52, scale))
            {
                _isDraggingTitleBar = true;
                _dragStartPoint = e.Location;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isDraggingTitleBar && e.Button == MouseButtons.Left)
            {
                Point screenPt = PointToScreen(e.Location);
                Location = new Point(screenPt.X - _dragStartPoint.X, screenPt.Y - _dragStartPoint.Y);
                return;
            }

            bool hc = _closeButtonRect.Contains(e.Location);
            bool h4 = _ipv4ToggleRect.Contains(e.Location);
            bool h6 = _ipv6ToggleRect.Contains(e.Location);
            bool hf = _favoriteToggleRect.Contains(e.Location);
            bool hs = _saveButtonRect.Contains(e.Location);
            bool hcan = _cancelButtonRect.Contains(e.Location);

            if (hc != _hoverClose || h4 != _hoverIPv4 || h6 != _hoverIPv6 ||
                hf != _hoverFavorite || hs != _hoverSave || hcan != _hoverCancel)
            {
                _hoverClose = hc;
                _hoverIPv4 = h4;
                _hoverIPv6 = h6;
                _hoverFavorite = hf;
                _hoverSave = hs;
                _hoverCancel = hcan;

                Cursor = (hc || h4 || h6 || hf || hs || hcan) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isDraggingTitleBar = false;
        }
    }
}
