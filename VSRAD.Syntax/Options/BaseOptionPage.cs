using Microsoft.VisualStudio.Shell;

namespace VSRAD.Syntax.Options
{
    public class BaseOptionPage<T> : DialogPage where T : BaseOptionModel<T>, new()
    {
        private readonly BaseOptionModel<GeneralOptions> _model;

        public BaseOptionPage()
        {
            _model = GeneralOptions.Instance;
        }

        public override object AutomationObject => _model;

        public override void LoadSettingsFromStorage() => _model.Load();

        public override void SaveSettingsToStorage() => _model.Save();
    }
}
