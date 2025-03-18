using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using MediaPortal.Configuration;

namespace MPEIHelper
{
  static class Program
  {
    private static SplashScreen splashScreen = new SplashScreen();

    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
      string configFile = args[0];
      if (File.Exists(configFile))
      {
        TextReader reader = new StreamReader(configFile);
        string url = reader.ReadLine();
        string fileName = reader.ReadLine();
        string pluginName = reader.ReadLine();
        string silentMode = reader.ReadLine();
        string splashFile = reader.ReadLine();
        reader.Close();



        DownloadFile dlg = new DownloadFile();

        if (File.Exists(splashFile) && !String.IsNullOrEmpty(silentMode))
        {
          splashScreen.SetImg(splashFile);
          splashScreen.Show();
        }

        KillProcess("Configuration");
        KillProcess("MediaPortal");

        dlg.Client.DownloadProgressChanged += Client_DownloadProgressChanged;
        dlg.Client.DownloadFileCompleted += Client_DownloadFileCompleted;
        dlg.Text = pluginName;
        dlg.SilentMode = !String.IsNullOrEmpty(silentMode);

        if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
          dlg.StartDownload(url, fileName);
        else
        {
          if (String.IsNullOrEmpty(silentMode))
            MessageBox.Show("Uri is not valid, aborting install", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        string tempfile = fileName;
        if (File.Exists(fileName))
        {
          if (Path.GetExtension(fileName).ToLower() == ".mpe1")
          {
            silentMode = fileName + " " + silentMode;
            fileName = "mpeinstaller.exe";
          }

          try
          {
            if (File.Exists(fileName))
            {
              if (splashScreen.Visible)
              {
                splashScreen.ResetProgress();
                splashScreen.SetInfo("Installing extension. Please wait...");
              }
              Process process = string.IsNullOrEmpty(silentMode) ? Process.Start(fileName) : Process.Start(fileName, silentMode);
              process.WaitForExit();
            }
          }
          catch (Exception e)
          {
            if (splashScreen.Visible)
            {
              splashScreen.Close();
            }
            if (String.IsNullOrEmpty(silentMode))
              MessageBox.Show(e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
          }

          try
          {
            // cleanup
            File.Delete(configFile);
            File.Delete(tempfile);
          }
          catch { }         
        }

        // Start MediaPortal, installation complete
        Process.Start(Config.GetFile(Config.Dir.Base,"MediaPortal.exe"));
        Thread.Sleep(3000);
        if (splashScreen.Visible)
        {
          splashScreen.Close();
        }
        return;
      }

      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      Application.Run(new Form1(args[0]));
    }

    static void Client_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
    {
      if (splashScreen.Visible)
      {
        splashScreen.ResetProgress();
      }
    }

    static void Client_DownloadProgressChanged(object sender, System.Net.DownloadProgressChangedEventArgs e)
    {
      if(splashScreen.Visible)
      {
        splashScreen.SetProgress("Downloading", e.ProgressPercentage);
      }
    }

    private static void KillProcess(string name)
    {
      Process[] prs = Process.GetProcesses();
      foreach (Process pr in prs)
      {
        if (pr.ProcessName.Equals(name, StringComparison.InvariantCultureIgnoreCase))
        {
          pr.CloseMainWindow();
          pr.Close();
          Thread.Sleep(500);
        }
      }
      prs = Process.GetProcesses();
      foreach (Process pr in prs)
      {
        if (pr.ProcessName.Equals(name, StringComparison.InvariantCultureIgnoreCase))
        {
          try
          {
            Thread.Sleep(5000);
            pr.Kill();
          }
          catch (Exception) { }
        }
      }
    }
  }
}
