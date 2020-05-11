using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using VSRAD.Package.Options;
using VSRAD.Package.Utils;
using static VSRAD.Package.Utils.NativeMethods;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class CellStyling
    {
        private readonly VisualizerTable _table;
        private readonly VisualizerAppearance _appearance;

        public CellStyling(VisualizerTable table, VisualizerAppearance appearance)
        {
            _table = table;
            _appearance = appearance;
            _table.CellPainting += HandleCellPaint;
            _appearance.PropertyChanged += AppearancePropertyChanged;
        }

        private void AppearancePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VisualizerAppearance.DarkenAlternatingRowsBy))
                _table.Invalidate();
        }

        private void HandleCellPaint(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (_appearance.DarkenAlternatingRowsBy == 0 || e.RowIndex % 2 == 0 || e.RowIndex < 0 || e.RowIndex == _table.NewWatchRowIndex)
                return;

            e.CellStyle.ForeColor = DarkenColor(e.CellStyle.ForeColor, _appearance.DarkenAlternatingRowsBy / 100f);
            e.CellStyle.BackColor = DarkenColor(e.CellStyle.BackColor, _appearance.DarkenAlternatingRowsBy / 100f);
        }

        private static Color DarkenColor(Color c, float by)
        {
            int h = 0, l = 0, s = 0;
            c.ToHls(ref h, ref l, ref s);
            l = (int)(l * (1 - by));
            return FromHls(h, l, s);
        }
    }
}
