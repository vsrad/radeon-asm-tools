using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
using Microsoft.VisualStudio.Threading;
using VSRAD.Syntax.Helpers;
using AsyncServiceProvider = Microsoft.VisualStudio.Shell.AsyncServiceProvider;
using ThreadHelper = Microsoft.VisualStudio.Shell.ThreadHelper;

namespace VSRAD.Syntax.Options
{
    internal class GeneralOptionModel
    {
        private static readonly AsyncLazy<GeneralOptionModel> LiveModel = new AsyncLazy<GeneralOptionModel>(CreateAsync, ThreadHelper.JoinableTaskFactory);
        private static readonly AsyncLazy<ShellSettingsManager> SettingsManager = new AsyncLazy<ShellSettingsManager>(GetSettingsManagerAsync, ThreadHelper.JoinableTaskFactory);
        private static readonly Regex FileExtensionRegular = new Regex(@"^\.\w+$", RegexOptions.Compiled);
        private readonly GeneralOptionProvider _generalOptionProvider;

        protected GeneralOptionModel(GeneralOptionProvider generalOptionProvider)
        {
            _generalOptionProvider = generalOptionProvider;
#if DEBUG
            ThreadHelper.JoinableTaskFactory.Run(DeleteCollectionAsync);
#endif
        }

