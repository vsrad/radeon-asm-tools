using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;

namespace VSRAD.Syntax.Helpers
{
    public static class SnapshotPointExtension
    {
        public static TextExtent GetExtent(this SnapshotPoint snapshotPoint)
        {
            var startPoint = snapshotPoint;
            SnapshotPoint endPoint;

            while (startPoint > 0)
            {
                endPoint = startPoint - 1;
                if (endPoint.IsSuitableChar())
                    startPoint = endPoint;
                else
                    break;
            }

            endPoint = snapshotPoint;
            while (endPoint < snapshotPoint.Snapshot.Length - 1 )
            {
                if (endPoint.IsSuitableChar())
                    endPoint += 1;
                else
                    break;
            }

            return new TextExtent(new SnapshotSpan(startPoint, endPoint), (endPoint - startPoint) > 0);
        }

        private static bool IsSuitableChar(this SnapshotPoint snapshotPoint)
        {
            var pointChar = snapshotPoint.GetChar();
            return char.IsLetterOrDigit(pointChar) || pointChar == '_' || pointChar == '\\' || pointChar == '.';
        }
    }
}
