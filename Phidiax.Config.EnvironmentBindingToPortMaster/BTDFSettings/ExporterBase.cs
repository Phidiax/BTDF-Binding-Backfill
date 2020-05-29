using System;
using System.Collections.Generic;

namespace Phidiax.Config.EnvironmentBindingToPortMaster
{
    internal abstract class ExporterBase : PipelineElement
    {
        internal abstract void ExportSettings(List<SettingsFile> settingsFiles, string outputPath);
    }
}
