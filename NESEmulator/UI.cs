using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using dotNES.Controllers;
using dotNES.Renderers;

namespace dotNES
{
    public partial class UI : UserControl
    {
        private bool _rendererRunning = true;
        private Thread _renderThread;
        private IController _controller = new NES001Controller();

        public const int GameWidth = 256;
        public const int GameHeight = 240;
        public uint[] rawBitmap = new uint[GameWidth * GameHeight];
        public bool ready;
        public IRenderer _renderer;

        public enum FilterMode
        {
            NearestNeighbor, Linear
        }

        public FilterMode _filterMode = FilterMode.Linear;

        class SeparatorItem : MenuItem
        {
            public SeparatorItem() : base("-") { }
        }

        class Item : MenuItem
        {
            public Item(string title, Action<Item> build = null) : base(title)
            {
                build?.Invoke(this);
            }

            public void Add(MenuItem item) => MenuItems.Add(item);
        }

        class RadioItem : Item
        {
            public RadioItem(string title, Action<Item> build = null) : base(title, build)
            {
                RadioCheck = true;
            }
        }

        private int[] speeds = { 1, 2, 4, 8, 16 };
        private int activeSpeed = 1;
        private string[] sizes = { "1x", "2x", "4x", "8x" };
        private string activeSize = "2x";
        private Emulator emu;
        private bool suspended;
        public bool gameStarted;

        private Type[] possibleRenderers = { typeof(SoftwareRenderer), /* typeof(OpenGLRenderer),  */ typeof(Direct3DRenderer) };
        private List<IRenderer> availableRenderers = new List<IRenderer>();

        public UI()
        {
            InitializeComponent();

            FindRenderers();
            SetRenderer(availableRenderers.Last());
        }

        private void SetRenderer(IRenderer renderer)
        {
            if (_renderer == renderer) return;

            if (_renderer != null)
            {
                var oldCtrl = (Control)_renderer;
                oldCtrl.MouseClick -= UI_MouseClick;
                oldCtrl.KeyUp -= UI_KeyUp;
                oldCtrl.KeyDown -= UI_KeyDown;
                oldCtrl.PreviewKeyDown -= UI_PreviewKeyDown;
                _renderer.EndRendering();
                Controls.Remove(oldCtrl);
            }
            _renderer = renderer;
            var ctrl = (Control)renderer;
            ctrl.Dock = DockStyle.Fill;
            ctrl.TabStop = false;
            ctrl.MouseClick += UI_MouseClick;
            ctrl.KeyUp += UI_KeyUp;
            ctrl.KeyDown += UI_KeyDown;
            ctrl.PreviewKeyDown += UI_PreviewKeyDown;
            Controls.Add(ctrl);
            renderer.InitRendering(this);
        }

        private void FindRenderers()
        {
            foreach (var renderType in possibleRenderers)
            {
                try
                {
                    var renderer = (IRenderer)Activator.CreateInstance(renderType);
                    renderer.InitRendering(this);
                    renderer.EndRendering();
                    availableRenderers.Add(renderer);
                }
                catch (Exception)
                {
                    Console.WriteLine($"{renderType} failed to initialize");
                }
            }
        }

        public void BootCartridge(byte[] raw)
        {
            _renderThread?.Interrupt();
            
            emu = new Emulator(raw, _controller);
            _renderThread = new Thread(() =>
            {
                gameStarted = true;
                Console.WriteLine(emu.Cartridge);
                Stopwatch s = new Stopwatch();
                Stopwatch s0 = new Stopwatch();
                try
                {
                    while (_rendererRunning)
                {
                    if (suspended)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    s.Restart();
                    for (int i = 0; i < 60 && !suspended; i++)
                    {
                        s0.Restart();
                        emu.PPU.ProcessFrame();
                        rawBitmap = emu.PPU.RawBitmap;
                        Invoke((MethodInvoker)_renderer.Draw);
                        s0.Stop();
                        Thread.Sleep(Math.Max((int)(980 / 60.0 - s0.ElapsedMilliseconds), 0) / activeSpeed);
                    }
                    s.Stop();
                    Console.WriteLine($"60 frames in {s.ElapsedMilliseconds}ms");
                }
                }
                catch (ThreadInterruptedException)
                {

                    
                    // Thread was interrupted to stop rendering; exit gracefully
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Render thread error: {ex}");
                }
                });
            // mark as background so it won't block process exit
            _renderThread.IsBackground = true;
            _renderThread.Start();
        }

        private void UI_Load(object sender, EventArgs e)
        {
            /*string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
                BootCartridge(args[1]);*/
        }

