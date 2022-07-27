using Microsoft.VisualStudio.ComponentModelHost;
using System;

namespace VSRAD.Syntax.Helpers
{
    internal static class ServiceProviderExtension
    {
        public static T GetMefService<T>(this IServiceProvider serviceProvider) where T : class
        {
            var componentModel = serviceProvider.GetService(typeof(SComponentModel)) as IComponentModel;
            return componentModel.GetService<T>();
        }
    }
}
