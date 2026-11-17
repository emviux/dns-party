using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace DNSParty.UI
{
    public class FeedbackDialog : Form
    {
        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(
            int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        private ComboBox _cmbCategory;
        private TextBox _txtMessage;
        private Button _btnSubmit;
        private Button _btnCancel;
        private Label _lblStatus;
        private LinkLabel _lnkIssue;

        private readonly string _workerUrl = "https://dns-party-feedback.car-maintenance.workers.dev";
        private string _createdIssueUrl = "";

        public FeedbackDialog()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Size = new Size(500, 480);
            this.BackColor = Theme.WindowBackground;
            this.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 20, 20));

            InitializeComponents();
        }

        private void InitializeComponents()
        {
            Label lblTitle = new Label
            {
                Text = "Feedback & Support",
                Font = Theme.CreateBoldFont(12f),
                ForeColor = Theme.TextPrimary,
                Location = new Point(24, 24),
                AutoSize = true
            };
            this.Controls.Add(lblTitle);

            Label lblCategory = new Label
            {
                Text = "Category:",
                Font = Theme.CreateFont(9.5f),
                ForeColor = Theme.TextSecondary,
                Location = new Point(24, 60),
                AutoSize = true
            };
            this.Controls.Add(lblCategory);

            _cmbCategory = new ComboBox
            {
                Location = new Point(24, 84),
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = Theme.CreateFont(9.5f),
                BackColor = Theme.CardBackground,
                ForeColor = Theme.TextPrimary,
                FlatStyle = FlatStyle.Flat
            };
            _cmbCategory.Items.AddRange(new object[] { "Bug Report", "Feature Suggestion", "General Feedback" });
            _cmbCategory.SelectedIndex = 0;
            this.Controls.Add(_cmbCategory);

            Label lblMessage = new Label
            {
                Text = "Message:",
                Font = Theme.CreateFont(9.5f),
                ForeColor = Theme.TextSecondary,
                Location = new Point(24, 120),
                AutoSize = true
            };
            this.Controls.Add(lblMessage);

            _txtMessage = new TextBox
            {
                Location = new Point(24, 144),
                Width = 452,
                Height = 160,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = Theme.CreateFont(9.5f),
                BackColor = Theme.CardBackground,
                ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(_txtMessage);

            Label lblPrivacy = new Label
            {
                Text = "Your feedback will be submitted to the DNS Party GitHub repository and may be publicly visible. Please do not include passwords, personal information, or confidential data.",
                Font = Theme.CreateFont(8.5f),
                ForeColor = Theme.TextMuted,
                Location = new Point(24, 314),
                Size = new Size(452, 40)
            };
            this.Controls.Add(lblPrivacy);

            _lblStatus = new Label
            {
                Text = "",
                Font = Theme.CreateFont(9.5f),
                ForeColor = Theme.StatusWarning,
                Location = new Point(24, 360),
                Size = new Size(452, 40)
            };
            this.Controls.Add(_lblStatus);

            _lnkIssue = new LinkLabel
            {
                Text = "Open GitHub Issue",
                Font = Theme.CreateFont(9.5f),
                LinkColor = Theme.AccentViolet,
                Location = new Point(24, 400),
                AutoSize = true,
                Visible = false
            };
            _lnkIssue.LinkClicked += (s, e) => { if (!string.IsNullOrEmpty(_createdIssueUrl)) Process.Start(_createdIssueUrl); };
            this.Controls.Add(_lnkIssue);

            _btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(316, 420),
                Size = new Size(80, 36),
                FlatStyle = FlatStyle.Flat,
                Font = Theme.CreateFont(9.5f),
                ForeColor = Theme.TextPrimary,
                BackColor = Theme.CardBackground
            };
            _btnCancel.FlatAppearance.BorderColor = Theme.CardBorder;
            _btnCancel.Click += (s, e) => this.Close();
            this.Controls.Add(_btnCancel);

            _btnSubmit = new Button
            {
                Text = "Submit",
                Location = new Point(406, 420),
                Size = new Size(80, 36),
                FlatStyle = FlatStyle.Flat,
                Font = Theme.CreateFont(9.5f),
                ForeColor = Color.White,
                BackColor = Theme.AccentViolet
            };
            _btnSubmit.FlatAppearance.BorderColor = Theme.AccentViolet;
            _btnSubmit.Click += async (s, e) => await SubmitFeedbackAsync();
            this.Controls.Add(_btnSubmit);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(Theme.CardBorder, 2f))
            {
                e.Graphics.DrawPath(p, Theme.CreateRoundedRectPath(new Rectangle(0, 0, Width - 1, Height - 1), 20));
            }
        }

        private string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        private async Task SubmitFeedbackAsync()
        {
            string msg = _txtMessage.Text.Trim();
            if (string.IsNullOrEmpty(msg))
            {
                _lblStatus.Text = "Please enter a message.";
                _lblStatus.ForeColor = Theme.DangerRed;
                return;
            }

            if (msg.Length > 5000)
            {
                _lblStatus.Text = "Message exceeds 5000 characters.";
                _lblStatus.ForeColor = Theme.DangerRed;
                return;
            }

            _btnSubmit.Enabled = false;
            _btnCancel.Enabled = false;
            _txtMessage.Enabled = false;
            _lblStatus.ForeColor = Theme.TextSecondary;
            _lblStatus.Text = "Submitting feedback...";

            try
            {
                string osName = Environment.OSVersion.VersionString;
                string refId = Guid.NewGuid().ToString("N").Substring(0, 8);
                string category = _cmbCategory.SelectedItem.ToString();
                string appVersion = VersionInfo.CurrentVersion;

                string json = "{" +
                    "\"category\":\"" + EscapeJson(category) + "\"," +
                    "\"message\":\"" + EscapeJson(msg) + "\"," +
                    "\"appVersion\":\"" + EscapeJson(appVersion) + "\"," +
                    "\"os\":\"" + EscapeJson(osName) + "\"," +
                    "\"referenceId\":\"" + EscapeJson(refId) + "\"" +
                "}";

                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(15);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(_workerUrl, content);
                    var responseText = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        if (responseText.Contains("\"success\":true"))
                        {
                            var matchNum = Regex.Match(responseText, "\"issueNumber\":(\\d+)");
                            var matchUrl = Regex.Match(responseText, "\"issueUrl\":\"([^\"]+)\"");

                            string issueNum = matchNum.Success ? matchNum.Groups[1].Value : "Unknown";
                            _createdIssueUrl = matchUrl.Success ? matchUrl.Groups[1].Value : "";

                            _lblStatus.ForeColor = Theme.StatusGreen;
                            _lblStatus.Text = $"Thank you! Your feedback has been submitted successfully.\nReference: #{issueNum}";
                            if (!string.IsNullOrEmpty(_createdIssueUrl))
                            {
                                _lnkIssue.Visible = true;
                            }
                            _btnCancel.Text = "Close";
                            _btnCancel.Enabled = true;
                            return; // Success
                        }
                    }

                    string errorMsg = "Failed to submit.";
                    var matchErr = Regex.Match(responseText, "\"error\":\"([^\"]+)\"");
                    if (matchErr.Success)
                    {
                        errorMsg = matchErr.Groups[1].Value;
                    }

                    _lblStatus.ForeColor = Theme.DangerRed;
                    _lblStatus.Text = $"Error: {errorMsg}";
                }
            }
            catch (Exception ex)
            {
                _lblStatus.ForeColor = Theme.DangerRed;
                _lblStatus.Text = "Connection failed. Please check your internet connection.";
            }

            _btnSubmit.Enabled = true;
            _btnCancel.Enabled = true;
            _txtMessage.Enabled = true;
        }
    }
}
