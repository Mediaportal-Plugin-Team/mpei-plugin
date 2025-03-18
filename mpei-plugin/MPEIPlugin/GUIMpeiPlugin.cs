
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Timers;
using System.ComponentModel;
using MediaPortal.GUI.Library;
using MediaPortal.Profile;
using MediaPortal.Util;
using MediaPortal.Dialogs;
using MpeCore;
using MpeCore.Classes;
using MPEIPlugin.MPSite;
using Action = MediaPortal.GUI.Library.Action;


namespace MPEIPlugin
{
  public class GUIMpeiPlugin : GuiBase, ISetupForm, IComparer<GUIListItem>, IShowPlugin
  {
    #region enums
    enum SortMethod
    {
      Name = 0,
      Type = 1,
      Date = 2,
      Download = 3,
      Rating = 4,
    }

    enum View : int
    {
      List = 0,
      Icons = 1,
      LargeIcons = 2,
    }

    enum Views
    {
        Local = 0,
        Online = 1,
        Updates = 2,
        New = 3,
        Queue = 4,
        MpSite = 5,
        Compatibility = 6
    }

    #endregion

    #region Base variabeles
    View currentLayout = View.List;
    Views currentListing = Views.Local;
    SortMethod currentSortMethod = SortMethod.Date;
    bool sortAscending = true;
    bool periodicUpdateCheck = true;
    public static List<string> ignoredFullScreenPackages = new List<string>();
    private MPSiteUtil SiteUtil = new MPSiteUtil();
    private List<MPRelease> NewReleases = new List<MPRelease>();
    string currentFolder = string.Empty;
    int selectedItemIndex = -1;
    public static DownloadManager _downloadManager = new DownloadManager();

    private ApplicationSettings _setting = new ApplicationSettings();

    private Timer _timer = new Timer(4000);
    private System.Threading.Timer updatesTimer;
    private int updatesPeriod;

    private const string UpdateIndexUrl = "http://install.team-mediaportal.com/MPEI/extensions.txt";
    public const string MPUpdateUrl = @"http://edalex.dyndns.tv:8090/Index.txt";
    private const int newdays = 10;
    private const int daysToMs = 24 * 60 * 60 * 1000;

    private static bool cancelDownloadCheck;
    #endregion

    #region SkinControls

    [SkinControlAttribute(50)]
    protected GUIFacadeControl facadeView = null;
    [SkinControlAttribute(2)]
    protected GUIButtonControl btnViewAs = null;
    [SkinControlAttribute(3)]
    protected GUISortButtonControl btnSortBy = null;
    [SkinControlAttribute(5)]
    protected GUIButtonControl btnRestart = null;
    [SkinControlAttribute(6)]
    protected GUIButtonControl btnViews = null;
    [SkinControlAttribute(8)]
    protected GUIButtonControl btnUpdateAll = null;
    [SkinControlAttribute(9)]
    protected GUIButtonControl btnCheckForUpdates = null;
    [SkinControlAttribute(10)]
    protected GUICheckButton btnOnlyStable = null;
    [SkinControlAttribute(11)]
    protected GUICheckButton btnOnlyCompatible = null;
    #endregion

    public GUIMpeiPlugin()
    {

    }

    public override string GetModuleName()
    {
      return Translation.Name;
    }

    public override bool Init()
    {
      Log.Debug("[MPEI] Init Start");
      LoadSettings();
      MpeInstaller.Init();
      Translation.Init();

      queue = QueueCommandCollection.Load();
      _downloadManager.DownloadDone += _downloadManager_DownloadDone;
      _timer.Elapsed += _timer_Elapsed;
      _setting = ApplicationSettings.Load();
      MpeInstaller.InstalledExtensions.IgnoredUpdates = _setting.IgnoredUpdates;
      MpeInstaller.KnownExtensions.IgnoredUpdates = _setting.IgnoredUpdates;
      MpeInstaller.KnownExtensions.Hide(_setting.ShowOnlyStable, _setting.ShowOnlyCompatible);
      currentFolder = string.Empty;
      
      GenerateProperty();

      bool bResult = Load(GUIGraphicsContext.Skin + @"\myextensions2.xml");
      GUIWindowManager.OnDeActivateWindow += new GUIWindowManager.WindowActivationHandler(GUIWindowManager_OnDeActivateWindow);
      GUIGraphicsContext.Receivers += GUIGraphicsContext_Receivers;
      _timer.Enabled = true;
       //check for new MediaPortal versions   
      CheckForReleases();
      // schedule periodic updates, we already handle updates on startup so dont need to start now
      if (periodicUpdateCheck)
        updatesPeriod = _setting.UpdateDays == 0 ? daysToMs : _setting.UpdateDays * daysToMs;
      else
        updatesPeriod = System.Threading.Timeout.Infinite;

      updatesTimer = new System.Threading.Timer(new System.Threading.TimerCallback((o) => { DownloadUpdateXmlInfo(); }), null, updatesPeriod, updatesPeriod);
      
      Log.Debug("[MPEI] Init End");

      return bResult;
    }

    public override void DeInit()
    {
      SaveSettings();
      base.DeInit();
    }

    void GUIWindowManager_OnDeActivateWindow(int windowID)
    {
      // Settings/General window
      // this is where a user can change skins\languages from GUI
      if (windowID == (int)Window.WINDOW_SETTINGS_SKIN)
      {
        //did language change?
        if (Translation.CurrentLanguage != Translation.PreviousLanguage)
        {
          Log.Info("Language changed to '{0}' from GUI, re-initializing translations.", Translation.CurrentLanguage);
          Translation.Init();
        }
      }
    }

    void GUIGraphicsContext_Receivers(GUIMessage message)
    {
      if (message.Message == GUIMessage.MessageType.GUI_MSG_CLICKED)
      {
        GUIButtonControl cnt = GUIWindowManager.GetWindow(message.TargetWindowId).GetControl(message.SenderControlId) as GUIButtonControl;
        if (cnt != null)
        {
          string desc = cnt.Description;
          if (!string.IsNullOrEmpty(desc) && desc.Split(':').Length > 1)
          {
            string command = desc.Split(':')[0].Trim().ToUpper();
            string arg = desc.Split(':')[1].Trim().Replace("_", "-");
            switch (command)
            {
              case "MPEIUPDATE":
                UpdateExtension(arg);
                break;
              case "MPEISHOWCHANGELOG":
                ShowChangeLog(arg);
                break;
              case "MPEIINSTALL":
                InstallExtension(arg);
                break;
              case "MPEIUNINSTALL":
                UnInstallExtension(arg);
                break;
              case "MPEICONFIGURE":
                ConfigureExtension(arg);
                break;
              default:
                break;
            }
          }
        }
      }
    }

    void _timer_Elapsed(object sender, ElapsedEventArgs e)
    {
      _timer.Enabled = false;
      if(queue.Items.Count > 0)
      {
       if(AskForRestartWarning()) 
         RestartMP();
      }
      else
      {
        if (_setting.DoUpdateInStartUp)
        {
          Log.Info("[MPEI] Next download of updates scheduled for {0}", _setting.LastUpdate.AddDays(_setting.UpdateDays));
          
          int i = DateTime.Now.Subtract(_setting.LastUpdate).Days;
          if (_setting.DoUpdateInStartUp && i >= _setting.UpdateDays)
          {
            Log.Info("[MPEI] Download of updates is required, downloading now...");
            DownloadUpdateXmlInfo();
          }
        }
      }
    }

    void DownloadUpdateXmlInfo()
    {
      _downloadManager.AddToDownloadQueue(UpdateIndexUrl, DownloadManager.GetTempFilename(), DownLoadItemType.IndexList);
      _setting.LastUpdate = DateTime.Now;
      _setting.Save();

      updatesTimer.Change(updatesPeriod, updatesPeriod);
    }

    #region downloadmanager

    void _downloadManager_DownloadDone(DownLoadInfo info)
    {
      switch (info.ItemType)
      {
        case DownLoadItemType.IndexList:
          {
            LoadExtensionIndex(info.Destination);
            DownloadUpdateInfo();
          }
          break;
        case DownLoadItemType.UpdateInfo:
          {
            MpeInstaller.KnownExtensions.Add(GetUniquePack(info.Destination));
            MpeInstaller.KnownExtensions.Hide(_setting.ShowOnlyStable, _setting.ShowOnlyCompatible);
            
            Log.Debug("[MPEI] Update info loaded from {0}", info.Url);
            File.Delete(info.Destination);
            GenerateProperty();
            MpeInstaller.Save();
            if (_setting.UpdateAll)
            {
              UpdateAll();
            }
          }
          break;
        case DownLoadItemType.UpdateInfoComplete:
          Log.Info("[MPEI] Finished downloading updates for extensions");
          if (GUIWindowManager.ActiveWindow == GetID && currentListing != Views.MpSite)
            LoadDirectory(currentFolder);
          break;
        case DownLoadItemType.Extension:
          break;
        case DownLoadItemType.Logo:
          if (info.Package != null)
            SetLogo(info.Package, info.ListItem);
          if (info.SiteItem != null)
            SetLogo(info.SiteItem, info.ListItem);
          break;
        case DownLoadItemType.Other:
          break;
        default:
          //throw new ArgumentOutOfRangeException();
          break;
      }
    }
    
    
    #endregion

    #region ISetupForm Members

    public bool CanEnable()
    {
      return true;
    }

    public string PluginName()
    {
      return "Extensions";
    }

    public bool HasSetup()
    {
      return false;
    }

    public bool DefaultEnabled()
    {
      return true;
    }

