using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
using Microsoft.VisualStudio.Threading;
using VSRAD.Syntax.Helpers;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Syntax.Options
{
    public abstract class BaseOptionModel<T> where T : BaseOptionModel<T>, new()
    {
        private static readonly AsyncLazy<T> _liveModel = new AsyncLazy<T>(CreateAsync, ThreadHelper.JoinableTaskFactory);
        protected static readonly AsyncLazy<ShellSettingsManager> _settingsManager = new AsyncLazy<ShellSettingsManager>(GetSettingsManagerAsync, ThreadHelper.JoinableTaskFactory);

        protected BaseOptionModel()
        { }

        public static T Instance
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return ThreadHelper.JoinableTaskFactory.Run(GetInstanceAsync);
            }
        }

        public static Task<T> GetInstanceAsync() => _liveModel.GetValueAsync();

        public static async Task<T> CreateAsync()
        {
            var instance = new T();
            await instance.LoadAsync();
            return instance;
        }

        protected virtual string CollectionName { get; } = typeof(T).FullName;

        public virtual void Load() => ThreadHelper.JoinableTaskFactory.Run(LoadAsync);

        public virtual async Task LoadAsync()
        {
            var manager = await _settingsManager.GetValueAsync();
            var settingsStore = manager.GetReadOnlySettingsStore(SettingsScope.UserSettings);

            if (!settingsStore.CollectionExists(CollectionName))
                return;

            foreach (PropertyInfo property in GetOptionProperties())
            {
                try
                {
                    var serializedProp = settingsStore.GetString(CollectionName, property.Name);
                    var value = DeserializeValue(serializedProp, property.PropertyType);
                    property.SetValue(this, value);
                }
                catch (Exception ex)
                {
                    Error.LogError(ex);
                }
            }
        }

        public virtual void Save() => ThreadHelper.JoinableTaskFactory.Run(SaveAsync);

        public virtual async Task SaveAsync()
        {
            ShellSettingsManager manager = await _settingsManager.GetValueAsync();
            WritableSettingsStore settingsStore = manager.GetWritableSettingsStore(SettingsScope.UserSettings);

            if (!settingsStore.CollectionExists(CollectionName))
                settingsStore.CreateCollection(CollectionName);

            foreach (PropertyInfo property in GetOptionProperties())
            {
                string output = SerializeValue(property.GetValue(this));
                settingsStore.SetString(CollectionName, property.Name, output);
            }

            T liveModel = await GetInstanceAsync();

            if (this != liveModel)
                await liveModel.LoadAsync();
        }

        protected virtual string SerializeValue(object value)
        {
            using (var stream = new MemoryStream())
            {
                var formatter = new BinaryFormatter 
                { 
                    Binder = TypeOnlyBinder.Instance
                };
                formatter.Serialize(stream, value);
                stream.Flush();
                return Convert.ToBase64String(stream.ToArray());
            }
        }

        protected virtual object DeserializeValue(string value, Type type)
        {
            byte[] b = Convert.FromBase64String(value);

            using (var stream = new MemoryStream(b))
            {
                var formatter = new BinaryFormatter 
                { 
                    Binder = TypeOnlyBinder.Instance
                };
                return formatter.Deserialize(stream);
            }
        }

        private static async Task<ShellSettingsManager> GetSettingsManagerAsync()
        {
            var svc = await AsyncServiceProvider.GlobalProvider.GetServiceAsync(typeof(SVsSettingsManager)) as IVsSettingsManager;

            Assumes.Present(svc);
            return new ShellSettingsManager(svc);
        }

        private IEnumerable<PropertyInfo> GetOptionProperties()
        {
            return GetType()
                .GetProperties()
                .Where(p => p.PropertyType.IsSerializable && p.PropertyType.IsPublic);
        }

        private class TypeOnlyBinder : SerializationBinder
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
}
