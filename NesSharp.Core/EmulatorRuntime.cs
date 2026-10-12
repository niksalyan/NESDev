
using NesSharp.Core;
using NesSharp.Core.Sound;
using NesSharp.WinForms.Sound;

namespace NesSharp.Core;



public enum EmulatorButton
{
    Up,
    Down,
    Left,
    Right,
    Start,
    Select,
    A,
    B
}

public static class EmulatorRuntime
{
    private static readonly object Sync = new();
    private static readonly object InputLock = new();

    private static Bus? _nesSystem;
    private static Thread? _emulationThread;
    private static CancellationTokenSource? _cancellation;
    private static CancellationToken _cancellationToken;

    private static readonly ISoundDriver SoundDriver =
        new NAudioSoundDriver();

    private static readonly Dictionary<EmulatorButton, bool> Buttons =
        Enum.GetValues<EmulatorButton>()
            .ToDictionary(button => button, _ => false);

    private static bool _initialized;
    private static bool _disposed;

    public static byte[]? Ram
    {
        get
        {
            lock (Sync)
                return _nesSystem?.Ram;
        }
    }

    public static bool IsRunning
    {
        get
        {
            lock (Sync)
                return _nesSystem?.IsRunning == true;
        }
    }

    // The UI receives a snapshot of the 256 x 240 RGB screen.
    public static event Action<ScreenPixel[]>? FrameReady;

    public readonly record struct ScreenPixel(byte R, byte G, byte B);

    public static void Initialize()
    {
        lock (Sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, typeof(EmulatorRuntime));

            if (_initialized)
                return;

            _nesSystem = new Bus();
            _nesSystem.SetSampleFrequency(44100);

            SoundDriver.InitializeAudio(44100, 1, 16, 1024);
            SoundDriver.SetSoundOutMethod(SoundOut);

            _initialized = true;
        }
    }

    public static void SetButtonState(
        EmulatorButton button,
        bool pressed)
    {
        lock (InputLock)
            Buttons[button] = pressed;
    }

    public static bool GetButtonState(EmulatorButton button)
    {
        lock (InputLock)
            return Buttons[button];
    }

    public static async Task BootCartridgeAsync(byte[] cartridgeBytes)
    {
        ArgumentNullException.ThrowIfNull(cartridgeBytes);

        Initialize();
        await StopAsync();

        var cartridge = await Cartridge.FromByteArray(cartridgeBytes);

        lock (Sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, typeof(EmulatorRuntime));

            var nes = _nesSystem!;
            nes.InsertCartridge(cartridge);
            nes.Reset();

            var cts = new CancellationTokenSource();
            _cancellation = cts;
            _cancellationToken = cts.Token;

            _emulationThread = new Thread(() => UpdateGame(nes, cts.Token))
            {
                IsBackground = true,
                Name = "NES Emulation"
            };

            _emulationThread.Start();
        }
    }

    public static void BootCartridge(byte[] cartridgeBytes)
    {
        _ = BootCartridgeAsync(cartridgeBytes);
    }

    public static async Task StopAsync()
    {
        Thread? thread;
        CancellationTokenSource? cts;
        Bus? nes;

        lock (Sync)
        {
            thread = _emulationThread;
            cts = _cancellation;
            nes = _nesSystem;

            _emulationThread = null;
            _cancellation = null;

            cts?.Cancel();
        }

        nes?.Stop();

        if (thread is not null && thread != Thread.CurrentThread)
        {
            await Task.Run(() => thread.Join());
        }

        cts?.Dispose();
    }

    public static void Stop()
    {
        _ = StopAsync();
    }

    public static void Shutdown()
    {
        lock (Sync)
        {
            if (_disposed)
                return;

            _disposed = true;
        }

        StopAsync().GetAwaiter().GetResult();

        SoundDriver.Dispose();

        lock (Sync)
        {
            _nesSystem = null;
            _initialized = false;
        }
    }

    private static void UpdateGame(Bus nes, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            SetControllerState(nes);

            if (token.IsCancellationRequested)
                break;

            var ppu = nes.Ppu;

            if (!ppu.FrameComplete)
                continue;

            ppu.FrameComplete = false;

            var source = ppu.Screen;
            var pixels = new ScreenPixel[256 * 240];

            for (int i = 0; i < pixels.Length; i++)
            {
                var pixel = source[i];
                pixels[i] = new ScreenPixel(
                    pixel.R,
                    pixel.G,
                    pixel.B);
            }

            FrameReady?.Invoke(pixels);
        }
    }

    private static void SetControllerState(Bus nes)
    {
        bool up, down, left, right, start, select, a, b;

        lock (InputLock)
        {
            up = Buttons[EmulatorButton.Up];
            down = Buttons[EmulatorButton.Down];
            left = Buttons[EmulatorButton.Left];
            right = Buttons[EmulatorButton.Right];
            start = Buttons[EmulatorButton.Start];
            select = Buttons[EmulatorButton.Select];
            a = Buttons[EmulatorButton.A];
            b = Buttons[EmulatorButton.B];
        }

        nes.SetControllerState(
            0, up, down, left, right, start, select, a, b);
    }

    private static float SoundOut(
        uint channel,
        float globalTime,
        float timeStep)
    {
        Bus? nes;
        CancellationToken token;

        lock (Sync)
        {
            nes = _nesSystem;
            token = _cancellationToken;
        }

        if (nes is null || !nes.IsRunning || token.IsCancellationRequested)
            return 0f;

        if (channel != 0)
            return 0f;

        while (!nes.Clock() && !token.IsCancellationRequested)
        {
            // Clock the NES until an audio sample is available.
        }

        return token.IsCancellationRequested
            ? 0f
            : (float)nes.AudioSample;
    }

    public static void Dispose()
    {
        Stop();
        SoundDriver?.Dispose();
    }
}

