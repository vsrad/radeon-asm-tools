using Microsoft.VisualStudio.Shell;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VSRAD.Package.ProjectSystem;

namespace VSRAD.Package.ToolWindows
{
    public interface IDisposableToolWindow
    {
        void DisposeToolWindow();
    }

    public abstract class BaseToolWindow : ToolWindowPane
    {
        protected UIElement Control { get; set; }

        private EnvDTE.WindowEvents _windowEvents;
        private bool _dteWindowHasFocus;
        protected EnvDTE.DTE _dte;

        private readonly UIElement _projectStateMissingMessage = new TextBlock
        {
            Text = "No active projects found.",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        protected BaseToolWindow(string caption) : base(null)
        {
            Caption = caption;
            Content = new Grid
            {
                Background = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            OnProjectUnloaded();
        }

        protected abstract UIElement CreateToolControl(IToolWindowIntegration integration);

        public void OnProjectLoaded(IToolWindowIntegration integration)
        {
            Control = CreateToolControl(integration);
            ((Grid)Content).Children.Clear();
            ((Grid)Content).Children.Add(Control);
        }

        public void OnProjectUnloaded()
        {
            if (Control is IDisposableToolWindow disposable)
                disposable.DisposeToolWindow();

            Control = null;
            ((Grid)Content).Children.Clear();
            ((Grid)Content).Children.Add(_projectStateMissingMessage);
        }

        public override void OnToolWindowCreated()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = (EnvDTE.DTE)GetService(typeof(EnvDTE.DTE));
            _windowEvents = dte.Events.WindowEvents;
            _dte = dte;
            _windowEvents.WindowActivated += OnDteWindowFocusChanged;
            Application.Current.Activated += (sender, e) => OnVsWindowFocusChanged(hasFocus: true);
            Application.Current.Deactivated += (sender, e) => OnVsWindowFocusChanged(hasFocus: false);
        }

        protected virtual void OnWindowFocusChanged(bool hasFocus) { }

        private void OnDteWindowFocusChanged(EnvDTE.Window gotFocus, EnvDTE.Window lostFocus)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (gotFocus?.Caption == Caption)
            {
                _dteWindowHasFocus = true;
                OnWindowFocusChanged(_dteWindowHasFocus);
            }
            else if (lostFocus?.Caption == Caption)
            {
                _dteWindowHasFocus = false;
                OnWindowFocusChanged(_dteWindowHasFocus);
            }
        }

        private void OnVsWindowFocusChanged(bool hasFocus)
        {
            if (_dteWindowHasFocus)
                OnWindowFocusChanged(hasFocus);
        }
    }
}
