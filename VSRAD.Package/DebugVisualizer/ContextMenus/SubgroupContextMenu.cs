using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.ContextMenus
{
    public sealed class SubgroupContextMenu : IContextMenu
    {
        public delegate void ColumnSelectorChanged(string newSelector);
        public delegate void ColorClicked(int columnIndex, ColumnHighlightColor? color);

        private readonly ColumnSelectorChanged _selectorChanged;
        private readonly ColorClicked _colorClicked;
        private readonly VisualizerTable _table;
        private readonly ContextMenu _menu;
        private int _clickedColumnIndex;
        private int _targetColumnIndex;
        private int _columnRelStart;

        public SubgroupContextMenu(VisualizerTable table, ColumnSelectorChanged selectorChanged, ColorClicked colorClicked)
        {
            _table = table;
            _selectorChanged = selectorChanged;
            _colorClicked = colorClicked;
            _menu = PrepareContextMenu();
        }

        public bool Show(MouseEventArgs e, DataGridView.HitTestInfo hit)
        {
            if (hit.RowIndex != -1 || hit.ColumnIndex < 0) return false;
            var screenStartOffset = _table.RowHeadersWidth + _table.Columns[VisualizerTable.NameColumnIndex].Width;
            _columnRelStart = hit.ColumnX - screenStartOffset;
            var invisibleColumns = _table.DataColumns.Count(x => x.Index < hit.ColumnIndex && x.Visible == false);
            _clickedColumnIndex = hit.ColumnIndex;
            _targetColumnIndex = hit.ColumnIndex - invisibleColumns - 1;
            _menu.MenuItems[12].Enabled = hit.ColumnIndex >= VisualizerTable.DataColumnOffset;
            _menu.MenuItems[16].Enabled = hit.ColumnIndex >= VisualizerTable.DataColumnOffset;
            _menu.Show(_table, new Point(e.X, e.Y));
            return true;
        }

        private ContextMenu PrepareContextMenu()
        {
            var keepFirst = CreatePartialSubgroupMenu(minSubgroupSize: 4, maxSubgroupSize: 512, displayLast: false);
            var keepLast = CreatePartialSubgroupMenu(minSubgroupSize: 4, maxSubgroupSize: 512, displayLast: true);

            var showAll = new MenuItem("All Columns", (s, e) => _selectorChanged($"0-{_table.GroupSize - 1}"));

            var highlightThis = new MenuItem("Highlight", new[]
            {
                new MenuItem("Green", (s, e) => _colorClicked(_clickedColumnIndex, ColumnHighlightColor.Green)),
                new MenuItem("Red", (s, e) => _colorClicked(_clickedColumnIndex, ColumnHighlightColor.Red)),
                new MenuItem("Blue", (s, e) => _colorClicked(_clickedColumnIndex, ColumnHighlightColor.Blue)),
                new MenuItem("None", (s, e) => _colorClicked(_clickedColumnIndex, null))
            });

            var fitWidth = new MenuItem("Fit Width", (s, e) =>
                _table.ColumnResizeController.FitWidth(_targetColumnIndex, _columnRelStart));

            var hideThis = new MenuItem("Hide This", (s, e) => _table.HideColumns(_clickedColumnIndex));

            var menuItems = new[] { new MenuItem("Keep First") { Enabled = false } }
                .Concat(keepFirst)
                .Append(new MenuItem("-"))
                .Append(new MenuItem("Keep Last", keepLast))
                .Append(showAll)
                .Append(new MenuItem("-"))
                .Append(highlightThis)
                .Append(new MenuItem("-"))
                .Append(fitWidth)
                .Append(new MenuItem("-"))
                .Append(hideThis);

            return new ContextMenu(menuItems.ToArray());
        }

        private void SelectPartialSubgroups(int groupSize, int displayedCount, bool displayLast) =>
            _selectorChanged(ColumnSelector.PartialSubgroups(_table.GroupSize, groupSize, displayedCount, displayLast));

        private MenuItem[] CreatePartialSubgroupMenu(int minSubgroupSize, int maxSubgroupSize, bool displayLast) =>
            PowersOfTwo(from: minSubgroupSize, upto: maxSubgroupSize / 2)
                .Select(displayedCount =>
                {
                    var groupSizeSubmenu = PowersOfTwo(from: displayedCount * 2, upto: maxSubgroupSize)
                        .Select(groupSize => new MenuItem(groupSize.ToString(),
                            (s, e) => SelectPartialSubgroups(groupSize, displayedCount, displayLast)))
                        .ToArray();
                    return new MenuItem(displayedCount.ToString(), groupSizeSubmenu);
                })
                .ToArray();

        private static IEnumerable<int> PowersOfTwo(int from, int upto)
        {
            while (from <= upto)
            {
                yield return from;
                from *= 2;
            }
        }
    }
}
