using MediaPortal.GUI.Library;
using MpeCore;
using MPEIPlugin.MPSite;

namespace MPEIPlugin
{
  public enum DownLoadItemType
  {
    IndexList,
    UpdateInfo,
    UpdateInfoComplete,
    Extension,
    Logo,
    Other
  }

  public class DownLoadInfo
  {
    public DownLoadInfo()
    {
      ItemType = DownLoadItemType.Other;
    }

    public string Url { get; set; }
    public string TempFile { get; set; }
    public string Destination { get; set; }
    public object Tag { get; set; }
    public DownLoadItemType ItemType { get; set; }
    public PackageClass Package { get; set; }
    public SiteItems SiteItem { get; set; }
    public GUIListItem ListItem { get; set; }

  }
}
