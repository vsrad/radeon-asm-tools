using System.Windows;
using System.Windows.Controls;

namespace VSRAD.Package.ProjectSystem.Profiles
{
    [TemplatePart(Name = "PART_HeaderMainContentPresenter", Type = typeof(ContentPresenter))]
    [TemplatePart(Name = "PART_HeaderSideContentPresenter", Type = typeof(ContentPresenter))]
    public partial class ExtendedExpander : Expander
    {
        public object HeaderMainContent
        {
            get => GetValue(HeaderMainContentProperty);
            set => SetValue(HeaderMainContentProperty, value);
        }

        public object HeaderSideContent
        {
            get => GetValue(HeaderSideContentProperty);
            set => SetValue(HeaderSideContentProperty, value);
        }

        public static readonly DependencyProperty HeaderMainContentProperty =
            DependencyProperty.Register(nameof(HeaderMainContent), typeof(object), typeof(ExtendedExpander), new UIPropertyMetadata(null));

        public static readonly DependencyProperty HeaderSideContentProperty =
            DependencyProperty.Register(nameof(HeaderSideContent), typeof(object), typeof(ExtendedExpander), new UIPropertyMetadata(null));

        static ExtendedExpander()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ExtendedExpander), new FrameworkPropertyMetadata(typeof(ExtendedExpander)));
        }

        public ExtendedExpander()
        {
            InitializeComponent();
        }
    }
}
