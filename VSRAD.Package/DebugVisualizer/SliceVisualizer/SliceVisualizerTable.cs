using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.SliceVisualizer
{
    class SliceVisualizerTable : DataGridView
    {
        private const int MaxGroupSize = 512;
        public SliceVisualizerTable() : base()
        {
            AllowUserToAddRows = false;

            for (int i = 0; i < MaxGroupSize; i++)
            {
                Columns.Add(new DataGridViewTextBoxColumn()
                {
                    HeaderText = i.ToString(),
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                });
            }

            new CustomSliceTableGraphics(this);
        }

        public void DisplayWatch(List<uint[]> data, uint groupSize)
        {
            Rows.Clear();

            for (int i = 1; i <= data.Count; i++)
            {
                var index = Rows.Add(new DataGridViewRow());
                Rows[index].HeaderCell.Value = i;
            }
            
            for (int i = 0; i < MaxGroupSize; i++)
            {
                if (i < groupSize)
                {
                    for (int j = 0; j < data.Count; j++)
                        Rows[j].Cells[i].Value = data[j][i];
                }
                else
                {
                    Columns[i].Visible = false;
                }
            }
        }
    }
}
