using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using MpeCore;
using MpeCore.Classes;
using MediaPortal.Configuration;

namespace MPEIPlugin
{
    public class UpgradeAdvisor
    {
        public static string SiteUrl = @"http://edalex.dyndns.tv:8090/";
        public static List<MPRelease> AllReleases = new List<MPRelease>();
        public static List<MPRelease> KnownReleases = new List<MPRelease>();
        public static List<CompItem> ModulePool = new List<CompItem>();
        public static WebClient wc = new WebClient();

        public static List<MPRelease> GetUpdate()
        {
            List<MPRelease> newReleases = new List<MPRelease>();
            KnownReleases.Clear();
            AllReleases.Clear();
            string index = Config.GetFile(Config.Dir.Config, @"Installer\V2\MPVersionIndex.txt");
            if (File.Exists(index))
            {
                string knownVersions = File.ReadAllText(index);
                KnownReleases = GetReleaseList(knownVersions);
            }
            bool success = GUIMpeiPlugin._downloadManager.DownloadNow(GUIMpeiPlugin.MPUpdateUrl, index);
            if (success)
            {
                string allVersions = File.ReadAllText(index);
                AllReleases = GetReleaseList(allVersions);
                newReleases = AllReleases.Except(KnownReleases, new MPReleaseComparer()).ToList();

            }
            foreach (MPRelease newRelease in newReleases)
            {
                DownloadCompatibilityInfo(newRelease);
            }
            foreach (MPRelease newRelease in AllReleases)
            {
                ParseInfo(newRelease);
            }
            return newReleases;
        }

