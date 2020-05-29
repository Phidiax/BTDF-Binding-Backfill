using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;


namespace Phidiax.Config.EnvironmentBindingToPortMaster
{
    class Program
    {
        static string sPortBinding, sEnvironment, sOutput, sEnvSettings;
        static BTDFEnvironmentSettings envSheet;

        static void BadArgs(string msg="")
        {
            Console.WriteLine($"\n\nPhidiax PortBindingMaster Variable Backfill{msg}");
            Console.WriteLine(new string('-', 79));
            Console.WriteLine("Takes an exported environment specific BizTalk binding file, and using the\nSettings File Generator as reference, replaces environment specific addresses,\nusers, and passwords with tokens needed in Port Binding Masters for BTDF\nInstallation use\n");
            Console.WriteLine("Note that for users and passwords to be replaced, the naming standard in\nthe Settings File Generator of USERID_name and ENCPW_name to match the user and\npassword combination must be used. The password need not be present in the\ninput file to be replaced (as it is not exported by BizTalk under normal\ncircumstances).\n");
            Console.WriteLine("Assembly and Schema sections are sorted to facilitate comparing files\nmore easily.\n\n");
            Console.WriteLine($"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name} [ExportedBindingPath] [OutputPortMasterBinding] [EnvironmentSettingsFile] [SourceEnvironment]");//3
            Console.WriteLine();
            return;
        }

        [STAThread()]
        static void Main(string[] args)
        {
            if (args.Count() != 4)
            {
                BadArgs();
                return;
            }


            sPortBinding = args[0];
            sOutput = args[1];
            sEnvSettings = args[2];
            sEnvironment = args[3];

            if (!(new System.IO.FileInfo(sPortBinding).Exists))
            {
                BadArgs($" - Port Binding File {sPortBinding} Not Found!");
                return;
            }

            if (!(new System.IO.FileInfo(sEnvSettings).Exists))
            {
                BadArgs($" - Environment Settings File Generator {sEnvSettings} Not Found!");
                return;
            }
            try
            {
                envSheet = new BTDFEnvironmentSettings(new System.IO.FileInfo(sEnvSettings));
            }
            catch
            {
                BadArgs($" - Environment Settings File Generator {sEnvSettings} Invalid!");
                return;
            }

            if (envSheet[sEnvironment] == null)
            {
                BadArgs($" - Environment {sEnvironment} not found in {sEnvSettings}!");
                return;
            }

            Console.WriteLine("Backfilling binding file {0}, replacing with variable names using {1} environment", sPortBinding, sEnvironment);
            Console.WriteLine("Output will be stored at {0}", sOutput);

            BackfillPortMasterBinding(sPortBinding, sEnvSettings, sOutput, sEnvironment);

        }

        static string BackfillPromptSelection(BTDFEnvironmentSettingsDifference diff, string input, bool stopAtFirstAccepted = false)
        {

            Console.WriteLine();
            Console.WriteLine("Variable(s) to use: {0} replace {1}", diff.PortName, diff.ReplacementValue);
            if (stopAtFirstAccepted)
                Console.WriteLine("NOTE : First value applied will end selection list.");

            Console.WriteLine(new string('-', 79));
            foreach (var rsl in diff.ReplacementSelectList)
            {
                Console.WriteLine("{0}) {1} - {2}", diff.ReplacementSelectList.IndexOf(rsl)+1, rsl.Name, rsl.Value);
                Console.WriteLine("Apply? (Y/N)");
                if (Console.ReadKey().Key == ConsoleKey.Y)
                {
                    input = input.Replace(rsl.Value, string.Format("{0}{1}{2}", "${", rsl.Name, "}"));
                    if (stopAtFirstAccepted)
                        break;
                }

                Console.WriteLine();
            }

            return input;
            
        }

        static Setting BackfillPromptSelectionUserPWPair(BTDFEnvironmentSettingsDifference diff)
        {
            do
            {
                Console.WriteLine();
                Console.WriteLine("Variable(s) to use: {0} replace {1}", diff.PortName, diff.ReplacementValue);
                Console.WriteLine("NOTE : First value applied will end selection list.");

                Console.WriteLine(new string('-', 79));

                foreach (var rsl in diff.ReplacementSelectList)
                {
                    Console.WriteLine("{0}) {1} - {2}", diff.ReplacementSelectList.IndexOf(rsl) + 1, rsl.Name, rsl.Value);
                    Console.WriteLine("Apply? (Y/N)");
                    if (Console.ReadKey().Key == ConsoleKey.Y)
                    {
                        return rsl;
                    }

                    Console.WriteLine();
                }

                Console.WriteLine("Must select one entry for user/password selection");
            }
            while (true);
        }

