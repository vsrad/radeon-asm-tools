using Microsoft.VisualStudio.Shell;
using Moq;
using System;
using System.Linq;
using System.Windows.Forms;
using VSRAD.Package.DebugVisualizer;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.PackageTests.DebugVisualizer
{
    [Collection(MockedVS.Collection)]
    public class VisualizerIntegrationTests
    {
        class TestVisualizer
        {
            public ProjectOptions Options { get; } = new ProjectOptions();
            public VisualizerContext Context { get; }
            public VisualizerControl Control { get; }

            Mock<IDebuggerIntegration> DebuggerMock { get; } = new Mock<IDebuggerIntegration>();
            Mock<IToolWindowIntegration> ToolWindowIntegrationMock { get; } = new Mock<IToolWindowIntegration>();

            public TestVisualizer(Action<ProjectOptions> setOptions)
            {
                setOptions(Options);
                Context = new VisualizerContext(Options, null, DebuggerMock.Object);

                ToolWindowIntegrationMock = new Mock<IToolWindowIntegration>();
                ToolWindowIntegrationMock.Setup(i => i.ProjectOptions).Returns(Options);
                ToolWindowIntegrationMock.Setup(i => i.GetVisualizerContext()).Returns(Context);

                var fontAndColorMock = new Mock<IFontAndColorProvider>();
                fontAndColorMock.Setup(f => f.FontAndColorState).Returns(new FontAndColorState());

                Control = new VisualizerControl(ToolWindowIntegrationMock.Object, fontAndColorMock.Object);
            }

            public void EnterBreak(BreakState breakState) => DebuggerMock.Raise(d => d.BreakEntered += null, null, breakState);

            public WatchNameCell GetNameCell(int rowIndex) => (WatchNameCell)Control.Table.Rows[rowIndex].Cells[VisualizerTable.NameColumnIndex];

            public string GetNameCellParentRowIndexes(int rowIndex) => string.Join(",", GetNameCell(rowIndex).ParentRows.Select(r => r.Index));

            public DataGridViewCell GetDataCell(int rowIndex, int dataColIndex) => Control.Table.Rows[rowIndex].Cells[VisualizerTable.DataColumnOffset + dataColIndex];
        }

        [Fact]
        public async Task DebugDataFormattingTestAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var packageErrors = TestHelper.CapturePackageMessageBoxErrors();

            var vis = new TestVisualizer(o => o.DebuggerOptions.Watches.AddRange(
                new[] { "a", "tid", "c", "c[1]", "c[1][1]", "c[1][1][1]", "lst" }.Select(w => new Watch(w, new VariableType(VariableCategory.Uint, 32)))));

            var debugData = TestHelper.ReadFixtureBytes("DebugBuffer.bin");
            Assert.True(BreakState.CreateBreakState(TestHelper.ReadFixture("ValidWatches.txt"), TestHelper.ReadFixture("DispatchParams.txt"),
                new BreakStateOutputFile(new[] { "" }, true, 0, default, debugData.Length / 4), debugData).TryGetResult(out var breakState, out _));

            vis.EnterBreak(breakState);
            vis.Context.GroupIndex.X = 13;
            Assert.Empty(packageErrors);

            // Lists do not affect top-level watches
            Assert.Equal(vis.Options.DebuggerOptions.Watches, vis.Control.Table.GetCurrentWatchState());

            Assert.Equal("System", vis.GetNameCell(0).Value);
            for (var wave = 0; wave < breakState.Data.WavesPerGroup; ++wave)
            {
                var waveColOffset = VisualizerTable.DataColumnOffset + wave * breakState.Data.WaveSize;
                Assert.Equal("0x77777777", vis.Control.Table.Rows[0].Cells[waveColOffset + BreakStateData.SystemMagicNumberLane].Value);
                Assert.Equal(wave % 2 == 0 ? "0x0" : "0x1", vis.Control.Table.Rows[0].Cells[waveColOffset + BreakStateData.SystemInstanceIdLane].Value);
            }

            Assert.Equal("a", vis.GetNameCell(1).Value);
            for (var wave = 0; wave < breakState.Data.WavesPerGroup; ++wave)
                for (var tid = wave * breakState.Data.WaveSize; tid < (wave + 1) * breakState.Data.WaveSize; ++tid)
                    Assert.Equal(wave % 2 == 0 ? $"{tid}" : "", vis.Control.Table.Rows[1].Cells[VisualizerTable.DataColumnOffset + tid].Value);
            Assert.Equal("tid", vis.GetNameCell(2).Value);
            for (var wave = 0; wave < breakState.Data.WavesPerGroup; ++wave)
                for (var tid = wave * breakState.Data.WaveSize; tid < (wave + 1) * breakState.Data.WaveSize; ++tid)
                    Assert.Equal(wave % 2 == 0 ? "" : $"{tid}", vis.Control.Table.Rows[2].Cells[VisualizerTable.DataColumnOffset + tid].Value);

            int c = 3, c0 = 4, c1 = 5, c10 = 6, c11 = 7, c2 = 8, c3 = 9, c30 = 10, c4 = 11, c5 = 12;
            Assert.Equal(("c", ""), (vis.GetNameCell(c).Value, vis.GetNameCellParentRowIndexes(c)));
            Assert.Equal(("[0]", $"{c}"), (vis.GetNameCell(c0).Value, vis.GetNameCellParentRowIndexes(c0)));
            Assert.Equal(("[1]", $"{c}"), (vis.GetNameCell(c1).Value, vis.GetNameCellParentRowIndexes(c1)));
            Assert.Equal(("[0]", $"{c},{c1}"), (vis.GetNameCell(c10).Value, vis.GetNameCellParentRowIndexes(c10)));
            Assert.Equal(("[1]", $"{c},{c1}"), (vis.GetNameCell(c11).Value, vis.GetNameCellParentRowIndexes(c11)));
            Assert.Equal(("[2]", $"{c}"), (vis.GetNameCell(c2).Value, vis.GetNameCellParentRowIndexes(c2)));
            Assert.Equal(("[3]", $"{c}"), (vis.GetNameCell(c3).Value, vis.GetNameCellParentRowIndexes(c3)));
            Assert.Equal(("[0]", $"{c},{c3}"), (vis.GetNameCell(c30).Value, vis.GetNameCellParentRowIndexes(c30)));
            Assert.Equal(("[4]", $"{c}"), (vis.GetNameCell(c4).Value, vis.GetNameCellParentRowIndexes(c4)));
            Assert.Equal(("[5]", $"{c}"), (vis.GetNameCell(c5).Value, vis.GetNameCellParentRowIndexes(c5)));
            for (var wave = 0; wave < breakState.Data.WavesPerGroup; ++wave)
            {
                for (var tid = wave * breakState.Data.WaveSize; tid < (wave + 1) * breakState.Data.WaveSize; ++tid)
                {
                    Assert.Equal(wave % 2 == 0 ? $"{wave}" : "<list of 6>", vis.GetDataCell(c, tid).Value);
                    Assert.Equal(wave % 2 == 0 ? "" : "", vis.GetDataCell(c0, tid).Value);
                    Assert.Equal(wave % 2 == 0 ? "" : "<list of 2>", vis.GetDataCell(c1, tid).Value);
                    Assert.Equal(wave % 2 == 0 ? "" : $"{tid % breakState.Data.WaveSize}", vis.GetDataCell(c10, tid).Value);
                    Assert.Equal(wave % 2 == 0 ? "" : $"{vis.Context.GroupIndex.X}", vis.GetDataCell(c11, tid).Value);
                    Assert.Equal(wave % 2 == 0 ? "" : "", vis.GetDataCell(c2, tid).Value);
                    Assert.Equal(wave % 2 == 0 ? "" : "<list of 1>", vis.GetDataCell(c3, tid).Value);
                    Assert.Equal(wave % 2 == 0 ? "" : "", vis.GetDataCell(c30, tid).Value);
                    Assert.Equal(wave % 2 == 0 ? "" : "<list of 0>", vis.GetDataCell(c4, tid).Value);
                    Assert.Equal(wave % 2 == 0 ? "" : $"{vis.Context.GroupIndex.DimX}", vis.GetDataCell(c5, tid).Value);
                }
            }

            int c_1 = 13, c_10 = 14, c_11 = 15;
            Assert.Equal(("c[1]", ""), (vis.GetNameCell(c_1).Value, vis.GetNameCellParentRowIndexes(c_1)));
            Assert.Equal(("[0]", $"{c_1}"), (vis.GetNameCell(c_10).Value, vis.GetNameCellParentRowIndexes(c_10)));
            Assert.Equal(("[1]", $"{c_1}"), (vis.GetNameCell(c_11).Value, vis.GetNameCellParentRowIndexes(c_11)));
            for (var tid = 0; tid < breakState.Data.GroupSize; ++tid)
            {
                Assert.Equal(vis.GetDataCell(c1, tid).Value, vis.GetDataCell(c_1, tid).Value);
                Assert.Equal(vis.GetDataCell(c10, tid).Value, vis.GetDataCell(c_10, tid).Value);
                Assert.Equal(vis.GetDataCell(c11, tid).Value, vis.GetDataCell(c_11, tid).Value);
            }

            int c_1_1 = 16;
            Assert.Equal(("c[1][1]", ""), (vis.GetNameCell(c_1_1).Value, vis.GetNameCellParentRowIndexes(c_1_1)));
            for (var tid = 0; tid < breakState.Data.GroupSize; ++tid)
                Assert.Equal(vis.GetDataCell(c11, tid).Value, vis.GetDataCell(c_1_1, tid).Value);

            int c_1_1_1 = 17;
            Assert.Equal(("c[1][1][1]", ""), (vis.GetNameCell(c_1_1_1).Value, vis.GetNameCellParentRowIndexes(c_1_1_1)));
            for (var tid = 0; tid < breakState.Data.GroupSize; ++tid)
                Assert.Equal("", vis.GetDataCell(c_1_1_1, tid).Value);

            int lst = 18, lst0 = 19, lst1 = 20, lst2 = 21;
            Assert.Equal(("lst", ""), (vis.GetNameCell(lst).Value, vis.GetNameCellParentRowIndexes(lst)));
            Assert.Equal(("[0]", $"{lst}"), (vis.GetNameCell(lst0).Value, vis.GetNameCellParentRowIndexes(lst0)));
            Assert.Equal(("[1]", $"{lst}"), (vis.GetNameCell(lst1).Value, vis.GetNameCellParentRowIndexes(lst1)));
            Assert.Equal(("[2]", $"{lst}"), (vis.GetNameCell(lst2).Value, vis.GetNameCellParentRowIndexes(lst2)));
        }
    }
}
