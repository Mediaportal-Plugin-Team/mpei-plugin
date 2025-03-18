using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using MediaPortal.Configuration;
using MediaPortal.GUI.Library;

namespace MPEIPlugin
{
  public class DownloadManager : IDisposable
  {
    public delegate void DownloadDoneEventHandler(DownLoadInfo info);

    public event DownloadDoneEventHandler DownloadDone;

    public event DownloadDoneEventHandler DownloadStart;

    private readonly WebClient _client = new WebClient();

    private readonly Queue<DownLoadInfo> _queue = new Queue<DownLoadInfo>();

    private readonly HashSet<string> _failedDownloads = new HashSet<string>();

    public DownloadManager()
    {
      _client.DownloadFileCompleted += client_DownloadFileCompleted;
      _client.UseDefaultCredentials = true;
      _client.Proxy.Credentials = CredentialCache.DefaultCredentials;
    }

    /// <summary>
    /// Adds download to the download queue. If the queue is empty, download begins immediately.
    /// </summary>
    public void AddToDownloadQueue(string source, string dest, DownLoadItemType type)
    {
      AddToDownloadQueue(new DownLoadInfo
      {
        Destination = dest,
        ItemType = type,
        TempFile = GetTempFilename(),
        Url = source
      });
    }

    /// <summary>
    /// Adds download to the download queue. If the queue is empty, download begins immediately.
    /// </summary>
    public void AddToDownloadQueue(DownLoadInfo info)
    {
      lock (_failedDownloads)
      {
        if (_failedDownloads.Contains(info.Url))
          return;
      }

      lock (_queue)
      {
        _queue.Enqueue(info); 
      }

      DownloadNextInQueue();
    }

    /// <summary>
    /// Downloads the file from given URL immediately and returns after download is success/failed.
    /// </summary>
    public bool DownloadNow(string url, string localFile)
    {
      try
      {
        Directory.CreateDirectory(Path.GetDirectoryName(localFile));

        Log.Debug("[MPEI] Downloading file from: {0}", url);

        using (WebClient webClient = new WebClient())
        {
          webClient.DownloadFile(url, localFile);
        }

        return true;
      }
      catch (Exception e)
      {
        Log.Error("[MPEI] Download failed from '{0}' to '{1}: {2}'", url, localFile, e.Message);

        try
        {
          if (File.Exists(localFile))
            File.Delete(localFile);
        }
        catch
        {
          // Ignore, nothing we can do
        }

        return false;
      }
    }

    public static string GetTempFilename()
    {
      try
      {
        return Path.GetTempFileName();
      }
      catch (IOException)
      {
        // This is ugly: most likely too many files in %temp%
        return Config.GetFile(Config.Dir.Cache, string.Format(@"mpei-{0}", Guid.NewGuid()));
      }
    }

    private void DownloadNextInQueue()
    {
      try
      {
        if (_client.IsBusy)
          return;

        DownLoadInfo info;

        lock (_queue)
        {
          if (_queue.Count == 0)
            return;

          info = _queue.Dequeue(); 
        }

        info.TempFile = GetTempFilename();

        if (DownloadStart != null)
          DownloadStart(info);

        _client.DownloadFileAsync(new Uri(info.Url), info.TempFile, info);
      }
      catch (Exception e)
      {
        Log.Error(e);
      }
    }

    private void client_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
    {
      DownLoadInfo item = (DownLoadInfo) e.UserState;

      if (e.Error == null)
      {
        try
        {
          if (File.Exists(item.TempFile))
          {
            string dir = Path.GetDirectoryName(item.Destination);
            
            if (!Directory.Exists(dir))
              Directory.CreateDirectory(dir);

            File.Copy(item.TempFile, item.Destination, true);
            File.Delete(item.TempFile);
          }          
          
          if (DownloadDone != null)
            DownloadDone(item);

        }
        catch (Exception exception)
        {
          Log.Error("[MPEI] Failed to process {0}: {1}", item.Url, exception.Message);
        }
      }
      else
      {
        Log.Warn("[MPEI] Failed to download file from {0}: {1}", item.Url, e.Error.Message);

        // don't download again in the same session..could add an expire but dont think its necessary
        if (item.ItemType == DownLoadItemType.UpdateInfo || item.ItemType == DownLoadItemType.Logo)
        {
          lock (_failedDownloads)
          {
            _failedDownloads.Add(item.Url); 
          }
        }
      }

      NotifyUpdateInfoCompleteIfRequired(item);

      DownloadNextInQueue();
    }

    private void NotifyUpdateInfoCompleteIfRequired(DownLoadInfo item)
    {
      bool updateInfoComplete = false;

      lock (_queue)
      {
        if (_queue.Count(d => d.ItemType == DownLoadItemType.UpdateInfo) == 0 &&
            item.ItemType == DownLoadItemType.UpdateInfo)
        {
          updateInfoComplete = true;
        }
      }

      if (DownloadDone != null && updateInfoComplete)
        DownloadDone(new DownLoadInfo() { ItemType = DownLoadItemType.UpdateInfoComplete }); 
    }

    public void Dispose()
    {
      _client.Dispose();
    }
  }
}
