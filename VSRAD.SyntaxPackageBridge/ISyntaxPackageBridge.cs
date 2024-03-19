using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VSRAD.SyntaxPackageBridge
{
    /// <summary>
    /// Specifies the interface for inter-extension communication between VSRAD.Package and VSRAD.Syntax.
    /// The interface is implemented by VSRAD.Package, and VSRAD.Syntax requests the implementation via MEF at runtime.
    /// </summary>
    public interface ISyntaxPackageBridge
    {
        /// <summary>Request the list of supported file extensions from the Syntax extension.</summary>
        event EventHandler<FileExtensionListEventArgs> PackageRequestedSupportedFileExtensionList;

        /// <summary>Request the list of supported target processors from the Syntax extension.</summary>
        event EventHandler<TargetProcessorListEventArgs> PackageRequestedTargetProcessorList;

        /// <summary>Notify the Syntax extension that the target processor selection has changed.</summary>
        event EventHandler<EventArgs> PackageUpdatedSelectedTargetProcessor;

        /// <summary>Request the selected target processor from the Package extension.</summary>
        Task<(string Processor, string InstructionSet)> GetSelectedTargetProcessor();
    }

    public sealed class FileExtensionListEventArgs : EventArgs
    {
        public IEnumerable<string> FileExtensions { get; set; }
    }

    public sealed class TargetProcessorListEventArgs : EventArgs
    {
        public IEnumerable<(string Processor, string InstructionSet)> TargetProcessors { get; set; }
    }
}
