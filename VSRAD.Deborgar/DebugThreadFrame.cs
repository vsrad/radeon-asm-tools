using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System;

namespace VSRAD.Deborgar
{
    public sealed class DebugThreadFrame : IDebugStackFrame2, IDebugDocumentContext2, IDebugCodeContext2
    {
        public string Location { get; }
        public string DisplayName { get; }

        private readonly string _sourcePath;
        private readonly TEXT_POSITION _sourcePosition;

        public DebugThreadFrame(string name, string sourcePath, TEXT_POSITION sourcePosition)
        {
            Location = $"{sourcePath}:{sourcePosition.dwLine + 1}";
            DisplayName = string.IsNullOrEmpty(name) ? $"{System.IO.Path.GetFileName(sourcePath)}:{sourcePosition.dwLine + 1}" : name;
            _sourcePath = sourcePath;
            _sourcePosition = sourcePosition;
        }

        public void SetFrameInfo(enum_FRAMEINFO_FLAGS dwFieldSpec, out FRAMEINFO frameInfo)
        {
            var hasSource = !string.IsNullOrEmpty(_sourcePath);
            frameInfo = new FRAMEINFO
            {
                m_bstrFuncName = DisplayName,
                m_bstrLanguage = Constants.LanguageName,
                m_pFrame = this,
                m_fHasDebugInfo = hasSource ? 1 : 0,
                m_dwFlags = hasSource ? 0 : (uint)enum_FRAMEINFO_FLAGS_VALUES.FIFV_NON_USER_CODE
            };
            frameInfo.m_dwValidFields |= enum_FRAMEINFO_FLAGS.FIF_FRAME
                | enum_FRAMEINFO_FLAGS.FIF_LANGUAGE
                | enum_FRAMEINFO_FLAGS.FIF_FUNCNAME
                | enum_FRAMEINFO_FLAGS.FIF_FLAGS
                | enum_FRAMEINFO_FLAGS.FIF_DEBUGINFO;
        }

        public int GetLanguageInfo(ref string pbstrLanguage, ref Guid pguidLanguage)
        {
            (pbstrLanguage, pguidLanguage) = (Constants.LanguageName, Constants.LanguageGuid);
            return VSConstants.S_OK;
        }

        public int GetDocumentContext(out IDebugDocumentContext2 ppSrcCxt)
        {
            ppSrcCxt = this;
            return VSConstants.S_OK;
        }

        #region IDebugStackFrame2 Members

        int IDebugStackFrame2.EnumProperties(enum_DEBUGPROP_INFO_FLAGS dwFields, uint dwRadix, ref Guid guidFilter, uint dwTimeout, out uint pcelt, out IEnumDebugPropertyInfo2 ppEnum)
        {
            ppEnum = new AD7PropertyInfoEnum(new DEBUG_PROPERTY_INFO[] { });
            ppEnum.GetCount(out pcelt);
            return VSConstants.S_OK;
        }

        int IDebugStackFrame2.GetCodeContext(out IDebugCodeContext2 ppCodeCxt)
        {
            ppCodeCxt = this;
            return VSConstants.S_OK;
        }

        int IDebugStackFrame2.GetName(out string pbstrName)
        {
            pbstrName = _sourcePath;
            return VSConstants.S_OK;
        }

        int IDebugStackFrame2.GetInfo(enum_FRAMEINFO_FLAGS dwFieldSpec, uint nRadix, FRAMEINFO[] pFrameInfo)
        {
            SetFrameInfo(dwFieldSpec, out pFrameInfo[0]);
            return VSConstants.S_OK;
        }

        int IDebugStackFrame2.GetPhysicalStackRange(out ulong paddrMin, out ulong paddrMax)
        {
            (paddrMin, paddrMax) = (0, 0);
            return VSConstants.E_NOTIMPL;
        }

        int IDebugStackFrame2.GetExpressionContext(out IDebugExpressionContext2 ppExprCxt)
        {
            ppExprCxt = null;
            return VSConstants.E_NOTIMPL;
        }