    public int GetWindowId()
    {
      return GetID;
    }

    public bool GetHome(out string strButtonText, out string strButtonImage, out string strButtonImageFocus, out string strPictureImage)
    {
      strButtonText = Translation.Name;
      strButtonImage = string.Empty;
      strButtonImageFocus = string.Empty;
      strPictureImage = "hover_extensions.png";
      return true;
    }

    public string Author()
    {
      return "Dukus, Migue, ltfearme";
    }

    public string Description()
    {
      return "Browse (Un)Install Extensions";
    }

    public void ShowPlugin()
    {

    }

    #endregion

    #region IShowPlugin Members

    public bool ShowDefaultHome()
    {
      return false;
    }

    #endregion

    void DownloadUpdateInfo()
    {
      List<string> onlineFiles = MpeInstaller.InstalledExtensions.GetUpdateUrls(new List<string>());
      onlineFiles = MpeInstaller.KnownExtensions.GetUpdateUrls(onlineFiles);
      onlineFiles = MpeInstaller.GetInitialUrlIndex(onlineFiles);
      foreach (string onlineFile in onlineFiles)
      {
        _downloadManager.AddToDownloadQueue(onlineFile, DownloadManager.GetTempFilename(), DownLoadItemType.UpdateInfo);
      }
    }

    static void LoadExtensionIndex(string tempUpdateIndex)
    {
      if (File.Exists(tempUpdateIndex))
      {
        var indexUrls = new Dictionary<string, string>();
        string[] lines = File.ReadAllLines(tempUpdateIndex);
        foreach (string line in lines)
        {
          if (string.IsNullOrEmpty(line)) continue;
          if (line.StartsWith("#")) continue;
            string url = line.Split(';')[0];
            string guid = line.Split(';')[1];
            if (!indexUrls.ContainsKey(guid))
            {
                PackageClass PackByGUID = MpeInstaller.KnownExtensions.Get(guid);
                if (PackByGUID != null)
                {
                    if (url != PackByGUID.GeneralInfo.UpdateUrl)
                    {
                        ReplaceUpdateURL(guid, url);
                    }
                    indexUrls.Add(guid, url);
                }
            }
        }
        MpeInstaller.SetInitialUrlIndex(indexUrls.Values.ToList());
      }
    }

    void UpdateInform()
    {
        if (NewReleases.Count > 0)
        {
            if (GUIUtils.ShowYesNoDialog(Translation.Notification, string.Format(Translation.MPUpdateAvailable, NewReleases[0].DisplayedVersion), true))
            {
                currentListing = Views.Compatibility;
            }
        }
    }

    void CheckForReleases()
    {
        BackgroundWorker bw = new BackgroundWorker();
        bw.DoWork +=new DoWorkEventHandler(bw_DoWork);
        bw.RunWorkerAsync();

    }
    private void bw_DoWork(object sender, DoWorkEventArgs e)
    {
        NewReleases = UpgradeAdvisor.GetUpdate();   
    }


    void UpdateAll()
    {
      var updatelist = new Dictionary<PackageClass, PackageClass>();
      foreach (PackageClass packageClass in MpeInstaller.InstalledExtensions.Items)
      {
        PackageClass update = MpeInstaller.KnownExtensions.GetUpdate(packageClass);
        if (update == null)
          continue;
        updatelist.Add(packageClass, update);
      }
      foreach (KeyValuePair<PackageClass, PackageClass> valuePair in updatelist)
      {
        if (valuePair.Value == null)
          continue;
        DownloadExtension(valuePair.Value);
        queue.Add(new QueueCommand(valuePair.Value, CommandEnum.Install));
      }
      if (queue.Items.Count > 0)
        queue.Save();
    }

    List<PackageClass> GetUpdates()
    {
      List<PackageClass> updates = new List<PackageClass>();
      foreach (PackageClass packageClass in MpeInstaller.InstalledExtensions.Items)
      {
        PackageClass update = MpeInstaller.KnownExtensions.GetUpdate(packageClass);
        if (update == null)
          continue;
        updates.Add(update);
      }
      return updates;
    }

    #region Serialisation
    void LoadSettings()
    {
      Log.Info("[MPEI] Loading settings");
    
      using (MediaPortal.Profile.Settings xmlreader = new MPSettings())
      {
        string tmpLine = string.Empty;
        tmpLine = xmlreader.GetValue("myextensions2", "viewby");
        if (tmpLine != null)
        {
          if (tmpLine == "list") currentLayout = View.List;
          else if (tmpLine == "icons") currentLayout = View.Icons;
          else if (tmpLine == "largeicons") currentLayout = View.LargeIcons;
        }

        tmpLine = (string)xmlreader.GetValue("myextensions2", "sort");
        if (tmpLine != null)
        {
          switch (tmpLine)
          {
              case "name":
                  currentSortMethod = SortMethod.Name;
                  break;
              case "type":
                  currentSortMethod = SortMethod.Type;
                  break;
              case "date":
                  currentSortMethod = SortMethod.Date;
                  break;
              case "download":
                  currentSortMethod = SortMethod.Download;
                  break;
              case "rate":
                  currentSortMethod = SortMethod.Rating;
                  break;
          }
        }
        tmpLine = (string)xmlreader.GetValue("myextensions2", "listing");
        if (tmpLine != null)
        {
          switch (tmpLine)
          {
              case "local":
                  currentListing = Views.Local;
                  break;
              case "online":
                  currentListing = Views.Online;
                  break;
              case "updates":
                  currentListing = Views.Updates;
                  break;
              case "new":
                  currentListing = Views.New;
                  break;
              case "queue":
                  currentListing = Views.Queue;
                  break;
              case "site":
                  currentListing = Views.MpSite;
                  break;
              case "compatibility":
                  currentListing = Views.Compatibility;
                  break;
          }
        }
        sortAscending = xmlreader.GetValueAsBool("myextensions2", "sortascending", true);
        periodicUpdateCheck = xmlreader.GetValueAsBool("myextensions2", "periodicupdatecheck", true);
        // default packages to ignore
        // user can add more to settings file
        List<string> ignoreList = new List<string>()
        {
            "b7738156-b6ec-4f0f-b1a8-b5010349d8b1", // LAV Filters
            "88f9a821-bd54-4a40-9bfc-222b3324973d", // Backup Settings
            "269bd257-7ce5-450a-b786-1c2834c81849"  // OnlineVideos (includes LAV)
        };
        ignoredFullScreenPackages = xmlreader.GetValueAsString("myextensions2", "ignoredfullscreenpackages", ignoreList.ToJSON()).FromJSONArray<string>().ToList();
      }
    }

    void SaveSettings()
    {
      Log.Info("[MPEI] Saving settings");

      using (MediaPortal.Profile.Settings xmlwriter = new MPSettings())
      {
        switch (currentLayout)
        {
          case View.List:
            xmlwriter.SetValue("myextensions2", "viewby", "list");
            break;
          case View.Icons:
            xmlwriter.SetValue("myextensions2", "viewby", "icons");
            break;
          case View.LargeIcons:
            xmlwriter.SetValue("myextensions2", "viewby", "largeicons");
            break;
        }

        switch (currentSortMethod)
        {
          case SortMethod.Name:
            xmlwriter.SetValue("myextensions2", "sort", "name");
            break;
          case SortMethod.Type:
            xmlwriter.SetValue("myextensions2", "sort", "type");
            break;
          case SortMethod.Date:
            xmlwriter.SetValue("myextensions2", "sort", "date");
            break;
          case SortMethod.Download:
            xmlwriter.SetValue("myextensions2", "sort", "download");
            break;
          case SortMethod.Rating:
            xmlwriter.SetValue("myextensions2", "sort", "rate");
            break;
        }

        switch (currentListing)
        {
          case Views.Local:
            xmlwriter.SetValue("myextensions2", "listing", "local");
            break;
          case Views.Online:
            xmlwriter.SetValue("myextensions2", "listing", "online");
            break;
          case Views.Updates:
            xmlwriter.SetValue("myextensions2", "listing", "updates");
            break;
          case Views.New:
            xmlwriter.SetValue("myextensions2", "listing", "new");
            break;
          case Views.Queue:
            xmlwriter.SetValue("myextensions2", "listing", "queue");
            break;
          case Views.MpSite:
            xmlwriter.SetValue("myextensions2", "listing", "site");
            break;
        }

        xmlwriter.SetValueAsBool("myextensions2", "sortascending", sortAscending);
        xmlwriter.SetValueAsBool("myextensions2", "periodicupdatecheck", periodicUpdateCheck);
        xmlwriter.SetValue("myextensions2", "ignoredfullscreenpackages", ignoredFullScreenPackages.ToJSON());
      }
    }

    private void GetLoadingParameters()
    {
      try
      {
        foreach (String currParam in _loadParameter.Split('|'))
        {
          String[] keyValue = currParam.Split(':');
          String key = keyValue[0];
          String value = keyValue[1];

          try
          {
            switch (key)
            {
              case "view":
                switch (value)
                {
                    case "local":
                        currentListing = Views.Local;
                        break;
                    case "online":
                        currentListing = Views.Online;
                        break;
                    case "updates":
                        currentListing = Views.Updates;
                        break;
                    case "new":
                        currentListing = Views.New;
                        break;
                    case "queue":
                        currentListing = Views.Queue;
                        break;
                    case "site":
                        currentListing = Views.MpSite;
                        break;
                    case "compatibility":
                        currentListing = Views.Compatibility;
                        break;
                }
                break;

              default:
                break;
            }
          }
          catch (FormatException)
          {
            Log.Warn("[MPEI] Received invalid parameter: " + currParam);
          }
        }
      }
      catch (Exception ex)
      {
        Log.Error("[MPEI] Unexpected error parsing paramaters: " + _loadParameter, ex);
      }
    }

