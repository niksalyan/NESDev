using System.Drawing.Drawing2D;
using System.Reflection;

namespace NTOSDev.Controls
{
    public sealed class AboutForm : Form
    {
        private static readonly Color BackgroundColor =
            Color.FromArgb(32, 34, 37);

        private static readonly Color AccentColor =
            Color.FromArgb(88, 166, 255);

        private static readonly Color PrimaryText =
            Color.FromArgb(245, 245, 245);

        private static readonly Color SecondaryText =
            Color.FromArgb(170, 170, 175);

        public AboutForm()
        {
            InitializeForm();
            BuildControls();
        }

        private void InitializeForm()
        {
            Text = "About NTOS Dev";

            ClientSize = new Size(520, 420);

            StartPosition = FormStartPosition.CenterParent;

            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            BackColor = BackgroundColor;
            ForeColor = PrimaryText;

            Font = new Font("Segoe UI", 9F);
        }

        private void BuildControls()
        {
            // =========================================================
            // Logo
            // =========================================================

            var logo = new LogoPanel
            {
                Location = new Point(28, 28),
                Size = new Size(82, 82)
            };

            Controls.Add(logo);

            // =========================================================
            // Application name
            // =========================================================

            var title = new Label
            {
                AutoSize = false,
                Text = "NTOS Dev",
                Font = new Font(
                    "Segoe UI Semibold",
                    20F,
                    FontStyle.Regular),
                ForeColor = PrimaryText,

                Location = new Point(130, 23),
                Size = new Size(350, 42),

                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            Controls.Add(title);


            // =========================================================
            // Subtitle
            // =========================================================

            var subtitle = new Label
            {
                AutoSize = false,
                Text = "Arduino OS Development compilator.",
                Font = new Font(
                    "Segoe UI",
                    11F,
                    FontStyle.Regular),
                ForeColor = AccentColor,

                Location = new Point(133, 67),
                Size = new Size(350, 28),

                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            Controls.Add(subtitle);


            // =========================================================
            // Version
            // =========================================================

            var version =
                Assembly.GetExecutingAssembly()
                    .GetName()
                    .Version?
                    .ToString(3)
                ?? "1.0.0";

            var versionLabel = new Label
            {
                AutoSize = true,
                Text = $"Version {version}",
                Font = new Font("Segoe UI", 9F),
                ForeColor = SecondaryText,
                Location = new Point(134, 101)
            };

            Controls.Add(versionLabel);

            // =========================================================
            // Separator
            // =========================================================

            var separator = new Panel
            {
                Location = new Point(28, 132),
                Size = new Size(464, 1),
                BackColor = Color.FromArgb(65, 67, 72)
            };

            Controls.Add(separator);

            // =========================================================
            // Description
            // =========================================================

            var description = new Label
            {
                AutoSize = false,
                Location = new Point(30, 153),
                Size = new Size(460, 90),

                Text =
    "NTOS Dev is a development environment for creating, " +
    "building and testing applications for the NTOS embedded " +
    "operating system, microcontrollers and graphical displays.",

                Font = new Font("Segoe UI", 10F),
                ForeColor = SecondaryText
            };

            Controls.Add(description);

            // =========================================================
            // Created by
            // =========================================================

            var createdBy = new Label
            {
                AutoSize = true,
                Text = "Created by",
                Font = new Font("Segoe UI", 9F),
                ForeColor = SecondaryText,
                Location = new Point(30, 242)
            };

            Controls.Add(createdBy);

            // =========================================================
            // Author
            // =========================================================

            var author = new Label
            {
                AutoSize = true,
                Text = "Niksalyan Tigran",
                Font = new Font(
                    "Segoe UI Semibold",
                    11F),
                ForeColor = PrimaryText,
                Location = new Point(30, 263)
            };

            Controls.Add(author);

            // =========================================================
            // Copyright
            // =========================================================

            var copyright = new Label
            {
                AutoSize = true,
                Text = $"© {DateTime.Now.Year} Niksalyan Tigran",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = SecondaryText,
                Location = new Point(30, 293)
            };

            Controls.Add(copyright);

            // =========================================================
            // Close button
            // =========================================================

            var closeButton = new Button
            {
                Text = "Close",

                Size = new Size(90, 32),

                // 420 client height - 20 bottom margin - 32 height
                Location = new Point(402, 368),

                FlatStyle = FlatStyle.Flat,

                BackColor = Color.FromArgb(55, 57, 62),
                ForeColor = PrimaryText,

                Font = new Font(
                    "Segoe UI Semibold",
                    9F),

                TabStop = false
            };

            closeButton.FlatAppearance.BorderSize = 0;

            closeButton.FlatAppearance.MouseOverBackColor =
                Color.FromArgb(65, 68, 74);

            closeButton.Click += (_, _) => Close();

            Controls.Add(closeButton);

            AcceptButton = closeButton;
            CancelButton = closeButton;
        }

        // =============================================================
        // Image2Cpp logo
        // =============================================================

        private sealed class LogoPanel : Panel
        {
            public LogoPanel()
            {
                DoubleBuffered = true;

                SetStyle(
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer,
                    true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                var g = e.Graphics;

                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                var rect = new Rectangle(
                    4,
                    4,
                    Width - 8,
                    Height - 8);

                using var backgroundBrush =
                    new LinearGradientBrush(
                        rect,
                        Color.FromArgb(65, 120, 190),
                        Color.FromArgb(45, 75, 120),
                        45f);

                using var borderPen =
                    new Pen(
                        Color.FromArgb(100, 160, 230),
                        1.5f);

                using var textBrush =
                    new SolidBrush(Color.White);

                using var font =
                    new Font(
                        "Segoe UI Black",
                        22F,
                        FontStyle.Bold);

                using var smallFont =
                    new Font(
                        "Consolas",
                        8F,
                        FontStyle.Bold);

                using var path =
                    RoundedRectangle(rect, 14);

                g.FillPath(backgroundBrush, path);
                g.DrawPath(borderPen, path);

                using var pixelBrush =
                    new SolidBrush(
                        Color.FromArgb(180, 220, 255));

                g.FillRectangle(pixelBrush, 17, 18, 7, 7);
                g.FillRectangle(pixelBrush, 26, 27, 7, 7);
                g.FillRectangle(pixelBrush, 35, 18, 7, 7);

                g.DrawString(
                    "2C",
                    font,
                    textBrush,
                    new PointF(14, 39));

                g.DrawString(
                    "PP",
                    smallFont,
                    textBrush,
                    new PointF(43, 58));
            }

            private static GraphicsPath RoundedRectangle(
                Rectangle rectangle,
                int radius)
            {
                var path = new GraphicsPath();

                int diameter = radius * 2;

                path.AddArc(
                    rectangle.X,
                    rectangle.Y,
                    diameter,
                    diameter,
                    180,
                    90);

                path.AddArc(
                    rectangle.Right - diameter,
                    rectangle.Y,
                    diameter,
                    diameter,
                    270,
                    90);

                path.AddArc(
                    rectangle.Right - diameter,
                    rectangle.Bottom - diameter,
                    diameter,
                    diameter,
                    0,
                    90);

                path.AddArc(
                    rectangle.X,
                    rectangle.Bottom - diameter,
                    diameter,
                    diameter,
                    90,
                    90);

                path.CloseFigure();

                return path;
            }
        }
    }
}