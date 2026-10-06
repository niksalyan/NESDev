
using WeifenLuo.WinFormsUI.Docking;

namespace NTOSDev.Components
{
    public partial class FileViewer : DockContent
    {
        public string filePath;
        public FileViewer(string filePath)
        {

            InitializeComponent();
            if (File.Exists(filePath))
            {
                this.filePath = filePath;
                UpdateFileName();
                LoadFile();


            }
        }

        private void LoadFile()
        {
            try
            {
                pictureBox1.Image = Image.FromFile(filePath);
                return;
            }
            catch { }

            try
            {
                // pictureBox1.Image = new NTOSBitmap(filePath).ToBitmap();
                return;
            }
            catch { }

        }

        private void UpdateFileName()
        {
            Text = Path.GetFileName(filePath);
        }
    }
}
