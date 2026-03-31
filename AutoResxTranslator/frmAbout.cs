using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

/* 
 * AutoResxTranslator
 * by Salar Khalilzadeh
 * 
 * https://github.com/salarcode/AutoResxTranslator/
 * Mozilla Public License v2
 */
namespace AutoResxTranslator
{
	public partial class frmAbout : Form
	{
		private readonly Color _ubsBg = Color.FromArgb(246, 248, 252);
		private readonly Color _ubsSurface = Color.White;
		private readonly Color _ubsText = Color.FromArgb(0, 20, 50);
		private readonly Color _ubsAccent = Color.FromArgb(255, 94, 12);
		private readonly Color _ubsAccentPressed = Color.FromArgb(230, 77, 0);
		private readonly Color _ubsDark = Color.FromArgb(0, 20, 50);
		private readonly Color _ubsDarkHover = Color.FromArgb(9, 35, 76);
		private readonly Color _ubsDarkPressed = Color.FromArgb(0, 14, 36);
		private bool _closeHovered;
		private bool _closePressed;

		public frmAbout()
		{
			InitializeComponent();
		}

		private void btnClose_Click(object sender, EventArgs e)
		{
			Close();
		}

		private void frmAbout_Load(object sender, EventArgs e)
		{
			ApplyUbsTheme();
			lblVersion.Text = Assembly.GetExecutingAssembly().GetName().Version.ToString();
			this.Icon = Application.OpenForms[0].Icon;
		}

		private void ApplyUbsTheme()
		{
			Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
			BackColor = _ubsBg;
			ForeColor = _ubsText;
			Paint += frmAbout_PaintAccent;

			groupBox1.BackColor = _ubsSurface;
			groupBox1.ForeColor = _ubsText;
			pictureBox1.BackColor = _ubsSurface;
			pictureBox1.BorderStyle = BorderStyle.FixedSingle;

			btnClose.FlatStyle = FlatStyle.Flat;
			btnClose.UseVisualStyleBackColor = false;
			btnClose.FlatAppearance.BorderSize = 1;
			btnClose.FlatAppearance.BorderColor = _ubsDark;
			btnClose.FlatAppearance.MouseOverBackColor = _ubsDarkHover;
			btnClose.FlatAppearance.MouseDownBackColor = _ubsDarkPressed;
			btnClose.BackColor = _ubsDark;
			btnClose.ForeColor = Color.White;
			btnClose.Paint += btnClose_PaintModern;
			btnClose.Resize += btnClose_ResizeModern;
			btnClose.MouseEnter += btnClose_MouseEnterModern;
			btnClose.MouseLeave += btnClose_MouseLeaveModern;
			btnClose.MouseDown += btnClose_MouseDownModern;
			btnClose.MouseUp += btnClose_MouseUpModern;
			ApplyRoundedRegion(btnClose, 10);

			foreach (Control control in groupBox1.Controls)
			{
				if (control is Label label)
				{
					label.ForeColor = _ubsText;
				}
				else if (control is LinkLabel link)
				{
					link.LinkColor = _ubsAccent;
					link.ActiveLinkColor = _ubsAccentPressed;
					link.VisitedLinkColor = _ubsAccent;
					link.LinkBehavior = LinkBehavior.HoverUnderline;
				}
			}

			groupBox1.FlatStyle = FlatStyle.Flat;
		}

		private void frmAbout_PaintAccent(object sender, PaintEventArgs e)
		{
			using (var brush = new SolidBrush(_ubsAccent))
			{
				e.Graphics.FillRectangle(brush, new Rectangle(0, 0, Width, 4));
			}
		}

		private void btnClose_ResizeModern(object sender, EventArgs e)
		{
			ApplyRoundedRegion(btnClose, 10);
		}

		private void btnClose_MouseEnterModern(object sender, EventArgs e)
		{
			_closeHovered = true;
			btnClose.Invalidate();
		}

		private void btnClose_MouseLeaveModern(object sender, EventArgs e)
		{
			_closeHovered = false;
			_closePressed = false;
			btnClose.Invalidate();
		}

		private void btnClose_MouseDownModern(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				_closePressed = true;
				btnClose.Invalidate();
			}
		}

		private void btnClose_MouseUpModern(object sender, MouseEventArgs e)
		{
			_closePressed = false;
			btnClose.Invalidate();
		}

		private void btnClose_PaintModern(object sender, PaintEventArgs e)
		{
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			var rect = new Rectangle(0, 0, btnClose.Width - 1, btnClose.Height - 1);
			var fill = _closePressed ? _ubsDarkPressed : (_closeHovered ? _ubsDarkHover : _ubsDark);

			using (var path = CreateRoundRectPath(rect, 10))
			using (var brush = new SolidBrush(fill))
			using (var pen = new Pen(_ubsDark))
			{
				e.Graphics.FillPath(brush, path);
				e.Graphics.DrawPath(pen, path);
			}

			TextRenderer.DrawText(
				e.Graphics,
				btnClose.Text,
				btnClose.Font,
				new Rectangle(0, 0, btnClose.Width, btnClose.Height),
				Color.White,
				TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
		}

		private static GraphicsPath CreateRoundRectPath(Rectangle rect, int radius)
		{
			var path = new GraphicsPath();
			var diameter = Math.Max(2, radius * 2);
			var arc = new Rectangle(rect.Location, new Size(diameter, diameter));

			path.AddArc(arc, 180, 90);
			arc.X = rect.Right - diameter;
			path.AddArc(arc, 270, 90);
			arc.Y = rect.Bottom - diameter;
			path.AddArc(arc, 0, 90);
			arc.X = rect.Left;
			path.AddArc(arc, 90, 90);
			path.CloseFigure();
			return path;
		}

		private static void ApplyRoundedRegion(Control control, int radius)
		{
			if (control.Width <= 2 || control.Height <= 2)
				return;

			using (var path = CreateRoundRectPath(new Rectangle(0, 0, control.Width - 1, control.Height - 1), radius))
			{
				var oldRegion = control.Region;
				control.Region = new Region(path);
				oldRegion?.Dispose();
			}
		}

		private void lnkUpdate_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			var start = new ProcessStartInfo(lnkUpdate.Text);
			try
			{
				start.UseShellExecute = true;
				Process.Start(start);
			}
			catch { }
		}

		private void lnkWebSite_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			var start = new ProcessStartInfo(lnkWebSite.Text);
			try
			{
				start.UseShellExecute = true;
				Process.Start(start);
			}
			catch { }
		}
	}
}
