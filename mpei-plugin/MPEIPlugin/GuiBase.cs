using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using MediaPortal.Configuration;
using MediaPortal.Dialogs;
using MediaPortal.GUI.Library;
//using MediaPortal.Profile;
using MediaPortal.Profile;
using MpeCore;
using MpeCore.Classes;
using MPEIPlugin.MPSite;

namespace MPEIPlugin
{
  public class GuiBase : GUIWindow
  {
    public bool _askForRestart = true;
    public QueueCommandCollection queue = new QueueCommandCollection();
    public SiteItems SiteItem = null;


    public void GetPackageConfigFile(PackageClass pk)
    {
      string configfile = pk.LocationFolder + "extension_settings.xml";
      if (!File.Exists(configfile) && File.Exists(pk.LocationFolder + pk.GeneralInfo.Id + ".mpe2"))
      {
        pk = pk.ZipProvider.Load(pk.LocationFolder + pk.GeneralInfo.Id + ".mpe2");
        foreach (FileItem fileItem in pk.UniqueFileList.Items)
        {
          if (Path.GetFileName(fileItem.DestinationFilename).ToLower() == "extension_settings.xml")
            pk.ZipProvider.Extract(fileItem, configfile);
        }

      }
    }

    public void ConfigureExtension(string id)
    {
      PackageClass pak = MpeInstaller.InstalledExtensions.Get(id);

      if (pak != null)
      {
        GetPackageConfigFile(pak);
        GUISettings guiSettings = (GUISettings)GUIWindowManager.GetWindow(803);
        guiSettings.SettingsFile = pak.LocationFolder + "extension_settings.xml";
        GUIWindowManager.ActivateWindow(803);
      }
      else
      {
        Log.Error("[MPEI] The {0} extension isn't installed", id);
      }
    }

    public void UnInstallExtension(string id)
    {
      PackageClass pak = MpeInstaller.InstalledExtensions.Get(id);

      if (pak != null)
      {
        queue.Add(new QueueCommand(pak, CommandEnum.Uninstall));
        queue.Save();
        NotifyUser();
      }
      else
      {
        Log.Error("[MPEI] The {0} extension isn't installed", id);
      }
    }

    public void InstallExtension(string id)
    {
      PackageClass pak = MpeInstaller.KnownExtensions.Get(id);
      if (pak != null)
      {
        queue.Add(new QueueCommand(pak, CommandEnum.Install));
        queue.Save();
        NotifyUser();
      }
      else
      {
        Log.Error("[MPEI] No extension was found :{0}", id);
      }
    }

    public void InstallExtension(SiteItems items)
    {
      items.LoadFileName();

      if (!Uri.IsWellFormedUriString(items.FileUrl, UriKind.Absolute))
      {
        GUIUtils.ShowOKDialog(Translation.Notification, Translation.UnKnownFileType);
        return;
      }

      if (!string.IsNullOrEmpty(items.File) && (Path.GetExtension(items.File) == ".exe" || Path.GetExtension(items.File) == ".mpe1"))
      {
        if(AskForRestart())
        {
          string conffile = DownloadManager.GetTempFilename();
          TextWriter streamWriter = new StreamWriter(conffile, false);
          streamWriter.WriteLine(items.FileUrl);
          streamWriter.WriteLine(Path.Combine(Path.GetTempPath(), items.File));
          streamWriter.WriteLine(items.Name);
          if (AskYesNo(Translation.UseSilent))
            streamWriter.WriteLine("/S");
          else
            streamWriter.WriteLine("");

          using (MediaPortal.Profile.Settings xmlreader = new MPSettings())
          {
            string skinFilePath = ReadSplashScreenXML();
            if (string.IsNullOrEmpty(skinFilePath))
              skinFilePath = ReadReferenceXML();
            bool useFullScreenSplash = xmlreader.GetValueAsBool("general", "usefullscreensplash", true);
            bool startFullScreen = xmlreader.GetValueAsBool("general", "startfullscreen", true);
            if (useFullScreenSplash && startFullScreen && QueueSupportsSilentMode())
              streamWriter.WriteLine(skinFilePath);
            else
              streamWriter.WriteLine("");
          }

          streamWriter.Close();
          System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
          {
              Arguments = Path.GetFileName(conffile),
              WorkingDirectory = Path.GetDirectoryName(conffile),
              FileName = Config.GetFile(Config.Dir.Base, "MPEIHelper.exe")
          });
        }
        return;
      }
      GUIUtils.ShowOKDialog(Translation.Notification, Translation.UnKnownFileType);
    }

    public void UpdateExtension(string id)
    {
      PackageClass installedpak = MpeInstaller.InstalledExtensions.Get(id);
      if (installedpak != null && MpeInstaller.KnownExtensions.GetUpdate(installedpak) != null)
      {
        queue.Add(new QueueCommand(MpeInstaller.KnownExtensions.GetUpdate(installedpak), CommandEnum.Install));
        queue.Save();
        NotifyUser();
      }
    }

