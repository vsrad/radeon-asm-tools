﻿using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace VSRAD.Syntax.Core
{
    public delegate void ActiveDocumentChangedEventHandler(IDocument activeDocument);
    public delegate void DocumentCreatedEventHandler(IDocument document);
    public delegate void DocumentDisposedEventHandler(IDocument document);

    public interface IDocumentFactory
    {
        /// <summary>
        /// Returns current active document full path
        /// </summary>
        /// <returns>String, active document full path</returns>
        string GetActiveDocumentPath();

        /// <summary>
        /// Gets <see cref="IDocument"/> with the specified content type or creates if doesn't exist. If content type is null, it is inferred from file extension.
        /// </summary>
        /// <returns><see cref="IDocument"/> for RadAsm content type otherwise null</returns>
        IDocument GetOrCreateDocument(string path, IContentType contentType = null);

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
