

using NESCompiler;
using NTOSDev.Components;
using NTOSDev.Libs;
using System.Diagnostics;
using System.Text;
using WeifenLuo.WinFormsUI.Docking;

namespace NTOSDev
{
    public static class NES
    {
        public static ThemeBase Theme = new VS2015DarkTheme();
        public static Main Main;

        public static Action<string> Reload;

        public static Compiler Compiler = new Compiler();


        public static string LastProject { get => RegistryHelper.ReadRegistry("NESLastProject"); set => RegistryHelper.WriteRegistry("NESLastProject", value); }


        public static void Compile(string src)
        {
            var cartridge = Compiler.Compile(src);

            Debug.WriteLine($"Cartridge size: {cartridge.Length} bytes");

            File.WriteAllBytes("output.nes", cartridge);

            File.WriteAllText("output.txt", ToArduinoArray(cartridge));

            DEmulator.BootCartridge(cartridge);
        }


        public static string ToArduinoArray(byte[] bytecode, int columns = 8)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < bytecode.Length; i++)
            {
                sb.Append($"0x{bytecode[i]:X2}");

                if (i < bytecode.Length - 1)
                {
                    sb.Append(',');
                }

                if ((i + 1) % columns == 0)
                {
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }


        public static void OpenProject()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select Project Folder";
                dialog.ShowNewFolderButton = true;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedPath = dialog.SelectedPath;

                    // Optional: validate folder (e.g., contains a specific file)
                    if (System.IO.Directory.Exists(selectedPath))
                    {
                        OpenProject(selectedPath);
                    }
                    else
                    {
                        MessageBox.Show("Invalid folder selected.", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        public static void OpenProject(string path)
        {
            LastProject = path;
            Reload?.Invoke(path);
        }
    }
}
