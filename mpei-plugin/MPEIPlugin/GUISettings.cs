
using System;
using System.Collections.Generic;
//using System.Windows.Forms;
using System.IO;
using System.Text.RegularExpressions;
using MediaPortal.GUI.Library;
using MediaPortal.Dialogs;
using MpeCore;
using MPEIPlugin.Classes;
using Action = MediaPortal.GUI.Library.Action;

namespace MPEIPlugin
{
  public class GUISettings : GuiBase
  {
    [SkinControlAttribute(50)]
    protected GUIFacadeControl facadeView = null;
    [SkinControlAttribute(2)]
    protected GUIButtonControl btnSections = null;
    [SkinControlAttribute(3)]
    protected GUIButtonControl btnDefaults = null;

    public string SettingsFile { get; set; }
    private bool SettingsChanged { get; set; }
    private KeyValuePair<int,string> CurrentSection { get; set; }
    private string GUID { get; set; }
    private ExtensionSettings settings = new ExtensionSettings();

    public delegate void SettingsChangedHandler(string guid);
    public event SettingsChangedHandler OnSettingsChanged;

    public GUISettings()
    {
      GetID = 803;
    }

    public override string GetModuleName()
    {
      return Translation.NameSettings;
    }

    public override bool Init()
    {
      return Load(GUIGraphicsContext.Skin + @"\myextensions2settings.xml");
    }

    protected override void OnPageLoad()
    {
      ClearProperties();

      if (!string.IsNullOrEmpty(_loadParameter) && File.Exists(_loadParameter))
        SettingsFile = _loadParameter;

      if (SettingsFile == null)
      {
        Log.Debug("[MPEI] Unable to Load Settings File, file must exist!");
        GUIWindowManager.ShowPreviousWindow();
        return;
      }

      Match match = Regex.Match(SettingsFile, @"\\Installer\\V\d+\\(?<guid>[^\\]+)\\", RegexOptions.Singleline);
      if (match.Success) GUID = match.Groups["guid"].Value;

      // get package details (we may have jumped from external plugin so properties may not be loaded)
      PackageClass pk = MpeInstaller.KnownExtensions.Get(GUID);
        //MpeInstaller.KnownExtensions.Items
      SetProperties(pk);
      if (File.Exists(SettingsFile))
      {
          settings.Load(SettingsFile);
      }
      else
      {
          settings.LoadDefaultSettings(pk);
      }
      PopulateFacade(0);
      base.OnPageLoad();
    }

    void PopulateFacade(int section)
    {
      GUIControl.ClearControl(GetID, facadeView.GetID);
      if (settings.Settings.Count > 1)
        btnSections.Disabled = false;
      else
        btnSections.Disabled = true;
      int i = 0;
      foreach (KeyValuePair<string, List<ExtensionSetting>> settingsection in settings.Settings)
      {
        if (i == section)
        {
          CurrentSection = new KeyValuePair<int, string>(i, settingsection.Key);
          foreach (ExtensionSetting setting in settingsection.Value)
          {
            var item = new GUIListItem {Label = setting.DisplayName, Label2 = setting.DisplayValue, MusicTag = setting};
            item.OnItemSelected += new GUIListItem.ItemSelectedHandler(item_OnItemSelected);
            facadeView.Add(item);
          }
        }
        i++;
      }
      
      GUIControl.SelectItemControl(GetID, facadeView.GetID, 0);
      GUIPropertyManager.SetProperty("#itemcount", facadeView.Count.ToString());
    }

    protected override void OnPageDestroy(int new_windowId)
    {
      MediaPortal.Profile.Settings.SaveCache();

      // signal to plugins that property re-load should occur
      if (OnSettingsChanged != null && SettingsChanged)
        OnSettingsChanged(GUID);

      base.OnPageDestroy(new_windowId);
    }

    protected override void OnClicked(int controlId, GUIControl control, Action.ActionType actionType)
    {
      // mandatory control
      if (control == btnSections)
      {
        GUIDialogMenu dlg = (GUIDialogMenu)GUIWindowManager.GetWindow((int)Window.WINDOW_DIALOG_MENU);
        if (dlg == null) return;
        dlg.Reset();
        dlg.SetHeading(Translation.Sections);
        foreach (KeyValuePair<string, List<ExtensionSetting>> settingsection in settings.Settings)
        {
          dlg.Add(settingsection.Key);
        }
        dlg.DoModal(GetID);
        if (dlg.SelectedId == -1) return;
        PopulateFacade(dlg.SelectedId - 1);
        GUIControl.FocusControl(GetID, facadeView.GetID);
        return;
      }

      // non-mandatory control - check for null
      // control can be null so have to be careful with check
      // otherwise we will restore defaults
      if (control != null && control == btnDefaults)
      {
        RestoreDefaults();
        return;
      }

      // mandatory control
      if (control == facadeView)
      {
        GUIMessage msg = new GUIMessage(GUIMessage.MessageType.GUI_MSG_ITEM_SELECTED, GetID, 0, controlId, 0, 0, null);
        OnMessage(msg);
        int itemIndex = (int)msg.Param1;
        if (actionType == Action.ActionType.ACTION_SELECT_ITEM)
        {
          GUIListItem item = facadeView.SelectedListItem;
          ExtensionSetting set = item.MusicTag as ExtensionSetting;
          GetValue(set);
          item.Label2 = set.DisplayValue;
          return;
        }
      }

      base.OnClicked(controlId, control, actionType);
    }

