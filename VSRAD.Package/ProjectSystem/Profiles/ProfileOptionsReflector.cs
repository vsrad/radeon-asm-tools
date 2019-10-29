using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using VSRAD.Package.Options;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem.Profiles
{
    public delegate object GetValueDelegate(object instance);
    public delegate object ConstructorDelegate(object[] values);

    public sealed class PropertyPage
    {
        public string DisplayName { get; }
        public GetValueDelegate GetValue { get; }
        public List<Property> Properties { get; }
        public ConstructorDelegate Constructor { get; }

        public PropertyPage(string displayName, GetValueDelegate getValue, List<Property> properties, ConstructorDelegate constructor)
        {
            DisplayName = displayName;
            GetValue = getValue;
            Properties = properties;
            Constructor = constructor;
        }

        public override string ToString() => DisplayName;
    }

    public sealed class Property
    {
        public string DisplayName { get; }
        public string Description { get; }
        public string Macro { get; }
        public string TrueString { get; }
        public string FalseString { get; }
        public GetValueDelegate GetValue { get; }

        public string FullDescription => Macro != null ? ("$(" + Macro + ")" + Environment.NewLine + Description) : Description;

        public Property(string displayName, string description, string macro, string trueString, string falseString, GetValueDelegate getValue)
        {
            DisplayName = displayName;
            Description = description;
            Macro = macro;
            TrueString = trueString;
            FalseString = falseString;
            GetValue = getValue;
        }
    }

    public static class ProfileOptionsReflector
    {
        public static Lazy<List<PropertyPage>> PropertyPages { get; } =
            new Lazy<List<PropertyPage>>(BuildPropertyPages);

        public static ProfileOptions ConstructProfileOptions(Dictionary<PropertyPage, Dictionary<Property, object>> values)
        {
            var constructor = typeof(ProfileOptions).GetFullConstructor();
            var pages = PropertyPages.Value
                .Select((page) => page.Constructor(page.Properties.Select((property) => values[page][property]).ToArray()));
            return (ProfileOptions)constructor.Invoke(pages.ToArray());
        }

        public static Dictionary<PropertyPage, Dictionary<Property, object>> GetPropertyValues(ProfileOptions profile)
        {
            var propertyValues = new Dictionary<PropertyPage, Dictionary<Property, object>>();
            foreach (var page in ProfileOptionsReflector.PropertyPages.Value)
            {
                var pageInstance = page.GetValue(profile);
                propertyValues[page] = page.Properties.ToDictionary((p) => p, (p) => p.GetValue(pageInstance));
            }
            return propertyValues;
        }

        public static List<PropertyPage> BuildPropertyPages()
        {
            var pageProperties = typeof(ProfileOptions).GetProperties();
            var constructor = typeof(ProfileOptions).GetFullConstructor();

            return pageProperties
                .OrderBy((p) => p.GetConstructorParameterPosition(constructor))
                .Select((p) => new PropertyPage(
                    displayName: p.Name,
                    getValue: p.GetValue,
                    properties: GetPageProperties(p.PropertyType),
                    constructor: p.PropertyType.GetFullConstructor().Invoke
                ))
                .ToList();
        }

        private static List<Property> GetPageProperties(Type pagePropertyType)
        {
            var properties = pagePropertyType.GetProperties();
            var constructor = pagePropertyType.GetFullConstructor();

            return properties
                .Where((p) => !Attribute.IsDefined(p, typeof(JsonIgnoreAttribute)))
                .OrderBy((p) => p.GetConstructorParameterPosition(constructor))
                .Select(ReflectProperty)
                .ToList();
        }

        private static Property ReflectProperty(PropertyInfo propertyInfo)
        {
            var boolDisplayAttr = propertyInfo.GetCustomAttribute<BooleanDisplayValuesAttribute>();

            return new Property(
                displayName: propertyInfo.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? propertyInfo.Name,
                description: propertyInfo.GetCustomAttribute<DescriptionAttribute>()?.Description,
                macro: propertyInfo.GetCustomAttribute<MacroAttribute>()?.MacroName,
                trueString: boolDisplayAttr?.True ?? "True",
                falseString: boolDisplayAttr?.False ?? "False",
                getValue: propertyInfo.GetValue
            );
        }
    }
}
