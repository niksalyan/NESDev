using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace NTOSDev.Controls
{
    public class ChrEditorControl : DockContent
    {
        public const int ChrSize = 4096;
        public const int TileSize = 8;
        public const int TileBytes = 16;
        public const int TileCount = 256;
        public const int TilesPerRow = 16;

        private readonly byte[] _chr = new byte[ChrSize];

        // NES palette indexes are 0..3.
        private readonly Color[] _colors =
        {
            Color.FromArgb(84, 84, 84),
            Color.FromArgb(0, 30, 116),
            Color.FromArgb(8, 16, 144),
            Color.FromArgb(48, 0, 136)
        };

        private int _selectedColor;
        private Rectangle _selection = new Rectangle(0, 0, 2, 2);
        private bool _selecting;
        private Point _selectionStart;

        private readonly TileSheetCanvas _sheet;
        private readonly SpriteCanvas _sprite;
        private readonly FlowLayoutPanel _palettePanel;
        private readonly Label _status;
        private readonly NumericUpDown _widthInput;
        private readonly NumericUpDown _heightInput;

        private readonly Label _tileIndexLabel;

        public event EventHandler ChrChanged;
        public event EventHandler SelectionChanged;

        public ChrEditorControl()
        {
            DoubleBuffered = true;
            MinimumSize = new Size(650, 450);
            BackColor = SystemColors.Control;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(5)
            };

            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 75));

            Controls.Add(root);

            // Toolbar
            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                AutoScroll = true
            };

            var importButton = new Button
            {
                Text = "Import bitmap",
                AutoSize = true
            };

            var exportButton = new Button
            {
                Text = "Export sprite",
                AutoSize = true
            };

            var clearButton = new Button
            {
                Text = "Clear",
                AutoSize = true
            };

            var widthLabel = new Label
            {
                Text = "Tiles W:",
                AutoSize = true,
                Padding = new Padding(4, 8, 0, 0)
            };

            var heightLabel = new Label
            {
                Text = "H:",
                AutoSize = true,
                Padding = new Padding(4, 8, 0, 0)
            };

            _widthInput = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 16,
                Value = 2,
                Width = 50
            };

            _heightInput = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 16,
                Value = 2,
                Width = 50
            };

            var applySizeButton = new Button
            {
                Text = "Apply size",
                AutoSize = true
            };

            toolbar.Controls.Add(importButton);
            toolbar.Controls.Add(exportButton);
            toolbar.Controls.Add(clearButton);
            toolbar.Controls.Add(widthLabel);
            toolbar.Controls.Add(_widthInput);
            toolbar.Controls.Add(heightLabel);
            toolbar.Controls.Add(_heightInput);
            toolbar.Controls.Add(applySizeButton);

            root.Controls.Add(toolbar, 0, 0);

            // Main editing area
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 512,
                FixedPanel = FixedPanel.Panel1,
                Panel1MinSize = 300,
                Panel2MinSize = 10
            };

            

            root.Controls.Add(split, 0, 1);


            var sheetContainer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };

            sheetContainer.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100));
            sheetContainer.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 30));

            var sheetHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(35, 35, 35)
            };

            _sheet = new TileSheetCanvas(this)
            {
                Location = new Point(4, 4),
                Size = new Size(16 * 18 + 1, 16 * 18 + 1)
            };

            sheetHost.Controls.Add(_sheet);

            _tileIndexLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Tile: --",
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 0, 0, 0)
            };

            sheetContainer.Controls.Add(sheetHost, 0, 0);
            sheetContainer.Controls.Add(_tileIndexLabel, 0, 1);

            split.Panel1.Controls.Add(sheetContainer);


            var spriteHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(35, 35, 35)
            };

            _sprite = new SpriteCanvas(this)
            {
                Location = new Point(8, 8),
                Size = new Size(384, 384)
            };

            spriteHost.Controls.Add(_sprite);
            split.Panel2.Controls.Add(spriteHost);

            // Palette and status
            var bottom = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };

            bottom.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100));

            bottom.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 180));

            _palettePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false
            };

            _status = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            bottom.Controls.Add(_palettePanel, 0, 0);
            bottom.Controls.Add(_status, 1, 0);
            root.Controls.Add(bottom, 0, 2);

            RebuildPalette();
            UpdateStatus();

            importButton.Click += (_, _) => ImportBitmapDialog();
            exportButton.Click += (_, _) => ExportBitmapDialog();

            clearButton.Click += (_, _) =>
            {
                ClearSelection();
            };

            applySizeButton.Click += (_, _) =>
            {
                SetSelectionSize(
                    (int)_widthInput.Value,
                    (int)_heightInput.Value);
            };

            split.SplitterDistance = 250;
        }

        private void UpdateHoveredTile(int x, int y)
        {
            const int cellSize = 18;

            int tileX = Math.Clamp(x / cellSize, 0, 15);
            int tileY = Math.Clamp(y / cellSize, 0, 15);
            int index = tileY * 16 + tileX;

            _tileIndexLabel.Text =
                $"Tile: {index} (0x{index:X2}) | X: {tileX}, Y: {tileY}";
        }

        // ---------------------------------------------------------
        // CHR MEMORY API
        // ---------------------------------------------------------

        /// <summary>
        /// Returns a copy of the complete 4096-byte CHR table.
        /// </summary>
        public byte[] GetChrBytes()
        {
            return (byte[])_chr.Clone();
        }

        /// <summary>
        /// Replaces the complete CHR table.
        /// The input must contain exactly 4096 bytes.
        /// </summary>
        public void SetChrBytes(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            if (bytes.Length != ChrSize)
                throw new ArgumentException(
                    "CHR data must contain exactly 4096 bytes.",
                    nameof(bytes));

            Buffer.BlockCopy(bytes, 0, _chr, 0, ChrSize);

            RefreshCanvases();
            OnChrChanged();
        }

        public byte[] GetTileBytes(int tileIndex)
        {
            ValidateTile(tileIndex);

            var result = new byte[TileBytes];

            Buffer.BlockCopy(
                _chr,
                tileIndex * TileBytes,
                result,
                0,
                TileBytes);

            return result;
        }

        public void SetTileBytes(int tileIndex, byte[] bytes)
        {
            ValidateTile(tileIndex);

            if (bytes == null || bytes.Length != TileBytes)
                throw new ArgumentException(
                    "A tile must contain exactly 16 bytes.",
                    nameof(bytes));

            Buffer.BlockCopy(
                bytes,
                0,
                _chr,
                tileIndex * TileBytes,
                TileBytes);

            RefreshCanvases();
            OnChrChanged();
        }

        // ---------------------------------------------------------
        // NES PIXEL ENCODING
        // ---------------------------------------------------------

        /// <summary>
        /// Returns a pixel's 2-bit color index, from 0 to 3.
        /// Coordinates are local to one 8x8 tile.
        /// </summary>
        public int GetPixel(int tileIndex, int x, int y)
        {
            ValidateTile(tileIndex);
            ValidatePixel(x, y);

            int offset = tileIndex * TileBytes;

            int bit = 7 - x;

            int low = (_chr[offset + y] >> bit) & 1;
            int high = (_chr[offset + 8 + y] >> bit) & 1;

            return low | (high << 1);
        }

        public void SetPixel(
            int tileIndex,
            int x,
            int y,
            int colorIndex)
        {
            ValidateTile(tileIndex);
            ValidatePixel(x, y);

            if (colorIndex < 0 || colorIndex > 3)
                throw new ArgumentOutOfRangeException(
                    nameof(colorIndex));

            int offset = tileIndex * TileBytes;
            int bit = 7 - x;
            int mask = 1 << bit;

            int lowOffset = offset + y;
            int highOffset = offset + 8 + y;

            _chr[lowOffset] = (byte)(
                (_chr[lowOffset] & ~mask) |
                (((colorIndex & 1) != 0) ? mask : 0));

            _chr[highOffset] = (byte)(
                (_chr[highOffset] & ~mask) |
                (((colorIndex & 2) != 0) ? mask : 0));

            RefreshCanvases();
            OnChrChanged();
        }

        // ---------------------------------------------------------
        // SPRITE / TILE RECTANGLE API
        // ---------------------------------------------------------

        /// <summary>
        /// Selection in tile coordinates, not pixel coordinates.
        /// </summary>
        public Rectangle SelectedTiles => _selection;

        public int SelectedPixelWidth => _selection.Width * 8;

        public int SelectedPixelHeight => _selection.Height * 8;

        public void SetSelection(int tileX, int tileY, int width, int height)
        {
            if (width < 1 || height < 1 ||
                tileX < 0 || tileY < 0 ||
                tileX + width > TilesPerRow ||
                tileY + height > TilesPerRow)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(width),
                    "The selection must fit inside the 16x16 tile sheet.");
            }

            _selection = new Rectangle(tileX, tileY, width, height);

            _widthInput.Value = width;
            _heightInput.Value = height;

            RefreshCanvases();
            UpdateStatus();

            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetSelectionSize(int width, int height)
        {
            int x = _selection.X;
            int y = _selection.Y;

            // Keep the new rectangle inside the sheet.
            x = Math.Min(x, TilesPerRow - width);
            y = Math.Min(y, TilesPerRow - height);

            SetSelection(x, y, width, height);
        }

        /// <summary>
        /// Returns selected tiles in row-major tile order.
        /// Each tile contributes its original 16 CHR bytes.
        /// </summary>
        public byte[] GetSelectedSpriteBytes()
        {
            byte[] result = new byte[
                _selection.Width *
                _selection.Height *
                TileBytes];

            int destination = 0;

            for (int y = 0; y < _selection.Height; y++)
            {
                for (int x = 0; x < _selection.Width; x++)
                {
                    int tile = TileIndex(
                        _selection.X + x,
                        _selection.Y + y);

                    Buffer.BlockCopy(
                        _chr,
                        tile * TileBytes,
                        result,
                        destination,
                        TileBytes);

                    destination += TileBytes;
                }
            }

            return result;
        }

        /// <summary>
        /// Writes row-major tile data into the selected rectangle.
        /// </summary>
        public void SetSelectedSpriteBytes(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            int expected = _selection.Width *
                           _selection.Height *
                           TileBytes;

            if (bytes.Length != expected)
                throw new ArgumentException(
                    $"Expected exactly {expected} bytes.",
                    nameof(bytes));

            int source = 0;

            for (int y = 0; y < _selection.Height; y++)
            {
                for (int x = 0; x < _selection.Width; x++)
                {
                    int tile = TileIndex(
                        _selection.X + x,
                        _selection.Y + y);

                    Buffer.BlockCopy(
                        bytes,
                        source,
                        _chr,
                        tile * TileBytes,
                        TileBytes);

                    source += TileBytes;
                }
            }

            RefreshCanvases();
            OnChrChanged();
        }

        // ---------------------------------------------------------
        // COLOR PALETTE API
        // ---------------------------------------------------------

        public int SelectedColor
        {
            get => _selectedColor;
            set
            {
                if (value < 0 || value > 3)
                    throw new ArgumentOutOfRangeException(nameof(value));

                _selectedColor = value;
                RebuildPalette();
            }
        }

        public Color GetColor(int colorIndex)
        {
            ValidateColor(colorIndex);
            return _colors[colorIndex];
        }

        public void SetColor(int colorIndex, Color color)
        {
            ValidateColor(colorIndex);

            _colors[colorIndex] = color;

            RebuildPalette();
            RefreshCanvases();
        }

        public Color[] GetColors()
        {
            return (Color[])_colors.Clone();
        }

        public void SetColors(Color[] colors)
        {
            if (colors == null || colors.Length != 4)
                throw new ArgumentException(
                    "Exactly four colors are required.",
                    nameof(colors));

            Array.Copy(colors, _colors, 4);

            RebuildPalette();
            RefreshCanvases();
        }

        private void RebuildPalette()
        {
            if (_palettePanel == null)
                return;

            _palettePanel.Controls.Clear();

            for (int i = 0; i < 4; i++)
            {
                int index = i;

                var button = new Button
                {
                    Width = 65,
                    Height = 42,
                    Margin = new Padding(3),
                    BackColor = _colors[i],
                    ForeColor = ContrastColor(_colors[i]),
                    Text = $"Color {i}",
                    FlatStyle = FlatStyle.Flat
                };

                button.FlatAppearance.BorderSize =
                    index == _selectedColor ? 3 : 1;

                button.Click += (_, _) =>
                {
                    _selectedColor = index;
                    RebuildPalette();
                };

                // Right-click a color to change its preview RGB value.
                button.MouseUp += (_, e) =>
                {
                    if (e.Button != MouseButtons.Right)
                        return;

                    using var dialog = new ColorDialog
                    {
                        Color = _colors[index],
                        FullOpen = true
                    };

                    if (dialog.ShowDialog(this) == DialogResult.OK)
                        SetColor(index, dialog.Color);
                };

                _palettePanel.Controls.Add(button);
            }
        }

        // ---------------------------------------------------------
        // BITMAP IMPORT / EXPORT
        // ---------------------------------------------------------

        public void ImportBitmap(Bitmap bitmap)
        {
            if (bitmap == null)
                throw new ArgumentNullException(nameof(bitmap));

            int width = SelectedPixelWidth;
            int height = SelectedPixelHeight;

            using var resized = new Bitmap(
                bitmap,
                new Size(width, height));

            // Import uses the nearest of the four preview colors.
            // Transparency is treated as color index 0.
            for (int ty = 0; ty < _selection.Height; ty++)
            {
                for (int tx = 0; tx < _selection.Width; tx++)
                {
                    int tile = TileIndex(
                        _selection.X + tx,
                        _selection.Y + ty);

                    for (int py = 0; py < 8; py++)
                    {
                        for (int px = 0; px < 8; px++)
                        {
                            Color c = resized.GetPixel(
                                tx * 8 + px,
                                ty * 8 + py);


                            int colorBw = (c.R + c.G + c.B) / 3 / 85;
                            int colorIndex = Math.Clamp(colorBw, 0, 3);

                            WritePixelRaw(tile, px, py, colorIndex);
                        }
                    }
                }
            }

            RefreshCanvases();
            OnChrChanged();
        }

        public Bitmap ExportSelectedSprite()
        {
            var bitmap = new Bitmap(
                SelectedPixelWidth,
                SelectedPixelHeight,
                PixelFormat.Format32bppArgb);

            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    int tileX = _selection.X + x / 8;
                    int tileY = _selection.Y + y / 8;

                    int tile = TileIndex(tileX, tileY);

                    int colorIndex = GetPixel(
                        tile,
                        x % 8,
                        y % 8);

                    bitmap.SetPixel(
                        x,
                        y,
                        _colors[colorIndex]);
                }
            }

            return bitmap;
        }

        private void ImportBitmapDialog()
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Bitmap images|*.png;*.bmp;*.gif;*.jpg;*.jpeg"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                using var original = new Bitmap(dialog.FileName);
                ImportBitmap(original);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Import failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ExportBitmapDialog()
        {
            using var dialog = new SaveFileDialog
            {
                Filter = "PNG image|*.png|Bitmap image|*.bmp",
                DefaultExt = "png",
                FileName = "sprite.png"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            using var bitmap = ExportSelectedSprite();

            var format = dialog.FilterIndex == 2
                ? ImageFormat.Bmp
                : ImageFormat.Png;

            bitmap.Save(dialog.FileName, format);
        }

        private int FindNearestColor(Color color)
        {
            int bestIndex = 0;
            double bestDistance = double.MaxValue;

            for (int i = 0; i < 4; i++)
            {
                double dr = color.R - _colors[i].R;
                double dg = color.G - _colors[i].G;
                double db = color.B - _colors[i].B;

                double distance = dr * dr + dg * dg + db * db;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        // ---------------------------------------------------------
        // RAW PIXEL HELPERS
        // ---------------------------------------------------------

        private void WritePixelRaw(
            int tile,
            int x,
            int y,
            int colorIndex)
        {
            int offset = tile * TileBytes;
            int mask = 1 << (7 - x);

            if ((colorIndex & 1) != 0)
                _chr[offset + y] |= (byte)mask;
            else
                _chr[offset + y] &= (byte)~mask;

            if ((colorIndex & 2) != 0)
                _chr[offset + 8 + y] |= (byte)mask;
            else
                _chr[offset + 8 + y] &= (byte)~mask;
        }

        private void ClearSelection()
        {
            for (int y = 0; y < _selection.Height; y++)
            {
                for (int x = 0; x < _selection.Width; x++)
                {
                    int tile = TileIndex(
                        _selection.X + x,
                        _selection.Y + y);

                    Array.Clear(_chr, tile * TileBytes, TileBytes);
                }
            }

            RefreshCanvases();
            OnChrChanged();
        }

        private int TileIndex(int x, int y)
        {
            return y * TilesPerRow + x;
        }

        private void RefreshCanvases()
        {
            _sheet?.Invalidate();
            _sprite?.Invalidate();
        }

        private void UpdateStatus()
        {
            if (_status == null)
                return;

            _status.Text =
                $"Tiles: {_selection.Width}x{_selection.Height} " +
                $"({SelectedPixelWidth}x{SelectedPixelHeight}px)";
        }

        private void OnChrChanged()
        {
            ChrChanged?.Invoke(this, EventArgs.Empty);
        }

        private static Color ContrastColor(Color color)
        {
            return color.GetBrightness() > 0.5
                ? Color.Black
                : Color.White;
        }

        private static void ValidateTile(int tileIndex)
        {
            if (tileIndex < 0 || tileIndex >= TileCount)
                throw new ArgumentOutOfRangeException(nameof(tileIndex));
        }

        private static void ValidatePixel(int x, int y)
        {
            if (x < 0 || x >= 8 || y < 0 || y >= 8)
                throw new ArgumentOutOfRangeException(nameof(x));
        }

        private static void ValidateColor(int colorIndex)
        {
            if (colorIndex < 0 || colorIndex > 3)
                throw new ArgumentOutOfRangeException(nameof(colorIndex));
        }

        // =========================================================
        // TILE SHEET CANVAS
        // =========================================================

        private sealed class TileSheetCanvas : Control
        {
            private readonly ChrEditorControl _owner;
            private const int Cell = 18;
            private const int Pixel = 2;

            public TileSheetCanvas(ChrEditorControl owner)
            {
                _owner = owner;
                DoubleBuffered = true;
                BackColor = Color.FromArgb(35, 35, 35);
                Cursor = Cursors.Cross;

                Size = new Size(
                    TilesPerRow * Cell + 1,
                    TilesPerRow * Cell + 1);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                e.Graphics.Clear(BackColor);

                for (int tileY = 0; tileY < 16; tileY++)
                {
                    for (int tileX = 0; tileX < 16; tileX++)
                    {
                        int tile = tileY * 16 + tileX;
                        int ox = tileX * Cell;
                        int oy = tileY * Cell;

                        for (int py = 0; py < 8; py++)
                        {
                            for (int px = 0; px < 8; px++)
                            {
                                int colorIndex = _owner.GetPixel(
                                    tile, px, py);

                                using var brush = new SolidBrush(
                                    _owner._colors[colorIndex]);

                                e.Graphics.FillRectangle(
                                    brush,
                                    ox + px * Pixel,
                                    oy + py * Pixel,
                                    Pixel,
                                    Pixel);
                            }
                        }

                        using var gridPen = new Pen(
                            Color.FromArgb(90, 90, 90));

                        e.Graphics.DrawRectangle(
                            gridPen,
                            ox,
                            oy,
                            Cell,
                            Cell);
                    }
                }

                Rectangle s = _owner._selection;

                using var selectionPen = new Pen(Color.Yellow, 2);

                e.Graphics.DrawRectangle(
                    selectionPen,
                    s.X * Cell + 1,
                    s.Y * Cell + 1,
                    s.Width * Cell - 2,
                    s.Height * Cell - 2);
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);

                if (e.Button != MouseButtons.Left)
                    return;

                _owner._selecting = true;
                _owner._selectionStart = new Point(
                    Math.Clamp(e.X / Cell, 0, 15),
                    Math.Clamp(e.Y / Cell, 0, 15));

                UpdateSelection(e.Location);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);

                _owner.UpdateHoveredTile(e.X, e.Y);

                if (_owner._selecting)
                    UpdateSelection(e.Location);
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);

                if (!_owner._selecting)
                    return;

                UpdateSelection(e.Location);
                _owner._selecting = false;
            }

            private void UpdateSelection(Point point)
            {
                int x = Math.Clamp(point.X / Cell, 0, 15);
                int y = Math.Clamp(point.Y / Cell, 0, 15);

                int left = Math.Min(_owner._selectionStart.X, x);
                int top = Math.Min(_owner._selectionStart.Y, y);

                int width = Math.Abs(x - _owner._selectionStart.X) + 1;
                int height = Math.Abs(y - _owner._selectionStart.Y) + 1;

                _owner.SetSelection(left, top, width, height);
            }
        }

        // =========================================================
        // SELECTED SPRITE CANVAS
        // =========================================================

        private sealed class SpriteCanvas : Control
        {
            private readonly ChrEditorControl _owner;
            private const int Zoom = 16;

            private bool _painting;

            public SpriteCanvas(ChrEditorControl owner)
            {
                _owner = owner;
                DoubleBuffered = true;
                BackColor = Color.FromArgb(35, 35, 35);
                Cursor = Cursors.Cross;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                e.Graphics.Clear(BackColor);
                e.Graphics.InterpolationMode =
                    InterpolationMode.NearestNeighbor;

                int width = _owner.SelectedPixelWidth;
                int height = _owner.SelectedPixelHeight;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int tileX = _owner._selection.X + x / 8;
                        int tileY = _owner._selection.Y + y / 8;

                        int tile = tileY * 16 + tileX;

                        int colorIndex = _owner.GetPixel(
                            tile,
                            x % 8,
                            y % 8);

                        using var brush = new SolidBrush(
                            _owner._colors[colorIndex]);

                        e.Graphics.FillRectangle(
                            brush,
                            x * Zoom,
                            y * Zoom,
                            Zoom,
                            Zoom);
                    }
                }


                using var gridPen = new Pen(
                    Color.FromArgb(65, 65, 65));

                // Individual pixel grid
                for (int x = 0; x <= width; x++)
                {
                    e.Graphics.DrawLine(
                        gridPen,
                        x * Zoom,
                        0,
                        x * Zoom,
                        height * Zoom);
                }

                for (int y = 0; y <= height; y++)
                {
                    e.Graphics.DrawLine(
                        gridPen,
                        0,
                        y * Zoom,
                        width * Zoom,
                        y * Zoom);
                }

                using var tilePen = new Pen(
                    Color.FromArgb(180, 180, 180), 2);

                // 8x8 tile boundaries
                for (int x = 0; x <= width; x += 8)
                {
                    e.Graphics.DrawLine(
                        tilePen,
                        x * Zoom,
                        0,
                        x * Zoom,
                        height * Zoom);
                }

                for (int y = 0; y <= height; y += 8)
                {
                    e.Graphics.DrawLine(
                        tilePen,
                        0,
                        y * Zoom,
                        width * Zoom,
                        y * Zoom);
                }


            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);

                if (e.Button != MouseButtons.Left &&
                    e.Button != MouseButtons.Right)
                    return;

                _painting = true;
                PaintAt(e.Location, e.Button == MouseButtons.Right);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);

                if (!_painting)
                    return;

                PaintAt(
                    e.Location,
                    (e.Button & MouseButtons.Right) != 0);
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                _painting = false;
            }

            private void PaintAt(Point point, bool erase)
            {
                int x = point.X / Zoom;
                int y = point.Y / Zoom;

                if (x < 0 || y < 0 ||
                    x >= _owner.SelectedPixelWidth ||
                    y >= _owner.SelectedPixelHeight)
                    return;

                int tileX = _owner._selection.X + x / 8;
                int tileY = _owner._selection.Y + y / 8;
                int tile = tileY * 16 + tileX;

                _owner.SetPixel(
                    tile,
                    x % 8,
                    y % 8,
                    erase ? 0 : _owner._selectedColor);
            }
        }
    }
}