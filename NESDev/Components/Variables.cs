
using WeifenLuo.WinFormsUI.Docking;

namespace NTOSDev.Components
{
    public partial class Variables : DockContent
    {
        public Variables()
        {
            InitializeComponent();
            dataGridView1.DataSource = NES.Compiler.Variables;
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            dataGridView1.DataSource = NES.Compiler.Variables;
        }
    }
}