        private static void DownloadCompatibilityInfo(MPRelease release)
        {
            string localPath = Config.GetFile(Config.Dir.Config, Path.Combine(@"Installer\V2\", release.FileName));
            if (!File.Exists(localPath))
            {
                GUIMpeiPlugin._downloadManager.DownloadNow(string.Format("{0}/{1}", SiteUrl, release.FileName),localPath);
            }
        }

        public static List<MPRelease> GetReleaseList(string index)
        {
            List<MPRelease> list = new List<MPRelease>();
            string[] lines = index.Split('\r', '\n').Where(e => !string.IsNullOrEmpty(e)).ToArray();
            foreach (string line in lines)
            {
                MPRelease mp = new MPRelease();
                string[] items = line.Split(';');
                mp.Version = new Version(items[0]);
                mp.DisplayedVersion = items[1];
                mp.FileName = items[2];
                list.Add(mp);
            }
            return list;
        }

        public static string GetCompatiblePluginVersion(PackageClass pack, Version version)
        {
            List<PackageClass> packsById = MpeInstaller.KnownExtensions.GetList(pack.GeneralInfo.Id).Items;
            Dictionary<string, bool> compatibles = new Dictionary<string, bool>();
            foreach (PackageClass p in packsById)
            {
                bool isCompatible = false;
                if (p.PluginDependencies != null && p.PluginDependencies.Items.Count > 0)
                {
                    PluginDependencyItem i = p.PluginDependencies.Items[0];
                    string designedFor = i.CompatibleVersion.Items[0].DesignedForVersion;
                    if (i.SubSystemsUsed == null || i.SubSystemsUsed.Items.Count == 0)
                    {
                        isCompatible = ModulePool.Exists(x => x.MPVersion == version && x.Version.CompareTo(designedFor) <= 0);
                    }
                    else
                    {
                        List<string> usedSubs = i.SubSystemsUsed.Items.Select(s => s.Name).ToList();
                        isCompatible = ModulePool.Where(x => x.MPVersion == version && usedSubs.Contains(x.Name)).All(y => y.Version.CompareTo(designedFor) <= 0);

                    }
                }
                if (!compatibles.ContainsKey(p.GeneralInfo.Version.ToString()))
                compatibles.Add(p.GeneralInfo.Version.ToString(), isCompatible);
            }
            List<string> matched = compatibles.Where(v => v.Value).Select(x=>x.Key).OrderByDescending(x=>x).ToList();
            return (matched.Count > 0 ? matched.First() : "Not compatible");
        }

        public static string GetCompatibleMPRange(PackageClass package)
        {
            List<MPRelease> compMp = new List<MPRelease>();
            if (package.PluginDependencies != null && package.PluginDependencies.Items.Count > 0)
                {
                    PluginDependencyItem i = package.PluginDependencies.Items[0];
                    Version designedFor = new Version(i.CompatibleVersion.Items[0].DesignedForVersion);
                    Version minRequired = new Version(i.CompatibleVersion.Items[0].MinRequiredVersion);
                    if (i.SubSystemsUsed == null || i.SubSystemsUsed.Items.Count == 0)
                    {
                        List<Version> compList = ModulePool.Where(x => x.Name == "*" && x.Version.CompareTo(designedFor) <= 0 && x.Version.CompareTo(minRequired) >= 0).Select(r => r.MPVersion).Distinct().ToList();
                        if (compList.Count != 0)
                        {
                            compMp = AllReleases.Where(x => compList.Contains(x.Version)).ToList();
                        }
                        else
                        {
                            compMp = AllReleases.Where(y => y.Version.CompareTo(designedFor) <= 0 && y.Version.CompareTo(minRequired) >= 0).ToList();
                        }
                    }
                    else
                    {
                        //List incompatible MediaPortal versions first
                        List<string> usedSubs = i.SubSystemsUsed.Items.Select(s => s.Name).ToList();

                        List<CompItem> tempList = ModulePool.Where(x => usedSubs.Contains(x.Name)).Distinct().ToList();
                        List<Version> inCompMpMax = tempList.Where(x => x.Version.CompareTo(designedFor) > 0).Select(z => z.MPVersion).Distinct().ToList();
                        List<Version> inCompMpMin = tempList.Where(x => x.Version.CompareTo(minRequired) < 0).Select(z => z.MPVersion).Distinct().ToList();
                        compMp = AllReleases.Where(t => !inCompMpMax.Contains(t.Version) && !inCompMpMin.Contains(t.Version)).ToList();
                    }
            }
            if (compMp.Count > 0)
            {
                string min = compMp.OrderBy(m => m.Version).First().DisplayedVersion;
                string max = compMp.OrderBy(n => n.Version).Last().DisplayedVersion;
                if (min != max)
                {
                    return string.Format("For MP {0}-{1}", min, max);
                }
                return string.Format("For MP {0} only", min);
            }
            return "No compatibility info";
        }

        public static void ParseInfo(MPRelease mp)
        {
            string localPath = Config.GetFile(Config.Dir.Config, Path.Combine(@"Installer\V2\", mp.FileName));
            if (File.Exists(localPath))
            {
                string index = File.ReadAllText(localPath);
                string[] lines =
                    index.Split('\r', '\n').Where(e => !string.IsNullOrEmpty(e) && e.StartsWith("[")).ToArray();
                foreach (string line in lines)
                {
                    string[] props = line.Split('"');
                    CompItem ci = new CompItem();
                    ci.MPVersion = mp.Version;
                    ci.Name = props[1];
                    ci.Version = new Version(props[3]);
                    ModulePool.Add(ci);
                }
            }
        }
    }
    public class MPRelease
    {
        public Version Version { get; set; }
        public string DisplayedVersion { get; set; }
        public string FileName { get; set; }
        public List<SubModule> Info { get; set; }
    }

    public class SubModule
    {
        public string Name { get; set; }
        public Version Version { get; set; }
    }

    public class CompItem
    {
        public Version MPVersion { get; set; }
        public string Name { get; set; }
        public Version Version { get; set; }
    }

    class MPReleaseComparer : IEqualityComparer<MPRelease>
    {
        public bool Equals(MPRelease x, MPRelease y)
        {
            if (ReferenceEquals(x, y)) return true;

            if (ReferenceEquals(x, null) ||
                ReferenceEquals(y, null))
                return false;

            return x.Version == y.Version && x.FileName == y.FileName;
        }

        public int GetHashCode(MPRelease number)
        {
            if (ReferenceEquals(number, null)) return 0;

            int hashVersion = number.Version == null
                ? 0 : number.Version.GetHashCode();

            int hashFilename = number.FileName.GetHashCode();

            return hashVersion ^ hashFilename;
        }
    }
}
