using System.Collections.Generic;
using System.Xml;
using System.IO;
using MpeCore;
using MediaPortal.Configuration;

namespace MPEIPlugin.Classes
{
  public class ExtensionSettings
  {
    public Dictionary<string, List<ExtensionSetting>> Settings { get; set; }

    public ExtensionSetting DisableSetting { get; set; }

    public ExtensionSettings()
    {
      Settings = new Dictionary<string, List<ExtensionSetting>>();
      DisableSetting = new ExtensionSetting();
    }

    public void Load(string file)
    {
      Settings.Clear();
      DisableSetting = new ExtensionSetting();

      XmlDocument doc = new XmlDocument();
      if(File.Exists(file))
      {
        doc.Load(file);
        XmlNode xml_file = doc.DocumentElement.SelectSingleNode("/extension_settings");
        DisableSetting.Load(doc.DocumentElement.SelectSingleNode("/extension_settings/disable_entry/setting"));
        XmlNodeList sections = xml_file.SelectNodes("settings");
        if (sections != null)
          foreach (XmlNode section in sections)
          {
            if (section.Attributes != null)
            {
              List<ExtensionSetting> set;
              string sectionname = ExtensionSetting.GetTranslatedString(section.Attributes["section"].Value);
              if (Settings.ContainsKey(sectionname))
                set = Settings[sectionname];
              else
              {
                set = new List<ExtensionSetting>();
                Settings.Add(sectionname, set);
              }
              XmlNodeList xml_settings = section.SelectNodes("setting");
              foreach (XmlNode node in xml_settings)
              {
                set.Add(new ExtensionSetting().Load(node));
              }
            }
          }
      }
    }
    public void LoadDefaultSettings(PackageClass package)
    {
        Settings.Clear();
        DisableSetting = new ExtensionSetting();
        string isPluginEnabled = string.Empty;
        string listedInHome = string.Empty;
        string listedInPlugins = string.Empty;
        using (var xmlreader = new MediaPortal.Profile.Settings(Config.GetFile(Config.Dir.Config, "MediaPortal.xml")))
        {
            isPluginEnabled = xmlreader.GetValueAsString("plugins", package.GeneralInfo.Name, "");
            listedInHome = xmlreader.GetValueAsString("home", package.GeneralInfo.Name, "");
            listedInPlugins = xmlreader.GetValueAsString("myplugins", package.GeneralInfo.Name, "");
        }
        DisableSetting.EntryName = "plugins";
        DisableSetting.Name = "Extensions";
        DisableSetting.DisplayName = Translation.SettingListedPluginsName;
        DisableSetting.Description = package.ReplaceInfo("Enable / Disable this setting to control if [Name] is loaded with MediaPortal.");
        DisableSetting.DefaultValue = isPluginEnabled;
        DisableSetting.Type = SettingType.SString;
        DisableSetting.ListValues = "yes|no";
        DisableSetting.DisplayValues = new List<string> { Translation.Yes, Translation.No };
        DisableSetting.ConfigFile = "Mediaportal.xml";
        DisableSetting.Value = isPluginEnabled;
        List<ExtensionSetting> DefaultConfigs = new List<ExtensionSetting>();

        if (!string.IsNullOrEmpty(isPluginEnabled))
        {
        ExtensionSetting IsPluginEnabled = new ExtensionSetting();
        IsPluginEnabled.EntryName = "plugins";
        IsPluginEnabled.Name = "Extensions";
        IsPluginEnabled.DisplayName = Translation.SettingPluginEnabledName;
        IsPluginEnabled.Description = package.ReplaceInfo("Enable / Disable this setting to control if [Name] is loaded with MediaPortal.");
        IsPluginEnabled.DefaultValue = isPluginEnabled;
        IsPluginEnabled.Type = SettingType.SString;
        IsPluginEnabled.ListValues = "yes|no";
        IsPluginEnabled.DisplayValues = new List<string> { Translation.Yes, Translation.No };
        IsPluginEnabled.ConfigFile = "Mediaportal.xml";
        DefaultConfigs.Add(IsPluginEnabled);
        }
        if (!string.IsNullOrEmpty(listedInHome))
        {
            ExtensionSetting ListedInHome = new ExtensionSetting();
            ListedInHome.EntryName = "myhome";
            ListedInHome.Name = "Extensions";
            ListedInHome.DisplayName = Translation.SettingListedHomeName;
            ListedInHome.Description = package.ReplaceInfo("Enable this setting for [Name] plugin to appear in the main Home screen menu items.");
            ListedInHome.DefaultValue = listedInHome;
            ListedInHome.Type = SettingType.SString;
            ListedInHome.ListValues = "yes|no";
            ListedInHome.DisplayValues = new List<string> { Translation.Yes, Translation.No };
            ListedInHome.ConfigFile = "Mediaportal.xml";
            DefaultConfigs.Add(ListedInHome);
        }
        if (!string.IsNullOrEmpty(listedInPlugins))
        {
            ExtensionSetting ListedInPlugins = new ExtensionSetting();
            ListedInPlugins.EntryName = "myhome";
            ListedInPlugins.Name = "Extensions";
            ListedInPlugins.DisplayName = Translation.SettingListedPluginsName;
            ListedInPlugins.Description = package.ReplaceInfo("Enable this setting for [Name] plugin to appear in the My Plugins screen menu items.");
            ListedInPlugins.DefaultValue = listedInPlugins;
            ListedInPlugins.Type = SettingType.SString;
            ListedInPlugins.ListValues = "yes|no";
            ListedInPlugins.DisplayValues = new List<string> { Translation.Yes, Translation.No };
            ListedInPlugins.ConfigFile = "Mediaportal.xml";
            DefaultConfigs.Add(ListedInPlugins);
        }
        Settings.Add("Plugin", DefaultConfigs);
    }
  }
}