    protected override void OnShowContextMenu()
    {
      GUIDialogMenu dlg = (GUIDialogMenu)GUIWindowManager.GetWindow((int)Window.WINDOW_DIALOG_MENU);
      if (dlg == null) return;
      
      dlg.Reset();
      dlg.SetHeading(Translation.Settings);

      dlg.Add(new GUIListItem(Translation.RestoreDefaultValue));
      dlg.Add(new GUIListItem(Translation.RestoreAllDefaults));

      dlg.DoModal(GetID);
      if (dlg.SelectedId == -1) return;

      if (dlg.SelectedLabelText == Translation.RestoreDefaultValue)
      {
        // get currently selected setting
        ExtensionSetting setting = facadeView.SelectedListItem.MusicTag as ExtensionSetting;
        if (setting == null) return;

        setting.Value = setting.DefaultValue;
        facadeView.SelectedListItem.Label2 = setting.DisplayValue;
      }
      else if (dlg.SelectedLabelText == Translation.RestoreAllDefaults)
      {
        RestoreDefaults();
      }

      base.OnShowContextMenu();
    }

    private void RestoreDefaults()
    {
      // restore the defaults for selected section
      var sectionSettings = settings.Settings[CurrentSection.Value];
      foreach (var setting in sectionSettings)
      {
        setting.Value = setting.DefaultValue;
      }
      PopulateFacade(CurrentSection.Key);
      GUIControl.FocusControl(GetID, facadeView.GetID);
    }

    private void GetValue(ExtensionSetting setting)
    {
      string settingValue = setting.Value;

      if (setting.Values != null && setting.Values.Count > 0)
      {
        GUIDialogMenu dlg = (GUIDialogMenu)GUIWindowManager.GetWindow((int)Window.WINDOW_DIALOG_MENU);
        if (dlg == null) return;
        dlg.Reset();
        dlg.SetHeading(setting.DisplayName);
        foreach (string displayValue in setting.DisplayValues)
        {
          GUIListItem pItem = new GUIListItem(displayValue);
          if (displayValue == setting.DisplayValue)
            pItem.Selected = true;
          dlg.Add(pItem);
        }
        dlg.DoModal(GetID);
        if (dlg.SelectedId == -1) return;
        setting.Value = setting.Values[dlg.SelectedId - 1];
      }
      else
      {
        string enteredValue = settingValue;
        if (GUIUtils.GetStringFromKeyboard(ref enteredValue))
        {
          // do some validation based on type
          switch (setting.Type)
          {
            case SettingType.SInt:
              int outValue;
              if (!Int32.TryParse(enteredValue, out outValue))
              {
                GUIUtils.ShowOKDialog(Translation.Error, Translation.SettingsValidationInt);
                return;
              }
              else
              {
                // check withing min/max value limits
                if (setting.HasMinValue && setting.MinValue > outValue)
                {
                  GUIUtils.ShowOKDialog(Translation.Error, string.Format(Translation.SettingsValidationIntMin, setting.MinValue));
                  return;
                }
                if (setting.HasMaxValue && setting.MaxValue < outValue)
                {
                  GUIUtils.ShowOKDialog(Translation.Error, string.Format(Translation.SettingsValidationIntMax, setting.MaxValue));
                  return;
                }
              }
              break;
          }
          setting.Value = enteredValue;
        }
      }

      if (settingValue != setting.Value)
      {
        SettingsChanged = true;
      }

    }

    private void ClearProperties()
    {
      GUIUtils.SetProperty("#MPE.Selection.Description", string.Empty);

      GUIUtils.SetProperty("#MPE.Selected.Id", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.Name", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.Version", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.Author", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.Description", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.VersionDescription", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.ReleaseDate", string.Empty);
      GUIUtils.SetProperty("#MPE.Selected.Status", string.Empty);
    }

    private void SetProperties(PackageClass package)
    {
      if (package == null) return;
      GUIUtils.SetProperty("#MPE.Selected.Id", package.GeneralInfo.Id);
      GUIUtils.SetProperty("#MPE.Selected.Name", package.GeneralInfo.Name);
      GUIUtils.SetProperty("#MPE.Selected.Version", package.GeneralInfo.Version.ToString());
      GUIUtils.SetProperty("#MPE.Selected.Author", package.GeneralInfo.Author);
      GUIUtils.SetProperty("#MPE.Selected.Description", package.GeneralInfo.ExtensionDescription);
      GUIUtils.SetProperty("#MPE.Selected.VersionDescription", package.GeneralInfo.VersionDescription);
      GUIUtils.SetProperty("#MPE.Selected.ReleaseDate", package.GeneralInfo.ReleaseDate.ToShortDateString());
      GUIUtils.SetProperty("#MPE.Selected.Status", package.GeneralInfo.DevelopmentStatus);
    }

    private void item_OnItemSelected(GUIListItem item, GUIControl parent)
    {
      ExtensionSetting setting = item.MusicTag as ExtensionSetting;
      if (setting == null) return;

      GUIUtils.SetProperty("#MPE.Selection.Description", setting.Description);
    }
  }
}