    #endregion

    #region BaseWindow Members

    public override int GetID
    {
      get
      {
        return 801;
      }
    }

    public override void OnAction(Action action)
    {
      if (action.wID == Action.ActionType.ACTION_KEY_PRESSED && action.m_key.KeyChar == 27)
      {
        cancelDownloadCheck = true;
      }

      if (action.wID == Action.ActionType.ACTION_PREVIOUS_MENU)
      {
        cancelDownloadCheck = true;

        if (facadeView.Focus)
        {
          GUIListItem item = facadeView[0];
          if (item != null)
          {
            if (item.IsFolder && item.Label == "..")
            {
              LoadDirectory(item.Path);
              return;
            }
          }
        }
      }

      if (action.wID == Action.ActionType.ACTION_PARENT_DIR)
      {
        GUIListItem item = facadeView[0];
        if (item != null)
        {
          if (item.IsFolder && item.Label == "..")
          {
            LoadDirectory(item.Path);
          }
        }
        return;
      }

      base.OnAction(action);
    }

    protected override void OnPageLoad()
    {
      ClearProperties();
      LoadSettings();
      
      if (!string.IsNullOrEmpty(_loadParameter))
      {
        GetLoadingParameters();
      }

      switch (currentSortMethod)
      {
        case SortMethod.Name:
          btnSortBy.SelectedItem = 0;
          break;
        case SortMethod.Type:
          btnSortBy.SelectedItem = 1;
          break;
        case SortMethod.Date:
          btnSortBy.SelectedItem = 2;
          break;
        case SortMethod.Download:
          btnSortBy.SelectedItem = 3;
          break;
        case SortMethod.Rating:
          btnSortBy.SelectedItem = 4;
          break;
      }

      UpdateInform();
      SelectCurrentItem();
      UpdateButtonStates();
      LoadDirectory(currentFolder);

      btnSortBy.SortChanged += SortChanged;
      btnOnlyCompatible.Selected = _setting.ShowOnlyCompatible;
      btnOnlyStable.Selected = _setting.ShowOnlyStable;
      _askForRestart = true;
      base.OnPageLoad();
    }

    protected override void OnShowContextMenu()
    {
      GUIListItem item = facadeView.SelectedListItem;
      SiteItems si = item.MusicTag as SiteItems;
      if (si != null)
      {
        GUIDialogMenu dlg1 = (GUIDialogMenu)GUIWindowManager.GetWindow((int)Window.WINDOW_DIALOG_MENU);
        if (dlg1 == null) return;
        dlg1.Reset();
        dlg1.Add(Translation.ShowSreenshots);
        dlg1.DoModal(GetID);
        if (dlg1.SelectedId == -1) return;
        si.LoadInfo();
        if (dlg1.SelectedLabelText == Translation.ShowSreenshots)
        {
          ShowSlideShow(si);
        }
        return;
      }

      PackageClass pk = item.MusicTag as PackageClass;
      if (pk == null)
        return;
      PackageClass installedpak = MpeInstaller.InstalledExtensions.Get(pk.GeneralInfo.Id);

      GUIDialogMenu dlg = (GUIDialogMenu)GUIWindowManager.GetWindow((int)Window.WINDOW_DIALOG_MENU);
      if (dlg == null) return;
      dlg.Reset();
      if (queue.Get(pk.GeneralInfo.Id) != null)
      {
        dlg.SetHeading(string.Format("{0} {1} v{2}", Translation.Revoke, queue.Get(pk.GeneralInfo.Id).CommandEnum == CommandEnum.Install ? Translation.Install : Translation.Uninstall, queue.Get(pk.GeneralInfo.Id).TargetVersion.ToString()));
        dlg.Add(Translation.RevokeLastAction);
      }
      else
      {
        dlg.SetHeading(Translation.Actions);
        dlg.Add(Translation.Install);
        if (installedpak != null && MpeInstaller.KnownExtensions.GetUpdate(installedpak) != null)
        {
          dlg.Add(Translation.Update);
        }
        if (MpeInstaller.InstalledExtensions.Get(pk) != null)
        {
          dlg.Add(Translation.Uninstall);
        }
        dlg.Add(_setting.IgnoredUpdates.Contains(pk.GeneralInfo.Id)
                  ? Translation.AlwaysCheckForUpdates
                  : Translation.NeverCheckForUpdates);
      }
      if (!string.IsNullOrEmpty(pk.GeneralInfo.Params[ParamNamesConst.ONLINE_SCREENSHOT].Value.Trim()) && pk.GeneralInfo.Params[ParamNamesConst.ONLINE_SCREENSHOT].Value.Split(ParamNamesConst.SEPARATORS).Length > 0)
      {
        dlg.Add(Translation.ShowSreenshots);
      }
      dlg.Add(Translation.ShowChangelogs);

      GetPackageConfigFile(pk);
      if (MpeInstaller.InstalledExtensions.Get(pk) != null)
      {
        dlg.Add(Translation.Settings);
      }

      dlg.DoModal(GetID);
      if (dlg.SelectedId == -1) return;
      if (dlg.SelectedLabelText == Translation.Install)
      {
        ShowInstall(pk);
      }
      else if (dlg.SelectedLabelText == Translation.Uninstall)
      {
        queue.Add(new QueueCommand(pk, CommandEnum.Uninstall));
        NotifyUser();
      }
      else if (dlg.SelectedLabelText == Translation.RevokeLastAction)
      {
        queue.Remove(pk.GeneralInfo.Id);
        NotifyRemoveUser();
      }
      else if (dlg.SelectedLabelText == Translation.Update)
      {
        queue.Add(new QueueCommand(MpeInstaller.KnownExtensions.GetUpdate(installedpak), CommandEnum.Install));
        NotifyUser();
      }
      else if (dlg.SelectedLabelText == Translation.NeverCheckForUpdates)
      {
        _setting.IgnoredUpdates.Add(pk.GeneralInfo.Id);
      }
      else if (dlg.SelectedLabelText == Translation.AlwaysCheckForUpdates)
      {
        _setting.IgnoredUpdates.Remove(pk.GeneralInfo.Id);
      }
      else if (dlg.SelectedLabelText == Translation.ShowSreenshots)
      {
        ShowSlideShow(pk);
      }
      else if (dlg.SelectedLabelText == Translation.ShowChangelogs)
      {
        ShowChangeLog(pk);
      }
      else if (dlg.SelectedLabelText == Translation.Settings)
      {
        GUISettings guiSettings = (GUISettings)GUIWindowManager.GetWindow(803);
        GUIWindowManager.ActivateWindow(803,guiSettings.SettingsFile = pk.LocationFolder + "extension_settings.xml");
      }
      _setting.Save();
      queue.Save();
      
    }
    protected override void OnPageDestroy(int newWindowId)
    {
      cancelDownloadCheck = true;
      GUIBackgroundTask.Instance.StopBackgroundTask();

      selectedItemIndex = facadeView.SelectedListItemIndex;
      SaveSettings();
      if (queue.Items.Count > 0)
      {
        if (GUIUtils.ShowYesNoDialog(Translation.Notification, Translation.NotificationWarning, true))
        {
          RestartMP();
        }
      }
      base.OnPageDestroy(newWindowId);
    }

    protected override void OnClicked(int controlId, GUIControl control, Action.ActionType actionType)
    {
      // wait for any background action to finish
      if (GUIBackgroundTask.Instance.IsBusy) return;

      base.OnClicked(controlId, control, actionType);

      if (control == btnViewAs)
      {
        bool shouldContinue = false;
        do
        {
          shouldContinue = false;
          switch (currentLayout)
          {
            case View.List:
              currentLayout = View.Icons;
              if (facadeView.ThumbnailLayout == null)
                shouldContinue = true;
              else
                facadeView.CurrentLayout = GUIFacadeControl.Layout.SmallIcons;
              break;

            case View.Icons:
              currentLayout = View.LargeIcons;
              if (facadeView.ThumbnailLayout == null)
                shouldContinue = true;
              else
                facadeView.CurrentLayout = GUIFacadeControl.Layout.LargeIcons;
              break;

            case View.LargeIcons:
              currentLayout = View.List;
              if (facadeView.ListLayout == null)
                shouldContinue = true;
              else
                facadeView.CurrentLayout = GUIFacadeControl.Layout.List;
              break;
          }
        } while (shouldContinue);

        SelectCurrentItem();
        GUIControl.FocusControl(GetID, controlId);
        return;
      } //if (control == btnViewAs)

      if (control == btnSortBy)
      {
        OnShowSort();
      }

      if (control == btnRestart)
      {
        if (queue.Items.Count > 0)
        {
          if (GUIUtils.ShowYesNoDialog(Translation.Notification, Translation.NotificationMessage, true))
          {
            RestartMP();
          }
        }
      }

      if (control == btnViews)
      {
        OnShowViews();
        GUIControl.FocusControl(GetID, facadeView.GetID);
      }

      if (control == btnUpdateAll)
      {
        UpdateAll();
        LoadDirectory(currentFolder);
        GUIControl.FocusControl(GetID, facadeView.GetID);

        if (queue.Items.Count > 0)
        {
          if (GUIUtils.ShowYesNoDialog(Translation.Notification, Translation.NotificationMessage, true))
          {
            RestartMP();
          }
        }
      }

      if (control == btnCheckForUpdates)
      {
        GUIControl.FocusControl(GetID, facadeView.GetID);
        CheckForUpdates();
        return;
      }

      if (control == btnOnlyStable || control == btnOnlyCompatible)
      {
          _setting.ShowOnlyStable = btnOnlyStable.Selected;
          _setting.ShowOnlyCompatible = btnOnlyCompatible.Selected;
          _setting.Save();
          MpeInstaller.KnownExtensions.Hide(_setting.ShowOnlyStable, _setting.ShowOnlyCompatible);
          LoadDirectory(currentFolder);
      }



      if (control == facadeView)
      {
        GUIMessage msg = new GUIMessage(GUIMessage.MessageType.GUI_MSG_ITEM_SELECTED, GetID, 0, controlId, 0, 0, null);
        OnMessage(msg);
        int itemIndex = (int)msg.Param1;
        if (actionType == Action.ActionType.ACTION_SELECT_ITEM)
        {
          OnClick(itemIndex);
        }
      }
    }

