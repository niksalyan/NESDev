using NTOSDev.Libs;
using System.Data;
using WeifenLuo.WinFormsUI.Docking;

namespace NTOSDev.Controls
{
    public partial class ProjectExplorer : DockContent
    {
        public event Action<string> FileClicked;
        public event Action<string> FileDoubleClicked;

        public string RootPath { get; private set; }

        private ContextMenuStrip contextMenu;
        private TreeNode clickedNode;

        public ProjectExplorer()
        {
            InitializeComponent();

            treeView.Dock = DockStyle.Fill;
            treeView.BeforeExpand += TreeView1_BeforeExpand;
            treeView.NodeMouseClick += TreeView1_NodeMouseClick;
            treeView.NodeMouseDoubleClick += TreeView1_NodeMouseDoubleClick;
            treeView.NodeMouseClick += TreeView_RightClick;

            InitContextMenu();

            Controls.Add(treeView);
        }

        private void InitContextMenu()
        {
            contextMenu = new ContextMenuStrip();

            contextMenu.Items.Add("Add File", null, AddFile_Click);
            contextMenu.Items.Add("Rename", null, Rename_Click);
            contextMenu.Items.Add("Delete", null, Delete_Click);
        }

        public ProjectExplorer LoadFolder(string path)
        {
            RootPath = path;

            // Extract folder name safely
            string folderName = Path.GetFileName(
                path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            );

            // Fallback (in case of something like "C:\")
            if (string.IsNullOrEmpty(folderName))
                folderName = path;

            // Set DockContent title
            this.Text = folderName;

            treeView.Nodes.Clear();

            try
            {
                // Folders first
                foreach (var dir in Directory.GetDirectories(path).OrderBy(d => d))
                {
                    treeView.Nodes.Add(CreateDirectoryNode(dir));
                }

                // Files
                foreach (var file in Directory.GetFiles(path).OrderBy(f => f))
                {
                    treeView.Nodes.Add(new TreeNode(Path.GetFileName(file))
                    {
                        Tag = file
                    });
                }
            }
            catch
            {
                // ignore access issues
            }

            return this;
        }

        private TreeNode CreateDirectoryNode(string path)
        {
            var node = new TreeNode(Path.GetFileName(path))
            {
                Tag = path
            };

            // Add dummy node for lazy loading
            node.Nodes.Add("Loading...");

            return node;
        }

        private void TreeView1_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            // Lazy load
            if (e.Node.Nodes.Count == 1 && e.Node.Nodes[0].Text == "Loading...")
            {
                e.Node.Nodes.Clear();

                string path = e.Node.Tag.ToString();

                try
                {
                    // Folders
                    foreach (var dir in Directory.GetDirectories(path))
                    {
                        e.Node.Nodes.Add(CreateDirectoryNode(dir));
                    }

                    // Files
                    foreach (var file in Directory.GetFiles(path))
                    {
                        e.Node.Nodes.Add(new TreeNode(Path.GetFileName(file))
                        {
                            Tag = file
                        });
                    }
                }
                catch
                {
                    // ignore access issues
                }
            }
        }

        private (TreeNode node, string path) GetTargetDirectory(TreeNode node)
        {
            if (node == null) return (null, null);

            string path = node.Tag?.ToString();

            if (Directory.Exists(path))
            {
                return (node, path);
            }

            // File → use parent directory
            var parent = node.Parent;
            return (parent, Path.GetDirectoryName(path));
        }

