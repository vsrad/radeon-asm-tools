using Microsoft.VisualStudio.ProjectSystem;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem.EditorExtensions
{
    [Export]
    [AppliesTo(Constants.ProjectCapability)]
    public sealed class QuickInfoEvaluateSelectedState
    {
        private readonly Dictionary<string, uint[]> _evaluated = new Dictionary<string, uint[]>();
        private IProject _project;

        public bool HasAnyEvaluatedWatches => _evaluated.Count > 0;

        /* QuickInfoEvaluateSelectedState is instantiated prior to UnconfiguredProject (=> all our components)
         * becoming available, so we use this hack to obtain the project once it's created. */
        public void SetProjectOnLoad(IProject project) => _project = project;

        public void SetEvaluatedData(string watchName, uint[] data)
        {
            if (data != null)
                _evaluated[watchName] = data;
        }

        public bool TryGetEvaluated(string watchName, out string[] formattedValue)
        {
            if (_evaluated.TryGetValue(watchName, out var values))
            {
                throw new NotImplementedException();
                //var valuesIncluded = _project.Options.VisualizerColumnStyling.Computed.Visibility;

                //var formattedValues = new List<string>();
                //for (int i = 0; i < values.Length; i++)
                //    if (valuesIncluded[i])
                //        formattedValues.Add(DataFormatter.FormatDword(DebugVisualizer.VariableType.Hex, values[i]));

                //formattedValue = formattedValues.ToArray();
                //return true;
            }

            formattedValue = System.Array.Empty<string>();
            return false;
        }
    }
}
