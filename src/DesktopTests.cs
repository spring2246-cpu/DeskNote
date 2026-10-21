using System;
using System.IO;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
namespace DeskNote {
public static class DesktopTest {
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr hwnd);
 static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
 public static void Run(string folder){
  Directory.CreateDirectory(folder);var app=new DeskApp();DeskApp.CurrentApp=app;UI.InstallTheme(app);app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
  app.Store=new Store(Path.Combine(folder,"desktop-store-"+Guid.NewGuid().ToString("N")));app.Panel=new PanelWindow(app);app.Panel.Show();
  app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>{
   var report=new List<string>();Action<bool,string> check=(test,name)=>{if(!test)throw new Exception(name);report.Add("PASS "+name);};
   try{
    check(PanelWindow.StartOfWeek(new DateTime(2026,10,20))==new DateTime(2026,10,19),"Week starts on Monday");check(PanelWindow.IsoWeek(new DateTime(2021,1,1))==53,"ISO week handles year boundary");
    app.Panel.SetAppearance(.3,.8);check(Math.Abs(app.Panel.Opacity-.8)<.01,"Overall transparency preview updates window");
    app.Panel.ApplyPreferences();check(Math.Abs(app.Panel.Opacity-app.Store.Data.Settings.Opacity)<.01,"Cancelled appearance preview restores persisted setting");
    IntPtr normalHandle=new WindowInteropHelper(app.Panel).Handle,normalParent=DesktopHost.GetParent(normalHandle);object content=app.Panel.Content;
    app.Store.Change(d=>{d.Settings.BackgroundOpacity=.55;d.Settings.Opacity=.9;d.Settings.EmbedDesktop=true;});app.Panel.ApplyPreferences();
    IntPtr handle=app.Panel.Desktop.Handle,host=DesktopHost.FindHost();
    check(host!=IntPtr.Zero,"Explorer desktop icon host discovered");check(app.Panel.Desktop.Attached&&DesktopHost.IsChild(host,handle),"Panel actually reparented into Explorer desktop");
    check(app.Panel.Desktop.AboveIcons,"Panel is a sibling above the desktop icon surface, outside wallpaper-only hosts");
    check(handle!=normalHandle&&DesktopHost.GetParent(normalHandle)==normalParent,"Desktop uses a separate child HWND without reparenting the WPF Window");
    check(app.Panel.Desktop.HasRenderTarget,"Desktop child owns a transparent WPF render target created for child rendering");
    var before=DesktopHost.ScreenBoundsPixels(app.Panel);var moved=before;moved.X+=24;moved.Y+=12;DesktopHost.Move(app.Panel,moved);var after=DesktopHost.ScreenBoundsPixels(app.Panel);check(Math.Abs(after.X-moved.X)<3&&Math.Abs(after.Y-moved.Y)<3,"Embedded panel supports moving in screen coordinates");
    var sized=after;sized.Width+=30;sized.Height+=20;DesktopHost.Move(app.Panel,sized);after=DesktopHost.ScreenBoundsPixels(app.Panel);check(Math.Abs(after.Width-sized.Width)<3&&Math.Abs(after.Height-sized.Height)<3,"Embedded panel supports native resizing");
    app.Panel.SavePlacement();var restored=new Store(app.Store.Folder);check(Math.Abs(restored.Data.Settings.BackgroundOpacity-.55)<.01&&restored.Data.Settings.EmbedDesktop,"Transparency and embedded mode survive restart");
    app.Panel.Hide();app.Panel.Show();app.Panel.ApplyWindowMode();check(app.Panel.Desktop.Attached&&app.Panel.IsVisible,"Embedded panel can hide and restore");
    app.Store.Change(d=>{d.Settings.EmbedDesktop=false;d.Settings.Topmost=true;});app.Panel.ApplyPreferences();check(!app.Panel.Desktop.Attached&&!DesktopHost.IsWindow(handle)&&DesktopHost.GetParent(normalHandle)==normalParent&&app.Panel.Topmost&&ReferenceEquals(app.Panel.Content,content),"Detach destroys child surface and returns the original content to untouched normal Window");
    var detached=DesktopHost.ScreenBoundsPixels(app.Panel);check(Math.Abs(detached.X-after.X)<3&&Math.Abs(detached.Y-after.Y)<3,"Detaching preserves screen position");
    for(int i=0;i<12;i++){
     ((System.Windows.Controls.Grid)app.Panel.Content).MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.First));
     app.Store.Data.Settings.EmbedDesktop=true;app.Panel.ApplyPreferences();Pump();app.Tick();Pump();
     check(app.Panel.Desktop.HasRenderTarget,"Cycle "+i+": desktop render target is alive");
     IntPtr child=app.Panel.Desktop.Handle;app.Panel.Hide();app.Panel.Show();app.Panel.ApplyWindowMode();
     app.Store.Data.Settings.EmbedDesktop=false;app.Panel.ApplyPreferences();Pump();app.Panel.UpdateLayout();
     check(!DesktopHost.IsWindow(child)&&ReferenceEquals(app.Panel.Content,content)&&DesktopHost.GetParent(normalHandle)==normalParent&&((FrameworkElement)content).ActualWidth>400,"Cycle "+i+": normal content, layout and parent restored without a leaked child");
    }
    app.Store.Data.Settings.EmbedDesktop=true;app.Panel.ApplyPreferences();Pump();var lostBounds=DesktopHost.ScreenBoundsPixels(app.Panel);IntPtr lost=app.Panel.Desktop.Handle;
    check(DestroyWindow(lost),"Simulated native render-surface destruction succeeds without restarting Explorer");Pump();app.Panel.ApplyWindowMode();Pump();
    var rebuilt=DesktopHost.ScreenBoundsPixels(app.Panel);check(app.Panel.Desktop.HasRenderTarget&&app.Panel.Desktop.Handle!=lost&&Math.Abs(rebuilt.X-lostBounds.X)<3&&Math.Abs(rebuilt.Y-lostBounds.Y)<3,"Destroyed desktop render surface rebuilds with original content and position");
    app.Store.Change(d=>d.Settings.Opacity=.2);app.Panel.ApplyPreferences();app.RecoverPanel();Pump();
    check(!app.Panel.Desktop.Attached&&ReferenceEquals(app.Panel.Content,content)&&app.Panel.Opacity==1&&new Store(app.Store.Folder).Data.Settings.Opacity==1,"Find-panel restores normal render content and persists full opacity");
    report.Add("INFO Actual Win+D, Explorer process restart and mixed-DPI multi-monitor behavior require manual hardware verification.");File.WriteAllLines(Path.Combine(folder,"desktop-test.txt"),report);
   }catch(Exception e){report.Add("FAIL "+e);File.WriteAllLines(Path.Combine(folder,"desktop-test.txt"),report);Environment.ExitCode=1;}
   finally{app.Store.Data.Settings.EmbedDesktop=false;app.Panel.ApplyPreferences();app.Exiting=true;app.Panel.Close();app.Shutdown();}
  }));app.Run();
 }
}
}
