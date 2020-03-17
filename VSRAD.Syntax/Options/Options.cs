using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Parser;
using DisplayNameAttribute = System.ComponentModel.DisplayNameAttribute;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Syntax.Options
{
    public class OptionPage : DialogPage
    {
        const string asm1CollectionPath = "Asm1CollectionFileExtensions";
        const string asm2CollectionPath = "Asm2CollectionFileExtensions";
        private readonly IContentTypeRegistryService _contentTypeRegistryService;
        private readonly IFileExtensionRegistryService _fileExtensionRegistryService;
        private readonly IVsEditorAdaptersFactoryService _editorAdaptorFactory;
        private readonly IContentType _asm1ContentType;
        private readonly IContentType _asm2ContentType;
        private readonly CollectionConverter _converter;
        private IVsTextManager _textManager;


        public OptionPage()
        {
            _contentTypeRegistryService = Package.Instance.GetMEFComponent<IContentTypeRegistryService>();
            _fileExtensionRegistryService = Package.Instance.GetMEFComponent<IFileExtensionRegistryService>();
            _editorAdaptorFactory = Package.Instance.GetMEFComponent<IVsEditorAdaptersFactoryService>();

            _asm1ContentType = _contentTypeRegistryService.GetContentType(Constants.RadeonAsmSyntaxContentType);
            _asm2ContentType = _contentTypeRegistryService.GetContentType(Constants.RadeonAsm2SyntaxContentType);
            _converter = new CollectionConverter();
        }

        [Category("Function list")]
        [DisplayName("Function list default sort option")]
        [Description("Set default sort option for Function List")]
        [DefaultValue(SortState.ByName)]
        public SortState SortOptions { get; set; } = SortState.ByName;

        [Category("Syntax highlight")]
        [DisplayName("Indent guide lines")]
        [Description("Enable/disable indent guide lines")]
        [DefaultValue(true)]
        public bool IsEnabledIndentGuides { get; set; } = true;

        [Category("Syntax asm1 file extensions")]
        [DisplayName("Asm1 file extensions")]
        [Description("Enable/disable indent guide lines")]
        [Editor(@"System.Windows.Forms.Design.StringCollectionEditor, System.Design, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", typeof(System.Drawing.Design.UITypeEditor))]
        public List<string> Asm1FileExtensions { get; set; }

        [Category("Syntax asm2 file extensions")]
        [DisplayName("Asm2 file extensions")]
        [Description("Enable/disable indent guide lines")]
        [Editor(@"System.Windows.Forms.Design.StringCollectionEditor, System.Design, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", typeof(System.Drawing.Design.UITypeEditor))]
        public List<string> Asm2FileExtensions { get; set; }

        public enum SortState
        {
            [Description("by line number")]
            ByLine = 1,
            [Description("by line number descending")]
            ByLineDescending = 2,
            [Description("by name")]
            ByName = 3,
            [Description("by name descending")]
            ByNameDescending = 4,
        }

        protected override void OnApply(PageApplyEventArgs e)
        {
            base.OnApply(e);
            FunctionList.FunctionList.TryUpdateSortOptions(SortOptions);
            Task.Run(() => ChangeExtensionsAndUpdateCurrentFileAsync());
        }

        public override void SaveSettingsToStorage()
        {
            base.SaveSettingsToStorage();

            var settingsManager = new ShellSettingsManager(ServiceProvider.GlobalProvider);
            var userSettingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

            SaveCollectionSettings(userSettingsStore, asm1CollectionPath, Asm1FileExtensions, nameof(Asm1FileExtensions));
            SaveCollectionSettings(userSettingsStore, asm2CollectionPath, Asm2FileExtensions, nameof(Asm2FileExtensions));
        }

        public override void LoadSettingsFromStorage()
        {
            base.LoadSettingsFromStorage();

            var settingsManager = new ShellSettingsManager(ServiceProvider.GlobalProvider);
            var userSettingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

            var notExist = false;
            if (!userSettingsStore.PropertyExists(asm1CollectionPath, nameof(Asm1FileExtensions)))
            {
                Asm1FileExtensions = Constants.DefaultFileExtensionAsm1;
                notExist = true;
            }
            if (!userSettingsStore.PropertyExists(asm2CollectionPath, nameof(Asm2FileExtensions)))
            {
                Asm2FileExtensions = Constants.DefaultFileExtensionAsm2;
                notExist = true;
            }
            if (notExist) return;

            var converter = new CollectionConverter();
            Asm1FileExtensions = converter.ConvertFrom(
                userSettingsStore.GetString(asm1CollectionPath, nameof(Asm1FileExtensions))) as List<string>;
            Asm2FileExtensions = converter.ConvertFrom(
                userSettingsStore.GetString(asm2CollectionPath, nameof(Asm2FileExtensions))) as List<string>;
        }

        public void ChangeExtensions()
        {
            ChangeExtensions(_asm1ContentType, Asm1FileExtensions);
            ChangeExtensions(_asm2ContentType, Asm2FileExtensions);
        }

        public async Task ChangeExtensionsAndUpdateCurrentFileAsync()
        {
            try
            {
                ChangeExtensions();

                _textManager = _textManager ?? GetService(typeof(VsTextManagerClass)) as IVsTextManager;
                _textManager.GetActiveView(1, null, out var vsTextView);
                var wpfTextView = _editorAdaptorFactory.GetWpfTextView(vsTextView);

                if (wpfTextView.TextBuffer.Properties.TryGetProperty<ITextDocument>(typeof(ITextDocument), out var textDocument))
                {
                    var extension = System.IO.Path.GetExtension(textDocument.FilePath);
                    if (Asm1FileExtensions.Contains(extension))
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                        wpfTextView.TextBuffer.ChangeContentType(_asm1ContentType, null);
                        var parserManager = wpfTextView.TextBuffer.Properties.GetOrCreateSingletonProperty(() => new ParserManger());
                        parserManager.InitializeAsm1(wpfTextView.TextBuffer);
                        return;
                    }
                    if (Asm1FileExtensions.Contains(extension))
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                        wpfTextView.TextBuffer.ChangeContentType(_asm2ContentType, null);
                        var parserManager = wpfTextView.TextBuffer.Properties.GetOrCreateSingletonProperty(() => new ParserManger());
                        parserManager.InitializeAsm2(wpfTextView.TextBuffer);
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Error.LogError(e);
            }
        }

        private void SaveCollectionSettings(WritableSettingsStore userSettingsStore, string collectionPath, List<string> collection, string propertyName)
        {
            if (!userSettingsStore.CollectionExists(collectionPath))
                userSettingsStore.CreateCollection(collectionPath);

            userSettingsStore.SetString(
                collectionPath,
                propertyName,
                _converter.ConvertTo(collection, typeof(string)) as string);
        }

        private void ChangeExtensions(IContentType contentType, IEnumerable<string> extensions)
        {
            foreach (var ext in _fileExtensionRegistryService.GetExtensionsForContentType(contentType))
            {
                try
                {
                    _fileExtensionRegistryService.RemoveFileExtension(ext);
                }
                catch (Exception e)
                {
                    Error.LogError(e);
                }
            }
            foreach (var ext in extensions)
            {
                try
                {
                    _fileExtensionRegistryService.AddFileExtension(ext, contentType);
                }
                catch (Exception e)
                {
                    Error.LogError(e);
                }
            }
        }

        private class CollectionConverter : TypeConverter
        {
            private const string delimiter = "#!!#";

            public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            {
                return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
            }

            public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
            {
                return destinationType == typeof(IList<string>) || base.CanConvertTo(context, destinationType);
            }

            public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
            {
                return !(value is string v)
                    ? base.ConvertFrom(context, culture, value)
                    : v.Split(new[] { delimiter }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }

            public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
            {
                var v = value as IList<string>;
                if (destinationType != typeof(string) || v == null)
                {
                    return base.ConvertTo(context, culture, value, destinationType);
                }
                return string.Join(delimiter, v);
            }
        }
    }
}