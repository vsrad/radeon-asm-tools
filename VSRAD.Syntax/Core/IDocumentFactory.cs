﻿using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Core
{
    public delegate void ActiveDocumentChangedEventHandler(IDocument activeDocument);
    public delegate void DocumentCreatedEventHandler(IDocument document);
    public delegate void DocumentDisposedEventHandler(IDocument document);

    public interface IDocumentFactory
    {
        /// <summary>
        /// Gets <see cref="IDocument"/> or creates if doesn't exist
        /// </summary>
        /// <param name="path"></param>
        /// <param name="observe">If true then document is managed with factory otherwise it's should managed with caller</param>
        /// <returns><see cref="IDocument"/> if <paramref name="path"/> is RadAsm content type otherwise null</returns>
        IDocument GetOrCreateDocument(string path, bool observe = true);

        /// <summary>
        /// Gets <see cref="IDocument"/> or creates if doesn't exist
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns><see cref="IDocument"/> if <paramref name="buffer"/> is RadAsm content type otherwise null</returns>
        IDocument GetOrCreateDocument(ITextBuffer buffer);

        /// <summary>
        /// Invokes when the active document changed.
        /// Pass <see cref="IDocument"/> if the current window is RadAsm documet otherwise null
        /// </summary>
        event ActiveDocumentChangedEventHandler ActiveDocumentChanged;

        event DocumentCreatedEventHandler DocumentCreated;
        event DocumentDisposedEventHandler DocumentDisposed;
    }
}
