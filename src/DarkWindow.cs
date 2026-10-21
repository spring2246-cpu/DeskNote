using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shell;
namespace DeskNote {
public class DarkWindow:Window {
 static readonly ControlTemplate Chrome=(ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Window'><Border Background='{TemplateBinding Background}' BorderBrush='#245371' BorderThickness='1' CornerRadius='14'><DockPanel><Border x:Name='Header' DockPanel.Dock='Top' Background='#141E30' BorderBrush='#344052' BorderThickness='0,0,0,1' Padding='20,14,12,14' CornerRadius='14,14,0,0'><DockPanel><StackPanel DockPanel.Dock='Right' Orientation='Horizontal' VerticalAlignment='Center'><Button x:Name='MinimizeButton' Content='−' ToolTip='最小化' Width='30' Height='30' Background='Transparent' BorderThickness='0' Margin='0,0,4,0'/><Button x:Name='CloseButton' Content='×' ToolTip='关闭' FontSize='20' Width='30' Height='30' Background='Transparent' BorderThickness='0'/></StackPanel><Border Background='#38BDF8' CornerRadius='8' Width='32' Height='32' Margin='0,0,11,0' DockPanel.Dock='Left'><Image Source='{TemplateBinding Icon}' Width='24' Height='24'/></Border><StackPanel VerticalAlignment='Center'><TextBlock Text='{TemplateBinding Title}' Foreground='#F1F5F9' FontSize='15' FontWeight='SemiBold' TextTrimming='CharacterEllipsis'/><TextBlock Text='桌面随记 · 本地与私密' Foreground='#94A3B8' FontSize='10' Margin='0,4,0,0'/></StackPanel></DockPanel></Border><AdornerDecorator><ContentPresenter/></AdornerDecorator></DockPanel></Border></ControlTemplate>");
 public DarkWindow(){Icon=Branding.WindowIcon;Background=new SolidColorBrush(Color.FromRgb(15,23,42));Foreground=UI.Ink;FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=12;WindowStyle=WindowStyle.None;Template=Chrome;WindowChrome.SetWindowChrome(this,new WindowChrome{CaptionHeight=0,ResizeBorderThickness=new Thickness(6),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(14)});}
 public override void OnApplyTemplate(){base.OnApplyTemplate();var close=Template.FindName("CloseButton",this) as Button;if(close!=null)close.Click+=(s,e)=>Close();var minimize=Template.FindName("MinimizeButton",this) as Button;if(minimize!=null)minimize.Click+=(s,e)=>WindowState=WindowState.Minimized;var header=Template.FindName("Header",this) as Border;if(header!=null)header.MouseLeftButtonDown+=(s,e)=>{if(e.ChangedButton!=MouseButton.Left)return;if(e.ClickCount==2&&ResizeMode!=ResizeMode.NoResize)WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;else DragMove();};}
}
public static partial class UI {
 public static MessageBoxResult Message(string text,string title="桌面随记",MessageBoxButton buttons=MessageBoxButton.OK,MessageBoxImage image=MessageBoxImage.Information){
  var window=UI.Window(title,450,320);window.ResizeMode=ResizeMode.NoResize;window.SizeToContent=SizeToContent.Height;window.MinHeight=220;window.MaxHeight=Math.Max(300,SystemParameters.WorkArea.Height-60);
  var p=UI.Body(window);p.Children.Add(UI.Text(text,14));var row=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,18,0,0)};p.Children.Add(row);
  MessageBoxResult result=buttons==MessageBoxButton.YesNo?MessageBoxResult.No:MessageBoxResult.Cancel;
  Action<string,MessageBoxResult,bool> add=(label,value,primary)=>{var b=UI.Button(label,()=>{result=value;window.Close();});b.MinWidth=80;if(primary){b.Background=new SolidColorBrush(Color.FromRgb(56,189,248));b.Foreground=new SolidColorBrush(Color.FromRgb(8,37,58));b.IsDefault=true;}row.Children.Add(b);};
  if(buttons==MessageBoxButton.YesNo||buttons==MessageBoxButton.YesNoCancel){add("是",MessageBoxResult.Yes,false);add("否",MessageBoxResult.No,true);}else add("确定",MessageBoxResult.OK,true);
  if(buttons==MessageBoxButton.OKCancel||buttons==MessageBoxButton.YesNoCancel)add("取消",MessageBoxResult.Cancel,false);
  var app=DeskApp.CurrentApp;if(app!=null&&app.Unlocked&&app.PrivateWindows.Count>0)app.Track(window);
  window.ShowDialog();return result;
 }
}
}
