using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml.XPath;

namespace LocoSwap
{
    public class Route : ModelBase
    {
        private XDocument RouteProperties;
        private string _id;
        private string _name;
        private bool _isFavorite;
        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }
        public string Name
        {
            get => _name + (IsArchived ? " - ARCHIVÉE" : "");
            set => SetProperty(ref _name, value);
        }
        public bool IsFavorite
        {
            get => _isFavorite;
            set => SetProperty(ref _isFavorite, value);
        }
        public string RouteDirectory
        {
            get
            {
                return GetRouteDirectory(Id);
            }
        }
        public bool IsArchived { get; set; } = false;

        public Dictionary<string, ScenarioDb.ScenarioCompletion> LocalScenarioDb { get; } = new Dictionary<string, ScenarioDb.ScenarioCompletion>();

        public Route()
        {
            Id = "";
            Name = "Name not available";
        }

        public Route(string id)
        {
            Load(id);
        }

        public void Load(string id)
        {
            Id = id;
            //IsArchived = Id.EndsWith("_LocoSwapOff");

            string xmlPath = Path.Combine(RouteDirectory, "RouteProperties.xml");
            string xmlArchivedPath = Path.Combine(RouteDirectory, "RouteProperties.xml.LSoff");
            string xmlToLoad = "";

            if (File.Exists(xmlPath))
            {
                xmlToLoad = xmlPath;
            }
            else if(File.Exists(xmlArchivedPath))
            {
                xmlToLoad = xmlArchivedPath;
                IsArchived = true;
            }
            else
            {
                // Look in .ap files (or archived .ap.LSoff files)
                string apFileContainingRouteProperties = "";
                xmlToLoad = Path.Combine(Utilities.GetTempDir(), "RouteProperties.xml");
                Utilities.RemoveFile(xmlToLoad);

                string[] allowedExtensions = new[] { ".ap", ".ap.LSoff" };
                string[] apFiles = Directory.GetFiles(RouteDirectory, "*", SearchOption.TopDirectoryOnly).Where(file => allowedExtensions.Any(file.EndsWith)).ToArray();
                
                /*
                IEnumerator<string> e = (IEnumerator<string>) apFiles.GetEnumerator();
                string apPath = e.Current;
                while (apFileContainingRouteProperties == "" && e.MoveNext())
                {
                    try
                    {
                        var zipFile = ZipFile.Read(apPath);
                        var apEntry = zipFile.Where(entry => entry.FileName == "RouteProperties.xml").FirstOrDefault();
                        if (apEntry == null) continue;
                        apEntry.Extract(Utilities.GetTempDir());
                        apFileContainingRouteProperties = apPath;
                        zipFile.Dispose();
                        break;
                    }
                    catch (Exception)
                    {

                    }

                }
                */


                foreach (string apPath in apFiles)
                {
                    try
                    {
                        var zipFile = ZipFile.Read(apPath);
                        var apEntry = zipFile.Where(entry => entry.FileName == "RouteProperties.xml").FirstOrDefault();
                        if (apEntry == null) continue;
                        apEntry.Extract(Utilities.GetTempDir());
                        apFileContainingRouteProperties = apPath;
                        zipFile.Dispose();
                        IsArchived = apPath.EndsWith(".LSoff");
                        break;
                    }
                    catch (Exception)
                    {

                    }
                }
                if (apFileContainingRouteProperties == "") throw new Exception("RouteProperties.xml not found for this route ID");
            }
            RouteProperties = XmlDocumentLoader.Load(xmlToLoad);

            XElement displayName = RouteProperties.XPathSelectElement("/cRouteProperties/DisplayName/Localisation-cUserLocalisedString");
            Name = Utilities.DetermineDisplayName(displayName);

            IsFavorite = Properties.Settings.Default.FavoriteRoutes?.IndexOf(Id) >= 0;

            if (File.Exists(Path.Combine(RouteDirectory, "LocoSwap_ScenarioDb.xml")))
            {
                FileStream fs = File.Open(Path.Combine(RouteDirectory, "LocoSwap_ScenarioDb.xml"), FileMode.Open);
                XmlSerializer serializer = new XmlSerializer(typeof(List<SerializableScenarioDb>));
                List<SerializableScenarioDb> listOfScenarioCompletionsFromLocalDb = (List < SerializableScenarioDb>) serializer.Deserialize(fs);

                foreach(SerializableScenarioDb scenarioCompletionFromLocalDb in listOfScenarioCompletionsFromLocalDb)
                {
                    LocalScenarioDb[scenarioCompletionFromLocalDb.Key] = ScenarioDb.parseCompletion(scenarioCompletionFromLocalDb.Value);
                }

                fs.Close();
            }
        }

