

using NTOSDev.Libs;
using WeifenLuo.WinFormsUI.Docking;

namespace NTOSDev
{
    public static class NES
    {
        public static ThemeBase Theme = new VS2015DarkTheme();
        public static Main Main;

        public static Action<string> Reload;


        public static string LastProject { get => RegistryHelper.ReadRegistry("NESLastProject"); set => RegistryHelper.WriteRegistry("NESSLastProject", value); }


        
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
