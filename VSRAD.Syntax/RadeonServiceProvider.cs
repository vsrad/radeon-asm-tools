using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;
using EnvDTE;

namespace VSRAD.Syntax
{
    [Export]
    internal sealed class RadeonServiceProvider
    {
        [ImportingConstructor]
        public RadeonServiceProvider([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider, 
            IVsEditorAdaptersFactoryService editorAdaptersFactoryService)
        {
            ServiceProvider = serviceProvider;
            Dte = ServiceProvider.GetService(typeof(DTE)) as DTE;
            EditorAdaptersFactoryService = editorAdaptersFactoryService;
        }

        public readonly DTE Dte;

        public readonly IServiceProvider ServiceProvider;

        public readonly IVsEditorAdaptersFactoryService EditorAdaptersFactoryService;
    }
}
