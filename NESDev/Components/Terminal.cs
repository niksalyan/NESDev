
using NESDev.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace NTOSDev.Components
{
    public partial class Terminal : DockContent
    {
        public Terminal()
        {
            InitializeComponent();
            dataGridView1.DataSource = NES.DebugOutput;
            dataGridView1.CellFormatting += dataGridView1_CellFormatting;
        }

        private void dataGridView1_CellFormatting(
        object sender,
        DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            e.CellStyle.SelectionBackColor = e.CellStyle.BackColor;
            e.CellStyle.SelectionForeColor = e.CellStyle.ForeColor;
            if (dataGridView1.Rows[e.RowIndex].DataBoundItem is DebugLine debugLine &&
                debugLine.IsError)
            {

                e.CellStyle.ForeColor = Color.Red;
                e.CellStyle.SelectionForeColor = e.CellStyle.ForeColor;
            }

        }
    }
}
