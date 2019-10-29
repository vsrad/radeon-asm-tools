using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Composition;

namespace VSRAD.Package.Utils
{
    public static class ProjectSystemExtensions
    {
        public static T GetExport<T>(this ConfiguredProject configuredProject) =>
            configuredProject.GetService<ExportProvider>("ExportProvider").GetExportedValue<T>();

        public static T GetService<T>(this ConfiguredProject configuredProject, string serviceName)
        {
            var configuredServices = GetServices(configuredProject);
            return (T)configuredServices.GetType().GetProperty(serviceName).GetValue(configuredServices);
        }

        // https://github.com/Microsoft/VSProjectSystem/blob/master/doc/automation/subscribe_to_project_data.md
        // Due to a breaking change in VS2019 API (https://github.com/microsoft/VSProjectSystem/blob/master/doc/overview/breaking_changes_visual_studio_2019.md#changed-the-type-of-the-services-object),
        // the only way to obtain the Services value while retainining compatibility with VS2017 is to use reflection :/
        private static object GetServices(object project) => project.GetType().GetProperty("Services").GetValue(project);
    }
}
