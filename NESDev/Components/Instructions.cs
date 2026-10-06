
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace NTOSDev.Components
{
    public partial class Instructions : DockContent
    {
        public Instructions()
        {
            InitializeComponent();
            dataGridView1.DataSource = NES.Compiler.Instructions;
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            Debug.WriteLine("Instructions window activated.");
            dataGridView1.DataSource = NES.Compiler.Instructions;
        }


    }
}
