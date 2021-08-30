using System;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.Options
{
    public class GeneralOptionPage : BaseOptionPage<GeneralOptions>
    {
        private readonly OptionsProvider _optionsEventProvider;

        public GeneralOptionPage()
        {
            _optionsEventProvider = Package.Instance.GetMEFComponent<OptionsProvider>();
        }

        protected override void OnApply(PageApplyEventArgs e)
        {
            try
            {
                base.OnApply(e);
                _optionsEventProvider.OptionsUpdatedInvoke();
            }
            catch (Exception ex)
            {
                Error.ShowWarning(ex);
            }
        }
    }
}