        /// <summary>
        /// A singleton instance of the options. MUST be called from UI thread only.
        /// </summary>
        /// <remarks>
        /// Call <see cref="GetInstanceAsync" /> instead if on a background thread or in an async context on the main thread.
        /// </remarks>
        public static GeneralOptionModel Instance
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return ThreadHelper.JoinableTaskFactory.Run(GetInstanceAsync);
            }
        }

        /// <summary>
        /// Get the singleton instance of the options. Thread safe.
        /// </summary>
        public static Task<GeneralOptionModel> GetInstanceAsync() => LiveModel.GetValueAsync();

        /// <summary>
        /// Creates a new instance of the options class and loads the values from the store. For internal use only
        /// </summary>
        /// <returns></returns>
        private static async Task<GeneralOptionModel> CreateAsync()
        {
            var optionProvider = GeneralOptionProvider.Instance;
            var instance = new GeneralOptionModel(optionProvider);

            await instance.LoadAsync();

            // required initialization before options
            var serviceProvider = AsyncServiceProvider.GlobalProvider;
            _ = await serviceProvider.GetMefServiceAsync<ContentTypeManager>();

            optionProvider.OptionsUpdatedInvoke();
            return instance;
        }

        /// <summary>
        /// The name of the options collection as stored in the registry.
        /// </summary>
        protected virtual string CollectionName { get; } = typeof(GeneralOptionModel).FullName;

        private async Task DeleteCollectionAsync()
        {
            var manager = await SettingsManager.GetValueAsync();
            var settingsStore = manager.GetWritableSettingsStore(SettingsScope.UserSettings);

            if (settingsStore.CollectionExists(CollectionName))
            {
                settingsStore.DeleteCollection(CollectionName);
            }
        }

        /// <summary>
        /// Hydrates the properties from the registry.
        /// </summary>
        public virtual void Load()
        {
            ThreadHelper.JoinableTaskFactory.Run(LoadAsync);
        }

        /// <summary>
        /// Hydrates the properties from the registry asynchronously.
        /// </summary>
        public virtual async Task LoadAsync()
        {
            var manager = await SettingsManager.GetValueAsync();
            var settingsStore = manager.GetReadOnlySettingsStore(SettingsScope.UserSettings);

            if (!settingsStore.CollectionExists(CollectionName))
            {
                return;
            }

            try
            {
                _generalOptionProvider.SortOptions = ReadSetting<GeneralOptionPage.SortState>(settingsStore, nameof(_generalOptionProvider.SortOptions));
                _generalOptionProvider.AutoScroll = ReadSetting<bool>(settingsStore, nameof(_generalOptionProvider.AutoScroll));
                _generalOptionProvider.IsEnabledIndentGuides = ReadSetting<bool>(settingsStore, nameof(_generalOptionProvider.IsEnabledIndentGuides));
                _generalOptionProvider.IndentGuideThickness = ReadSetting<double>(settingsStore, nameof(_generalOptionProvider.IndentGuideThickness));
                _generalOptionProvider.IndentGuideDashSize = ReadSetting<double>(settingsStore, nameof(_generalOptionProvider.IndentGuideDashSize));
                _generalOptionProvider.IndentGuideSpaceSize = ReadSetting<double>(settingsStore, nameof(_generalOptionProvider.IndentGuideSpaceSize));
                _generalOptionProvider.IndentGuideOffsetY = ReadSetting<double>(settingsStore, nameof(_generalOptionProvider.IndentGuideOffsetY));
                _generalOptionProvider.IndentGuideOffsetX = ReadSetting<double>(settingsStore, nameof(_generalOptionProvider.IndentGuideOffsetX));
                _generalOptionProvider.Asm1FileExtensions = ReadSetting<IReadOnlyList<string>>(settingsStore, nameof(_generalOptionProvider.Asm1FileExtensions));
                _generalOptionProvider.Asm2FileExtensions = ReadSetting<IReadOnlyList<string>>(settingsStore, nameof(_generalOptionProvider.Asm2FileExtensions));
                _generalOptionProvider.InstructionsPaths = ReadSetting<IReadOnlyList<string>>(settingsStore, nameof(_generalOptionProvider.InstructionsPaths));
                _generalOptionProvider.AutocompleteInstructions = ReadSetting<bool>(settingsStore, nameof(_generalOptionProvider.AutocompleteInstructions));
                _generalOptionProvider.AutocompleteFunctions = ReadSetting<bool>(settingsStore, nameof(_generalOptionProvider.AutocompleteFunctions));
                _generalOptionProvider.AutocompleteLabels = ReadSetting<bool>(settingsStore, nameof(_generalOptionProvider.AutocompleteLabels));
                _generalOptionProvider.AutocompleteVariables = ReadSetting<bool>(settingsStore, nameof(_generalOptionProvider.AutocompleteVariables));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex);
            }
        }

        /// <summary>
        /// Saves the properties to the registry.
        /// </summary>
        public virtual void Save()
        {
            ThreadHelper.JoinableTaskFactory.Run(SaveAsync);
        }

        /// <summary>
        /// Saves the properties to the registry asynchronously.
        /// </summary>
        public virtual async Task SaveAsync()
        {
            if (!Validate()) return;

            var manager = await SettingsManager.GetValueAsync();
            var settingsStore = manager.GetWritableSettingsStore(SettingsScope.UserSettings);

            if (!settingsStore.CollectionExists(CollectionName))
            {
                settingsStore.CreateCollection(CollectionName);
            }

            WriteSetting(settingsStore, _generalOptionProvider.SortOptions, nameof(_generalOptionProvider.SortOptions));
            WriteSetting(settingsStore, _generalOptionProvider.AutoScroll, nameof(_generalOptionProvider.AutoScroll));
            WriteSetting(settingsStore, _generalOptionProvider.IsEnabledIndentGuides, nameof(_generalOptionProvider.IsEnabledIndentGuides));
            WriteSetting(settingsStore, _generalOptionProvider.IndentGuideThickness, nameof(_generalOptionProvider.IndentGuideThickness));
            WriteSetting(settingsStore, _generalOptionProvider.IndentGuideDashSize, nameof(_generalOptionProvider.IndentGuideDashSize));
            WriteSetting(settingsStore, _generalOptionProvider.IndentGuideSpaceSize, nameof(_generalOptionProvider.IndentGuideSpaceSize));
            WriteSetting(settingsStore, _generalOptionProvider.IndentGuideOffsetY, nameof(_generalOptionProvider.IndentGuideOffsetY));
            WriteSetting(settingsStore, _generalOptionProvider.IndentGuideOffsetX, nameof(_generalOptionProvider.IndentGuideOffsetX));
            WriteSetting(settingsStore, _generalOptionProvider.Asm1FileExtensions, nameof(_generalOptionProvider.Asm1FileExtensions));
            WriteSetting(settingsStore, _generalOptionProvider.Asm2FileExtensions, nameof(_generalOptionProvider.Asm2FileExtensions));
            WriteSetting(settingsStore, _generalOptionProvider.AutocompleteInstructions, nameof(_generalOptionProvider.AutocompleteInstructions));
            WriteSetting(settingsStore, _generalOptionProvider.AutocompleteFunctions, nameof(_generalOptionProvider.AutocompleteFunctions));
            WriteSetting(settingsStore, _generalOptionProvider.AutocompleteLabels, nameof(_generalOptionProvider.AutocompleteLabels));
            WriteSetting(settingsStore, _generalOptionProvider.AutocompleteVariables, nameof(_generalOptionProvider.AutocompleteVariables));

            if (!_generalOptionProvider.InstructionsPaths.Equals(GeneralOptionProvider.GetDefaultInstructionDirectoryPath()))
            {

                WriteSetting(settingsStore, _generalOptionProvider.InstructionsPaths,
                    nameof(_generalOptionProvider.InstructionsPaths));
            }

            _generalOptionProvider.OptionsUpdatedInvoke();
        }

        private T ReadSetting<T>(SettingsStore store, string name)
        {
            var serializedProp = store.GetString(CollectionName, name);
            return (T)DeserializeValue(serializedProp, typeof(T));
        }

        private void WriteSetting<T>(WritableSettingsStore store, T value, string name)
        {
            var output = SerializeValue(value);
            store.SetString(CollectionName, name, output);
        }

        /// <summary>
        /// Serializes an object value to a string using the binary serializer.
        /// </summary>
        protected virtual string SerializeValue(object value)
        {
            using (var stream = new MemoryStream())
            {
                var formatter = new BinaryFormatter { Binder = TypeOnlyBinder.Instance };
                formatter.Serialize(stream, value);
                stream.Flush();
                return Convert.ToBase64String(stream.ToArray());
            }
        }

        /// <summary>
        /// Deserializes a string to an object using the binary serializer.
        /// </summary>
        protected virtual object DeserializeValue(string value, Type type)
        {
            var b = Convert.FromBase64String(value);

            using (var stream = new MemoryStream(b))
            {
                var formatter = new BinaryFormatter { Binder = TypeOnlyBinder.Instance };
                return formatter.Deserialize(stream);
            }
        }

        public bool Validate()
        {
            var sb = new StringBuilder();
            foreach (var ext in _generalOptionProvider.Asm1FileExtensions)
            {
                if (!FileExtensionRegular.IsMatch(ext))
                    sb.AppendLine($"Invalid file extension format \"{ext}\"");
            }

            foreach (var ext in _generalOptionProvider.Asm2FileExtensions)
            {
                if (!FileExtensionRegular.IsMatch(ext))
                    sb.AppendLine($"Invalid file extension format \"{ext}\"");
            }

            var asm1Set = _generalOptionProvider.Asm1FileExtensions.ToHashSet();
            var asm2Set = _generalOptionProvider.Asm2FileExtensions.ToHashSet();
            asm1Set.IntersectWith(asm2Set);
            foreach (var ext in asm1Set)
                sb.AppendLine($"\"{ext}\" must be only in one syntax (asm1 or asm2)");

            foreach (var path in _generalOptionProvider.InstructionsPaths)
                if (!Directory.Exists(path))
                    sb.AppendLine($"\"{path}\" is not exists");

            if (sb.Length == 0) return true;

            Error.ShowErrorMessage(sb.ToString());
            return false;
        }

        private static async Task<ShellSettingsManager> GetSettingsManagerAsync()
        {
#pragma warning disable VSTHRD010 
            var svc = await AsyncServiceProvider.GlobalProvider.GetServiceAsync(typeof(SVsSettingsManager)) as IVsSettingsManager;
#pragma warning restore VSTHRD010 

            Assumes.Present(svc);
            return new ShellSettingsManager(svc);
        }
    }

    internal class TypeOnlyBinder : SerializationBinder
    {
        public static SerializationBinder Instance = new TypeOnlyBinder();

        public override Type BindToType(string assemblyName, string typeName)
        {
            return assemblyName.Equals("NA", StringComparison.Ordinal) ? Type.GetType(typeName) : null;
        }

        public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            assemblyName = "NA";
            typeName = serializedType.FullName;
        }
    }
}
