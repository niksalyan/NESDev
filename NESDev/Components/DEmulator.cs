
using NesSharp.Core;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using WeifenLuo.WinFormsUI.Docking;

namespace NTOSDev.Components;

public partial class DEmulator : DockContent
{
    private static DEmulator? _instance;

    private readonly Bitmap _screenBuffer =
        new(256, 240, PixelFormat.Format24bppRgb);

    private static readonly Rectangle ScreenRect =
        new(0, 0, 256, 240);

    private readonly object _screenBufferLock = new();
    private readonly Dictionary<Keys, EmulatorButton> _keyBindings = new();

    private int _paintRequestPending;

    public DEmulator()
    {
        _instance = this;

        InitializeComponent();

        Text = "Emulator";
        HideOnClose = true;
        TabStop = true;

        InitializeInputBindings();
    }

    // Keep this wrapper if existing code calls DEmulator.BootCartridge().
    public static void BootCartridge(byte[] cartridge)
    {
        EmulatorRuntime.BootCartridge(cartridge);
    }

    private void InitializeInputBindings()
    {
        _keyBindings[Keys.W] = EmulatorButton.Up;
        _keyBindings[Keys.S] = EmulatorButton.Down;
        _keyBindings[Keys.A] = EmulatorButton.Left;
        _keyBindings[Keys.D] = EmulatorButton.Right;
        _keyBindings[Keys.Enter] = EmulatorButton.Start;
        _keyBindings[Keys.Back] = EmulatorButton.Select;
        _keyBindings[Keys.O] = EmulatorButton.A;
        _keyBindings[Keys.K] = EmulatorButton.B;
    }

    private void DEmulator_Load(object sender, EventArgs e)
    {
        EmulatorRuntime.Initialize();
        EmulatorRuntime.FrameReady += Emulator_FrameReady;
    }

    private void Emulator_FrameReady(EmulatorRuntime.ScreenPixel[] pixels)
    {
        lock (_screenBufferLock)
        {
            var bitmapData = _screenBuffer.LockBits(
                ScreenRect,
                ImageLockMode.WriteOnly,
                _screenBuffer.PixelFormat);

            try
            {
                unsafe
                {
                    var dst = (byte*)bitmapData.Scan0.ToPointer();

                    for (int y = 0; y < 240; y++)
                    {
                        for (int x = 0; x < 256; x++)
                        {
                            var pixel = pixels[y * 256 + x];
                            int offset = y * bitmapData.Stride + x * 3;

                            dst[offset] = pixel.B;
                            dst[offset + 1] = pixel.G;
                            dst[offset + 2] = pixel.R;
                        }
                    }
                }
            }
            finally
            {
                _screenBuffer.UnlockBits(bitmapData);
            }
        }

        // Request a repaint without queueing duplicate UI callbacks.
        if (Interlocked.Exchange(ref _paintRequestPending, 1) != 0)
            return;

        try
        {
            if (IsHandleCreated && !IsDisposed)
            {
                BeginInvoke((Action)(() =>
                {
                    Interlocked.Exchange(ref _paintRequestPending, 0);
                    Invalidate();
                }));
            }
            else
            {
                Interlocked.Exchange(ref _paintRequestPending, 0);
            }
        }
        catch (InvalidOperationException)
        {
            Interlocked.Exchange(ref _paintRequestPending, 0);
        }
    }

    private void DEmulator_KeyDown(object sender, KeyEventArgs e)
    {
        if (_keyBindings.TryGetValue(e.KeyCode, out var button))
            EmulatorRuntime.SetButtonState(button, true);
    }

    private void DEmulator_KeyUp(object sender, KeyEventArgs e)
    {
        if (_keyBindings.TryGetValue(e.KeyCode, out var button))
            EmulatorRuntime.SetButtonState(button, false);
    }

    private void DEmulator_Resize(object sender, EventArgs e)
    {
        Invalidate();
    }

    private void DEmulator_Paint(object sender, PaintEventArgs e)
    {
        const float aspectRatio = 4f / 3f;

        int width = ClientSize.Width;
        int height = (int)(width / aspectRatio);

        if (height > ClientSize.Height)
        {
            height = ClientSize.Height;
            width = (int)(height * aspectRatio);
        }

        e.Graphics.CompositingMode = CompositingMode.SourceCopy;
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        e.Graphics.SmoothingMode = SmoothingMode.None;

        lock (_screenBufferLock)
        {
            e.Graphics.DrawImage(
                _screenBuffer,
                new Rectangle(
                    (ClientSize.Width - width) / 2,
                    (ClientSize.Height - height) / 2,
                    width,
                    height));
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        EmulatorRuntime.FrameReady -= Emulator_FrameReady;

        base.OnFormClosed(e);
    }

    private void DisposeEmulator(bool disposing)
    {
        if (disposing)
        {
            EmulatorRuntime.Stop();
            EmulatorRuntime.FrameReady -= Emulator_FrameReady;

            lock (_screenBufferLock)
                _screenBuffer.Dispose();

            if (ReferenceEquals(_instance, this))
                _instance = null;
        }

        base.Dispose(disposing);
        EmulatorRuntime.Dispose();
        
    }
}