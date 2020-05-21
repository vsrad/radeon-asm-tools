using Microsoft.VisualStudio.ProjectSystem;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem
{
    [Export(typeof(IPasteDataObjectProcessor))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    [Order(10000)]
    public sealed class ProjectItemPasteProcessor : IPasteDataObjectProcessor
    {
        [ImportMany]
        private OrderPrecedenceImportCollection<IPasteDataObjectProcessor> PasteProcessors { get; }

        private IPasteDataObjectProcessor _underlyingProcessor;

        [ImportingConstructor]
        public ProjectItemPasteProcessor(UnconfiguredProject unconfiguredProject)
        {
            PasteProcessors = new OrderPrecedenceImportCollection<IPasteDataObjectProcessor>(projectCapabilityCheckProvider: unconfiguredProject);
        }

        bool IPasteDataObjectProcessor.CanHandleDataObject(object dataObject, IProjectTree dropTarget, IProjectTreeProvider currentProvider)
        {
            _underlyingProcessor = PasteProcessors.FirstOrDefault(p => p.Value != this && p.Value.CanHandleDataObject(dataObject, dropTarget, currentProvider))?.Value;
            return _underlyingProcessor != null;
        }

        async Task<IEnumerable<ICopyPasteItem>> IPasteDataObjectProcessor.ProcessDataObjectAsync(object dataObject, IProjectTree dropTarget, IProjectTreeProvider currentProvider, DropEffects effect)
        {
            var pasteItems = await _underlyingProcessor.ProcessDataObjectAsync(dataObject, dropTarget, currentProvider, effect);
            // For some reason, the default system paste processor (Microsoft.VisualStudio.ProjectSystem.VS.Implementation.Package.WindowsPasteProcessor)
            // completely ignores DropEffects.Link, so we set IsLinked and LinkPath manually.
            if (effect == DropEffects.Link)
            {
                foreach (var item in pasteItems)
                {
                    var sourceProp = item.GetType().GetProperty("Source");
                    var isLinkedProp = item.GetType().GetProperty("IsLinked");
                    var linkPathProp = item.GetType().GetProperty("LinkPath");
                    if (sourceProp != null && isLinkedProp != null && linkPathProp != null)
                    {
                        var sourcePath = (string)sourceProp.GetValue(item);
                        var linkName = Path.GetFileName(sourcePath);

                        isLinkedProp.SetValue(item, true);
                        linkPathProp.SetValue(item, linkName);
                    }
                }
            }
            return pasteItems;
        }

        Task IPasteDataObjectProcessor.ProcessPostFilterAsync(IEnumerable<ICopyPasteItem> items) =>
            _underlyingProcessor.ProcessPostFilterAsync(items);

        DropEffects? IPasteDataObjectProcessor.QueryDropEffect(object dataObject, int grfKeyState, bool draggedFromThisProject)
        {
            if (!draggedFromThisProject)
            {
                // Ctrl+Shift
                if ((grfKeyState & NativeMethods.MK_SHIFT) != 0 && (grfKeyState & NativeMethods.MK_CONTROL) != 0)
                    return DropEffects.Copy;

                return DropEffects.Link;
            }

            return _underlyingProcessor.QueryDropEffect(dataObject, grfKeyState, draggedFromThisProject);
        }
    }
}