        public static string GetRoutesDirectory()
        {
            return Path.Combine(Properties.Settings.Default.TsPath, "Content", "Routes");
        }

        public static string GetRouteDirectory(string routeId)
        {
            return Path.Combine(Properties.Settings.Default.TsPath, "Content\\Routes", routeId);
        }

        public static Route[] ListAllRoutes()
        {
            /*List<Route> ret = new List<Route>();
            var routeDirectories = Directory.GetDirectories(GetRoutesDirectory());
            foreach (var directory in routeDirectories)
            {
                string id = new DirectoryInfo(directory).Name;

                if (File.Exists(Path.Combine(directory, "RouteProperties.xml"))
                    ||
                    File.Exists(Path.Combine(directory, "RouteProperties.xml.LSoff"))
                    )
                {
                    ret.Add(new Route(id));
                    continue;
                }
                var allowedExtensions = new[] { ".ap", ".ap.LSoff"};
                string[] apFiles = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly).Where(file => allowedExtensions.Any(file.ToLower().EndsWith)).ToArray();
                bool found = false;
                foreach (string apPath in apFiles)
                {
                    try
                    {
                        var zipFile = ZipFile.Read(apPath);
                        var xmlEntry = zipFile.Where(entry => entry.FileName == "RouteProperties.xml").FirstOrDefault();
                        zipFile.Dispose();
                        if (xmlEntry == null) continue;
                        found = true;
                        break;
                    }
                    catch (Exception)
                    {

                    }
                }
                if (found) ret.Add(new Route(id));
            }*/

            List<Route> ret = new List<Route>();
            string[] routeDirectories = Directory.GetDirectories(GetRoutesDirectory());
            foreach (string directory in routeDirectories)
            {
                string id = new DirectoryInfo(directory).Name;
                try
                {
                    ret.Add(new Route(id));
                }
                catch(Exception)
                {
                    // TODO Log(route in directory <id> is not a valid route)
                }
            }
            return ret.ToArray();
        }



        public void ToggleArchive()
        {


            if (IsArchived)
            {
                //Directory.Move(RouteDirectory, RouteDirectory.Substring(-8));
            }
            else
            {
                /*string archivedRoutePath = RouteDirectory + "_LocoSwapOff";
                string unArchivedRoutePath = RouteDirectory;*/


                Dictionary<string, ScenarioDb.ScenarioCompletion> filteredScenarioDb = ScenarioDb.getScenarioDbRouteInfos(Id).Where(i =>
                    i.Value == ScenarioDb.ScenarioCompletion.CompletedSuccessfully ||
                    i.Value == ScenarioDb.ScenarioCompletion.CompletedFailed)
                    .ToDictionary(i => i.Key, i => i.Value);

                // TODO gérer les impossibilités d'accès
                //Directory.Move(unArchivedRoutePath, archivedRoutePath);

                //Id += "_LocoSwapOff";





                List<SerializableScenarioDb> entries = new List<SerializableScenarioDb>(filteredScenarioDb.Count);
                foreach (string key in filteredScenarioDb.Keys)
                {
                    entries.Add(new SerializableScenarioDb(key, filteredScenarioDb[key]));
                }

                if (entries.Count != 0)
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(List<SerializableScenarioDb>));

                    if (!File.Exists(Path.Combine(RouteDirectory, "LocoSwap_ScenarioDb.xml")))
                    {
                        FileStream fs = File.Open(Path.Combine(RouteDirectory, "LocoSwap_ScenarioDb.xml"), FileMode.Create);
                        serializer.Serialize(fs, entries);
                        fs.Close();
                    }
                }

                if (File.Exists(Path.Combine(RouteDirectory, "RouteProperties.xml")))
                {
                    File.Move(Path.Combine(RouteDirectory, "RouteProperties.xml"), Path.Combine(RouteDirectory, "RouteProperties.xml.LSoff"));
                }

                string[] apFiles = Directory.GetFiles(RouteDirectory, "*.ap", SearchOption.TopDirectoryOnly);
                foreach (string apPath in apFiles)
                {
                    File.Move(apPath, apPath + ".LSoff");
                }
            }
        }
    }

    public class SerializableScenarioDb
    {
        public string Key;
        public string Value;
        public SerializableScenarioDb()
        {
        }

        public SerializableScenarioDb(string key, ScenarioDb.ScenarioCompletion value)
        {
            Key = key;
            Value = value.ToString();
        }
    }
}
