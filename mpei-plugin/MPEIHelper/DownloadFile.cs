#region Copyright (C) 2005-2010 Team MediaPortal

// Copyright (C) 2005-2010 Team MediaPortal
// http://www.team-mediaportal.com
// 
// MediaPortal is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// MediaPortal is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MediaPortal. If not, see <http://www.gnu.org/licenses/>.

#endregion

using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace MPEIHelper
{
  public partial class DownloadFile : Form
  {
    private string Source;
    private string Dest;
    public WebClient Client = new WebClient();
    public bool SilentMode { get; set; }

    public DownloadFile()
    {
      InitializeComponent();
    }

    private void client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
    {
      if (e.Error != null)
      {
        if (File.Exists(Dest))
        {
          WaitForNoBusy();
          if (!Client.IsBusy)
          {
            try
            {
              File.Delete(Dest);
            }
            catch (Exception) {}
          }
        }
        if (!SilentMode)
          MessageBox.Show(e.Error.Message + "\n" + e.Error.InnerException, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
      btnCancel.Enabled = false;
      Close();
    }

    private void client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
    {
      if (e.TotalBytesToReceive > 0)
      {
        progressBar.Style = ProgressBarStyle.Blocks;
        progressBar.Value = e.ProgressPercentage;
        lblProgress.Text = string.Format("{0} kb/{1} kb", e.BytesReceived / 1024, e.TotalBytesToReceive / 1024);
      }
      else
      {
        progressBar.Value = e.ProgressPercentage;
        progressBar.Style = ProgressBarStyle.Marquee;
        lblProgress.Text = string.Format("{0} kb", e.BytesReceived / 1024);
      }
    }

    public DownloadFile(string source, string dest)
    {
      InitializeComponent();
      StartDownload(source, dest);
    }

    public void StartDownload(string source, string dest)
    {
      Source = source;
      Dest = dest;
      Client.DownloadProgressChanged += client_DownloadProgressChanged;
      Client.DownloadFileCompleted += client_DownloadFileCompleted;
      Client.UseDefaultCredentials = true;
      Client.Proxy.Credentials = CredentialCache.DefaultCredentials;

      progressBar.Minimum = 0;
      progressBar.Maximum = 100;
      progressBar.Value = 0;
      ShowDialog();
    }

    private void DownloadFile_Shown(object sender, EventArgs e)
    {
      Uri uri = null;
      try
      {
        uri = new Uri(Source);
        Client.DownloadFileAsync(uri, Dest);
      }
      catch(Exception ex)
      {
        if (!SilentMode)
          MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        this.Close();
      }      
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
      Client.CancelAsync();
      WaitForNoBusy();
      this.Close();
    }

    private void WaitForNoBusy()
    {
      int counter = 0;
      while (Client.IsBusy || counter < 10)
      {
        counter++;
        Thread.Sleep(100);
      }
    }
  }
}