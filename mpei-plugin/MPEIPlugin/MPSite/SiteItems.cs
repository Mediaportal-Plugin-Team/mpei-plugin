using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using MediaPortal.GUI.Library;

namespace MPEIPlugin.MPSite
{
  public class SiteItems
  {
    public SiteItems()
    {
      Images = new List<string>();
    }

    public string Name { get; set; }
    public string Descriptions { get; set; }
    public string Hits { get; set; }
    public string Url { get; set; }
    public string DateAdded { get; set; }
    public string DateUpdated { get; set; }
    public string Rating { get; set; }
    public string Votes { get; set; }
    public string Status { get; set; }
    public string LogoUrl { get; set; }
    public string Author { get; set; }
    public string Downloads { get; set; }
    public string FileUrl { get; set; }
    public string Version { get; set; }
    public string File { get; set; }
    public List<string> Images { get; set; }
    public bool EditorPick { get; set; }
    public bool JustAdded { get; set; }
    public bool Popular { get; set; }
    public string CompatibleVersions { get; set; }    

    public bool LoadInfo()
    {
      if (string.IsNullOrEmpty(Url))
        return false;

      WebClient client = new WebClient();
      string site = "http://www.team-mediaportal.com" + Url;

      try
      {
        site = client.DownloadString(site);
      }
      catch (WebException e)
      {
        Log.Error("[MPEI] Error Loading info from '{0}': {1}", site, e.Message);
        return false;
      }
      
      // try get author fields if available
      Author = Regex.Match(site, "<div class=\"caption\">Author[^<]+</div><div class=\"data\">(?<authors>[^<]+)</div>", RegexOptions.Singleline).Groups["authors"].Value;
      if (string.IsNullOrEmpty(Author))
      {
        // get submit by field instead
        Author = Regex.Match(site, "Submitted By.*?\"><a.*?>(?<name>.*?)</a", RegexOptions.Singleline).Groups["name"].Value;
      }
      Version = Regex.Match(site, "<div class=\"caption\">Version</div><div class=\"data\">(?<version>[^<]+)</div>", RegexOptions.Singleline).Groups["version"].Value;
      Downloads = Regex.Match(site, @"(?<downloads>\d+) Downloads.</div></div>", RegexOptions.Singleline).Groups["downloads"].Value;
      FileUrl = Regex.Match(site, "<div class=\"data-short\"><a href=\"(?<name>.*?)\" t", RegexOptions.Singleline).Groups["name"].Value;
      Descriptions = Regex.Match(site, "<div class=\"listing-desc\">(?<name>.*?)</div>", RegexOptions.Singleline).Groups["name"].Value;
      Descriptions = MediaPortal.Util.Utils.stripHTMLtags(HttpUtility.HtmlDecode(Descriptions.Replace(@"</p>","\n")));

      CaptureCollection compVersions = Regex.Match(site, "<div class=\"caption\">Compatibility</div><div class=\"data_full_row\">(?:<img src=\"(?<comp_img>[^\"]+)\" alt=\"(?<comp_full>[^\"]+)\" title=\"MediaPortal (?<comp_ver>[^\"]+)\" />(?:&nbsp;)?)*</div>", RegexOptions.Singleline).Groups["comp_ver"].Captures;
      // create a comma seperated list of versions that package supports
      if (compVersions.Count > 0)
        CompatibleVersions = compVersions.Cast<Capture>().Select(c => c.Value).Aggregate((c, n) => c + ", " + n);

      // get 10 star rating value
      var ratings = Regex.Match(site, "<div class=\"rating\"><div id=\"rating-msg\">(?<ratings>.+?)<div id=\"total-votes\">", RegexOptions.Singleline).Groups["ratings"].Value.Split(new char[] { ' ' });
      Rating = ((ratings.Count(r => r.Contains("star_10.png")) * 2) + ratings.Count(r => r.Contains("star_05.png"))).ToString();
      
      Regex regexObj = new Regex("<a rel=\"shadowbox.ca.\".*?href=\"(?<name>.*?)\">", RegexOptions.Singleline);
      Match matchResults = regexObj.Match(site);
      LogoUrl = LogoUrl.Replace("/s/", "/m/");
      Images.Clear();
      while (matchResults.Success)
      {
        Images.Add(matchResults.Groups[1].Value);
        matchResults = matchResults.NextMatch();
      }

      return true;
    }

    public void LoadFileName()
    {
      if(!string.IsNullOrEmpty(File))
        return;

      if (FileUrl.StartsWith("http://www.team-mediaportal.com/"))
      {
        try
        {
          var request = (HttpWebRequest)WebRequest.Create(FileUrl);
          request.Method = "HEAD";
          request.AllowAutoRedirect = true;

          using (var response = request.GetResponse() as HttpWebResponse)
          {
            if (!string.IsNullOrEmpty(response.GetResponseHeader("Content-Disposition")))
              File = response.GetResponseHeader("Content-Disposition").Split(';')[1].Split('=')[1].Replace("\"", "").ToLower();
          }
        }
        catch (Exception e)
        {
          Log.Error("[MPEI] Unable to generate filename from url: {0}", e.Message);
          File = string.Empty;
        }
      }
      else
      {
        File = Path.GetFileName(FileUrl);
      }
    }

  }
}
