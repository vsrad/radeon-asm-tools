using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer
{
    public enum ContentAlignment
    {
        Left, Center, Right
    }

    public static class ContentAlignmentMapping
    {
        public static DataGridViewContentAlignment AsDataGridViewContentAlignment(this ContentAlignment alignment)
        {
            switch (alignment)
            {
                case ContentAlignment.Left:
                    return DataGridViewContentAlignment.MiddleLeft;
                case ContentAlignment.Center:
                    return DataGridViewContentAlignment.MiddleCenter;
                case ContentAlignment.Right:
                    return DataGridViewContentAlignment.MiddleRight;
                default:
                    return DataGridViewContentAlignment.MiddleLeft;
            }
        }
    }
}
