using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using MediaPortal.Configuration;
using MediaPortal.GUI.Library;

namespace MPEIPlugin
{
  public static class Translation
  {
    #region Private variables

    private static Dictionary<string, string> _translations;
    private static string _path = string.Empty;
    private static readonly DateTimeFormatInfo _info;

    #endregion

    #region Constructor

    static Translation()
    {
      _info = DateTimeFormatInfo.GetInstance(CultureInfo.CurrentUICulture);
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the translated strings collection in the active language
    /// </summary>
    public static Dictionary<string, string> Strings
    {
      get
      {
        if (_translations == null)
        {
          _translations = new Dictionary<string, string>();
          Type transType = typeof(Translation);
          FieldInfo[] fields = transType.GetFields(BindingFlags.Public | BindingFlags.Static);
          foreach (FieldInfo field in fields)
          {
            _translations.Add(field.Name, field.GetValue(transType).ToString());
          }
        }
        return _translations;
      }
    }

    public static string CurrentLanguage
    {
      get
      {
        string language = string.Empty;
        try
        {
          language = GUILocalizeStrings.GetCultureName(GUILocalizeStrings.CurrentLanguage());
          
        }
        catch (Exception)
        {
          language = CultureInfo.CurrentUICulture.Name;
        }
        return language;
      }
    }
    public static string PreviousLanguage { get; set; }

    #endregion

    #region Public Methods

    public static void Init()
    {
      _translations = null;
      Log.Info("[MPEI] Using language " + CurrentLanguage);

      _path = Config.GetSubFolder(Config.Dir.Language, "MPEI");

      if (!Directory.Exists(_path))
        Directory.CreateDirectory(_path);

      string lang = PreviousLanguage = CurrentLanguage;
      LoadTranslations(lang);

      // publish all available translation strings
      foreach (string name in Strings.Keys)
      {
        GUIUtils.SetProperty("#MPEI.Translation." + name + ".Label", Translation.Strings[name]);
      }
    }

    public static int LoadTranslations(string lang)
    {
      XmlDocument doc = new XmlDocument();
      Dictionary<string, string> TranslatedStrings = new Dictionary<string, string>();
      string langPath = "";
      try
      {
        langPath = Path.Combine(_path, lang + ".xml");
        doc.Load(langPath);
      }
      catch (Exception e)
      {
        if (lang == "en")
          return 0; // otherwise we are in an endless loop!

        if (e.GetType() == typeof(FileNotFoundException))
          Log.Warn("[MPEI] Cannot find translation file {0}. Failing back to English", langPath);
        else
        {
          Log.Error("[MPEI] Error in translation xml file: {0}. Failing back to English", lang);
          Log.Error(e);
        }

        return LoadTranslations("en");
      }

      foreach (XmlNode stringEntry in doc.DocumentElement.ChildNodes)
      {
        if (stringEntry.NodeType == XmlNodeType.Element)
          try
          {
            TranslatedStrings.Add(stringEntry.Attributes.GetNamedItem("name").Value, stringEntry.InnerText.NormalizeTranslation());
          }
          catch (Exception ex)
          {
            Log.Error("[MPEI] Error in Translation Engine");
            Log.Error(ex);
          }
      }

      Type TransType = typeof(Translation);
      FieldInfo[] fieldInfos = TransType.GetFields(BindingFlags.Public | BindingFlags.Static);
      foreach (FieldInfo fi in fieldInfos)
      {
        if (TranslatedStrings != null && TranslatedStrings.ContainsKey(fi.Name))
          TransType.InvokeMember(fi.Name, BindingFlags.SetField, null, TransType, new object[] { TranslatedStrings[fi.Name] });
        else
          Log.Info("[MPEI] Translation not found for: {0}. Using hard-coded English default.", fi.Name);
      }
      return TranslatedStrings.Count;
    }

    public static string GetByName(string name)
    {
      if (!Strings.ContainsKey(name))
        return name;

      return Strings[name];
    }

    public static string GetByName(string name, params object[] args)
    {
      return String.Format(GetByName(name), args);
    }

    /// <summary>
    /// Takes an input string and replaces all ${named} variables with the proper translation if available
    /// </summary>
    /// <param name="input">a string containing ${named} variables that represent the translation keys</param>
    /// <returns>translated input string</returns>
    public static string ParseString(string input)
    {
      Regex replacements = new Regex(@"\$\{([^\}]+)\}");
      MatchCollection matches = replacements.Matches(input);
      foreach (Match match in matches)
      {
        input = input.Replace(match.Value, GetByName(match.Groups[1].Value));
      }
      return input;
    }

    public static string GetDayName(DayOfWeek dayOfWeek)
    {
      return _info.GetDayName(dayOfWeek);
    }
    public static string GetShortestDayName(DayOfWeek dayOfWeek)
    {
      return _info.GetShortestDayName(dayOfWeek);
    }

    /// <summary>
    /// Temp workaround to remove unwatched chars from Transifex
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static string NormalizeTranslation(this string input)
    {
        input = input.Replace("\\'", "'");
        input = input.Replace("\\\"", "\"");
        return input;
    }
    #endregion

    #region Translations / Strings

    /// <summary>
    /// These will be loaded with the language files content
    /// if the selected lang file is not found, it will first try to load en(us).xml as a backup
    /// if that also fails it will use the hardcoded strings as a last resort.
    /// </summary>


    // A
    public static string All = "All";
    public static string AlwaysCheckForUpdates = "Always check for updates";
    public static string Actions = "Actions";
    public static string ActionAdded = "Action was added to queue";
    public static string ActionRemoved = "Action was removed from queue";
    public static string Author = "Author";
    public static string AskForRestart = "This Operation will restart MediaPortal.\nDo you wish to continue?";
    public static string AllExtensions = "All Extensions";

    // C
    public static string ChangeLog = "Change Log";
    public static string Compatibility = "Compatibility";
    public static string CategoryRecentlyAdded = "Recently Added Extensions";
    public static string CategoryRecentlyUpdated = "Recently Updated Extensions";
    public static string CategoryMostInstalled = "Most Installed Extensions";
    public static string CategoryFeatured = "Featured Extensions";
    public static string CategoryMostPopular = "Most Popular Extensions";
    public static string CategoryMostRated = "Most Rated Extensions";
    public static string CategoryTopRated = "Top Rated Extensions";
    public static string CategoryMostReviewed = "Most Reviewed Extensions";
    public static string CategoryHTPC = "HTPC";
    public static string CategoryCustomization = "Customization";
    public static string CategoryBootScreens = "Boot Screens";
    public static string CategoryTools = "Tools";
    public static string CategorySystemUtilities = "System/Utilities";
    public static string CategoryUtilities = "Utilities";
    public static string CategoryDriversFirmware = "Drivers & Firmware";
    public static string CategoryCodecs = "Codecs";
    public static string CategoryLogos = "Logos";
    public static string CategoryTVChannels = "TV Channels";
    public static string CategoryRadioStations = "Radio Stations";
    public static string CategoryMediaInfo = "Media Info";
    public static string CategorySkins = "Skins";
    public static string CategoryWidescreen = "Widescreen";
    public static string CategoryNonWidescreen = "Non-Widescreen";
    public static string CategoryPlugins = "Plugins";
    public static string CategoryWeather = "Weather";
    public static string CategoryOther = "Other";
    public static string CategorySystem = "System";
    public static string CategoryEmail = "Email";
    public static string CategoryPIMs = "PIMs";
    public static string CategoryHomeAutomation = "Home Automation";
    public static string CategoryPictures = "Pictures";
    public static string CategoryRadio = "Radio";
    public static string CategoryInputOutput = "Input & Output";
    public static string CategoryOtherInputDevices = "Other Input Devices";
    public static string CategoryExternalDisplays = "External Displays";
    public static string CategoryRemotes = "Remotes";
    public static string CategoryNewsInfo = "News & Info";
    public static string CategoryInfo = "Info";
    public static string CategoryNews = "News";
    public static string CategoryGamesFun = "Games & Fun";
    public static string CategoryMusic = "Music";
    public static string CategoryTelevision = "Television";
    public static string CategoryMoviesVideos = "Movies & Videos";

    // D
    public static string Download = "Download";
    public static string Downloads = "Downloads";
    public static string Disable = "Disable";
    public static string DateAdded = "Date Added";
    public static string Description = "Description";
    public static string DownloadUpdates = "Download Updates";
    public static string DownloadingUpdates = "Downloading Updates";
    public static string DownloadingExtensionIndex = "Downloading Extension Index";
    public static string DownloadingExtension = "Downloading Extension Update Definitions";
    public static string DownloadProgress = "Download Progress [{0}/{1}] {2}%";

    // E
    public static string Exit = "Exit";
    public static string Enable = "Enable";
    public static string Error = "Error";
    public static string ErrorLoadingSite = "Error Loading Category list from the MediaPortal website";
    public static string ErrorLoadingExtensionList = "Error Loading Extension list for category";
    public static string ErrorExtensionInfo = "Error getting Extension Info";
    public static string ErrorDownloadingExtensionIndex = "Error downloading extension list from server";

    // F

    // G
    public static string GetCategories = "Getting Categories";
    public static string GetExtensionInfo = "Getting Extension Info";
   
    // H
    public static string Hits = "Hits";

    // I
    public static string Id = "Id";
    public static string Install = "Install";
    public static string InstalledExtensions = "Installed Extensions";
    public static string InvalidUrl = "Invalid Url, extension can not be downloaded!";

    // L
    
    // M
    public static string MPOnlineExtensions = "MP Website Extensions";
    public static string MPUpdateAvailable = "New Mediaportal version {0} is online!\nCheck your plugins compatibility?";

    // N
    public static string Name = "Extensions";
    public static string NameInfo = "Extension Information";
    public static string NameSettings = "Extension Settings";
    public static string NeverCheckForUpdates = "Never check for updates";
    public static string NewExtensions = "New Extensions";
    public static string NewUpdates = "New updates";
    public static string NewVersion = "New version";
    public static string Notification = "MPEI Notification";
    public static string NotificationWarning = "There are installation tasks still pending.\nRestart and execute now?";
    public static string NotificationMessage = "Would you like to restart MediaPortal\nand execute pending tasks now?";
    public static string No = "No";
    public static string NoScreenshots = "No Screenshots are available for this extension.";

    // O
    public static string OnlineExtensions = "Online Extensions";
   

    // P    
    public static string Plugin = "Plugin";

    // R
    public static string Rating = "Rating";
    public static string Revoke = "Revoke";
    public static string ReleaseDate = "Release Date";
    public static string Restart = "Restart";
    public static string Rotate = "Rotate";
    public static string RevokeLastAction = "Revoke last action";
    public static string RestartNow = "This operation requires a restart of\nMediaPortal. Do you want to restart now?\nIf not, the task will be queued.";
    public static string RestoreDefaultValue = "Restore Default Value";
    public static string RestoreDefaults = "Restore Defaults";
    public static string RestoreAllDefaults = "Restore All Defaults";

    // S
    public static string Sections = "Sections";
    public static string Settings = "Settings";
    public static string ShowSreenshots = "Show Screenshots";
    public static string ShowChangelogs = "Show Change Logs";
    public static string ShowOnlyStable = "Show Obly Stable";
    public static string ShowOnlyCompatible = "Show Only Compatible";
    public static string SelectMPVersion = "Select version";
    public static string SelectVersion = "Select MediaPortal version";
    public static string SelectVersionToInstall = "Select version to (Re)Install";
    public static string StartSlideshow = "Start slideshow";
    public static string Screenshots = "Screenshots";
    public static string Status = "Status";
    public static string SettingPluginEnabledName = "Plugin Enabled";
    public static string SettingPluginEnabledDescription = "Enable / Disable this setting to control if Extensions plugin is loaded with MediaPortal.";
    public static string SettingListedHomeName = "Listed in Home";
    public static string SettingListedHomeDescription = "Enable this setting for Extensions plugin to appear in the main Home screen menu items.";
    public static string SettingListedPluginsName = "Listed in My Plugins";
    public static string SettingListedPluginsDescription = "Enable this setting for Extensions plugin to appear in the My Plugins screen menu items.";
    public static string SettingsValidationInt = "The entered value must be a number.";
    public static string SettingsValidationIntMin = "The entered value must be greater than or equal to {0}.";
    public static string SettingsValidationIntMax = "The entered value must be less than or equal to {0}.";

    // T
    public static string Timeout = "Timeout";

    // U
    public static string UseSilent = "Install extension silently?";
    public static string Update = "Update";
    public static string UpdateAvailable = "Update Available";
    public static string UpdateAll = "Update All";
    public static string Updates = "Updates";
    public static string Uninstall = "Uninstall";
    public static string UnKnownFileType = "Installation is not possible\nUnknown file type detected!";

    // V
    public static string Views = "Views";
    public static string Version = "Version";
    public static string Votes = "Votes";

    // W

    // Y
    public static string Yes = "Yes";

    #endregion

  }

}