    public void ShowSlideShow(SiteItems si)
    {
      if (si == null) return;

      List<string> images = new List<string>();

      foreach (string files in si.Images)
      {
        if (!string.IsNullOrEmpty(files)) images.Add(files);
      }

      ShowSlideShow(images);
    }

    public void ShowSlideShow(PackageClass pk)
    {
        if (pk == null) return;

        List<string> images = new List<string>();

        foreach (string files in pk.GeneralInfo.Params[ParamNamesConst.ONLINE_SCREENSHOT].Value.Split(ParamNamesConst.SEPARATORS))
        {
          if (!string.IsNullOrEmpty(files)) images.Add(files);
        }

        ShowSlideShow(images);
    }

    void ShowSlideShow(List<string> images)
    {
      if (images.Count == 0)
      {
        GUIUtils.ShowOKDialog(Translation.Screenshots, Translation.NoScreenshots);
        return;
      }

      GUISlideShow slideShow = (GUISlideShow)GUIWindowManager.GetWindow(802);
      if (slideShow == null) return;

      slideShow.Reset();
      foreach (string image in images)
      {
        slideShow.Add(image);
      }

      GUIWindowManager.ActivateWindow(802);
      slideShow.StartSlideShow();
    
    }

    public void ShowChangeLog(string id)
    {
      PackageClass installedpak = MpeInstaller.InstalledExtensions.Get(id);
      if (installedpak != null)
      {
        PackageClass update = MpeInstaller.KnownExtensions.GetUpdate(installedpak);
        if (update != null)
        {
          string text = string.Format("{0} - {1}", update.GeneralInfo.Name, update.GeneralInfo.Version.ToString()) + "\n" + update.GeneralInfo.VersionDescription;
          GUIUtils.ShowTextDialog(Translation.ChangeLog, text);
        }
      }
    }

    public void ShowChangeLog(PackageClass pk)
    {
      if (pk == null)
      {
       return;
      }
      string selected = "";
      while (true)
      {
        GUIDialogMenu dlg = (GUIDialogMenu)GUIWindowManager.GetWindow((int)Window.WINDOW_DIALOG_MENU);
        if (dlg == null) return;
        dlg.Reset();
        dlg.SetHeading(Translation.SelectVersion);
        
        ExtensionCollection paks = MpeInstaller.KnownExtensions.GetList(pk.GeneralInfo.Id);
        PackageClass ver = null;

        if (paks.Items.Count > 1)
        {
          foreach (PackageClass item in paks.Items)
          {
            GUIListItem guiListItem = new GUIListItem(item.GeneralInfo.Version.ToString());
            if (MpeInstaller.InstalledExtensions.Get(item) != null)
              guiListItem.Selected = true;
            guiListItem.Label2 = UpgradeAdvisor.GetCompatibleMPRange(item);
            dlg.Add(guiListItem);
          }
          dlg.selectOption(selected);
          dlg.DoModal(GetID);
          if (dlg.SelectedId == -1) return;
          ver = MpeInstaller.KnownExtensions.Get(pk.GeneralInfo.Id, dlg.SelectedLabelText);
          selected = dlg.SelectedLabelText;
        }
        else
        {
          ver = pk;
        }

        if (ver != null)
        {
          string text = string.Format("{0} - {1}", ver.GeneralInfo.Name, ver.GeneralInfo.Version.ToString()) + "\n" + ver.GeneralInfo.VersionDescription;
          GUIUtils.ShowTextDialog(Translation.ChangeLog, text);
          if (paks.Items.Count <= 1) return;
        }
      }
    }

    void NotifyUser()
    {
      if (_askForRestart)
      {
        if (GUIUtils.ShowYesNoDialog(Translation.Notification, Translation.RestartNow, true))
        {
          RestartMP();
          return;
        }
      }

      GUIUtils.ShowNotifyDialog(Translation.Notification, Translation.ActionAdded, 2);
      _askForRestart = false;
    }

    public bool AskForRestart()
    {
      return GUIUtils.ShowYesNoDialog(Translation.Notification, Translation.AskForRestart, true);
    }

    public bool AskForRestartWarning()
    {
      return GUIUtils.ShowYesNoDialog(Translation.Notification, Translation.NotificationWarning, true);
    }

    public bool AskYesNo(string question)
    {
      return GUIUtils.ShowYesNoDialog(Translation.Notification, question, true);
    }

    public void RestartMP()
    {
      string cmdLine = "/MPQUEUE";
      using (MediaPortal.Profile.Settings xmlreader = new MPSettings())
      {
        string skinFilePath = ReadSplashScreenXML();
        if (string.IsNullOrEmpty(skinFilePath))
          skinFilePath = ReadReferenceXML();
        bool useFullScreenSplash = xmlreader.GetValueAsBool("general", "usefullscreensplash", true);
        bool startFullScreen = xmlreader.GetValueAsBool("general", "startfullscreen", true);
        if (useFullScreenSplash && startFullScreen && QueueSupportsSilentMode())
          cmdLine += " /BK=\"" + skinFilePath + "\"";
      }
      Log.Debug("[MPEI] Plugin Start:" + Config.GetFile(Config.Dir.Base, "MPEInstaller.exe ") + cmdLine);
      System.Diagnostics.Process.Start(Config.GetFile(Config.Dir.Base, "MPEInstaller.exe"), cmdLine);
    }


