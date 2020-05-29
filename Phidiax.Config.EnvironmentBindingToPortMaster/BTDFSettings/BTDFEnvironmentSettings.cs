using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.XPath;

namespace Phidiax.Config.EnvironmentBindingToPortMaster
{
    public class BTDFEnvironmentSettings 
    {
        //Using open source classes from BTDF utility EnvSettingsManager
        //To read/write in same manner expected by BTDF
        
        SpreadsheetMLExporter exp = new SpreadsheetMLExporter();

        SettingsFile sf;

        public List<Setting> this[string sEnvironmentName]
        {
            get

            {
                if (sf.Environments.ContainsKey(sEnvironmentName))
                    return (from s in sf.Environments[sEnvironmentName].Settings select s.Value).ToList();
                else
                    return null;
            }
        }

        public BTDFEnvironmentSettings(System.IO.FileInfo fiEnvironmentSettings)
        {
            sf = (new SpreadsheetMLImporter()).ImportSettings(fiEnvironmentSettings.FullName);
        }

        
    }
}