        private void TreeView1_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (File.Exists(e.Node.Tag?.ToString()))
            {
                FileClicked?.Invoke(e.Node.Tag.ToString());
            }
        }

        private void TreeView1_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (File.Exists(e.Node.Tag?.ToString()))
            {
                NES.Main.OpenFile(e.Node.Tag.ToString());
                FileDoubleClicked?.Invoke(e.Node.Tag.ToString());
            }
        }

        private void TreeView_RightClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                treeView.SelectedNode = e.Node;
                clickedNode = e.Node;

                contextMenu.Show(treeView, e.Location);
            }
        }

        private void AddFile_Click(object sender, EventArgs e)
        {
            if (clickedNode == null) return;

            var (dirNode, dirPath) = GetTargetDirectory(clickedNode);
            if (dirPath == null) return;

            string fileName = Prompt("Enter file name:");
            if (string.IsNullOrWhiteSpace(fileName)) return;

            string fullPath = Path.Combine(dirPath, fileName);

            if (File.Exists(fullPath))
            {
                MessageBox.Show("File already exists!");
                return;
            }

            File.WriteAllText(fullPath, "");

            var newNode = new TreeNode(fileName) { Tag = fullPath };

            string clickedPath = clickedNode.Tag?.ToString();

            // 🧠 THE COMPLETE INSERT LOGIC
            if (File.Exists(clickedPath))
            {
                if (clickedNode.Parent != null)
                {
                    // Normal case (file inside folder)
                    int index = clickedNode.Index;
                    clickedNode.Parent.Nodes.Insert(index + 1, newNode);
                }
                else
                {
                    // 🔥 TOP-LEVEL FILE CASE
                    int index = clickedNode.Index;
                    treeView.Nodes.Insert(index + 1, newNode);
                }
            }
            else
            {
                // Folder → add inside
                dirNode.Nodes.Add(newNode);
                dirNode.Expand();
            }
        }

        private void Rename_Click(object sender, EventArgs e)
        {
            if (clickedNode == null) return;

            string oldPath = clickedNode.Tag.ToString();
            string currentName = Path.GetFileName(oldPath);

            string newName = Prompt("Enter new name:", currentName);
            if (string.IsNullOrWhiteSpace(newName)) return;

            string newPath = Path.Combine(Path.GetDirectoryName(oldPath), newName);

            try
            {
                if (File.Exists(oldPath))
                    File.Move(oldPath, newPath);
                else if (Directory.Exists(oldPath))
                    Directory.Move(oldPath, newPath);

                clickedNode.Text = newName;
                clickedNode.Tag = newPath;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void Delete_Click(object sender, EventArgs e)
        {
            if (clickedNode == null) return;

            var result = TBMessageBox.Confirm(
                this,
                "Are you sure you want to delete this?",
                "Confirm Delete"
            );

            if (result != DialogResult.Yes) return;

            string path = clickedNode.Tag.ToString();

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
                else if (Directory.Exists(path))
                    Directory.Delete(path, true);

                clickedNode.Remove();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private string Prompt(string text, string defaultValue = "")
        {
            Form form = new Form()
            {
                Width = 350,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = text,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false
            };

            Label label = new Label()
            {
                Left = 15,
                Top = 10,
                Width = 300,
                Text = text,
                BorderStyle = BorderStyle.None
            };

            TextBox textBox = new TextBox()
            {
                Left = 15,
                Top = 35,
                Width = 300,
                Text = defaultValue,
                BorderStyle = BorderStyle.None
            };

            Button buttonOk = new Button()
            {
                Text = "OK",
                Left = 160,
                Width = 75,
                Height = 25,
                Top = 70,
                DialogResult = DialogResult.OK,
                FlatStyle = FlatStyle.Flat
            };

            Button buttonCancel = new Button()
            {
                Text = "Cancel",
                Left = 240,
                Width = 75,
                Height = 25,
                Top = 70,
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat
            };

            form.Controls.Add(label);
            form.Controls.Add(textBox);
            form.Controls.Add(buttonOk);
            form.Controls.Add(buttonCancel);

            form.AcceptButton = buttonOk;
            form.CancelButton = buttonCancel;

            form.Shown += (s, e) =>
            {
                textBox.Focus();
                textBox.SelectAll();
            };

            return form.ShowDialog(this) == DialogResult.OK
                ? textBox.Text
                : null;
        }

    }
}