    void CheckForUpdates()
    {
      // let user choose between all or installed extensions
      List<GUIListItem> choices = new List<GUIListItem>();
      GUIListItem item = new GUIListItem();

      choices.Add(new GUIListItem(Translation.InstalledExtensions) { });
      choices.Add(new GUIListItem(Translation.AllExtensions) { });
      
      int choice = GUIUtils.ShowMenuDialog(Translation.DownloadUpdates, choices);
      if (choice < 0) return;

      bool installedExtensionsOnly = choice == 0;

      // setup progress dialog
      GUIDialogProgress progressDialog = (GUIDialogProgress)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_PROGRESS);
      progressDialog.Reset();
      progressDialog.DisplayProgressBar = true;
      progressDialog.ShowWaitCursor = false;
      progressDialog.DisableCancel(true);
      progressDialog.SetHeading(Translation.DownloadingUpdates);
      progressDialog.Percentage = 0;
      progressDialog.SetLine(1, string.Empty);
      progressDialog.SetLine(2, string.Empty);
      progressDialog.StartModal(GetID);
      
      // Download Main Update File
      progressDialog.SetLine(1, Translation.DownloadingExtensionIndex);
      GUIWindowManager.Process();

      string extensionIndex = DownloadManager.GetTempFilename();

      cancelDownloadCheck = false;
      bool success = _downloadManager.DownloadNow(UpdateIndexUrl, extensionIndex);
      if (!success)
      {
        progressDialog.Close();
        GUIUtils.ShowNotifyDialog(Translation.Error, Translation.ErrorDownloadingExtensionIndex);
        return;
      }     

      // Load Index List
      LoadExtensionIndex(extensionIndex);

      int downloadCounter = 0;

      List<string> onlineFiles = MpeInstaller.InstalledExtensions.GetUpdateUrls(new List<string>());
      if (!installedExtensionsOnly)
      {
        onlineFiles = MpeInstaller.KnownExtensions.GetUpdateUrls(onlineFiles);
        onlineFiles = MpeInstaller.GetInitialUrlIndex(onlineFiles);
      }
      
      foreach (string onlineFile in onlineFiles)
      {
        if (cancelDownloadCheck) break;

        // update progress
        int progress = Convert.ToInt32(((double)downloadCounter++ / (double)onlineFiles.Count) * 100.0);

        progressDialog.SetLine(1, Translation.DownloadingExtension);
        progressDialog.SetLine(2, string.Format(Translation.DownloadProgress, downloadCounter, onlineFiles.Count, progress));
        progressDialog.Percentage = progress;
        GUIWindowManager.Process();

        string tempFile = DownloadManager.GetTempFilename();
        if (_downloadManager.DownloadNow(onlineFile, tempFile))
        {
          Log.Info("[MPEI] Update info loaded from " + onlineFile);

          MpeInstaller.KnownExtensions.Add(GetUniquePack(tempFile));
          //MpeInstaller.KnownExtensions.Add(ExtensionCollection.Load(tempFile));
          //MpeInstaller.KnownExtensions.Hide(_setting.ShowOnlyStable, _setting.ShowOnlyCompatible);
          GenerateProperty();

          try
          {
            //MpeInstaller.Save();
            File.Delete(tempFile);
          }
          catch { }
        }

        if (progressDialog.IsCanceled)
        {
          Log.Info("[MPEI] Download Updates Cancelled.");
          break;
        }

      }

      MpeInstaller.KnownExtensions.Hide(_setting.ShowOnlyStable, _setting.ShowOnlyCompatible);
      MpeInstaller.Save();
      // close dialog
      progressDialog.Close();

      // update listing
      LoadDirectory(currentFolder);

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

      _askForRestart = false;

      GUIUtils.ShowNotifyDialog(Translation.Notification, Translation.ActionAdded, 2);
      LoadDirectory(currentFolder);
    }

    void NotifyRemoveUser()
    {
      GUIUtils.ShowNotifyDialog(Translation.Notification, Translation.ActionRemoved, 2);
      LoadDirectory(currentFolder);
    }

    void OnClick(int itemIndex)
    {
      GUIListItem item = facadeView.SelectedListItem;
      if (item == null) return;
      Category category = item.MusicTag as Category;
      if (category != null)
      {
        LoadDirectory(category.Id);
        return;
      }

      if (item.IsFolder)
      {
        selectedItemIndex = -1;
        LoadDirectory(item.Path);
      }
      else
      {
        SiteItems si = item.MusicTag as SiteItems;
        if (si != null)
        {
          GUIBackgroundTask.Instance.ExecuteInBackgroundAndCallback(() =>
          {
            return si.LoadInfo();
          },
          delegate(bool success, object result)
          {
            if (success && (bool)result)
            {
              // we can't click anything else until the current bg thread completes so this is safe
              item_OnItemSelected(item, facadeView);
              GUIInfo guiinfo = (GUIInfo)GUIWindowManager.GetWindow(804);
              guiinfo.SiteItem = si;
              guiinfo.queue = queue;
              guiinfo._askForRestart = _askForRestart;
              guiinfo.Package = null;
              GUIWindowManager.ActivateWindow(804);
            }
            else if (success && !(bool)result)
            {
              GUIUtils.ShowNotifyDialog(Translation.Error, Translation.ErrorExtensionInfo);
            }
          }, Translation.GetExtensionInfo, true);
        }
        else
        {
          PackageClass pk = item.MusicTag as PackageClass;
          GetPackageConfigFile(pk);
          item_OnItemSelected(item, facadeView);
          GUIInfo guiinfo = (GUIInfo) GUIWindowManager.GetWindow(804);
          guiinfo.SiteItem = null;
          guiinfo.queue = queue;
          guiinfo._askForRestart = _askForRestart;
          guiinfo.Package = pk;
          guiinfo.SettingsFile = pk.LocationFolder + "extension_settings.xml";
          GUIWindowManager.ActivateWindow(804);
        }
      }
    }

    void DownloadExtension(PackageClass packageClass)
    {
      string pak = packageClass.LocationFolder + packageClass.GeneralInfo.Id + ".mpe2";
      if (!File.Exists(pak) && !string.IsNullOrEmpty(packageClass.GeneralInfo.OnlineLocation))
        _downloadManager.AddToDownloadQueue(new DownLoadInfo
        {
          Destination = pak,
          ItemType = DownLoadItemType.Extension,
          Package = packageClass,
          Url = packageClass.GeneralInfo.OnlineLocation
        });
     
    }

    void GenerateProperty()
    {
      foreach (PackageClass item in MpeInstaller.InstalledExtensions.Items)
      {
        PackageClass update = MpeInstaller.KnownExtensions.GetUpdate(item);
        if (update != null)
        {
          GUIPropertyManager.SetProperty("#mpei." + item.GeneralInfo.Id.Replace("-", "_") + ".haveupdate", "true");
          GUIPropertyManager.SetProperty("#mpei." + item.GeneralInfo.Id.Replace("-", "_") + ".updatelog", update.GeneralInfo.VersionDescription);
          GUIPropertyManager.SetProperty("#mpei." + item.GeneralInfo.Id.Replace("-", "_") + ".updatedate",update.GeneralInfo.ReleaseDate.ToShortDateString());
          GUIPropertyManager.SetProperty("#mpei." + item.GeneralInfo.Id.Replace("-", "_") + ".updateversion", update.GeneralInfo.Version.ToString());
        }
        else
        {
          GUIPropertyManager.SetProperty("#mpei." + item.GeneralInfo.Id.Replace("-", "_") + ".haveupdate", "false");
          GUIPropertyManager.SetProperty("#mpei." + item.GeneralInfo.Id.Replace("-", "_") + ".updatelog", " ");
          GUIPropertyManager.SetProperty("#mpei." + item.GeneralInfo.Id.Replace("-", "_") + ".updatedate", " ");
          GUIPropertyManager.SetProperty("#mpei." + item.GeneralInfo.Id.Replace("-", "_") + ".updateversion", " ");
        }
        GUIPropertyManager.SetProperty("#mpei." + item.GeneralInfo.Id.Replace("-", "_") + ".installedversion", item.GeneralInfo.Version.ToString());
        GUIPropertyManager.SetProperty("#mpei." + item.GeneralInfo.Id.Replace("-", "_") + ".isinstalled", "true");
      }

      foreach (PackageClass item in MpeInstaller.KnownExtensions.GetUniqueList().Items)
      {
        GUIPropertyManager.SetProperty("#mpei." + item.GeneralInfo.Id.Replace("-", "_") + ".name", item.GeneralInfo.Name);
        GUIPropertyManager.SetProperty("#mpei." + item.GeneralInfo.Id.Replace("-", "_") + ".author", item.GeneralInfo.Author);
      }

      string s = "";
      GUIPropertyManager.SetProperty("#mpei.updates", " ");
      foreach (PackageClass update in GetUpdates())
      {
        s += update.GeneralInfo.Name + " - " + update.GeneralInfo.Version.ToString() + ".::.";
      }
      if(!string.IsNullOrEmpty(s))
      {
        s = Translation.NewUpdates + " : " + s;
        GUIPropertyManager.SetProperty("#mpei.updates", s);
      }

      s = "";
      GUIPropertyManager.SetProperty("#mpei.newextensions", " ");
      foreach (PackageClass pk in MpeInstaller.KnownExtensions.GetUniqueList().Items)
      {
        if (DateTime.Now.Subtract(pk.GeneralInfo.ReleaseDate).Days < newdays)
        {
          s += pk.GeneralInfo.Name + " - " + pk.GeneralInfo.Version.ToString() + ".::.";
        }
      }
      if (!string.IsNullOrEmpty(s))
      {
        s = Translation.NewExtensions + " : " + s;
        GUIPropertyManager.SetProperty("#mpei.newextensions", s);
      }

    }

