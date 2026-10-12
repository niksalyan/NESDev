using NESCompiler;
using NesSharp.Core;
using NesSharp.Core;
using NesSharp.Core.Sound;
using NesSharp.Core.Sound;
using NesSharp.WinForms.Sound;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using WeifenLuo.WinFormsUI.Docking;

namespace NTOSDev.Components
{
    public partial class DEmulator : DockContent
    {

        private static DEmulator instance;

        private Thread? _emulationThread;
        private CancellationTokenSource _emulatorCancellationTokenSource = new();
        private CancellationToken _cancellationToken;

        private Bus? _nesSystem;

        private readonly Dictionary<Keys, bool> _playerOne = new();

        private readonly Bitmap _screenBuffer = new Bitmap(256, 240, PixelFormat.Format24bppRgb);
        private static readonly Rectangle Rect = new(0, 0, 256, 240);

        private readonly ISoundDriver _soundDriver = new NAudioSoundDriver();

        private readonly object _screenBufferLock = new();

        private int _paintRequestPending;

        // private DebugWindow? _debugWindow;

        public DEmulator()
        {
            instance = this;
            InitializeComponent();
            Text = "Emulator";
            HideOnClose = true;
            TabStop = true;

        }

        public static void BootCartridge(byte[] cartridge)
        {
            instance?.BootCartridgeL(cartridge);
        }

        public async void BootCartridgeL(byte[] cartridgeB)
        {
            StopEmulator();
            var cartridge = await Cartridge.FromByteArray(cartridgeB);
            StartEmulator(cartridge);
        }

        private void StartEmulator(Cartridge cartridge)
        {
            _nesSystem!.InsertCartridge(cartridge);

            _nesSystem.Reset();
            _emulatorCancellationTokenSource = new();

            var cancelToken = _emulatorCancellationTokenSource.Token;
            var emulatorState = new EmulatorState(_nesSystem, cancelToken);

            _emulationThread = new Thread(UpdateGame);
            _emulationThread.Start(emulatorState);

            _cancellationToken = cancelToken;
        }

        private record EmulatorState(Bus NesSystem, CancellationToken CancellationToken = default);

        private void UpdateGame(object? obj)
        {
            if (obj is not EmulatorState emulatorState)
            {
                return;
            }

            while (!emulatorState.CancellationToken.IsCancellationRequested)
            {
                RunEmulator(emulatorState);
            }
        }

        private void RunEmulator(EmulatorState emulatorState)
        {
            emulatorState.NesSystem.SetControllerState(0, _playerOne[P1KeyUp], _playerOne[P1KeyDown], _playerOne[P1KeyLeft], _playerOne[P1KeyRight], _playerOne[P1KeyStart], _playerOne[P1KeySelect], _playerOne[P1KeyA], _playerOne[P1KeyB]);

            if (emulatorState.CancellationToken.IsCancellationRequested)
            {
                return;
            }

            var ppu = emulatorState.NesSystem.Ppu;
            if (!ppu.FrameComplete)
            {
                return;
            }

            UpdateEmulatorOutputAndDebugWindow(ppu);
        }


        private void UpdateEmulatorOutputAndDebugWindow(Ppu ppu)
        {
            ppu.FrameComplete = false;

            lock (_screenBufferLock)
            {
                var bitmapData = _screenBuffer.LockBits(
                    Rect,
                    ImageLockMode.WriteOnly,
                    _screenBuffer.PixelFormat);

                try
                {
                    unsafe
                    {
                        var dstPointer = (byte*)bitmapData.Scan0.ToPointer();
                        var src = ppu.Screen;

                        for (var y = 0; y < 240; y++)
                        {
                            for (var x = 0; x < 256; x++)
                            {
                                var pixel = src[y * 256 + x];
                                var offset = y * bitmapData.Stride + x * 3;

                                dstPointer[offset] = pixel.B;
                                dstPointer[offset + 1] = pixel.G;
                                dstPointer[offset + 2] = pixel.R;
                            }
                        }
                    }
                }
                finally
                {
                    _screenBuffer.UnlockBits(bitmapData);
                }
            }

            // Request a repaint without queuing duplicate UI callbacks.
            if (Interlocked.Exchange(ref _paintRequestPending, 1) == 0)
            {
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
        }

        private void StopEmulator()
        {
            _nesSystem!.Stop();
            _emulatorCancellationTokenSource.Cancel();
        }

        // Player 1 Inputs
        public Keys P1KeyUp { get; set; }
        public Keys P1KeyDown { get; set; }
        public Keys P1KeyLeft { get; set; }
        public Keys P1KeyRight { get; set; }
        public Keys P1KeyStart { get; set; }
        public Keys P1KeySelect { get; set; }
        public Keys P1KeyA { get; set; }
        public Keys P1KeyB { get; set; }

        private void InitializeInput()
        {
            P1KeyUp = Keys.W;
            P1KeyDown = Keys.S;
            P1KeyLeft = Keys.A;
            P1KeyRight = Keys.D;
            P1KeyStart = Keys.Enter;
            P1KeySelect = Keys.Back;
            P1KeyA = Keys.O;
            P1KeyB = Keys.K;

            _playerOne.Add(P1KeyUp, false);
            _playerOne.Add(P1KeyDown, false);
            _playerOne.Add(P1KeyLeft, false);
            _playerOne.Add(P1KeyRight, false);
            _playerOne.Add(P1KeyStart, false);
            _playerOne.Add(P1KeySelect, false);
            _playerOne.Add(P1KeyA, false);
            _playerOne.Add(P1KeyB, false);
        }


        private void DEmulator_Resize(object sender, EventArgs e)
        {
            Invalidate();
        }

        private void DEmulator_Load(object sender, EventArgs e)
        {
            _nesSystem = new Bus();

            InitializeInput();
            InitializeSound();
        }

        private void DEmulator_KeyDown(object sender, KeyEventArgs e)
        {
            if (_playerOne.ContainsKey(e.KeyCode))
            {
                _playerOne[e.KeyCode] = true;
            }
        }

        private void DEmulator_KeyUp(object sender, KeyEventArgs e)
        {
            if (_playerOne.ContainsKey(e.KeyCode))
            {
                _playerOne[e.KeyCode] = false;
            }
        }

        private void InitializeSound()
        {
            _nesSystem!.SetSampleFrequency(44100);

            _soundDriver.InitializeAudio(44100, 1, 16, 1024);
            _soundDriver.SetSoundOutMethod(SoundOut);
        }

        private float SoundOut(uint channel, float globalTime, float timeStep)
        {
            if (_nesSystem is null || !_nesSystem.IsRunning)
            {
                return 0f;
            }

            if (channel == 0)
            {
                while (!_nesSystem.Clock() && !_cancellationToken.IsCancellationRequested)
                {
                    // keep emulating until we have a sample to play. This is to keep the timing of the sound accurate.
                }

                return (float)_nesSystem.AudioSample;
            }

            return 0f;
        }

        private void DisposeSoundOutput()
        {
            _soundDriver.Dispose();
        }


        private void DisposeBuffers()
        {
            StopEmulator();

            _screenBuffer.Dispose();
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

            e.Graphics.CompositingMode =
                System.Drawing.Drawing2D.CompositingMode.SourceCopy;

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
    }
}
