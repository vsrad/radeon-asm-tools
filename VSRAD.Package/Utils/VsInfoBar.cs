using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;

namespace VSRAD.Package.Utils
{
    public sealed class VsInfoBar : IVsInfoBarUIEvents
    {
        private readonly InfoBarModel _model;
        private IVsInfoBarUIElement _uiElement;
        private readonly uint _listenerCookie;
        private readonly EventHandler<InfoBarActionItemEventArgs> _actionItemClickHandler;

        public bool IsVisible => _uiElement != null;

        public VsInfoBar(SVsServiceProvider serviceProvider, InfoBarModel model, EventHandler<InfoBarActionItemEventArgs> actionItemClickHandler)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var vsInfoBarFactory = (IVsInfoBarUIFactory)serviceProvider.GetService(typeof(SVsInfoBarUIFactory));
            Assumes.Present(vsInfoBarFactory);

            var vsShell = (IVsShell)serviceProvider.GetService(typeof(SVsShell));
            Assumes.Present(vsShell);

            ErrorHandler.ThrowOnFailure(vsShell.GetProperty((int)__VSSPROPID7.VSSPROPID_MainWindowInfoBarHost, out var host));
            var vsInfoBarHost = (IVsInfoBarHost)host;

            _model = model;
            _uiElement = vsInfoBarFactory.CreateInfoBar(_model);
            _uiElement.Advise(this, out _listenerCookie);
            vsInfoBarHost.AddInfoBar(_uiElement);

            _actionItemClickHandler = actionItemClickHandler;
        }

        public void Close()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _uiElement?.Close();
        }

        void IVsInfoBarUIEvents.OnClosed(IVsInfoBarUIElement infoBarUIElement)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _uiElement?.Unadvise(_listenerCookie);
            _uiElement = null;
        }

        void IVsInfoBarUIEvents.OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem)
        {
            _actionItemClickHandler?.Invoke(this, new InfoBarActionItemEventArgs(infoBarUIElement, _model, actionItem));
        }
    }
}
