using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Formatting;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Media;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.SyntaxHighlighter
{
    [Export]
    internal class ThemeColorManager
    {
        private const string Module = "Theme color manager";

        #region colors
        static readonly Dictionary<string, FontColor> LightAndBlueColors = new Dictionary<string, FontColor>
        {
            { PredefinedClassificationTypeNames.Instructions, new FontColor(Colors.Purple) },
            { PredefinedClassificationTypeNames.Arguments, new FontColor(Color.FromRgb(110, 110, 110)) },
            { PredefinedClassificationTypeNames.Functions, new FontColor(Colors.Teal) },
            { PredefinedClassificationTypeNames.Labels, new FontColor(Colors.Olive) },
        };
        static readonly Dictionary<string, FontColor> LightAndBlueEditorColors = new Dictionary<string, FontColor>
        {
            { PredefinedMarkerFormatNames.ReferenceIdentifier, new FontColor(null, Color.FromRgb(219, 224, 204)) },
            { PredefinedMarkerFormatNames.DefinitionIdentifier, new FontColor(null, Color.FromRgb(219, 224, 204)) },
            { PredefinedMarkerFormatNames.BraceMatching, new FontColor(null, Color.FromRgb(219, 224, 204)) },
        };

        static readonly Dictionary<string, FontColor> DarkColors = new Dictionary<string, FontColor>
        {
            { PredefinedClassificationTypeNames.Instructions, new FontColor(Colors.DarkOrange) },
            { PredefinedClassificationTypeNames.Arguments, new FontColor(Color.FromRgb(200, 200, 200)) },
            { PredefinedClassificationTypeNames.Functions, new FontColor(Color.FromRgb(78, 201, 178)) },
            { PredefinedClassificationTypeNames.Labels, new FontColor(Colors.Olive) },
        };
        static readonly Dictionary<string, FontColor> DarkEditorColors = new Dictionary<string, FontColor>
        {
            { PredefinedMarkerFormatNames.ReferenceIdentifier, new FontColor(Color.FromRgb(173, 192, 211), Color.FromRgb(14, 69, 131)) },
            { PredefinedMarkerFormatNames.DefinitionIdentifier, new FontColor(Color.FromRgb(192, 211, 173), Color.FromRgb(72, 131, 14)) },
            { PredefinedMarkerFormatNames.BraceMatching, new FontColor(Color.FromRgb(173, 192, 211), Color.FromRgb(14, 69, 131)) },
        };
        #endregion

        private readonly IEditorFormatMapService _editorFormatMapService;
        private readonly IClassificationFormatMapService _classificationFormatMapService;
        private readonly IClassificationTypeRegistryService _classificationTypeRegistry;
        private VisualStudioTheme currentTheme;

        [ImportingConstructor]
        public ThemeColorManager(
            IEditorFormatMapService editorFormatMapService,
            IClassificationFormatMapService classificationFormatMapService,
            IClassificationTypeRegistryService classificationTypeRegistry)
        {
            _editorFormatMapService = editorFormatMapService;
            _classificationFormatMapService = classificationFormatMapService;
            _classificationTypeRegistry = classificationTypeRegistry;
            currentTheme = GetTheme();
        }

        public FontColor GetDefaultColors(string category)
        {
            var color = FontColor.Default;
            switch (currentTheme)
            {
                case VisualStudioTheme.Light:
                case VisualStudioTheme.Blue:
                    if (!LightAndBlueColors.TryGetValue(category, out color))
                        LightAndBlueEditorColors.TryGetValue(category, out color);
                    return color;
                case VisualStudioTheme.Dark:
                    if (!DarkColors.TryGetValue(category, out color))
                        DarkEditorColors.TryGetValue(category, out color);
                    return color;
                default:
                    Error.LogError($"Unknown theme color", Module);
                    return color;
            }
        }

        public void UpdateColors()
        {
            currentTheme = GetTheme();

            var classificationColors = currentTheme == VisualStudioTheme.Light ? LightAndBlueColors : DarkColors;
            var editorColors = currentTheme == VisualStudioTheme.Light ? LightAndBlueEditorColors : DarkEditorColors;

            var classificationFormatMap = _classificationFormatMapService.GetClassificationFormatMap(category: "text");
            var editorFormatMap = _editorFormatMapService.GetEditorFormatMap(category: "text");

            UpdateClassificationColors(classificationFormatMap, classificationColors);
            UpdateEditorColors(editorFormatMap, editorColors);
        }

        private void UpdateClassificationColors(IClassificationFormatMap formatMap, Dictionary<string, FontColor> colors)
        {
            try
            {
                formatMap.BeginBatchUpdate();
                foreach (var pair in colors)
                {
                    var type = pair.Key;
                    var color = pair.Value;

                    var classificationType = _classificationTypeRegistry.GetClassificationType(type);
                    if (classificationType == null)
                    {
                        Error.LogError($"Cannot find classification type related to {type}", Module);
                        continue;
                    }
                    
                    var oldProp = formatMap.GetTextProperties(classificationType);

                    var foregroundBrush = color.Foreground == null
                        ? null
                        : new SolidColorBrush(color.Foreground.Value);

                    var backgroundBrush = color.Background == null
                            ? null
                            : new SolidColorBrush(color.Background.Value);

                    var newProp = TextFormattingRunProperties.CreateTextFormattingRunProperties(
                        foregroundBrush, backgroundBrush, oldProp.Typeface, null, null, oldProp.TextDecorations,
                        oldProp.TextEffects, oldProp.CultureInfo);

                    formatMap.SetTextProperties(classificationType, newProp);
                }
            }
            finally
            {
                formatMap.EndBatchUpdate();
            }
        }

        private void UpdateEditorColors(IEditorFormatMap formatMap, Dictionary<string, FontColor> colors)
        {
            try
            {
                formatMap.BeginBatchUpdate();
                foreach (var pair in colors)
                {
                    var type = pair.Key;
                    var color = pair.Value;

                    var property = formatMap.GetProperties(type);
                    if (property == null)
                    {
                        Error.LogError($"Cannot find editor format related to {type}", Module);
                        continue;
                    }

                    property.Remove("ForegroundColor");
                    property.Remove("BackgroundColor");
                    property.Add("ForegroundColor", color.Foreground);
                    property.Add("BackgroundColor", color.Background);

                    formatMap.SetProperties(type, property);
                }
            }
            finally
            {
                formatMap.EndBatchUpdate();
            }
        }

        private static VisualStudioTheme GetTheme()
        {
            var themeColor = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowTextColorKey);
            return themeColor.GetBrightness() > 0.5 ? VisualStudioTheme.Dark : VisualStudioTheme.Light;
        }

        private enum VisualStudioTheme
        {
            Light,
            Blue,
            Dark,
        }

        public class FontColor
        {
            public static FontColor Default => new FontColor(Color.FromRgb(220, 220, 220), Color.FromRgb(30, 30, 30));
            public readonly Color? Foreground;
            public readonly Color? Background;

            public FontColor(Color? foreground = null, Color? background = null)
            {
                Foreground = foreground;
                Background = background;
            }
        }
    }
}
