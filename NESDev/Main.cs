using NTOSDev.Components;
using NTOSDev.Controls;
using System.Diagnostics;
using System.Text.RegularExpressions;
using WeifenLuo.WinFormsUI.Docking;

namespace NTOSDev
{
    public partial class Main : Form
    {

        public ProjectExplorer projectExplorer = new ProjectExplorer()
        {
            HideOnClose = true
        };

        public DEmulator dEmulator = new DEmulator()
        {
            HideOnClose = true
        };


        public Instructions dInstructions = new Instructions()
        {
            HideOnClose = true
        };

        public Variables dVariables = new Variables()
        {
            HideOnClose = true
        };

        public Terminal dTerminal = new Terminal()
        {
            HideOnClose = true
        };
        private string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private string appLayoutFile;

        public Main()
        {
            NES.Main = this;
            appLayoutFile = Path.Combine(appDataPath, "NTOSDev", "layout.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(appLayoutFile));
            Debug.WriteLine($"Layout file path: {appLayoutFile}");
            InitializeComponent();
            dockPanel.Theme = NES.Theme;
            try
            {
                var deserializeDockContent = new DeserializeDockContent(GetContentFromPersistString);
                dockPanel.LoadFromXml(appLayoutFile, deserializeDockContent);
            }
            catch { } // Ignore if layout file doesn't exist or is invalid

            AttachMenuHandlers(menuStrip.Items);
        }

        private void Main_Load(object sender, EventArgs e)
        {

            // new TerminalControl().Show(dockPanel, DockState.DockBottom);

            NES.Reload += (s) =>
            {
                CloseAllPanels(typeof(CodeEditor));
                projectExplorer.LoadFolder(s);
                DoAction("projectExplorer");
                DoAction("runEmulator");

                //Emulator.VM.ClearMemory();
                //Emulator.Compiler.ClearVariables();

                string appName = new DirectoryInfo(s).Name;
                try
                {
                    if (!string.IsNullOrWhiteSpace(NES.LastProject) && Directory.Exists(NES.LastProject))
                    {
                        string lastProj = NES.LastProject + "\\main.js";
                        if (!File.Exists(lastProj))
                        {
                            File.WriteAllText(lastProj, @$"
// NTOS Main executable file
function init() {{
    // This function is called when the project is initialized
}}

function draw() {{
    dialog(""{appName}"");
}}

draw();

function loop() {{
    // This function is called every frame
    var key = getKey();
    switch(key) {{
        case '*':
            // Do something when the '*' key is pressed
            break;
        case '#':
            // Do something when the '#' key is pressed
            break;
    }}
    delay(1);
}}
");
                        }

                        if (File.Exists(lastProj))
                        {
                            OpenFile(lastProj);
                        }
                        projectExplorer.LoadFolder(NES.LastProject);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                }



                /*Emulator.DebugOutputAdded += (log) =>  {
                    CodeEditor.ClearAllErrors();
                };

                Emulator.DebugErrorAdded += (error) => {
                    CodeEditor.ClearAllErrors();
                    if (CodeEditor.CurrentView != null && CodeEditor.currentFile == Emulator.CurrentFile && CodeEditor.codeViews.ContainsKey(CodeEditor.currentFile))
                    {
                        if (TryParseErrorPosition(error, out int line, out int character))
                        {
                            CodeEditor.codeViews[CodeEditor.currentFile].ShowError(line, character, error);
                        } else
                        {
                            DoAction("debugOutput");
                        }
                        
                        
                    }
                };*/




            };

            string command = Environment.GetCommandLineArgs().Skip(1).FirstOrDefault();
            string lastProject = NES.LastProject;
            if (!string.IsNullOrWhiteSpace(command) && Directory.Exists(command))
            {
                NES.OpenProject(command);
            }
            else if (!string.IsNullOrWhiteSpace(lastProject) && Directory.Exists(lastProject))
            {
                NES.OpenProject(lastProject);
            }
            else
            {
                // SM.InitializeEmptyProject();
            }

            DoAction("runEmulator");



#if RELEASE
            WindowState = FormWindowState.Maximized;
            // SplashScreen();
#endif
        }

        private void AttachMenuHandlers(ToolStripItemCollection items)
        {
            foreach (ToolStripItem item in items)
            {
                if (item is ToolStripMenuItem menuItem)
                {
                    menuItem.Text = menuItem.Text;
                    menuItem.Click += MenuItem_Click;

                    if (menuItem.DropDownItems.Count > 0)
                    {
                        AttachMenuHandlers(menuItem.DropDownItems);
                    }
                }
            }
        }

        private void MenuItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item)
                DoAction(item.Name.Replace("ToolStripMenuItem", ""));
        }

        private IDockContent GetContentFromPersistString(string persistString)
        {
            // Tool windows: return existing instances so state is preserved
            if (persistString == typeof(ProjectExplorer).ToString()) return projectExplorer;
            if (persistString == typeof(DEmulator).ToString()) return dEmulator;
            if (persistString == typeof(Instructions).ToString()) return dInstructions;
            if (persistString == typeof(Variables).ToString()) return dVariables;
            if (persistString == typeof(Terminal).ToString()) return dTerminal;
            
            // Documents: try to extract a file path after a separator (comma or pipe)
            try
            {
                if (persistString.StartsWith(typeof(Controls.CodeEditor).ToString()) || persistString.Contains("CodeEditor"))
                {
                    string path = null;
                    int idx = persistString.IndexOf(',');
                    if (idx >= 0 && persistString.Length > idx + 1)
                        path = persistString.Substring(idx + 1).Trim();
                    else
                    {
                        idx = persistString.IndexOf('|');
                        if (idx >= 0 && persistString.Length > idx + 1)
                            path = persistString.Substring(idx + 1).Trim();
                    }

                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                        return new Controls.CodeEditor(path);
                    return null;
                }

                if (persistString.StartsWith(typeof(Components.FileViewer).ToString()) || persistString.Contains("FileViewer"))
                {
                    string path = null;
                    int idx = persistString.IndexOf(',');
                    if (idx >= 0 && persistString.Length > idx + 1)
                        path = persistString.Substring(idx + 1).Trim();
                    else
                    {
                        idx = persistString.IndexOf('|');
                        if (idx >= 0 && persistString.Length > idx + 1)
                            path = persistString.Substring(idx + 1).Trim();
                    }

                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                        return new Components.FileViewer(path);
                    return null;
                }
            }
            catch { }

            return null;
        }


        public void OpenFile(string filePath)
        {
            if (Path.GetExtension(filePath)?.ToLower() == ".js" || Path.GetExtension(filePath)?.ToLower() == ".ntx")
            {
                new CodeEditor(filePath).Show(dockPanel, DockState.Document);
            }
            else
            {
                new FileViewer(filePath).Show(dockPanel, DockState.Document);
            }

        }

        public void CloseAllPanels(Type type = null)
        {
            var contents = dockPanel.Contents.ToArray();
            foreach (DockContent p in contents)
            {
                if (type == null || p.GetType() == type)
                {
                    if (p.HideOnClose)
                    {
                        p.Hide();
                    }
                    else
                    {
                        p.Close();
                    }
                }
            }
        }

        public DockContentCollection GetAllPanels()
        {
            return dockPanel.Contents;
        }

        public void DoAction(string action)
        {
            switch (action)
            {
                case "openProject":
                    NES.OpenProject();
                    break;
                case "projectExplorer":
                    projectExplorer.Show(dockPanel, DockState.DockLeft);
                    break;
                case "runEmulator":
                    dEmulator.Show(dockPanel, DockState.DockRight);
                    //dEmulator.InitEmulator();
                    break;
                case "build":
                    CodeEditor.currentFile = null;
                    dEmulator.Show(dockPanel, DockState.DockRight);
                    //dEmulator.InitEmulator();
                    dEmulator.BuildAll();
                    //serialManager.Show(dockPanel, DockState.DockBottom);
                    break;
                case "imageConverter":
                    //dImage.Show(dockPanel, DockState.Document);
                    break;
                case "instructions":
                    dInstructions.Show(dockPanel, DockState.DockRight);
                    break;
                case "variables":
                    dVariables.Show(dockPanel, DockState.DockRight);
                    break;
                case "debugOutput":
                    dTerminal.Show(dockPanel, DockState.DockBottom);
                    break;
                case "about":
                    new AboutForm().ShowDialog();
                    break;
                case "colorPicker":
                    if (CodeEditor.CurrentView != null)
                    {
                        var colorDialog = new ColorDialog();
                        if (colorDialog.ShowDialog(this) == DialogResult.OK)
                        {
                            CodeEditor.CurrentView.InsertTextAtCaret("C" + (colorDialog.Color.R / 36) + (colorDialog.Color.G / 36) + (colorDialog.Color.B / 36));
                        }
                        colorDialog.Dispose();
                    }
                    break;
                case "exit":
                    Close();
                    break;
            }
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                dockPanel.SaveAsXml(appLayoutFile);
            }
            catch { }
#if !DEBUG
            if (TBMessageBox.Confirm(this, "Project is not saved!", "Do you want to save this project?") == DialogResult.Yes)
            {
                // DoAction("save");
            }
#else
            DoAction("save");
#endif
        }

        private static bool TryParseErrorPosition(
    string error,
    out int line,
    out int character)
        {
            line = 0;
            character = 0;

            Match match = Regex.Match(error, @"\((\d+):(\d+)\)$");

            if (!match.Success)
                return false;

            line = int.Parse(match.Groups[1].Value);
            character = int.Parse(match.Groups[2].Value);

            return true;
        }

        private void Main_FormClosed(object sender, FormClosedEventArgs e)
        {
#if DEBUG
            // TranslationService.Save();
#endif
        }
    }
}
