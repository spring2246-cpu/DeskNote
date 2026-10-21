using System;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
namespace DeskNote {
public static class DisplayProbe {
 public static void Run(string folder){
  var app=new DeskApp();DeskApp.CurrentApp=app;UI.InstallTheme(app);app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
  app.Store=new Store(Path.Combine(folder,"probe-"+Guid.NewGuid().ToString("N")));
  var w=new PanelWindow(app,folder.IndexOf("opaque")<0);app.Panel=w;
  w.Title="桌面随记 · 渲染诊断 "+folder;w.Show();
  if(folder.IndexOf("software")>=0)((HwndSource)PresentationSource.FromVisual(w)).CompositionTarget.RenderMode=RenderMode.SoftwareOnly;
  w.Activate();app.Run();
 }
}
}
