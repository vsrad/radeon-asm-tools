using Microsoft.VisualStudio.Shell;
using System.ComponentModel;

namespace VSRAD.Syntax.Options
{
    public class OptionPage : DialogPage
    {
        [Category("Function list")]
        [DisplayName("Function list default sort option")]
        [Description("Set default sort option for Function List")]
        [DefaultValue(SortState.ByName)]
        public SortState sortOptions { get; set; } = SortState.ByName;

        [Category("Syntax highlight")]
        [DisplayName("Indent guide lines")]
        [Description("Enable/disable indent guide lines")]
        [DefaultValue(true)]
        public bool isEnabledIndentGuides { get; set; } = true;

        public enum SortState
        {
            [Description("by line number")]
            ByLine = 1,
            [Description("by line number descending")]
            ByLineDescending = 2,
            [Description("by name")]
            ByName = 3,
            [Description("by name descending")]
            ByNameDescending = 4,
        }

        protected override void OnApply(PageApplyEventArgs e)
        {
            base.OnApply(e);
            FunctionList.FunctionListControl.OnChangeOptions(sortOptions);
        }
    }
}