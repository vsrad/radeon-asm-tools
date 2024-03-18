using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VSRAD.SyntaxPackageBridge
{
    public sealed class TargetProcessorListEventArgs : EventArgs
    {
        public IReadOnlyList<(string TargetProcessor, string SyntaxName)> List { get; set; }
    }

    /// <summary>
    /// Specifies the interface for inter-extension communication between VSRAD.Package and VSRAD.Syntax.
    /// The interface is implemented by VSRAD.Package, and VSRAD.Syntax requests the implementation via MEF at runtime.
    /// </summary>
    public interface ISyntaxPackageBridge
    {
        /// <summary>Request the list of supported target processors from the Syntax extension.</summary>
        event EventHandler<TargetProcessorListEventArgs> PackageRequestedTargetProcessorList;

        /// <summary>Notify the Syntax extension that the target processor selection has changed.</summary>
        event EventHandler<EventArgs> PackageUpdatedSelectedTargetProcessor;

        /// <summary>Request the selected target processor from the Package extension.</summary>
        Task<(string TargetProcessor, string SyntaxName)> GetSelectedTargetProcessor();
    }
}
