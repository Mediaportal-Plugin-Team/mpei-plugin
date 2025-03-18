using System.Collections.Generic;

namespace MPEIPlugin.MPSite
{
  public class Category
  {
    public Category()
    {
      SiteItems = new List<SiteItems>();
    }
    public string Name { get; set; }
    public string Url { get; set; }
    public string Id { get; set; }
    public string PId { get; set; }
    public string Number { get; set; }
    public List<SiteItems> SiteItems { get; set; }
    public bool Updated { get; set; }

    public override string ToString()
    {
      return Name + "-" + Id + "-" + PId + "-" + Url + "-" + Number;
    }
  }
}
