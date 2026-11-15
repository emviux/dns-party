using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DNSParty.UI
{
    public class TrayMenuColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground
        {
            get { return Theme.CardBackground; }
        }

        public override Color ImageMarginGradientBegin
        {
            get { return Theme.CardBackground; }
        }

        public override Color ImageMarginGradientMiddle
        {
            get { return Theme.CardBackground; }
        }

        public override Color ImageMarginGradientEnd
        {
            get { return Theme.CardBackground; }
        }

        public override Color MenuBorder
        {
            get { return Theme.CardBorder; }
        }

        public override Color MenuItemBorder
        {
            get { return Color.Transparent; }
        }

        public override Color MenuItemSelected
        {
            get { return Theme.SecondaryButtonHover; }
        }

        public override Color MenuItemSelectedGradientBegin
        {
            get { return Theme.SecondaryButtonHover; }
        }

        public override Color MenuItemSelectedGradientEnd
        {
            get { return Theme.SecondaryButtonHover; }
        }

        public override Color SeparatorDark
        {
            get { return Theme.CardBorder; }
        }

        public override Color SeparatorLight
        {
            get { return Color.Transparent; }
        }
    }

    public class TrayMenuRenderer : ToolStripProfessionalRenderer
    {
        public TrayMenuRenderer() : base(new TrayMenuColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.ConfigureHighQuality(g);
            using (SolidBrush bg = new SolidBrush(Theme.CardBackground))
            {
                g.FillRectangle(bg, e.AffectedBounds);
            }
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            // Keep image margin seamless with the menu background
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.ConfigureHighQuality(g);
            Rectangle rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            using (Pen borderPen = new Pen(Theme.CardBorder, 1.5f))
            {
                g.DrawRectangle(borderPen, rect);
            }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.ConfigureHighQuality(g);

            Rectangle itemRect = new Rectangle(4, 2, e.Item.Width - 8, e.Item.Height - 4);
            if (e.Item.Selected && e.Item.Enabled)
            {
                using (SolidBrush hoverBrush = new SolidBrush(Theme.SecondaryButtonHover))
                using (Pen hoverPen = new Pen(Theme.CardHoverBorder, 1f))
                {
                    Theme.FillRoundedRect(g, hoverBrush, itemRect, 6);
                    Theme.DrawRoundedRect(g, hoverPen, itemRect, 6);
                }
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            var menuItem = e.Item as ToolStripMenuItem;
            if (menuItem != null && menuItem.Checked)
            {
                e.TextColor = Theme.TextPrimary;
            }
            else if (!e.Item.Enabled)
            {
                e.TextColor = Theme.TextSecondary;
            }
            else
            {
                e.TextColor = Theme.TextPrimary;
            }
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.ConfigureHighQuality(g);

            // Crisp green checkmark (#22C55E) matching ui.png tray menu
            Rectangle r = e.ImageRectangle;
            RectangleF iconBounds = new RectangleF(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
            IconRenderer.DrawIcon(g, "check", iconBounds, Theme.StatusGreen);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            int y = e.Item.Height / 2;
            using (Pen pen = new Pen(Theme.CardBorder, 1f))
            {
                g.DrawLine(pen, 10, y, e.Item.Width - 10, y);
            }
        }

        /// <summary>
        /// Renders a crisp 16x16 or 18x18 icon bitmap for use on ContextMenuStrip items.
        /// </summary>
        public static Bitmap CreateMenuIconBitmap(string iconName, Color color, int size = 18, bool isProvider = false)
        {
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                Theme.ConfigureHighQuality(g);
                g.Clear(Color.Transparent);
                RectangleF rect = new RectangleF(1f, 1f, size - 2f, size - 2f);
                if (isProvider)
                {
                    IconRenderer.DrawProviderIcon(g, iconName, rect);
                }
                else
                {
                    IconRenderer.DrawIcon(g, iconName, rect, color, iconName == "app-logo");
                }
            }
            return bmp;
        }
    }
}
