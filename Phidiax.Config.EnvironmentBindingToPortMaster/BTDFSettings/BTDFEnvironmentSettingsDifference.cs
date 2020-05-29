using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Phidiax.Config.EnvironmentBindingToPortMaster
{
    public class BTDFEnvironmentSettingsDifference
    {
        public string ReplacementValue { get; set; }
        public List<Setting> ReplacementSelectList { get; set; }
        public Setting SelectedReplacement { get; set; }
        public string PortName { get; set; }
    }
}
