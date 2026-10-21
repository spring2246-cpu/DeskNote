using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Media.Imaging;
namespace DeskNote {
public static class Branding {
 public static readonly Icon TrayIcon=LoadTrayIcon();
 public static readonly BitmapSource WindowIcon=LoadWindowIcon();
 static Icon LoadTrayIcon(){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("DeskNote.AppIcon")){if(stream==null)throw new InvalidOperationException("缺少程序图标资源");using(var icon=new Icon(stream,System.Windows.Forms.SystemInformation.SmallIconSize)){return (Icon)icon.Clone();}}}
 static BitmapSource LoadWindowIcon(){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("DeskNote.AppIcon")){var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);var frame=decoder.Frames[decoder.Frames.Count-1];frame.Freeze();return frame;}}
}
}
