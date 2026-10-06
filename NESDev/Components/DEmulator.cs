using dotNES;
using NESCompiler;
using NTOSDev.Controls;

using System.Diagnostics;
using WeifenLuo.WinFormsUI.Docking;

namespace NTOSDev.Components
{
    public partial class DEmulator : DockContent
    {

        private static DEmulator instance;
        private bool initialized = false;

        public DEmulator()
        {
            InitializeComponent();
            Text = "Emulator";
            HideOnClose = true;
            TabStop = true;
            instance = this;
        }

        public static void RefreshEmulator()
        {
            // instance?.InitEmulator();
        }

        public void BuildAll()
        {
            //Emulator.BuildAll();
        }

        private async void InitEmulator()
        {
            if (initialized)
                return;
            initialized = true;
            // var cartridge = File.ReadAllBytes(@"D:\Games\NES\roms\Contra.nes");

            
            var emulator = new UI();

            emulator.Dock = DockStyle.Fill;
            Controls.Add(emulator);

            var compiler = new Compiler();
            var cartridge = compiler.Compile();

            Debug.WriteLine($"Cartridge size: {cartridge.Length} bytes");

            File.WriteAllBytes("output.nes", cartridge);

            emulator.BootCartridge(cartridge);
            emulator.Focus();

        }

        private void DEmulator_Load(object sender, EventArgs e)
        {

            InitEmulator();
        }
    }
}
