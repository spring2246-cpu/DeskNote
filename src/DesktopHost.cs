using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
namespace DeskNote {
// Create a real child render target. Never reparent a live layered WPF Window.
public sealed class DesktopHost : IDisposable {
 HwndSource surface;
 FrameworkElement visual;
 IntPtr host;
 bool changing;
 Rect lastBounds=Rect.Empty;
 public Rect LastBounds {get{return lastBounds;}}
 public IntPtr Handle {get{return surface==null||surface.IsDisposed?IntPtr.Zero:surface.Handle;}}
 public bool Attached {get{return Handle!=IntPtr.Zero&&IsWindow(Handle)&&IsWindow(host)&&GetParent(Handle)==host;}}
 public bool HasRenderTarget {get{return Attached&&surface.RootVisual==visual&&surface.UsesPerPixelOpacity;}}
 public string Status="普通窗口";
 public IntPtr Parent {get{return Handle==IntPtr.Zero?IntPtr.Zero:GetParent(Handle);}}
 public bool AboveIcons {get{return Attached&&FindWindowEx(host,IntPtr.Zero,"SHELLDLL_DefView",null)!=IntPtr.Zero&&GetTopWindow(host)==Handle;}}
 [StructLayout(LayoutKind.Sequential)] public struct RECT {public int Left,Top,Right,Bottom;}
 [StructLayout(LayoutKind.Sequential)] struct POINT {public int X,Y;}
 delegate bool EnumProc(IntPtr hwnd,IntPtr param);
 [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,IntPtr lparam);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
 [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr hwnd);
 [DllImport("user32.dll",SetLastError=true)] static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int w,int h,uint flags);
 [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr hwnd,ref POINT point);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out RECT rect);
 [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
 [DllImport("user32.dll")] public static extern bool IsChild(IntPtr parent,IntPtr child);
 [DllImport("user32.dll")] static extern IntPtr GetTopWindow(IntPtr parent);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd,StringBuilder result,int max);
 [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd,int mode);
 public static IntPtr FindHost() {
  IntPtr result=IntPtr.Zero;
  EnumWindows((window,param)=>{if(FindWindowEx(window,IntPtr.Zero,"SHELLDLL_DefView",null)!=IntPtr.Zero){var name=new StringBuilder(128);GetClassName(window,name,128);if(name.ToString()=="Progman"||name.ToString()=="WorkerW"){result=window;return false;}}return true;},IntPtr.Zero);return result;
 }
 public bool Attach(Window window) {
  if(changing)return Attached;
  if(Attached&&host==FindHost()){EnsureVisible(window);return true;}
  if(surface!=null)Detach(window);
  IntPtr target=FindHost();if(target==IntPtr.Zero){Status="桌面宿主不可用 · 已回退普通窗口";return false;}
  IntPtr hwnd=new WindowInteropHelper(window).Handle;if(hwnd==IntPtr.Zero){Status="窗口尚未就绪";return false;}
  Rect bounds=ScreenBoundsPixels(window);changing=true;visual=window.Content as FrameworkElement;
  try {
   if(visual==null)throw new InvalidOperationException("面板内容未就绪");
   var p=new POINT{X=(int)bounds.X,Y=(int)bounds.Y};ScreenToClient(target,ref p);
   var parameters=new HwndSourceParameters("DeskNote.DesktopSurface") {ParentWindow=target,WindowStyle=0x40000000|0x10000000|0x04000000,PositionX=p.X,PositionY=p.Y,Width=(int)bounds.Width,Height=(int)bounds.Height,UsesPerPixelTransparency=true,TreatAsInputRoot=true};
   surface=new HwndSource(parameters);host=target;surface.CompositionTarget.BackgroundColor=Colors.Transparent;
   visual.SetValue(System.Windows.Documents.TextElement.FontFamilyProperty,window.FontFamily);
   visual.SetValue(System.Windows.Documents.TextElement.FontSizeProperty,window.FontSize);
   visual.SetValue(System.Windows.Documents.TextElement.ForegroundProperty,window.Foreground);
   visual.Resources.MergedDictionaries.Add(window.Resources);
   window.Content=null;surface.RootVisual=visual;visual.Opacity=window.Opacity;
   window.Topmost=false;window.ShowInTaskbar=false;ShowWindow(hwnd,0);
   EnsureVisible(window);Status="已嵌入桌面";return true;
  }catch(Exception ex){
   ReleaseSurface();if(visual!=null){visual.Resources.MergedDictionaries.Remove(window.Resources);visual.Opacity=1;window.Content=visual;visual=null;}
   window.ShowInTaskbar=true;ShowWindow(hwnd,4);Status="桌面渲染失败 · 普通窗口："+ex.Message;return false;
  }finally{changing=false;}
 }
 void ReleaseSurface(){if(surface!=null&&!surface.IsDisposed){surface.RootVisual=null;surface.Dispose();}surface=null;host=IntPtr.Zero;}
 public void Detach(Window window) {
  if(surface==null||changing)return;
  Rect bounds=ScreenBoundsPixels(window);changing=true;
  try {ReleaseSurface();if(visual!=null){visual.Resources.MergedDictionaries.Remove(window.Resources);visual.Opacity=1;window.Content=visual;visual=null;}window.ShowInTaskbar=true;Move(window,bounds);if(window.IsVisible)ShowWindow(new WindowInteropHelper(window).Handle,4);Status="普通窗口";}
  finally{changing=false;}
 }
 public void SyncVisibility(Window w){if(surface==null||changing)return;if(w.IsVisible)EnsureVisible(w);else if(Handle!=IntPtr.Zero)ShowWindow(Handle,0);}
 public void SetOpacity(double opacity){if(visual!=null)visual.Opacity=opacity;}
 public static IntPtr WindowHandle(Window w){var p=w as PanelWindow;return p!=null&&p.Desktop.Handle!=IntPtr.Zero?p.Desktop.Handle:new WindowInteropHelper(w).Handle;}
 public static Rect ScreenBoundsPixels(Window w) {RECT r;var panel=w as PanelWindow;if(panel!=null&&panel.Desktop.surface!=null&&!panel.Desktop.Attached&&!panel.Desktop.lastBounds.IsEmpty)return panel.Desktop.lastBounds;IntPtr hwnd=WindowHandle(w);if(hwnd!=IntPtr.Zero&&GetWindowRect(hwnd,out r)){var bounds=new Rect(r.Left,r.Top,Math.Max(1,r.Right-r.Left),Math.Max(1,r.Bottom-r.Top));if(panel!=null)panel.Desktop.lastBounds=bounds;return bounds;}if(panel!=null&&!panel.Desktop.lastBounds.IsEmpty)return panel.Desktop.lastBounds;return new Rect(w.Left,w.Top,w.Width,w.Height);}
 public static Rect ScreenBoundsDips(Window w) {Rect r=ScreenBoundsPixels(w);var s=HwndSource.FromHwnd(WindowHandle(w));if(s!=null&&s.CompositionTarget!=null){var m=s.CompositionTarget.TransformFromDevice;return new Rect(m.Transform(r.TopLeft),m.Transform(r.BottomRight));}return r;}
 public static void Move(Window w,Rect screenBounds) {
  IntPtr hwnd=WindowHandle(w);var panel=w as PanelWindow;var p=new POINT{X=(int)Math.Round(screenBounds.X),Y=(int)Math.Round(screenBounds.Y)};
  // GetParent on top-level windows can be an owner; only child coordinates are relative.
  if(panel!=null&&panel.Desktop.Attached)ScreenToClient(panel.Desktop.Parent,ref p);
  if(!SetWindowPos(hwnd,IntPtr.Zero,p.X,p.Y,(int)Math.Round(screenBounds.Width),(int)Math.Round(screenBounds.Height),0x10))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
  if(panel!=null)panel.Desktop.lastBounds=screenBounds;
 }
 public void EnsureVisible(Window w){if(Attached&&w.IsVisible){ShowWindow(new WindowInteropHelper(w).Handle,0);SetWindowPos(Handle,IntPtr.Zero,0,0,0,0,1|2|0x10);ShowWindow(Handle,4);if(visual!=null)visual.InvalidateVisual();ScreenBoundsPixels(w);}}
 public void Dispose(){ReleaseSurface();visual=null;}
}
}

