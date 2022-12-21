using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace VSRAD.Syntax.Core.Parser
{
    [Export(typeof(DocumentManager))]
    public class DocumentManager
    {
        private List<DocumentNode> _documents;

        public DocumentManager()
        {
            _documents = new List<DocumentNode>();
        }

        private DocumentNode GetNodeForDoc(IDocument document)
        {
            var node = _documents.FirstOrDefault(d => d.Document == document);
            if (node == default(DocumentNode))
            {
                node = new DocumentNode(document);
                _documents.Add(node);
            }
            return node;
        }

        public DefinitionContainer GetContainerForDoc(IDocument document)
                                => GetNodeForDoc(document).DefinitionContainer;

        public void AddChild(IDocument parent, IDocument child)
        {
            var pNode = GetNodeForDoc(parent);
            var cNode = GetNodeForDoc(child);
            if (!pNode.Children.Contains(cNode))
                pNode.Children.Add(cNode);
        }
    }

    sealed class DocumentNode
    {
        public readonly IDocument Document;
        public readonly DefinitionContainer DefinitionContainer;
        public readonly List<DocumentNode> Children;

        public DocumentNode(IDocument doc)
        {
            Document = doc;
            DefinitionContainer = new DefinitionContainer();
            Children = new List<DocumentNode>();
        }
    }
}
