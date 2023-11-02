using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace VSRAD.Package.ProjectSystem.EditorExtensions
{
    [Export(typeof(IGlyphFactoryProvider))]
    [Name("RADBreakLineGlyph")]
    [Order(After = "VsTextMarker")]
    [ContentType("any")]
    [TagType(typeof(BreakLineGlyphTag))]
    public sealed class BreakLineGlyphFactoryProvider : IGlyphFactoryProvider
    {
        public IGlyphFactory GetGlyphFactory(IWpfTextView view, IWpfTextViewMargin margin) =>
            new BreakLineGlyphFactory();
    }

    public sealed class BreakLineGlyphFactory : IGlyphFactory
    {
        private static BitmapImage _glyph;
        private static BitmapImage Glyph
        {
            get
            {
                if (_glyph == null)
                    _glyph = new BitmapImage(new Uri(Constants.CurrentStatementIconResourcePackUri));
                return _glyph;
            }
        }

        public UIElement GenerateGlyph(IWpfTextViewLine line, IGlyphTag tag)
        {
            if (tag is BreakLineGlyphTag _)
                return new Image { Source = Glyph, Width = 11, Height = 11, Margin = new Thickness(1, 2.5, 0, 0) };
            return null;
        }
    }
}
