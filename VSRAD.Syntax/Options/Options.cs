using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using VSRAD.Syntax.Helpers;
using DisplayNameAttribute = System.ComponentModel.DisplayNameAttribute;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Syntax.Options
{
    public class OptionPage : DialogPage
    {
        const string asm1CollectionPath = "Asm1CollectionFileExtensions";
        const string asm2CollectionPath = "Asm2CollectionFileExtensions";
        private static readonly Regex fileExtensionRegular = new Regex(@"^\.\w+$");
        private readonly ContentTypeManager _contentTypeManager;
        private readonly CollectionConverter _converter;

        public OptionPage()
        {
            _contentTypeManager = Package.Instance.GetMEFComponent<ContentTypeManager>();
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
        [Description("List of file extensions for the asm1 syntax")]
        [Editor(@"System.Windows.Forms.Design.StringCollectionEditor, System.Design, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", typeof(System.Drawing.Design.UITypeEditor))]
        public List<string> Asm1FileExtensions { get; set; }

        [Category("Syntax asm2 file extensions")]
        [DisplayName("Asm2 file extensions")]
        [Description("List of file extensions for the asm2 syntax")]
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

        public Task InitializeAsync() =>
            UpdateRadeonContentTypeAsync();

        protected override void OnApply(PageApplyEventArgs e)
        {
            try
            {
                base.OnApply(e);

                ValidateExtensions(Asm1FileExtensions);
                ValidateExtensions(Asm2FileExtensions);
                FunctionList.FunctionList.TryUpdateSortOptions(SortOptions);
                ThreadHelper.JoinableTaskFactory.RunAsync(() => UpdateRadeonContentTypeAsync());
            }
            catch(Exception ex)
            {
                Error.ShowWarning(ex);
            }
        }
        
        private void ValidateExtensions(List<string> extensions)
        {
            var sb = new StringBuilder();
            foreach (var ext in extensions)
            {
                if (!fileExtensionRegular.IsMatch(ext))
                    sb.AppendLine($"Invalid file extension format \"{ext}\"");
            }
            if (sb.Length != 0)
            {
                sb.AppendLine();
                sb.AppendLine("Format example: .asm");
                throw new ArgumentException(sb.ToString());
            }
        }

        private Task UpdateRadeonContentTypeAsync() =>
            _contentTypeManager.ChangeRadeonExtensionsAsync(Asm1FileExtensions, Asm2FileExtensions);

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

        private void SaveCollectionSettings(WritableSettingsStore userSettingsStore, string collectionPath, List<string> collection, string propertyName)
        {
            if (!userSettingsStore.CollectionExists(collectionPath))
                userSettingsStore.CreateCollection(collectionPath);

            userSettingsStore.SetString(
                collectionPath,
                propertyName,
                _converter.ConvertTo(collection, typeof(string)) as string);
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