        int IDebugStackFrame2.GetDebugProperty(out IDebugProperty2 ppProperty)
        {
            ppProperty = null;
            return VSConstants.E_NOTIMPL;
        }

        int IDebugStackFrame2.GetThread(out IDebugThread2 ppThread)
        {
            ppThread = null;
            return VSConstants.E_NOTIMPL;
        }

        #endregion

        #region IDebugDocumentContext2 Members

        int IDebugDocumentContext2.Compare(enum_DOCCONTEXT_COMPARE Compare, IDebugDocumentContext2[] rgpDocContextSet, uint dwDocContextSetLen, out uint pdwDocContext)
        {
            pdwDocContext = 0;
            return VSConstants.E_NOTIMPL;
        }

        int IDebugDocumentContext2.EnumCodeContexts(out IEnumDebugCodeContexts2 ppEnumCodeCxts)
        {
            ppEnumCodeCxts = new AD7DebugCodeContextEnum(new IDebugCodeContext2[] { this });
            return VSConstants.S_OK;
        }

        int IDebugDocumentContext2.GetDocument(out IDebugDocument2 ppDocument)
        {
            ppDocument = null;
            return VSConstants.E_NOTIMPL;
        }

        int IDebugDocumentContext2.GetName(enum_GETNAME_TYPE gnType, out string pbstrFileName)
        {
            pbstrFileName = _sourcePath;
            return VSConstants.S_OK;
        }

        int IDebugDocumentContext2.GetSourceRange(TEXT_POSITION[] pBegPosition, TEXT_POSITION[] pEndPosition)
        {
            return VSConstants.E_NOTIMPL;
        }

        int IDebugDocumentContext2.GetStatementRange(TEXT_POSITION[] pBegPosition, TEXT_POSITION[] pEndPosition)
        {
            (pBegPosition[0], pEndPosition[0]) = (_sourcePosition, _sourcePosition);
            return VSConstants.S_OK;
        }

        // Moves the document context by a given number of statements or lines.
        // This is used primarily to support the Autos window in discovering the proximity statements around this document context. 
        int IDebugDocumentContext2.Seek(int nCount, out IDebugDocumentContext2 ppDocContext)
        {
            ppDocContext = null;
            return VSConstants.E_NOTIMPL;
        }

        #endregion

        #region IDebugCodeContext2 Members

        public int GetName(out string pbstrName)
        {
            pbstrName = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetInfo(enum_CONTEXT_INFO_FIELDS dwFields, CONTEXT_INFO[] pinfo)
        {
            pinfo[0].dwFields = 0;

            // Fields not supported by the engine
            if ((dwFields & enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS) != 0) { }
            if ((dwFields & enum_CONTEXT_INFO_FIELDS.CIF_ADDRESSOFFSET) != 0) { }
            if ((dwFields & enum_CONTEXT_INFO_FIELDS.CIF_ADDRESSABSOLUTE) != 0) { }
            if ((dwFields & enum_CONTEXT_INFO_FIELDS.CIF_MODULEURL) != 0) { }
            if ((dwFields & enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION) != 0) { }
            if ((dwFields & enum_CONTEXT_INFO_FIELDS.CIF_FUNCTIONOFFSET) != 0) { }

            return VSConstants.S_OK;
        }

        public int Add(ulong dwCount, out IDebugMemoryContext2 ppMemCxt)
        {
            ppMemCxt = null;
            return VSConstants.E_NOTIMPL;
        }

        public int Subtract(ulong dwCount, out IDebugMemoryContext2 ppMemCxt)
        {
            ppMemCxt = null;
            return VSConstants.E_NOTIMPL;
        }

        public int Compare(enum_CONTEXT_COMPARE Compare, IDebugMemoryContext2[] rgpMemoryContextSet, uint dwMemoryContextSetLen, out uint pdwMemoryContext)
        {
            pdwMemoryContext = 0;
            return VSConstants.E_NOTIMPL;
        }

        #endregion
    }
}