        static void BackfillPortMasterBinding(string portbinding, string environmentfile, string output, string environmentname)
        {
            //2) Load new binding file
            //3) Find selected environment values in file.
            //3a) if only one found, just replace with variable name
            //3b) if multiple found, present information to user for selection
            //3c) if name begins with USERID_, find matching ENCPW_ and poulate Password tag on same binding
            //4) Overwrite port master file

            
            XDocument xdNewBindings = System.Xml.Linq.XDocument.Load(portbinding);
            
            //Adding necessary preprocess directives
            xdNewBindings.AddFirst(new XComment("#ifdef _xml_preprocess"));
            xdNewBindings.Root.AddAfterSelf(new XComment("#endif"));
            
            XElement xeSends = xdNewBindings.Element("BindingInfo").Element("SendPortCollection");
            XElement xeReceives = xdNewBindings.Element("BindingInfo").Element("ReceivePortCollection");
            XElement xeTrackedSchemaAssemblies = xdNewBindings.Element("BindingInfo").Element("ModuleRefCollection");

            //Sort module ref items to make compare easier
            xeTrackedSchemaAssemblies.ReplaceNodes(xeTrackedSchemaAssemblies.Elements("ModuleRef").OrderBy(f => (string)f.Attribute("FullName")));

            foreach(XElement xeTrackedSchemaAssembly in xeTrackedSchemaAssemblies.Elements("ModuleRef"))
            {
                xeTrackedSchemaAssembly.Element("TrackedSchemas").ReplaceNodes(xeTrackedSchemaAssembly.Element("TrackedSchemas").Elements("Schema").OrderBy(f => (string)f.Attribute("AssemblyQualifiedName")).ThenBy(f=>(string)f.Attribute("RootName")));
            }

            System.Text.RegularExpressions.Regex regUserName = new System.Text.RegularExpressions.Regex("(?<begintag><UserName(.+)>)(?<userValue>.+)(?<endtag></UserName>)");
            System.Text.RegularExpressions.Regex regPW = new System.Text.RegularExpressions.Regex("<Password.*?/.*?>");

            foreach (XElement xeSend in xeSends.Elements("SendPort"))
            {
                XElement xeAddress = xeSend.Element("PrimaryTransport").Element("Address");
                XElement xeTransportData = XElement.Parse(xeSend.Element("PrimaryTransport").Element("TransportTypeData").Value);//.Replace("&gt;", ">").Replace("&lt;", "<")); ;

                XElement xeUserName, xePassword;

                try
                {
                    xeUserName = xeTransportData.Element("UserName");
                    if (xeUserName == null) throw new Exception();
                }
                catch
                {
                    xeUserName = new XElement("UserName");
                    xeUserName.Add(new XAttribute("vt", 1));
                    xeUserName.Value = string.Empty;
                    xeTransportData.Add(xeUserName);
                }

                try
                {
                    xePassword = xeTransportData.Element("Password");
                    if (xePassword == null) throw new Exception();
                }
                catch
                {
                    xePassword = new XElement("Password");
                    xePassword.Add(new XAttribute("vt", 1));
                    xePassword.Value = string.Empty;
                    xeTransportData.Add(xePassword);
                }

                //See if address contains any values from list. 
                //See if username is any values from list (also put PW in for matching user)
                //Put updated TransportData back into xml

                var lstFind = envSheet[environmentname];
                var lstFindAddress = lstFind.Where(s => xeAddress.Value.Contains(s.Value));
                var lstFindUser = lstFind.Where(s => xeUserName.Value == s.Value);
                var lstFindPassword = lstFind.Where(s => lstFindUser.Select(u=>u.Name.Replace("USERID_", "ENCPW_")).Contains(s.Name)).ToList();

                if (lstFindAddress.Count() == 1)
                {
                    xeAddress.Value = xeAddress.Value.Replace(lstFindAddress.First().Value, string.Format("{0}{1}{2}", "${", lstFindAddress.First().Name, "}"));
                }
                else
                {
                    if (lstFindAddress.Count() > 0)
                    {
                        xeAddress.Value = BackfillPromptSelection(new BTDFEnvironmentSettingsDifference() { ReplacementValue = xeAddress.Value, ReplacementSelectList = lstFindAddress.ToList(), PortName = xeSend.Attribute("Name").Value }, xeAddress.Value);
                    }
                }



                if (lstFindUser.Count() == 1)
                {
                    xeSend.Element("PrimaryTransport").Element("TransportTypeData").Value = regUserName.Replace(xeSend.Element("PrimaryTransport").Element("TransportTypeData").Value, string.Format("{0}{1}{2}", "${begintag}$${", lstFindUser.First().Name, "}${endtag}"));
                    xeUserName.Attribute("vt").Value = "8";
                }
                else
                {
                    if (lstFindUser.Count() > 0)
                    {
                        var selection = BackfillPromptSelectionUserPWPair(new BTDFEnvironmentSettingsDifference() { ReplacementValue = xeUserName.Value, ReplacementSelectList = lstFindUser.ToList(), PortName = xeSend.Attribute("Name").Value });
                        xeSend.Element("PrimaryTransport").Element("TransportTypeData").Value = regUserName.Replace(xeSend.Element("PrimaryTransport").Element("TransportTypeData").Value, string.Format("{0}{1}{2}", "${begintag}$${", selection.Name, "}${endtag}"));
                        lstFindPassword = lstFindPassword.Where(s => s.Name == selection.Name.Replace("USERID_", "ENCPW_")).ToList();
                    }
                }

                if (lstFindPassword.Count() == 1)
                {
                    xeSend.Element("PrimaryTransport").Element("TransportTypeData").Value = regPW.Replace(xeSend.Element("PrimaryTransport").Element("TransportTypeData").Value, string.Format("{0}{1}{2}", "<Password vt=\"8\">${", lstFindPassword.First().Name, "}</Password>"));
                    xePassword.Attribute("vt").Value = "8";
                }
                else
                {
                    if (lstFindPassword.Count() > 0)
                    {
                        Console.WriteLine("Should not find multiple matching password entries: exiting process");
                        return;
                    }
                }
            }

            foreach (XElement xeReceiveLocs in xeReceives.Elements("ReceivePort"))
            {
                foreach (XElement xeReceive in xeReceiveLocs.Element("ReceiveLocations").Elements("ReceiveLocation"))
                {
                    XElement xeAddress = xeReceive.Element("Address");
                    XElement xeTransportData = XElement.Parse(xeReceive.Element("ReceiveLocationTransportTypeData").Value);//.Replace("&gt;", ">").Replace("&lt;", "<"));

                    XElement xeUserName, xePassword;

                    try
                    {
                        xeUserName = xeTransportData.Element("UserName");
                        if (xeUserName == null) throw new Exception();
                    }
                    catch
                    {
                        xeUserName = new XElement("UserName");
                        xeUserName.Add(new XAttribute("vt", 1));
                        xeUserName.Value = string.Empty;
                        xeTransportData.Add(xeUserName);
                    }

                    try
                    {
                        xePassword = xeTransportData.Element("Password");
                        if (xePassword == null) throw new Exception();
                    }
                    catch
                    {
                        xePassword = new XElement("Password");
                        xePassword.Add(new XAttribute("vt", 1));
                        xePassword.Value = string.Empty;
                        xeTransportData.Add(xePassword);
                    }

                    //See if address contains any values from list. 
                    //See if username is any values from list (also put PW in for matching user)
                    //Put updated TransportData back into xml

                    var lstFind = envSheet[environmentname];
                    var lstFindAddress = lstFind.Where(s => xeAddress.Value.Contains(s.Value));
                    var lstFindUser = lstFind.Where(s => xeUserName.Value == s.Value);
                    var lstFindPassword = lstFind.Where(s => lstFindUser.Select(u => u.Name.Replace("USERID_", "ENCPW_")).Contains(s.Name)).ToList();

                    if (lstFindAddress.Count() == 1)
                    {
                        xeAddress.Value = xeAddress.Value.Replace(lstFindAddress.First().Value, string.Format("{0}{1}{2}", "${", lstFindAddress.First().Name, "}"));
                    }
                    else
                    {
                        if (lstFindAddress.Count() > 0)
                        {
                            xeAddress.Value = BackfillPromptSelection(new BTDFEnvironmentSettingsDifference() { ReplacementValue = xeAddress.Value, ReplacementSelectList = lstFindAddress.ToList(), PortName = xeReceive.Attribute("Name").Value }, xeAddress.Value);
                            
                        }
                    }


                    if (lstFindUser.Count() == 1)
                    {
                        xeReceive.Element("ReceiveLocationTransportTypeData").Value = regUserName.Replace(xeReceive.Element("ReceiveLocationTransportTypeData").Value, string.Format("{0}{1}{2}", "${begintag}$${", lstFindUser.First().Name, "}${endtag}"));
                        xeUserName.Attribute("vt").Value = "8";
                    }
                    else
                    {
                        if (lstFindUser.Count() > 0)
                        {
                            var selection = BackfillPromptSelectionUserPWPair(new BTDFEnvironmentSettingsDifference() { ReplacementValue = xeUserName.Value, ReplacementSelectList = lstFindUser.ToList(), PortName = xeReceive.Attribute("Name").Value });
                            xeReceive.Element("ReceiveLocationTransportTypeData").Value = regUserName.Replace(xeReceive.Element("ReceiveLocationTransportTypeData").Value, string.Format("{0}{1}{2}", "${begintag}$${", selection.Name, "}${endtag}"));
                            lstFindPassword = lstFindPassword.Where(s => s.Name == selection.Name.Replace("USERID_","ENCPW_")).ToList();
                        }
                    }

                    if (lstFindPassword.Count() == 1)
                    {
                        xeReceive.Element("ReceiveLocationTransportTypeData").Value = regPW.Replace(xeReceive.Element("ReceiveLocationTransportTypeData").Value, string.Format("{0}{1}{2}", "<Password vt=\"8\">${", lstFindPassword.First().Name, "}</Password>"));
                        xePassword.Attribute("vt").Value = "8";
                    }
                    else
                    {
                        if (lstFindPassword.Count() > 0)
                        {
                            Console.WriteLine("Should not find multiple matching password entries: exiting process");
                            return;
                        }
                    }

                    xeReceive.Element("ReceiveLocationTransportTypeData").Value = xeTransportData.ToString();
                }
            }

            xdNewBindings.Save(output);
        }


    }
}