    private string ReadSplashScreenXML()
    {
      string m_strSkin;
      string SkinFilePath = string.Empty;

      // try to find the splashscreen.xml ín the curent skin folder
      using (MediaPortal.Profile.Settings xmlreader = new MPSettings())
      {
        m_strSkin = xmlreader.GetValueAsString("skin", "name", "DefaultWide");
        SkinFilePath = Config.GetFile(Config.Dir.Skin, m_strSkin + "\\splashscreen.xml");
      }

      string BackgoundImageName = "splashscreen.png";
      string BackgroundImagePath = Config.GetFile(Config.Dir.Skin, m_strSkin + "\\media\\" + BackgoundImageName);
      if (File.Exists(BackgroundImagePath)) return BackgroundImagePath;

      XmlDocument doc = new XmlDocument();
      doc.Load(SkinFilePath);
      XmlNodeList ControlsList = doc.DocumentElement.SelectNodes("/window/controls/control");

      foreach (XmlNode Control in ControlsList)
      {
        if (Control.SelectSingleNode("type/text()").Value.ToLower() == "image"
            && Control.SelectSingleNode("id/text()").Value == "1") // if the background image control is found
        {
          BackgoundImageName = Control.SelectSingleNode("texture/text()").Value;
          BackgroundImagePath = Config.GetFile(Config.Dir.Skin, m_strSkin + "\\media\\" + BackgoundImageName);
          if (File.Exists(BackgroundImagePath))
          {
            return BackgroundImagePath;
          }
          continue;
        }
      }
      return "";
    }

    private string ReadReferenceXML()
    {
      string m_strSkin;
      string SkinReferenceFilePath = string.Empty;

      using (MediaPortal.Profile.Settings xmlreader = new MPSettings())
      {
        m_strSkin = xmlreader.GetValueAsString("skin", "name", "DefaultWide");
        SkinReferenceFilePath = Config.GetFile(Config.Dir.Skin, m_strSkin + "\\references.xml");
      }

      XmlDocument doc = new XmlDocument();
      doc.Load(SkinReferenceFilePath);
      XmlNodeList ControlsList = doc.DocumentElement.SelectNodes("/controls/control");

      foreach (XmlNode Control in ControlsList)
      {
        if (Control.SelectSingleNode("type/text()").Value.ToLower() == "image")
        {
          string BackgoundImageName = Control.SelectSingleNode("texture/text()").Value;
          string BackgroundImagePath = Config.GetFile(Config.Dir.Skin, m_strSkin + "\\media\\" + BackgoundImageName);
          if (File.Exists(BackgroundImagePath))
          {
            return BackgroundImagePath; // load the image as background
          }
        }
      }
      return "";
    }

    private bool QueueSupportsSilentMode()
    {
        // check if there is an ignored package
        if (queue.Items.Exists(q => GUIMpeiPlugin.ignoredFullScreenPackages.Contains(q.TargetId)))
            return false;
        
        return true;
    }
    public ExtensionCollection GetUniquePack(string file)
    {
        ExtensionCollection newExtension = ExtensionCollection.Load(file);
        ExtensionCollection unigueCollection = new ExtensionCollection();
        foreach (PackageClass package in newExtension.Items)
        {
            if (!MpeInstaller.KnownExtensions.Items.Exists(pk => pk.GeneralInfo.Id == package.GeneralInfo.Id && pk.GeneralInfo.Version.CompareTo(package.GeneralInfo.Version) == 0 && pk.GeneralInfo.ReleaseDate >= package.GeneralInfo.ReleaseDate))
            {

                unigueCollection.Items.Add(package);
            }

        }
        return unigueCollection;
    }
    public static void ReplaceUpdateURL(string PackageId, string url)
    {
        List<PackageClass> packs = MpeInstaller.KnownExtensions.GetList(PackageId).Items;
        foreach (PackageClass p in packs)
        {
            MpeInstaller.KnownExtensions.Items.Find(i => i.GeneralInfo.Id == p.GeneralInfo.Id
                && i.GeneralInfo.Version.CompareTo(p.GeneralInfo.Version) == 0
                && i.GeneralInfo.ReleaseDate == p.GeneralInfo.ReleaseDate).GeneralInfo.UpdateUrl = url;

        }
        PackageClass LocalPack = MpeInstaller.InstalledExtensions.Get(PackageId);
        if (LocalPack != null)
        {
            MpeInstaller.InstalledExtensions.Items.Find(i => i.GeneralInfo.Id == LocalPack.GeneralInfo.Id
                && i.GeneralInfo.Version.CompareTo(LocalPack.GeneralInfo.Version) == 0
                && i.GeneralInfo.ReleaseDate == LocalPack.GeneralInfo.ReleaseDate).GeneralInfo.UpdateUrl = url;
        }

    }
  }
}
