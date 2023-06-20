using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Moq;
using System.Linq;
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
        [Fact]
        public async Task DebugDataFormattingTestAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var packageErrors = TestHelper.CapturePackageMessageBoxErrors();

            var projectOptions = new ProjectOptions();
            projectOptions.DebuggerOptions.Watches.AddRange(new[] { "a", "tid", "c", "lst", "tide" } // "c[1]", "c[1][1]"
                .Select(w => new Watch(w, new VariableType(VariableCategory.Uint, 32))));
            var debuggerMock = new Mock<IDebuggerIntegration>();
            var context = new VisualizerContext(projectOptions, null, debuggerMock.Object);

            var integrationMock = new Mock<IToolWindowIntegration>();
            integrationMock.Setup(i => i.ProjectOptions).Returns(projectOptions);
            integrationMock.Setup(i => i.GetVisualizerContext()).Returns(context);

            var fontAndColorMock = new Mock<IFontAndColorProvider>();
            fontAndColorMock.Setup(f => f.FontAndColorState).Returns(new FontAndColorState());
            var control = new VisualizerControl(integrationMock.Object, fontAndColorMock.Object);

            var debugData = TestHelper.ReadFixtureBytes("DebugBuffer.bin");
            Assert.True(BreakState.CreateBreakState(TestHelper.ReadFixture("ValidWatches.txt"), TestHelper.ReadFixture("DispatchParams.txt"),
                new BreakStateOutputFile(new[] { "" }, true, 0, default, debugData.Length / 4), debugData).TryGetResult(out var breakState, out _));

            debuggerMock.Raise(d => d.BreakEntered += null, null, breakState);
            context.GroupIndex.X = 13;
            Assert.Empty(packageErrors);

            WatchNameCell GetNameCell(int rowIndex) => (WatchNameCell)control.Table.Rows[rowIndex].Cells[VisualizerTable.NameColumnIndex];

            Assert.Equal("System", GetNameCell(0).Value);
            for (var wave = 0; wave < breakState.Data.WavesPerGroup; ++wave)
            {
                var waveColOffset = VisualizerTable.DataColumnOffset + wave * breakState.Data.WaveSize;
                Assert.Equal("0x77777777", control.Table.Rows[0].Cells[waveColOffset + BreakStateData.SystemMagicNumberLane].Value);
                Assert.Equal(wave % 2 == 0 ? "0x0" : "0x1", control.Table.Rows[0].Cells[waveColOffset + BreakStateData.SystemInstanceIdLane].Value);
            }

            Assert.Equal("a", GetNameCell(1).Value);
            for (var wave = 0; wave < breakState.Data.WavesPerGroup; ++wave)
                for (var tid = wave * breakState.Data.WaveSize; tid < (wave + 1) * breakState.Data.WaveSize; ++tid)
                    Assert.Equal(wave % 2 == 0 ? $"{tid}" : "", control.Table.Rows[1].Cells[VisualizerTable.DataColumnOffset + tid].Value);
            Assert.Equal("tid", GetNameCell(2).Value);
            for (var wave = 0; wave < breakState.Data.WavesPerGroup; ++wave)
                for (var tid = wave * breakState.Data.WaveSize; tid < (wave + 1) * breakState.Data.WaveSize; ++tid)
                    Assert.Equal(wave % 2 == 0 ? "" : $"{tid}", control.Table.Rows[2].Cells[VisualizerTable.DataColumnOffset + tid].Value);

            int c = 3, c0 = 4, c1 = 5, c1_0 = 6, c1_1 = 7, c2 = 8, c3 = 9, c3_0 = 10, c4 = 11, c5 = 12;
            Assert.Equal(("c", ""), (GetNameCell(c).Value, string.Join(",", GetNameCell(c).ParentRowIndexes)));
            Assert.Equal(("[0]", $"{c}"), (GetNameCell(c0).Value, string.Join(",", GetNameCell(c0).ParentRowIndexes)));
            Assert.Equal(("[1]", $"{c}"), (GetNameCell(c1).Value, string.Join(",", GetNameCell(c1).ParentRowIndexes)));
            Assert.Equal(("[0]", $"{c},{c1}"), (GetNameCell(c1_0).Value, string.Join(",", GetNameCell(c1_0).ParentRowIndexes)));
            Assert.Equal(("[1]", $"{c},{c1}"), (GetNameCell(c1_1).Value, string.Join(",", GetNameCell(c1_1).ParentRowIndexes)));
            Assert.Equal(("[2]", $"{c}"), (GetNameCell(c2).Value, string.Join(",", GetNameCell(c2).ParentRowIndexes)));
            Assert.Equal(("[3]", $"{c}"), (GetNameCell(c3).Value, string.Join(",", GetNameCell(c3).ParentRowIndexes)));
            Assert.Equal(("[0]", $"{c},{c3}"), (GetNameCell(c3_0).Value, string.Join(",", GetNameCell(c3_0).ParentRowIndexes)));
            Assert.Equal(("[4]", $"{c}"), (GetNameCell(c4).Value, string.Join(",", GetNameCell(c4).ParentRowIndexes)));
            Assert.Equal(("[5]", $"{c}"), (GetNameCell(c5).Value, string.Join(",", GetNameCell(c5).ParentRowIndexes)));
            for (var wave = 0; wave < breakState.Data.WavesPerGroup; ++wave)
            {
                for (var tid = wave * breakState.Data.WaveSize; tid < (wave + 1) * breakState.Data.WaveSize; ++tid)
                {
                    Assert.Equal(wave % 2 == 0 ? $"{wave}" : "<6 elements>", control.Table.Rows[c].Cells[VisualizerTable.DataColumnOffset + tid].Value);
                    Assert.Equal(wave % 2 == 0 ? "" : "", control.Table.Rows[c0].Cells[VisualizerTable.DataColumnOffset + tid].Value);
                    Assert.Equal(wave % 2 == 0 ? "" : "<2 elements>", control.Table.Rows[c1].Cells[VisualizerTable.DataColumnOffset + tid].Value);
                    Assert.Equal(wave % 2 == 0 ? "" : $"{tid % breakState.Data.WaveSize}", control.Table.Rows[c1_0].Cells[VisualizerTable.DataColumnOffset + tid].Value);
                    Assert.Equal(wave % 2 == 0 ? "" : $"{context.GroupIndex.X}", control.Table.Rows[c1_1].Cells[VisualizerTable.DataColumnOffset + tid].Value);
                    Assert.Equal(wave % 2 == 0 ? "" : "", control.Table.Rows[c2].Cells[VisualizerTable.DataColumnOffset + tid].Value);
                    Assert.Equal(wave % 2 == 0 ? "" : "<1 element>", control.Table.Rows[c3].Cells[VisualizerTable.DataColumnOffset + tid].Value);
                    Assert.Equal(wave % 2 == 0 ? "" : "", control.Table.Rows[c3_0].Cells[VisualizerTable.DataColumnOffset + tid].Value);
                    Assert.Equal(wave % 2 == 0 ? "" : "<0 elements>", control.Table.Rows[c4].Cells[VisualizerTable.DataColumnOffset + tid].Value);
                    Assert.Equal(wave % 2 == 0 ? "" : $"{context.GroupIndex.DimX}", control.Table.Rows[c5].Cells[VisualizerTable.DataColumnOffset + tid].Value);
                }
            }
        }
    }
}