    void ShowInstall(PackageClass pk)
    {
      GUIDialogMenu dlg = (GUIDialogMenu)GUIWindowManager.GetWindow((int)Window.WINDOW_DIALOG_MENU);
      if (dlg == null) return;
      dlg.Reset();
      dlg.SetHeading(Translation.SelectVersionToInstall); 
      ExtensionCollection paks = MpeInstaller.KnownExtensions.GetList(pk.GeneralInfo.Id);
      foreach (PackageClass item in paks.Items)
      {
        GUIListItem guiListItem = new GUIListItem(item.GeneralInfo.Version.ToString());
        if (MpeInstaller.InstalledExtensions.Get(item) != null)
          guiListItem.Selected = true;
        dlg.Add(guiListItem);
      }
      dlg.DoModal(GetID);
      if (dlg.SelectedId == -1) return;
    
      if (!Directory.Exists(paks.Items[dlg.SelectedId - 1].LocationFolder))
        Directory.CreateDirectory(paks.Items[dlg.SelectedId - 1].LocationFolder);

      DownloadExtension(paks.Items[dlg.SelectedId - 1]);
      queue.Add(new QueueCommand(paks.Items[dlg.SelectedId - 1], CommandEnum.Install));
      NotifyUser();
    }

    void OnShowSort()
    {
      GUIDialogMenu dlg = (GUIDialogMenu)GUIWindowManager.GetWindow((int)Window.WINDOW_DIALOG_MENU);
      if (dlg == null) return;
      dlg.Reset();
      dlg.SetHeading(495); // Sort options

      dlg.AddLocalizedString(103); // name
      //dlg.AddLocalizedString(668); // Type
      dlg.AddLocalizedString(104); // date
      //dlg.AddLocalizedString(14016); // download
      //dlg.AddLocalizedString(14017); // rate

      dlg.SelectedLabel = (int)currentSortMethod;

      // show dialog and wait for result
      dlg.DoModal(GetID);
      if (dlg.SelectedId == -1) return;

      switch (dlg.SelectedId)
      {
        case 103:
          currentSortMethod = SortMethod.Name;
          break;
        case 668:
          currentSortMethod = SortMethod.Type;
          break;
        case 104:
          currentSortMethod = SortMethod.Date;
          break;
        case 14016:
          currentSortMethod = SortMethod.Download;
          break;
        case 14017:
          currentSortMethod = SortMethod.Rating;
          break;
        default:
          currentSortMethod = SortMethod.Name;
          break;
      }

      OnSort();
      if (btnSortBy != null)
        GUIControl.FocusControl(GetID, btnSortBy.GetID);
    }

    void OnShowViews()
    {
      GUIDialogMenu dlg = (GUIDialogMenu)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_MENU);
      if (dlg == null) return;
      dlg.Reset();
      dlg.SetHeading(Translation.Views); // Sort options
      if (MpeInstaller.InstalledExtensions.Items.Count > 0)
      {
        dlg.Add(Translation.InstalledExtensions); // local
      }
      if (MpeInstaller.KnownExtensions.Items.Count > 0)
      {
        dlg.Add(Translation.OnlineExtensions); // online
      }
      if (GetUpdates().Count > 0)
        dlg.Add(Translation.Updates); // updates
      
      if(queue.Items.Count>0)
        dlg.Add(Translation.Actions);

      dlg.Add(Translation.NewExtensions); // new
      dlg.Add(Translation.MPOnlineExtensions); // mp online
      dlg.Add(Translation.Compatibility);

      dlg.SelectedLabel = (int)currentListing;

      // show dialog and wait for result
      dlg.DoModal(GetID);
      if (dlg.SelectedId == -1) return;
        
      if (dlg.SelectedLabelText == Translation.InstalledExtensions)
        currentListing = Views.Local;

      if (dlg.SelectedLabelText == Translation.OnlineExtensions)
        currentListing = Views.Online;

      if (dlg.SelectedLabelText == Translation.Updates)
        currentListing = Views.Updates;

      if (dlg.SelectedLabelText == Translation.NewExtensions)
        currentListing = Views.New;
      
      if (dlg.SelectedLabelText == Translation.Actions)
        currentListing = Views.Queue;

      if (dlg.SelectedLabelText == Translation.MPOnlineExtensions)
          currentListing = Views.MpSite;

        if (dlg.SelectedLabelText == Translation.Compatibility)
            currentListing = Views.Compatibility;

