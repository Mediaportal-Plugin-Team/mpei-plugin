using System;
using System.Collections.Generic;
using System.Xml;
using MediaPortal.Configuration;

namespace MPEIPlugin.Classes
{
  public enum SettingType
  {
    SString,
    SBool,
    SList,
    SInt
  }

  public class ExtensionSetting
  {
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public string EntryName { get; set; }
    public string DefaultValue { get; set; }
    public string Description { get; set; }
    public string ConfigFile { get; set; }
    
    private string _value;
    public string Value
    {
      get
      {
        if (string.IsNullOrEmpty(Name) || string.IsNullOrEmpty(EntryName)) return null;
        using (var xmlreader = new MediaPortal.Profile.Settings(Config.GetFile(Config.Dir.Config, string.IsNullOrEmpty(ConfigFile) ? "MediaPortal.xml" : ConfigFile)))
        {
          _value = xmlreader.GetValueAsString(GetName(EntryName), GetName(Name), DefaultValue);
        }
        return _value;
      }
      set
      {
        _value = value;
        using (var xmlwriter = new MediaPortal.Profile.Settings(Config.GetFile(Config.Dir.Config, string.IsNullOrEmpty(ConfigFile) ? "MediaPortal.xml" : ConfigFile)))
        {
          xmlwriter.SetValue(GetName(EntryName), GetName(Name), _value);
        }
      }
    }

    public SettingType Type { get; set; }
    public string ListValues { get; set; }
    public List<string> DisplayValues { get; set; }
    public int MinValue { get; set; }
    public int MaxValue { get; set; }
    public bool HasMinValue { get; set; }
    public bool HasMaxValue { get; set; }

    public List<string> Values
    {
      get
      {
        if (ListValues == null) return null;

        var vallist = new List<string>();
        string[] val = ListValues.Split('|');
        foreach (string s in val)
        {
          if (!string.IsNullOrEmpty(s))
            vallist.Add(s);
        }
        return vallist;
      }
    }

    public string DisplayValue
    {
      get
      {
        if (string.IsNullOrEmpty(Value)) return string.Empty;

        string s = Value;
        if (DisplayValues != null && DisplayValues.Count > 0)
        {
          int i = 0;
          foreach (string value in Values)
          {
            if (value == s)
              return DisplayValues[i];
            i++;
          }
        }
        return s;
      }
    }

    public ExtensionSetting Load(XmlNode node)
    {
      if (node == null)
        return new ExtensionSetting();
      if (node.Attributes["name"] != null)
        Name = node.Attributes["name"].Value;
      if (node.Attributes["displayname"] != null)
        DisplayName = GetTranslatedString(node.Attributes["displayname"].Value);
      if (node.Attributes["entryname"] != null)
        EntryName = node.Attributes["entryname"].Value;
      if (node.Attributes["defaultvalue"] != null)
        DefaultValue = node.Attributes["defaultvalue"].Value;
      if (node.Attributes["listvalues"] != null)
        ListValues = node.Attributes["listvalues"].Value;
      if (node.Attributes["displaylistvalues"] != null)
        DisplayValues = GetTranslatedStrings(node.Attributes["displaylistvalues"].Value);
      if (node.Attributes["description"] != null)
        Description = GetTranslatedString(node.Attributes["description"].Value);
      if (node.Attributes["configfile"] != null)
        ConfigFile = GetTranslatedString(node.Attributes["configfile"].Value);
      if (node.Attributes["minvalue"] != null)
      {
        int outValue = Int32.MinValue;
        Int32.TryParse(node.Attributes["minvalue"].Value, out outValue);
        MinValue = outValue;
        HasMinValue = true;
      }
      if (node.Attributes["maxvalue"] != null)
      {
        int outValue = Int32.MaxValue;
        Int32.TryParse(node.Attributes["maxvalue"].Value, out outValue);
        MaxValue = outValue;
        HasMaxValue = true;
      }
      if (node.Attributes["type"] != null)
      {
        if (node.Attributes["type"].Value == "string")
          Type = SettingType.SString;
        if (node.Attributes["type"].Value == "bool")
          Type = SettingType.SBool;
        if (node.Attributes["type"].Value == "list")
          Type = SettingType.SList;
        if (node.Attributes["type"].Value == "int")
          Type = SettingType.SInt;
      }
      return this;
    }

    /// <summary>
    /// Gets a translated string from a skin property
    /// </summary>
    /// <param name="description">Translation Property</param>
    /// <returns>Translated string</returns>
    public static string GetTranslatedString(string description)
    {
      string translatedString = description.Trim();
      if (translatedString.StartsWith("#") && translatedString.Contains("."))
      {
        translatedString = GUIUtils.GetProperty(description);
      }
      return translatedString;
    }

    List<string> GetTranslatedStrings(string descriptions)
    {
      var translatedStrings = descriptions.Trim().Split('|');
      var returnStrings = new List<string>();

      foreach (var translatedString in translatedStrings)
      {
        returnStrings.Add(GetTranslatedString(translatedString));
      }

      return returnStrings;
    }

    string GetName(string name)
    {
      name = name.Trim();
      if(name.StartsWith("$") && name.Contains("."))
      {
        string entryName = name.Substring(1).Split('.')[0];
        string valname = name.Substring(1).Split('.')[1];
        using (var xmlreader = new MediaPortal.Profile.Settings(Config.GetFile(Config.Dir.Config, string.IsNullOrEmpty(ConfigFile) ? "MediaPortal.xml" : ConfigFile)))
        {
          _value = xmlreader.GetValueAsString(entryName, valname, "");
        }
      }
      return name;
    }

  }
}
