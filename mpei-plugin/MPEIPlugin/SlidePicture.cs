#region Copyright (C) 2005-2009 Team MediaPortal

/* 
 *	Copyright (C) 2005-2009 Team MediaPortal
 *	http://www.team-mediaportal.com
 *
 *  This Program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation; either version 2, or (at your option)
 *  any later version.
 *   
 *  This Program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 *  GNU General Public License for more details.
 *   
 *  You should have received a copy of the GNU General Public License
 *  along with GNU Make; see the file COPYING.  If not, write to
 *  the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA. 
 *  http://www.gnu.org/copyleft/gpl.html
 *
 */

#endregion

using System;
using System.IO;
using System.Net;
using System.Threading;
using MediaPortal.GUI.Library;
//using MediaPortal.Picture.Database;
using MediaPortal.Util;
using Microsoft.DirectX.Direct3D;

#region SlidePicture class

internal class SlidePicture : IDisposable
{
  private const int MAX_PICTURE_WIDTH = 2040;
  private const int MAX_PICTURE_HEIGHT = 2040;

  private Texture _texture;
  private int _width = 0;
  private int _height = 0;
  private int _rotation = 0;
  
  public string LocalFile{ get; set;}

  private string _filePath;
  private bool _useActualSizeTexture;
  WebClient client = new WebClient();

  public Texture Texture
  {
    get
    {
      if(client.IsBusy)
        do
        {
          Thread.Sleep(100);
        } while (client.IsBusy);
      return _texture;
    }
  }

  public string FilePath
  {
    get { return _filePath; }
  }

  public bool TrueSizeTexture
  {
    get { return _useActualSizeTexture; }
  }

  public int Width
  {
    get { return _width; }
  }

  public int Height
  {
    get { return _height; }
  }

  public int Rotation
  {
    get { return _rotation; }
  }

  public SlidePicture(string strFileUrl, bool useActualSizeTexture)
  {
    client.DownloadProgressChanged += client_DownloadProgressChanged;
    client.DownloadFileCompleted += client_DownloadFileCompleted;
    _filePath = strFileUrl;

    _rotation = 0;//PictureDatabase.GetRotation(_filePath);

    int iMaxWidth = GUIGraphicsContext.OverScanWidth;
    int iMaxHeight = GUIGraphicsContext.OverScanHeight;

    _useActualSizeTexture = useActualSizeTexture;
    if (_useActualSizeTexture)
    {
      iMaxWidth = MAX_PICTURE_WIDTH;
      iMaxHeight = MAX_PICTURE_HEIGHT;
    }
    LocalFile = GetLocalImageFileName(strFileUrl);
    if (!File.Exists(LocalFile))
    {
      Dispose();
      client.DownloadFileAsync(new Uri(strFileUrl), LocalFile);
    }
    else
    {
      _texture = Picture.Load(LocalFile, _rotation, iMaxWidth, iMaxHeight, true, false, true, out _width, out _height);
    }
  }

  static public string GetLocalImageFileName(string strURL)
  {
    if (strURL == "")
      return string.Empty;
    string url = String.Format("mpei-{0}.jpg", MediaPortal.Util.Utils.EncryptLine(strURL));
    return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.InternetCache), url); ;
  }

  void client_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
  {
    int iMaxWidth = GUIGraphicsContext.OverScanWidth;
    int iMaxHeight = GUIGraphicsContext.OverScanHeight;
    if (_useActualSizeTexture)
    {
      iMaxWidth = MAX_PICTURE_WIDTH;
      iMaxHeight = MAX_PICTURE_HEIGHT;
    }
    if (File.Exists(LocalFile))
    {
      _texture = Picture.Load(LocalFile, _rotation, iMaxWidth, iMaxHeight, true, false, true, out _width, out _height);
    }
    GUIPropertyManager.SetProperty("#Social.DownloadProgress", " ");
  }

  void client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
  {
    GUIPropertyManager.SetProperty("#Social.DownloadProgress",e.ProgressPercentage.ToString());
  }

  ~SlidePicture()
  {
    Dispose();
  }

  public void Dispose()
  {
    if (_texture != null && !_texture.Disposed)
    {
      _texture.Dispose();
      _texture = null;
    }
  }
}

#endregion