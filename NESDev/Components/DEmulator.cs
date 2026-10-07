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

            
            Controls.Add(Emulator);

        }


        public static void BootCartridge(byte[] cartridge)
        {
            instance?.Emulator?.BootCartridge(cartridge);
        }

        private void DEmulator_Resize(object sender, EventArgs e)
        {
            const float aspectRatio = 4f / 3f;

            int width = ClientSize.Width;
            int height = (int)(width / aspectRatio);

            if (height > ClientSize.Height)
            {
                height = ClientSize.Height;
                width = (int)(height * aspectRatio);
            }

            Emulator.Size = new Size(width, height);

            Emulator.Location = new Point(
                (ClientSize.Width - width) / 2,
                (ClientSize.Height - height) / 2);
        }
    }
}