      ClearProperties();
      LoadDirectory(string.Empty);
    }

    #endregion

    #region Sort Members
    void OnSort()
    {
      SetLabels();
      facadeView.Sort(this);
      UpdateButtonStates();
      SelectCurrentItem();
    }

    public int Compare(GUIListItem item1, GUIListItem item2)
    {
      if (currentListing == Views.MpSite)
        return -1;
      if (item1 == item2) return 0;
      if (item1 == null) return -1;
      if (item2 == null) return -1;
      if (item1.IsFolder && item1.Label == "..") return -1;
      if (item2.IsFolder && item2.Label == "..") return -1;
      if (item1.IsFolder && !item2.IsFolder) return -1;
      else if (!item1.IsFolder && item2.IsFolder) return 1;
      

      SortMethod method = currentSortMethod;
      bool bAscending = sortAscending;
      PackageClass pk1 = item1.MusicTag as PackageClass;
      PackageClass pk2 = item2.MusicTag as PackageClass;
      
      if(pk1==null && pk2==null)
      {
        if (item1.Label == Translation.All)
          return -1;
        if (item2.Label == Translation.All)
          return -1;
        return item1.Label.CompareTo(item2.Label);
      }

      switch (method)
      {
        case SortMethod.Name:
          if (bAscending)
          {
            return String.Compare(item1.Label, item2.Label, true);
          }
          else
          {
            return String.Compare(item2.Label, item1.Label, true);
          }

        case SortMethod.Type:
          //if (bAscending)
          //{
          //  return String.Compare(pk1.InstallerInfo.Group, pk2.InstallerInfo.Group, true);
          //}
          //else
          //{
          //  return String.Compare(pk2.InstallerInfo.Group, pk1.InstallerInfo.Group, true);
          //}
          return 0;
        case SortMethod.Date:
          if (bAscending)
          {
            return DateTime.Compare(pk1.GeneralInfo.ReleaseDate, pk2.GeneralInfo.ReleaseDate);
          }
          else
          {
            return DateTime.Compare(pk2.GeneralInfo.ReleaseDate, pk1.GeneralInfo.ReleaseDate);
          }
        case SortMethod.Download:
          //if (bAscending)
          //{
          //  return pk1.DownloadCount - pk2.DownloadCount;
          //}
          //else
          //{
          //  return pk2.DownloadCount - pk1.DownloadCount;
          //}
          return 0;
        case SortMethod.Rating:
          //if (bAscending)
          //{
          //  return (int)pk1.VoteValue - (int)pk2.VoteValue;
          //}
          //else
          //{
          //  return (int)pk2.VoteValue - (int)pk1.VoteValue;
          //}
          return 0;
      }
      return 0;
    }
    #endregion

    #region helper func's

    void LoadDirectory(string strNewDirectory)
    {
      if (GUIWindowManager.ActiveWindow != GetID) return;

      selectedItemIndex = facadeView.SelectedListItemIndex;

      GUIControl.ClearControl(GetID, facadeView.GetID);

      //------------
      switch (currentListing)
      {
        case Views.Local:
          {
            GUIPropertyManager.SetProperty("#MPE.View.Name", Translation.InstalledExtensions);
            GUIListItem item = new GUIListItem();            
            foreach (PackageClass pk in MpeInstaller.InstalledExtensions.Items)
            {
              item = new GUIListItem();
              item.IconImage = "defaultExtension.png";
              item.IconImageBig = "defaultExtensionBig.png";
              item.ThumbnailImage = "defaultExtensionBig.png";
              item.MusicTag = pk;
              item.IsFolder = false;
              item.Label = pk.GeneralInfo.Name;
              item.Label2 = pk.GeneralInfo.Version.ToString();
              SetLogo(pk, item);
              item.OnItemSelected += item_OnItemSelected;
              facadeView.Add(item);
            }
          }
          FinializeDirectory(strNewDirectory);
          break;
        case Views.Online:
          {
            if (string.IsNullOrEmpty(strNewDirectory))
            {
              GUIPropertyManager.SetProperty("#MPE.View.Name", Translation.OnlineExtensions);
              GUIListItem item = new GUIListItem();
              item.Label = Translation.All;
              item.Path = item.Label;
              item.IsFolder = true;
              item.MusicTag = null;
              item.ThumbnailImage = string.Empty;
              Utils.SetDefaultIcons(item);
              item.OnItemSelected += item_OnItemSelected;
              facadeView.Add(item);

              Dictionary<string, int> TagList = new Dictionary<string, int>();

              foreach (PackageClass pak in MpeInstaller.KnownExtensions.GetUniqueList().Items)
              {
                foreach (var tag in pak.GeneralInfo.TagList.Tags)
                {
                  if (!TagList.ContainsKey(tag))
                    TagList.Add(tag, 1);
                  else
                    TagList[tag]++;
                }
              }
              foreach (KeyValuePair<string, int> tagList in TagList)
              {
                if (tagList.Value > 1)
                {
                  item = new GUIListItem();
                  item.IconImage = "defaultExtension.png";
                  item.IconImageBig = "defaultExtensionBig.png";
                  item.ThumbnailImage = "defaultExtensionBig.png";
                  item.Label = tagList.Key;
                  item.Path = tagList.Key;
                  item.IsFolder = true;
                  item.MusicTag = null;
                  item.ThumbnailImage = string.Empty;
                  Utils.SetDefaultIcons(item);
                  facadeView.Add(item);
                }
              }
            }
            else
            {
              GUIPropertyManager.SetProperty("#MPE.View.Name", Translation.OnlineExtensions + ": " + strNewDirectory);
              GUIListItem item = new GUIListItem();
              item.Label = "..";
              item.Path = string.Empty;
              item.IsFolder = true;
              item.MusicTag = null;
              item.ThumbnailImage = string.Empty;
              item.OnItemSelected += item_OnBackSelected;
              Utils.SetDefaultIcons(item);
              facadeView.Add(item);
              foreach (PackageClass pk in MpeInstaller.KnownExtensions.GetUniqueList().Items)
              {
                if ((pk.GeneralInfo.TagList.Tags.Contains(strNewDirectory) || strNewDirectory == Translation.All))
                {
                  item = new GUIListItem();
                  item.IconImage = "defaultExtension.png";
                  item.IconImageBig = "defaultExtensionBig.png";
                  item.ThumbnailImage = "defaultExtensionBig.png";
                  item.MusicTag = pk;
                  item.IsFolder = false;
                  item.Label = pk.GeneralInfo.Name;
                  item.Label2 = pk.GeneralInfo.Version.ToString();
                  SetLogo(pk, item);
                  item.OnItemSelected += item_OnItemSelected;
                  facadeView.Add(item);
                }
              }
            }
          }
          FinializeDirectory(strNewDirectory);
          break;
        case Views.Updates:
          {
            GUIPropertyManager.SetProperty("#MPE.View.Name", Translation.Updates);
            GUIListItem item = new GUIListItem();
            foreach (PackageClass pk in GetUpdates())
            {
              item = new GUIListItem();
              item.IconImage = "defaultExtension.png";
              item.IconImageBig = "defaultExtensionBig.png";
              item.ThumbnailImage = "defaultExtensionBig.png";
              item.MusicTag = pk;
              item.IsFolder = false;
              item.Label = pk.GeneralInfo.Name;
              item.Label2 = pk.GeneralInfo.Version.ToString();
              SetLogo(pk, item);
              item.OnItemSelected += item_OnItemSelected;
              facadeView.Add(item);
            }            
          }

          if (facadeView.Count == 0)
          {
            currentListing = Views.Local;
            LoadDirectory(string.Empty);
          }

          FinializeDirectory(strNewDirectory);
          break;
        case Views.New:
          {
            GUIPropertyManager.SetProperty("#MPE.View.Name", Translation.NewExtensions);
            GUIListItem item = new GUIListItem();
            foreach (PackageClass pk in MpeInstaller.KnownExtensions.GetUniqueList().Items)
            {
              if (DateTime.Now.Subtract(pk.GeneralInfo.ReleaseDate).Days < newdays)
              {
                item = new GUIListItem();
                item.IconImage = "defaultExtension.png";
                item.IconImageBig = "defaultExtensionBig.png";
                item.ThumbnailImage = "defaultExtensionBig.png";
                item.MusicTag = pk;
                item.IsFolder = false;
                item.Label = pk.GeneralInfo.Name;
                item.Label2 = pk.GeneralInfo.Version.ToString();
                SetLogo(pk, item);
                item.OnItemSelected += item_OnItemSelected;
                facadeView.Add(item);
              }
            }
          }
          FinializeDirectory(strNewDirectory);
          break;
        case Views.Queue:
          {
            GUIPropertyManager.SetProperty("#MPE.View.Name", Translation.Actions);
            GUIListItem item = new GUIListItem();
            foreach (QueueCommand command in queue.Items)
            {
              PackageClass pk = MpeInstaller.KnownExtensions.Get(command.TargetId, command.TargetVersion.ToString());
              if (pk != null)
              {
                item = new GUIListItem();
                item.IconImage = "defaultExtension.png";
                item.IconImageBig = "defaultExtensionBig.png";
                item.ThumbnailImage = "defaultExtensionBig.png";
                item.MusicTag = pk;
                item.IsFolder = false;
                item.Label = pk.GeneralInfo.Name+" - "+command.CommandEnum.ToString();
                item.Label2 = pk.GeneralInfo.Version.ToString();
                SetLogo(pk, item);
                item.OnItemSelected += item_OnItemSelected;
                facadeView.Add(item);
              }
            }
          }

          if (facadeView.Count == 0)
          {
            currentListing = Views.Local;
            LoadDirectory(string.Empty);
          }

          FinializeDirectory(strNewDirectory);
          break;
        case Views.MpSite:
          {
            GUIPropertyManager.SetProperty("#MPE.View.Name", Translation.MPOnlineExtensions);
            GUIListItem item = new GUIListItem();
            List<Category> categories = new List<Category>();

            Category parentCategory = SiteUtil.GetCat(strNewDirectory);

            if (string.IsNullOrEmpty(strNewDirectory) || strNewDirectory == "0" || parentCategory == null)
            {
              GUIBackgroundTask.Instance.ExecuteInBackgroundAndCallback(() =>
              {
                // this only goes online once unless there was an error on previous try
                return SiteUtil.LoadCatTree();
              },
              delegate(bool success, object result)
              {
                if (success && (bool)result)
                {
                  // get the root categories once list is retrieved
                  categories = SiteUtil.GetCats("0");
                }
                else if (success && !(bool)result)
                {
                  GUIUtils.ShowNotifyDialog(Translation.Error, Translation.ErrorLoadingSite);
                }

                // if its a success or not, doesn't matter
                LoadMPSiteDirectory(strNewDirectory, parentCategory, categories);

              }, Translation.GetCategories, true);

              return;
            }
            else
            {
              // create back item
              Category cate = new Category() { Name = "..", Id = parentCategory.PId };
              item = new GUIListItem();
              item.MusicTag = cate;
              item.IsFolder = true;
              item.Label = cate.Name;
              item.Label2 = "";
              item.OnItemSelected += item_OnItemSelected;
              Utils.SetDefaultIcons(item);
              facadeView.Add(item);

              // get sub-categories
              categories = SiteUtil.GetCats(strNewDirectory);
              LoadMPSiteDirectory(strNewDirectory, parentCategory, categories);
            }            
          }
          break;
        case Views.Compatibility:
          {
              MPRelease selectedMP = new MPRelease();
              if (NewReleases.Count > 0)
              {
                  selectedMP = NewReleases[0];
              }
              else
              {
                  List<GUIListItem> Versions = new List<GUIListItem>();
                  foreach (MPRelease mp in UpgradeAdvisor.AllReleases)
                  {
                      GUIListItem item = new GUIListItem();
                      item.Label = mp.DisplayedVersion;
                      item.Label2 = mp.Version.ToString();
                      Versions.Add(item);
                  }
                  int selection = GUIUtils.ShowMenuDialog(Translation.SelectMPVersion, Versions);
                  selectedMP = UpgradeAdvisor.AllReleases[selection];
              }
              GUIPropertyManager.SetProperty("#MPE.View.Name", string.Format("{0} with MediaPortal {1}", Translation.Compatibility, selectedMP.DisplayedVersion));
              foreach (PackageClass pk in MpeInstaller.InstalledExtensions.Items)
              {
                  GUIListItem item = new GUIListItem();
                  item.IconImage = "defaultExtension.png";
                  item.IconImageBig = "defaultExtensionBig.png";
                  item.ThumbnailImage = "defaultExtensionBig.png";
                  item.MusicTag = pk;
                  item.IsFolder = false;
                  item.Label = pk.GeneralInfo.Name;
                  item.Label2 = UpgradeAdvisor.GetCompatiblePluginVersion(pk, selectedMP.Version);
                  SetLogo(pk, item);
                  item.OnItemSelected += item_OnItemSelected;
                  facadeView.Add(item);
              }
          }
          FinializeDirectory(strNewDirectory);
          break;
      }

    }

    void LoadMPSiteDirectory(string strNewDirectory, Category parentCategory, List<Category> categories)
    {
      GUIListItem item = new GUIListItem();

      // display sub-categories
      if (categories.Count > 0)
      {
        foreach (Category category in categories.Where(c => c.Number != "0"))
        {
          item = new GUIListItem();
          item.MusicTag = category;
          item.IsFolder = true;
          item.Label = category.Name;
          item.Label2 = category.Number;
          item.OnItemSelected += item_OnItemSelected;
          Utils.SetDefaultIcons(item);
          facadeView.Add(item);
        }
      }

      if (parentCategory != null)
      {
        // get extensions
        if (parentCategory.SiteItems.Count == 0 && !parentCategory.Updated)
        {
          GUIBackgroundTask.Instance.ExecuteInBackgroundAndCallback(() =>
          {
            return SiteUtil.LoadItems(parentCategory);
          },
          delegate(bool success, object result)
          {
            if (success && (bool)result)
            {
              parentCategory.Updated = true;
              LoadExtensionDirectory(parentCategory);
            }
            else if (success && !(bool)result)
            {
              GUIUtils.ShowNotifyDialog(Translation.Error, Translation.ErrorLoadingExtensionList);
            }

            FinializeDirectory(strNewDirectory);
            return;

          }, Translation.GetCategories, true);
        }
        else
        {
          LoadExtensionDirectory(parentCategory);
          FinializeDirectory(strNewDirectory);
        }
      }
      else
      {
        FinializeDirectory(strNewDirectory);
      }
    }

    void LoadExtensionDirectory(Category parentCategory)
    {
      GUIListItem item = new GUIListItem();

      foreach (SiteItems siteItem in parentCategory.SiteItems)
      {
        item = new GUIListItem();
        item.IconImage = "defaultExtension.png";
        item.IconImageBig = "defaultExtensionBig.png";
        item.ThumbnailImage = "defaultExtensionBig.png";
        item.MusicTag = siteItem;
        item.IsFolder = false;
        item.Label = siteItem.Name;
        SetLogo(siteItem, item);
        item.OnItemSelected += item_OnItemSelected;
        facadeView.Add(item);
      }
    }

    void FinializeDirectory(string strNewDirectory)
    {
      int itemCount = facadeView.Count;
      if (itemCount > 0 && facadeView[0].Label == "..") itemCount--;

      GUIPropertyManager.SetProperty("#itemcount", itemCount.ToString());
      SetLabels();
      SwitchLayout();
      OnSort();
      SelectCurrentItem();

      //set selected item
      if (selectedItemIndex >= 0 && currentFolder == strNewDirectory)
        GUIControl.SelectItemControl(GetID, facadeView.GetID, selectedItemIndex);

      // set focus on view button if no items.
      if (facadeView.Count == 0)
        GUIControl.FocusControl(GetID, btnViews.GetID);

      currentFolder = strNewDirectory;
    }

    void SetLogo(SiteItems items, GUIListItem listItem)
    {
        string tempFile = Path.Combine(Path.GetTempPath(), Utils.EncryptLine(items.LogoUrl));
        if (File.Exists(tempFile))
        {
            listItem.IconImage = tempFile;
            listItem.IconImageBig = tempFile;
            listItem.ThumbnailImage = tempFile;
        }
        else
        {
            _downloadManager.AddToDownloadQueue(new DownLoadInfo()
            {
                Destination = tempFile,
                ItemType = DownLoadItemType.Logo,
                ListItem = listItem,
                SiteItem = items,
                Url = items.LogoUrl
            });
        }
    }

    void SetLogo(PackageClass packageClass, GUIListItem listItem)
    {
        string extensionIcon = string.Empty;
        if (Directory.Exists(packageClass.LocationFolder))
        {
            if (File.Exists(packageClass.LocationFolder + "icon.png"))
                extensionIcon = packageClass.LocationFolder + "icon.png";
        }

        #region Update Extension
        // is there an update available ?
        if (MpeInstaller.KnownExtensions.GetUpdate(packageClass) != null)
        {
            string updateIcon = string.Empty;
            if (File.Exists(GUIGraphicsContext.Skin + @"\media\extension_update.png"))
                updateIcon = GUIGraphicsContext.Skin + @"\media\extension_update.png";

            // if there is no logo file available for extension then just show the update icon
            // if the skin does not have a update icon then it will just show nothing.
            if (string.IsNullOrEmpty(extensionIcon))
            {
                listItem.IconImage = updateIcon;
                listItem.IconImageBig = updateIcon;
                listItem.ThumbnailImage = updateIcon;
                return;
            }

            // if we don't have an update icon just show the standard extension icon
            if (string.IsNullOrEmpty(updateIcon))
            {
                Log.Warn("[MPEI] Unable to add update icon to extension '{0}', skin does not have 'extension_update.png' in media folder.", packageClass.GeneralInfo.Name);
                listItem.IconImage = extensionIcon;
                listItem.IconImageBig = extensionIcon;
                listItem.ThumbnailImage = extensionIcon;
                return;
            }

            // if we have an update icon, overlay on extension icon
            if (!string.IsNullOrEmpty(updateIcon))
            {
                string tempIconFile = Path.Combine(Path.GetTempPath(), Utils.EncryptLine("Update" + packageClass.GeneralInfo.Id + packageClass.GeneralInfo.Version));
                if (!File.Exists(tempIconFile))
                {
                    try
                    {
                        Graphics gResult = null;
                        Image imgBackground = Image.FromFile(extensionIcon);
                        Image imgForeground = Image.FromFile(updateIcon);

                        gResult = System.Drawing.Graphics.FromImage(imgBackground);
                        gResult.DrawImage(imgForeground, 0, (imgBackground.Height / 2) - 1, imgBackground.Width / 2, imgBackground.Height / 2);
                        gResult.Save();
                        imgBackground.Save(tempIconFile, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    catch (Exception e)
                    {
                        Log.Error("[MPEI] Failed to create temp update icon '{0}' for extension '{1}': '{2}'.", tempIconFile, packageClass.GeneralInfo.Name, e.Message);
                    }
                }

                listItem.IconImage = tempIconFile;
                listItem.IconImageBig = tempIconFile;
                listItem.ThumbnailImage = tempIconFile;
                return;
            }

            return;
        }
        #endregion

        #region Queue Extension
        // is extension in the queue for a action
        if (queue.Get(packageClass.GeneralInfo.Id) != null)
        {
            string queueIcon = string.Empty;
            if (File.Exists(GUIGraphicsContext.Skin + @"\media\extension_action.png"))
                queueIcon = GUIGraphicsContext.Skin + @"\media\extension_action.png";

            // if there is no logo file available for extension then just show the action icon
            // if the skin does not have an update icon then it will just show nothing.
            if (string.IsNullOrEmpty(extensionIcon))
            {
                listItem.IconImage = queueIcon;
                listItem.IconImageBig = queueIcon;
                listItem.ThumbnailImage = queueIcon;
                return;
            }

            // if we don't have an queue icon just show the standard extension icon
            if (string.IsNullOrEmpty(queueIcon))
            {
                Log.Warn("[MPEI] Unable to add queue icon to extension '{0}', skin does not have 'extension_action.png' in media folder.", packageClass.GeneralInfo.Name);
                listItem.IconImage = extensionIcon;
                listItem.IconImageBig = extensionIcon;
                listItem.ThumbnailImage = extensionIcon;
                return;
            }

            string tempIconFile = Path.Combine(Path.GetTempPath(), Utils.EncryptLine("Action" + packageClass.GeneralInfo.Id + packageClass.GeneralInfo.Version));
            if (!File.Exists(tempIconFile))
            {
                try
                {
                    Graphics gResult = null;
                    Image imgBackground = Image.FromFile(extensionIcon);
                    Image imgForeground = Image.FromFile(queueIcon);

                    gResult = System.Drawing.Graphics.FromImage(imgBackground);
                    gResult.DrawImage(imgForeground, 0, (imgBackground.Height / 2) - 1, imgBackground.Width / 2, imgBackground.Height / 2);
                    gResult.Save();
                    imgBackground.Save(tempIconFile, System.Drawing.Imaging.ImageFormat.Png);
                }
                catch (Exception e)
                {
                    Log.Error("[MPEI] Failed to create temp queue icon '{0}' for extension '{1}': '{2}'.", tempIconFile, packageClass.GeneralInfo.Name, e.Message);
                }

                listItem.IconImage = tempIconFile;
                listItem.IconImageBig = tempIconFile;
                listItem.ThumbnailImage = tempIconFile;
                return;
            }
            return;
        }
        #endregion

        // no overlay needed on icon, just show the extension icon
        if (File.Exists(extensionIcon))
        {
            listItem.IconImage = extensionIcon;
            listItem.IconImageBig = extensionIcon;
            listItem.ThumbnailImage = extensionIcon;
        }
        else
        {
            extensionIcon = packageClass.LocationFolder + "icon.png";
            string url = packageClass.GeneralInfo.Params[ParamNamesConst.ONLINE_ICON].Value;
            if (!string.IsNullOrEmpty(url))
            {
                _downloadManager.AddToDownloadQueue(new DownLoadInfo()
                {
                    Destination = extensionIcon,
                    ItemType = DownLoadItemType.Logo,
                    ListItem = listItem,
                    Package = packageClass,
                    Url = url
                });
            }
        }
        return;
    }

    void ClearProperties()
    {
      GUIUtils.SetProperty("#MPE.Selected.Id", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.Name", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.Version", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.Author", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.Description", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.VersionDescription", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.ReleaseDate", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.Icon", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.JustAded", "false");
      GUIUtils.SetProperty("#MPE.Selected.JustAdded", "false");
      GUIUtils.SetProperty("#MPE.Selected.Popular", "false");
      GUIUtils.SetProperty("#MPE.Selected.DeveloperPick", "false");
      GUIUtils.SetProperty("#MPE.Selected.isinstalled", "false");
      GUIUtils.SetProperty("#MPE.Selected.haveupdate", "false");
      GUIUtils.SetProperty("#MPE.Selected.installedversion", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.updatelog", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.updatedate", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.updateversion", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.Downloads", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.Hits", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.Rating", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.Status", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.Votes", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.DateAdded", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.CompatibileVersions", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.CompatibleVersions", string.Empty);

      GUIUtils.SetProperty("#selectedthumb", string.Empty);
    }

    void item_OnBackSelected(GUIListItem item, GUIControl parent)
    {
      // clear selection properties
      ClearProperties();
    }

    void item_OnItemSelected(GUIListItem item, GUIControl parent)
    {
      SiteItems siteItem = item.MusicTag as SiteItems;
      if (siteItem != null)
      {
        GUIUtils.SetProperty("#MPE.Selected.Id", string.Empty);
        GUIUtils.SetProperty("#MPE.Selected.Name", siteItem.Name);
        GUIUtils.SetProperty("#MPE.Selected.Version", siteItem.Version);
        GUIUtils.SetProperty("#MPE.Selected.Author", siteItem.Author);
        GUIUtils.SetProperty("#MPE.Selected.Description", siteItem.Descriptions);
        GUIUtils.SetProperty("#MPE.Selected.VersionDescription", string.Empty);
        GUIUtils.SetProperty("#MPE.Selected.ReleaseDate", siteItem.DateUpdated == "Never" ? siteItem.DateAdded : siteItem.DateUpdated);
        GUIUtils.SetProperty("#MPE.Selected.Icon", item.IconImageBig);
        GUIUtils.SetProperty("#MPE.Selected.JustAded", siteItem.JustAdded ? "true" : "false");
        GUIUtils.SetProperty("#MPE.Selected.JustAdded", siteItem.JustAdded ? "true" : "false");
        GUIUtils.SetProperty("#MPE.Selected.Popular", siteItem.Popular ? "true" : "false");
        GUIUtils.SetProperty("#MPE.Selected.DeveloperPick", siteItem.EditorPick ? "true" : "false");
        GUIUtils.SetProperty("#MPE.Selected.Downloads", siteItem.Downloads);
        GUIUtils.SetProperty("#MPE.Selected.Hits", siteItem.Hits);
        GUIUtils.SetProperty("#MPE.Selected.Rating", siteItem.Rating);
        GUIUtils.SetProperty("#MPE.Selected.Status", siteItem.Status);
        GUIUtils.SetProperty("#MPE.Selected.Votes", siteItem.Votes);
        GUIUtils.SetProperty("#MPE.Selected.DateAdded", siteItem.DateAdded);
        GUIUtils.SetProperty("#MPE.Selected.CompatibileVersions", siteItem.CompatibleVersions);
        GUIUtils.SetProperty("#MPE.Selected.CompatibleVersions", siteItem.CompatibleVersions);

        GUIUtils.SetProperty("#selectedthumb", item.IconImageBig);
        return;
      }

      GUIUtils.SetProperty("#MPE.Selected.JustAded", "false");
      GUIUtils.SetProperty("#MPE.Selected.JustAdded", "false");
      GUIUtils.SetProperty("#MPE.Selected.Popular", "false");
      GUIUtils.SetProperty("#MPE.Selected.DeveloperPick", "false");
      GUIUtils.SetProperty("#MPE.Selected.Downloads", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.Hits", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.Rating", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.Status", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.Votes", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.DateAdded", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.CompatibileVersions", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.CompatibleVersions", string.Empty);

      PackageClass pak = item.MusicTag as PackageClass;
      if (pak != null)
      {
        PackageClass update = MpeInstaller.KnownExtensions.GetUpdate(pak);
        if (update != null)
        {
          GUIUtils.SetProperty("#MPE.Selected.haveupdate", "true");
          GUIUtils.SetProperty("#MPE.Selected.updatelog", update.GeneralInfo.VersionDescription);
          GUIUtils.SetProperty("#MPE.Selected.updatedate", update.GeneralInfo.ReleaseDate.ToShortDateString());
          GUIUtils.SetProperty("#MPE.Selected.updateversion", update.GeneralInfo.Version.ToString());
        }
        else
        {
          GUIUtils.SetProperty("#MPE.Selected.haveupdate", "false");
          GUIUtils.SetProperty("#MPE.Selected.updatelog", string.Empty);
          GUIUtils.SetProperty("#MPE.Selected.updatedate", string.Empty);
          GUIUtils.SetProperty("#MPE.Selected.updateversion", string.Empty);
        }

        PackageClass installed = MpeInstaller.InstalledExtensions.Get(pak);
        if (installed != null)
        {
          GUIUtils.SetProperty("#MPE.Selected.installedversion", installed.GeneralInfo.Version.ToString());
          GUIUtils.SetProperty("#MPE.Selected.isinstalled", "true");
        }
        else
        {
          GUIUtils.SetProperty("#MPE.Selected.installedversion", string.Empty);
          GUIUtils.SetProperty("#MPE.Selected.isinstalled", "false");
        }

        GUIUtils.SetProperty("#MPE.Selected.Id", pak.GeneralInfo.Id);
        GUIUtils.SetProperty("#MPE.Selected.Name", pak.GeneralInfo.Name);
        GUIUtils.SetProperty("#MPE.Selected.Version", pak.GeneralInfo.Version.ToString());
        GUIUtils.SetProperty("#MPE.Selected.Author", pak.GeneralInfo.Author);
        GUIUtils.SetProperty("#MPE.Selected.Description", pak.GeneralInfo.ExtensionDescription);
        GUIUtils.SetProperty("#MPE.Selected.VersionDescription", pak.GeneralInfo.VersionDescription);
        GUIUtils.SetProperty("#MPE.Selected.ReleaseDate", pak.GeneralInfo.ReleaseDate.ToShortDateString());
        GUIUtils.SetProperty("#MPE.Selected.Status", pak.GeneralInfo.DevelopmentStatus);
        GUIUtils.SetProperty("#MPE.Selected.Icon", item.IconImageBig);
        GUIUtils.SetProperty("#selectedthumb", item.IconImageBig);
        
      }
      else
      {
        ClearProperties();
      }
    }

    void SelectCurrentItem()
    {
      int iItem = facadeView.SelectedListItemIndex;
      if (iItem > -1)
      {
        GUIControl.SelectItemControl(GetID, facadeView.GetID, iItem);
      }
      UpdateButtonStates();
    }

    void UpdateButtonStates()
    {
      facadeView.IsVisible = false;
      facadeView.IsVisible = true;
      //GUIControl.FocusControl(GetID, facadeView.GetID);

      string strLine = string.Empty;
      View view = currentLayout;
      switch (view)
      {
        case View.List:
          strLine = GUILocalizeStrings.Get(101);
          break;
        case View.Icons:
          strLine = GUILocalizeStrings.Get(100);
          break;
        case View.LargeIcons:
          strLine = GUILocalizeStrings.Get(417);
          break;
      }
      if (btnViewAs != null)
      {
        btnViewAs.Label = strLine;
      }

      switch (currentSortMethod)
      {
        case SortMethod.Name:
          strLine = GUILocalizeStrings.Get(103);
          break;
        case SortMethod.Type:
          strLine = GUILocalizeStrings.Get(668);
          break;
        case SortMethod.Date:
          strLine = GUILocalizeStrings.Get(104);
          break;
        case SortMethod.Download:
          strLine = GUILocalizeStrings.Get(14016);
          break;
        case SortMethod.Rating:
          strLine = GUILocalizeStrings.Get(14017);
          break;
      }
      if (btnSortBy != null)
      {
        btnSortBy.Label = strLine;
        btnSortBy.IsAscending = sortAscending;
      }

      btnRestart.Disabled = !(queue.Items.Count > 0);
    }

    void SwitchLayout()
    {
      switch (currentLayout)
      {
        case View.List:
          facadeView.CurrentLayout = GUIFacadeControl.Layout.List;
          break;
        case View.Icons:
          facadeView.CurrentLayout = GUIFacadeControl.Layout.SmallIcons;
          break;
        case View.LargeIcons:
          facadeView.CurrentLayout = GUIFacadeControl.Layout.LargeIcons;
          break;
      }
      UpdateButtonStates();
    }

    void SortChanged(object sender, SortEventArgs e)
    {
      sortAscending = e.Order != System.Windows.Forms.SortOrder.Descending;

      OnSort();
      UpdateButtonStates();

      GUIControl.FocusControl(GetID, ((GUIControl)sender).GetID);
    }

    void SetLabels()
    {
      SortMethod method = currentSortMethod;
      for (int i = 0; i < facadeView.Count; ++i)
      {
        GUIListItem item = facadeView[i];
        if (item.MusicTag != null)
        {
          PackageClass pak = item.MusicTag as PackageClass;
          if (pak != null)
          {
            switch (method)
            {
              case SortMethod.Name:
                //item.Label2 = pak.GeneralInfo.Version.ToString();
                break;
              case SortMethod.Type:
                //item.Label2 = pak.InstallerInfo.Group;
                break;
              case SortMethod.Date:
                item.Label2 = pak.GeneralInfo.ReleaseDate.ToShortDateString();
                break;
              case SortMethod.Download:
                //              item.Label2 = pak.DownloadCount.ToString();
                break;
              case SortMethod.Rating:
                //              item.Label2 =((int) pak.VoteValue).ToString();
                break;
              default:
                break;
            }
          }
          if (method == SortMethod.Name)
          {
          }
        }
      }
    }
    #endregion

  }
}