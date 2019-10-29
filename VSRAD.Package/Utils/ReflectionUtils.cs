using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace VSRAD.Package.Utils
{
    public static class ReflectionUtils
    {
        // https://stackoverflow.com/a/41618045
        public static List<T> GetConstantValues<T>(this Type type) =>
            type
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where((f) => f.IsLiteral && !f.IsInitOnly)
                .Select((f) => f.GetRawConstantValue())
                .OfType<T>()
                .ToList();

        public static ConstructorInfo GetFullConstructor(this Type type) =>
            type.GetConstructors().OrderByDescending((c) => c.GetParameters().Length).First();

        public static int GetConstructorParameterPosition(this PropertyInfo property, ConstructorInfo constructor) =>
            constructor.GetParameters().First((p) => p.Name.Equals(property.Name, StringComparison.OrdinalIgnoreCase)).Position;
    }
}
