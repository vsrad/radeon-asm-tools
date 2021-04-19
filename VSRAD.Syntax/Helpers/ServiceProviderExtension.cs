using Microsoft.VisualStudio.ComponentModelHost;
using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace VSRAD.Syntax.Helpers
{
    internal static class ServiceProviderExtension
    {
        public static T GetMefService<T>(this IServiceProvider serviceProvider) where T : class
        {
            var componentModel = serviceProvider.GetService(typeof(SComponentModel)) as IComponentModel;
            return componentModel.GetService<T>();
        }

        public static async Task<T> GetMefServiceAsync<T>(this IAsyncServiceProvider serviceProvider) where T : class
        {
            var componentModel = await serviceProvider.GetServiceAsync(typeof(SComponentModel)) as IComponentModel;
            return componentModel.GetService<T>();
        }
    }
}
