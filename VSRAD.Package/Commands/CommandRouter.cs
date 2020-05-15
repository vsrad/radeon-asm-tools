using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace VSRAD.Package.Commands
{
    public interface ICommandRouter : IOleCommandTarget { }

    public interface ICommandHandler
    {
        Guid CommandSet { get; }

        OLECMDF GetCommandStatus(uint commandId);

        void Execute(uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut);
    }

    [Export(typeof(ICommandRouter))]
    public sealed class CommandRouter : ICommandRouter
    {
        [ImportMany]
        public IEnumerable<ICommandHandler> Handlers { get; set; }

        public int QueryStatus(ref Guid cmdSet, uint commandCount, OLECMD[] commands, IntPtr pCmdText)
        {
            foreach (var handler in Handlers)
            {
                if (handler.CommandSet == cmdSet)
                {
                    for (var cmd = 0; cmd < commands.Length; ++cmd)
                        commands[cmd].cmdf = (uint)handler.GetCommandStatus(commands[cmd].cmdID);
                    break;
                }
            }
            return VSConstants.S_OK;
        }

        public int Exec(ref Guid cmdSet, uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            foreach (var handler in Handlers)
            {
                if (handler.CommandSet == cmdSet)
                {
                    handler.Execute(commandId, commandExecOpt, variantIn, variantOut);
                    break;
                }
            }
            return VSConstants.S_OK;
        }
    }
}
