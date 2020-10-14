using System.Collections.Generic;
using VSRAD.Syntax.IntelliSense.Navigation;

namespace VSRAD.Syntax.Options.Instructions
{
    public sealed class Instruction
    {
        public string Text { get; }
        public IReadOnlyList<NavigationToken> Navigations { get; }

        public Instruction(string text, IReadOnlyList<NavigationToken> navigations)
        {
            Text = text;
            Navigations = navigations;
        }
    }
}
