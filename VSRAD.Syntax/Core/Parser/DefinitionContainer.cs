using System.Collections.Generic;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core.Parser
{
    [Export(typeof(DefinitionContainer))]
    public class DefinitionContainer
    {
        private readonly Dictionary<string, Stack<(IBlock, DefinitionToken)>> _container;
        public DefinitionContainer()
        {
            _container = new Dictionary<string, Stack<(IBlock, DefinitionToken)>>();
        }

        public void Add(IBlock block, DefinitionToken definitionToken)
        {
            var text = definitionToken.GetText();
            Add(block, definitionToken, text);
        }

        public void Add(IBlock block, DefinitionToken definitionToken, string text)
        {
            if (_container.TryGetValue(text, out var stack))
            {
                if (stack.Count > 0)
                {
                    var prevValue = stack.Peek();
                    if (prevValue.Item1 == block) return;
                }
            }
            else
            {
                stack = new Stack<(IBlock, DefinitionToken)>();
                _container.Add(text, stack);
            }

            stack.Push((block, definitionToken));
        }

        public void ClearScope(IBlock block)
        {
            foreach (var stack in _container.Values)
            {
                if (stack.Count > 0)
                {
                    var prevValue = stack.Peek();
                    if (prevValue.Item1 == block) stack.Pop();
                }
            }
        }

        public bool TryGetDefinition(string text, out DefinitionToken definitionToken)
        {
            if (_container.TryGetValue(text, out var stack))
            {
                if (stack.Count > 0)
                {
                    definitionToken = stack.Peek().Item2;
                    return true;
                }
            }

            definitionToken = null;
            return false;
        }

        public void Clear()
        {
            foreach (var stack in _container.Values)
                stack.Clear();
        }
    }
}
