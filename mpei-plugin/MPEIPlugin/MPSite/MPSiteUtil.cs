using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Text.RegularExpressions;
using MediaPortal.GUI.Library;

namespace MPEIPlugin.MPSite
{
  public class MPSiteUtil
  {    
    List<Category> cats = new List<Category>();    

    public bool LoadCatTree()
    {
      // just do this once as it doesnt really change
      if (cats.Count > 0) return true;

      string site = "http://www.team-mediaportal.com/extensions";

      WebClient client = new WebClient();
      try
      {
        site = client.DownloadString(site);
      }
      catch (WebException e)
      {
        Log.Error("[MPEI] Error Loading items from '{0}': {1}", site, e.Message);
        return false;
      }      

      string resultString = null;
      Regex regexObj = new Regex(@"dTree\('(.+?)</script>", RegexOptions.Singleline);
      resultString = regexObj.Match(site).Groups[1].Value;
      try
      {
        Regex regexObj1 = new Regex(@".add.(?<id>.+?),(?<pid>.+?),'(?<name>.+?)','(?<url>.+?)','', '',fpath\);");
        Match matchResults = regexObj1.Match(resultString);
        while (matchResults.Success)
        {
          // matched text: matchResults.Value
          // match start: matchResults.Index
          // match length: matchResults.Length
          cats.Add(new Category()
          {
            Id = matchResults.Groups["id"].Value,
            Name = GetCategoryTranslation(matchResults.Groups["name"].Value.Replace("&amp;","&").Trim()),
            Number = "",//matchResults.Groups["nr"].Value,
            Url = matchResults.Groups["url"].Value,
            PId = matchResults.Groups["pid"].Value
          });
          matchResults = matchResults.NextMatch();
        }
      }
      catch (Exception e)
      {
        Log.Error("[MPEI] Error: {0}", e.Message);
        return false;
      }

      cats.Add(new Category()
      {
        Id = "-1",
        Name = Translation.CategoryRecentlyAdded,
        Number = "",
        Url = "/extensions/new-listing",
        PId = "0"
      });
      cats.Add(new Category()
      {
        Id = "-2",
        Name = Translation.CategoryRecentlyUpdated,
        Number = "",
        Url = "/extensions/recently-updated",
        PId = "0"
      });
      cats.Add(new Category()
      {
        Id = "-3",
        Name = Translation.CategoryMostInstalled,
        Number = "",
        Url = "/extensions/most-favoured",
        PId = "0"
      });
      cats.Add(new Category()
      {
        Id = "-4",
        Name = Translation.CategoryFeatured,
        Number = "",
        Url = "/extensions/featured-listing",
        PId = "0"
      });
      cats.Add(new Category()
      {
        Id = "-5",
        Name = Translation.CategoryMostPopular,
        Number = "",
        Url = "/extensions/popular-listing-2",
        PId = "0"
      });
      cats.Add(new Category()
      {
        Id = "-6",
        Name = Translation.CategoryMostRated,
        Number = "",
        Url = "/extensions/most-rated",
        PId = "0"
      });
      cats.Add(new Category()
      {
        Id = "-7",
        Name = Translation.CategoryTopRated,
        Number = "",
        Url = "/extensions/top-rated",
        PId = "0"
      });
      cats.Add(new Category()
      {
        Id = "-8",
        Name = Translation.CategoryMostReviewed,
        Number = "",
        Url = "/extensions/most-reviewed",
        PId = "0"
      });
      
      return true;
    }

    string GetCategoryTranslation(string originalName)
    {
      string before = originalName;
      string after = string.Empty;
      string translation = string.Empty;

      // try to find a corresponding translation field
      foreach(var c in new char[]{' ', '-', '_', '&', '/', '\\'})
      {
        before = before.Replace(c.ToString(), string.Empty);
      }

      before = string.Format("Category{0}", before);
      translation = Translation.GetByName(before);

      if (translation == before)
        translation = originalName;

      return translation;
    }

