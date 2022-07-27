using System.Collections.Generic;
using VSRAD.Syntax.Core.Blocks;

namespace VSRAD.Syntax.Core.Helper
{
    internal static class ListBlockExtension
    {
        public static T AppendBlock<T>(this List<IBlock> blocks, T block) where T : IBlock
        {
            blocks.Add(block);
            if (block.Parent != null)
                block.Parent.AddChildren(block);

            return block;
        }

        public static IBlock GetParent(this IBlock block) => block.Parent ?? block;
    }
}
