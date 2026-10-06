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

        public UI Emulator;
        private bool initialized = false;

        public DEmulator()
        {
            instance = this;
            InitializeComponent();
            Text = "Emulator";
            HideOnClose = true;
            TabStop = true;

            Emulator = new UI();

            Emulator.Dock = DockStyle.Fill;
            Controls.Add(Emulator);

        }


        public static void BootCartridge(byte[] cartridge)
        {
            instance?.Emulator?.BootCartridge(cartridge);
        }

    }
}