    public bool LoadItems(Category category)
    {
      WebClient client = new WebClient();
      string site = "http://www.team-mediaportal.com" + category.Url;

      try
      {
        site = client.DownloadString(site);
      }
      catch (WebException e)
      {
        Log.Error("[MPEI] Error Loading items from '{0}': {1}", site, e.Message);
        return false;
      }
            
      Regex regexObj = new Regex("<div class=\"listing-summary(.*?)</div></div></div>", RegexOptions.Singleline);
      category.SiteItems.Clear();      
      Match match = regexObj.Match(site);
      try
      {
        while (match.Success)
        {
          string item = match.Groups[0].Value;
          regexObj = new Regex("<h3><a href=\"(?<url>[^\"]+)\">(?<name>[^<]+)</a>.+?</h3>(?<rating>.+?)<span class=\"reviews\">(?:.+?<span class=\"website\">(?:<img src=\"[^\"]+\" alt=\"MediaPortal (?<compatability>[^\"]+)\"[^>]+>(?:&nbsp;)?)*</span>)?.*?<p style=\"margin:0;\">(?:<a.+?</a>)?\\s*(?<desc>.+?)</p>.*?<div class=\"fields\">.*?<span class=\"caption\">Hits:</span><span class=\"output\">(?<hits>[^<]+)</span>.*?<span class=\"caption\">Votes:</span><span class=\"output\">(?<votes>[^<]+)</span>.*?<div class=\"fieldRow\"><span class=\"caption\">Date Added:</span><span class=\"output\">(?<date_added>[^<]+)</span>.*?<span class=\"caption\">Last Update:</span><span class=\"output\">(?<date_update>[^<]+)</span>.*?<span class=\"caption\">Version:</span><span class=\"output\">(?<version>[^<]+)</span>.*?<span class=\"caption\">Status:</span><span class=\"output\">(?<status>[^<]+)</span>", RegexOptions.Singleline | RegexOptions.Compiled);
          Match matchResults = regexObj.Match(item);
          while (matchResults.Success)
          {
            SiteItems items = new SiteItems()
            {
              Url = matchResults.Groups["url"].Value,
              Name = HttpUtility.HtmlDecode(matchResults.Groups["name"].Value),
              Descriptions = MediaPortal.Util.Utils.stripHTMLtags(HttpUtility.HtmlDecode(matchResults.Groups["desc"].Value)),
              Hits = matchResults.Groups["hits"].Value,
              Votes = matchResults.Groups["votes"].Value,
              DateAdded = matchResults.Groups["date_added"].Value,
              DateUpdated = matchResults.Groups["date_update"].Value,
              Version = matchResults.Groups["version"].Value,
              Status = matchResults.Groups["status"].Value,
              LogoUrl = Regex.Match(item, "<p.*?src=\"(?<img>.*?)\"", RegexOptions.Singleline).Groups["img"].Value,
              EditorPick = matchResults.Value.Contains("status_editorpick.png"),
              Popular = matchResults.Value.Contains("status_popular.png"),
              JustAdded = matchResults.Value.Contains("status_new.png")
            };

            CaptureCollection compVersions = matchResults.Groups["compatability"].Captures;
            // create a comma seperated list of versions that package supports
            if (compVersions.Count > 0)
              items.CompatibleVersions = compVersions.Cast<Capture>().Select(c => c.Value).Aggregate((c, n) => c + ", " + n);

            // get 10 star rating value
            var ratings = HttpUtility.HtmlDecode(matchResults.Groups["rating"].Value).Split(new char[] { ' ' });
            items.Rating = ((ratings.Count(r => r.Contains("star_10.png")) * 2) + ratings.Count(r => r.Contains("star_05.png"))).ToString();

            category.SiteItems.Add(items);
            matchResults = matchResults.NextMatch();
          }
          match = match.NextMatch();
        }
      }
      catch (Exception e)
      {
        // Syntax error in the regular expression
        Log.Error("[MPEI] Error: {0}", e.Message);
        return false;
      }

      return true;
    }

    public List<Category> GetCats(string pid)
    {
      List<Category> list = new List<Category>();
      foreach (Category category in cats)
      {
        if(category.PId==pid)
          list.Add(category);
      }
      return list;
    }

    public Category GetCat(string id)
    {
      foreach (Category category in cats)
      {
        if (category.Id == id)
          return category;
      }
      return null;
    }

  }
}
