using MediaPortal.GUI.Library;
using MpeCore;
using MpeCore.Classes;
using MPEIPlugin.Classes;
using Action = MediaPortal.GUI.Library.Action;

namespace MPEIPlugin
{
  public class GUIInfo : GuiBase
  {
    [SkinControlAttribute(2)]
    protected GUIButtonControl btnInstall = null;
    [SkinControlAttribute(3)]
    protected GUIButtonControl btnUnInstall = null;
    [SkinControlAttribute(4)]
    protected GUIButtonControl btnUpdate = null;
    [SkinControlAttribute(5)]
    protected GUIButtonControl btnDisable = null;
    [SkinControlAttribute(6)]
    protected GUIButtonControl btnEnable = null;
    [SkinControlAttribute(7)]
    protected GUIButtonControl btnSettings = null;
    [SkinControlAttribute(8)]
    protected GUIButtonControl btnChangeLog = null;
    [SkinControlAttribute(9)]
    protected GUIButtonControl btnScreenShots = null;

    public string SettingsFile { get; set; }
    private ExtensionSettings settings = new ExtensionSettings();

    public PackageClass Package { get; set; }
    public GUIInfo()
    {
      GetID = 804;
    }

    public override string GetModuleName()
    {
      return Translation.NameInfo;
    }

    public override bool Init()
    {
      return Load(GUIGraphicsContext.Skin + @"\myextensions2info.xml");
    }

    protected override void OnPageLoad()
    {
      if (Package == null && SiteItem == null)
      {
        Log.Debug("[MPEI] Unable to Load Info window, Package/SiteItem not set!");
        GUIWindowManager.ShowPreviousWindow();
      }

      GUIPropertyManager.SetProperty("#MPE.Selected.HaveSettings", "false");
      GUIPropertyManager.SetProperty("#MPE.Selected.HaveScreenShots", "false");
      GUIPropertyManager.SetProperty("#MPE.Selected.IsEnabled", "false");
      GUIPropertyManager.SetProperty("#MPE.Selected.IsDisabled", "false");

      if (Package != null)
      {
        settings.Load(SettingsFile);
        PackageClass pak = MpeInstaller.InstalledExtensions.Get(Package);


        if (pak != null)
        {
          if (settings.Settings.Count == 0)
          {
              settings.LoadDefaultSettings(pak);
          }
          GUIPropertyManager.SetProperty("#MPE.Selected.HaveSettings", "true");

          if (!string.IsNullOrEmpty(pak.GeneralInfo.Params[ParamNamesConst.ONLINE_SCREENSHOT].Value.Trim()) && pak.GeneralInfo.Params[ParamNamesConst.ONLINE_SCREENSHOT].Value.Split(ParamNamesConst.SEPARATORS).Length > 0)
          {
              GUIPropertyManager.SetProperty("#MPE.Selected.HaveScreenShots", "true");
          }
        }
        SetDisableState();
        SetFocus();
      }

      if (SiteItem != null)
      {
        if (SiteItem.Images.Count > 0)
        {
          GUIPropertyManager.SetProperty("#MPE.Selected.HaveScreenShots", "true");
        }
      }
      base.OnPageLoad();
    }

    void SetFocus()
    {
      if (MpeInstaller.KnownExtensions.GetUpdate(Package) != null)
      {
        if (btnUpdate != null) GUIControl.FocusControl(GetID, btnUpdate.GetID);
      }
      else if (MpeInstaller.InstalledExtensions.Get(Package) != null)
      {
        if (btnUnInstall != null) GUIControl.FocusControl(GetID, btnUnInstall.GetID);
      }
      else
      {
        if (btnInstall != null) GUIControl.FocusControl(GetID, btnInstall.GetID);
      }
    }

    void SetDisableState()
    {
      if (!string.IsNullOrEmpty(settings.DisableSetting.Name))
      {
        if (settings.DisableSetting.Value == "yes")
        {
          GUIPropertyManager.SetProperty("#MPE.Selected.IsEnabled", "true");
          GUIPropertyManager.SetProperty("#MPE.Selected.IsDisabled", "false");
        }
        else
        {
          GUIPropertyManager.SetProperty("#MPE.Selected.IsEnabled", "false");
          GUIPropertyManager.SetProperty("#MPE.Selected.IsDisabled", "true");
        }
      }
    }

    protected override void OnPageDestroy(int new_windowId)
    {
      MediaPortal.Profile.Settings.SaveCache();
      base.OnPageDestroy(new_windowId);
    }

    protected override void OnClicked(int controlId, GUIControl control, Action.ActionType actionType)
    {
      base.OnClicked(controlId, control, actionType);

      if (control == btnUpdate)
      {
        UpdateExtension(Package.GeneralInfo.Id);
      }

      if (control == btnUnInstall)
      {
        UnInstallExtension(Package.GeneralInfo.Id);
      }

      if (control == btnSettings)
      {
        ConfigureExtension(Package.GeneralInfo.Id);
      }

      if (control == btnInstall)
      {
        if (Package != null)
          InstallExtension(Package.GeneralInfo.Id);
        else if (SiteItem != null)
          InstallExtension(SiteItem);        
      }

      if (control == btnChangeLog)
      {
        ShowChangeLog(Package);
      }

      if (control == btnScreenShots)
      {
        if (SiteItem != null)
          ShowSlideShow(SiteItem);
        else if (Package != null)
          ShowSlideShow(Package);
      }

      if (control == btnEnable)
      {
        settings.DisableSetting.Value = "yes";
        MediaPortal.Profile.Settings.SaveCache();
        SetDisableState();
        GUIWindowManager.Process();
        GUIControl.FocusControl(GetID, btnDisable.GetID);
      }

      if (control == btnDisable)
      {
        settings.DisableSetting.Value = "no";
        MediaPortal.Profile.Settings.SaveCache();
        SetDisableState();
        GUIWindowManager.Process();
        GUIControl.FocusControl(GetID, btnEnable.GetID);
      }
      
    }

  }
}