        private void Screenshot()
        {
            var bitmap = new Bitmap(GameWidth, GameHeight, PixelFormat.Format32bppArgb);

            for (int y = 0; y < GameHeight; y++)
            {
                for (int x = 0; x < GameWidth; x++)
                {
                    bitmap.SetPixel(x, y, Color.FromArgb((int)(rawBitmap[y * GameWidth + x] | 0xff000000)));
                }
            }

            Clipboard.SetImage(bitmap);
        }

        private void UI_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Request renderer thread to stop and wait for it to finish instead of aborting
            _rendererRunning = false;
            if (_renderThread != null && _renderThread.IsAlive)
            {
                // Wake thread if sleeping
                try
                {
                    if (!_renderThread.Join(1000))
                    {
                        _renderThread.Interrupt();
                        _renderThread.Join(500);
                    }
                }
                catch (Exception)
                {
                    // ignore any exceptions while trying to stop the thread
                }
            }
            emu?.Save();
        }

        private void UI_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.F12:
                    Screenshot();
                    break;
                case Keys.F2:
                    suspended = false;
                    break;
                case Keys.F3:
                    suspended = true;
                    break;
                default:
                    _controller.PressKey(e);
                    break;
            }
        }

        private void UI_KeyUp(object sender, KeyEventArgs e)
        {
            _controller.ReleaseKey(e);
        }

        private void UI_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            var cms = new ContextMenuStrip();

            // Renderer group
            var rendererRoot = new ToolStripMenuItem("Renderer");
            foreach (var renderer in availableRenderers)
            {
                var it = new ToolStripMenuItem(renderer.RendererName) { CheckOnClick = true, Checked = renderer == _renderer };
                it.Click += (s, ev) =>
                {
                    // make this item the only checked one
                    foreach (ToolStripItem sibling in rendererRoot.DropDownItems)
                        if (sibling is ToolStripMenuItem tsi) tsi.Checked = false;
                    ((ToolStripMenuItem)s).Checked = true;
                    SetRenderer(renderer);
                };
                rendererRoot.DropDownItems.Add(it);
            }
            cms.Items.Add(rendererRoot);

            // Filter group
            var filterRoot = new ToolStripMenuItem("Filter");
            var filters = new Dictionary<string, FilterMode>()
            {
                {"None", FilterMode.NearestNeighbor},
                {"Linear", FilterMode.Linear},
            };
            foreach (var filter in filters)
            {
                var it = new ToolStripMenuItem(filter.Key) { CheckOnClick = true, Checked = filter.Value == _filterMode };
                it.Click += (s, ev) =>
                {
                    foreach (ToolStripItem sibling in filterRoot.DropDownItems)
                        if (sibling is ToolStripMenuItem tsi) tsi.Checked = false;
                    ((ToolStripMenuItem)s).Checked = true;
                    _filterMode = filter.Value;
                };
                filterRoot.DropDownItems.Add(it);
            }
            cms.Items.Add(filterRoot);

            cms.Items.Add(new ToolStripSeparator());

            // Screenshot
            var screenshotItem = new ToolStripMenuItem("Screenshot (F12)");
            screenshotItem.Click += (s, ev) => Screenshot();
            cms.Items.Add(screenshotItem);

            // Play/Pause
            var playPauseItem = new ToolStripMenuItem(suspended ? "&Play (F2)" : "&Pause (F3)");
            playPauseItem.Click += (s, ev) =>
            {
                suspended = !suspended;
                playPauseItem.Text = suspended ? "&Play (F2)" : "&Pause (F3)";
            };
            cms.Items.Add(playPauseItem);

            // Speed group
            var speedRoot = new ToolStripMenuItem("&Speed");
            foreach (var speed in speeds)
            {
                var it = new ToolStripMenuItem($"{speed}x") { CheckOnClick = true, Checked = speed == activeSpeed };
                it.Click += (s, ev) =>
                {
                    foreach (ToolStripItem sibling in speedRoot.DropDownItems)
                        if (sibling is ToolStripMenuItem tsi) tsi.Checked = false;
                    ((ToolStripMenuItem)s).Checked = true;
                    activeSpeed = speed;
                };
                speedRoot.DropDownItems.Add(it);
            }
            cms.Items.Add(speedRoot);

            // Reset
            var resetItem = new ToolStripMenuItem("&Reset...");
            cms.Items.Add(resetItem);

            cms.Show(this, new Point(e.X, e.Y));
        }


        private void UI_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            e.IsInputKey = true;
        }
    }
}
