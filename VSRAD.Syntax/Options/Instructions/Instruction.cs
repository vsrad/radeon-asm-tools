using System.Collections.Generic;
using VSRAD.Syntax.IntelliSense.Navigation;

namespace VSRAD.Syntax.Options.Instructions
{
    public sealed class Instruction
    {
        public string Text { get; }
        public IReadOnlyList<INavigationToken> Navigations { get; }

        public Instruction(string text, IReadOnlyList<INavigationToken> navigations)
        {
            Text = text;
            Navigations = navigations;
        }
    }
}
