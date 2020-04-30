using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Syntax.Options
{
    public abstract class BaseOptionPage : DialogPage
    {
        private readonly CollectionConverter _converter;
        protected IReadOnlyDictionary<string, KeyValuePair<string, (List<string>, IReadOnlyList<string>)>> _collectionSettings;

        public BaseOptionPage(): base()
        {
            _converter = new CollectionConverter();
        }

        public abstract Task InitializeAsync();

        public override void SaveSettingsToStorage()
        {
            base.SaveSettingsToStorage();

            var settingsManager = new ShellSettingsManager(ServiceProvider.GlobalProvider);
            var userSettingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

            foreach (var settings in _collectionSettings)
            {
                SaveCollectionSettings(userSettingsStore, settings);
            }
        }

        public override void LoadSettingsFromStorage()
        {
            base.LoadSettingsFromStorage();

            var settingsManager = new ShellSettingsManager(ServiceProvider.GlobalProvider);
            var userSettingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

            foreach (var settings in _collectionSettings)
            {
                LoadCollectionSettings(userSettingsStore, settings);
            }
        }

        private void SaveCollectionSettings(WritableSettingsStore userSettingsStore, KeyValuePair<string, KeyValuePair<string, (List<string>, IReadOnlyList<string>)>> collectionSettings)
        {
            var collectionPath = collectionSettings.Key;
            var propertyName = collectionSettings.Value.Key;
            var (collection, _) = collectionSettings.Value.Value;

            if (!userSettingsStore.CollectionExists(collectionSettings.Key))
                userSettingsStore.CreateCollection(collectionSettings.Key);

            userSettingsStore.SetString(
                collectionPath,
                propertyName,
                _converter.ConvertTo(collection, typeof(string)) as string);
        }

        private void LoadCollectionSettings(WritableSettingsStore userSettingsStore, KeyValuePair<string, KeyValuePair<string, (List<string>, IReadOnlyList<string>)>> collectionSettings)
        {
            var collectionPath = collectionSettings.Key;
            var propertyName = collectionSettings.Value.Key;
            var (collection, defaultValues) = collectionSettings.Value.Value;

            List<string> newValues;
            if (userSettingsStore.PropertyExists(collectionPath, propertyName))
                newValues = _converter.ConvertFrom(userSettingsStore.GetString(collectionPath, propertyName)) as List<string>;
            else
                newValues = defaultValues.ToList();

            collection.Clear();
            collection.AddRange(newValues);
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