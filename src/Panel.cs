using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Markup;
using System.Windows.Threading;

namespace DeskNote {
public partial class PanelWindow : Window {
 DeskApp app;
 readonly DesktopHost desktop=new DesktopHost();
 public DesktopHost Desktop {get{return desktop;}}
 StackPanel days;
 UniformGrid weekDays;
 TextBlock secondsSeparator;
 DockPanel progressRow;
 TextBlock completed,dateLabel,weekNumber,hours,minutes,seconds,timePercent,weekLabel,weekProgress,weatherCity,weatherValue,modeLabel;
 Button focusButton,layoutButton;
 Border frame;
 Grid root;
 Canvas timeTrack;
 Border timeFill;
 ScrollViewer scroll;
 DispatcherTimer clockTimer,placement;
 DateTime selected=DateTime.Today,weekStart=StartOfWeek(DateTime.Today),displayDate=DateTime.Today;
 string filter="全部",weatherText="--°C",loadedCity="";
 DateTime lastWeatherAttempt=DateTime.MinValue;
 bool weatherBusy;
 public bool AppearancePreview;
 double previewBackground=1,previewOverall=1;
 DateTime? focusDeadline;
 TimeSpan focusRemaining=TimeSpan.FromMinutes(25);
 readonly List<Border> tinted=new List<Border>();
 readonly Dictionary<DateTime,Border> dayCards=new Dictionary<DateTime,Border>();
 Point dragStart;
 Rect dragBounds;
 bool dragging;
 static readonly Brush Accent=Brush("#38BDF8"), White=Brush("#EEF3FB"),Muted=Brush("#94A3B8"),Line=Brush("#344052");
 static readonly ControlTemplate ButtonTemplate=(ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'><Border x:Name='box' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='7' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='#31BFF3'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='#31BFF3'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
 static Brush Brush(string hex){return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));}
 static TextBlock Label(string text,double size=12,Brush color=null){return new TextBlock{Text=text,FontSize=size,Foreground=color??White,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};}
 Button Button(string title,Action action,bool active=false){var b=UI.Button(title,action);b.Template=ButtonTemplate;b.Background=active?Accent:Brushes.Transparent;b.Foreground=active?Brush("#08253A"):Muted;b.BorderBrush=active?Accent:Line;b.BorderThickness=new Thickness(1);b.Padding=new Thickness(9,5,9,5);b.Margin=new Thickness(0,0,6,0);b.MinHeight=27;return b;}
 static DockPanel Row(){return new DockPanel{LastChildFill=true};}
 static void Right(DockPanel row,UIElement element){DockPanel.SetDock(element,Dock.Right);row.Children.Add(element);}
 static void DockLeft(DockPanel row,UIElement element){DockPanel.SetDock(element,Dock.Left);row.Children.Add(element);}
 Border Card(UIElement child,string color="#202B3C",double padding=14){var b=new Border{Background=Brush(color),BorderBrush=Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(16),Padding=new Thickness(padding),Child=child,Margin=new Thickness(0,0,0,12)};b.Tag=color;tinted.Add(b);return b;}
 public static DateTime StartOfWeek(DateTime date){return date.Date.AddDays(-(((int)date.DayOfWeek+6)%7));}
 public static int IsoWeek(DateTime date){DateTime thursday=date.Date.AddDays(3-(((int)date.DayOfWeek+6)%7));return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(thursday,CalendarWeekRule.FirstFourDayWeek,DayOfWeek.Monday);}
 public PanelWindow(DeskApp a) : this(a,true) {}
 internal PanelWindow(DeskApp a,bool layered) {
  Icon=Branding.WindowIcon;
  app=a;Title="桌面随记";FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=12;Foreground=White;WindowStyle=WindowStyle.None;AllowsTransparency=layered;Background=layered?Brushes.Transparent:Brush("#172131");ShowInTaskbar=true;ResizeMode=ResizeMode.NoResize;
  MinWidth=440;MinHeight=620;Width=a.Store.Data.Settings.Width<MinWidth?500:a.Store.Data.Settings.Width;Height=Math.Max(MinHeight,Math.Min(a.Store.Data.Settings.Height<MinHeight?820:a.Store.Data.Settings.Height,SystemParameters.WorkArea.Height));Left=a.Store.Data.Settings.Left;Top=a.Store.Data.Settings.Top;
  Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(@"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'><Style TargetType='ScrollBar'><Setter Property='Width' Value='6'/><Setter Property='Background' Value='Transparent'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'><Track x:Name='PART_Track' Orientation='Vertical' IsDirectionReversed='True' Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}' ViewportSize='{TemplateBinding ViewportSize}' Value='{TemplateBinding Value}'><Track.DecreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageUpCommand}' Opacity='0' Focusable='False'/></Track.DecreaseRepeatButton><Track.Thumb><Thumb MinHeight='24'><Thumb.Template><ControlTemplate TargetType='Thumb'><Border Background='#475569' CornerRadius='3' Margin='1'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageDownCommand}' Opacity='0' Focusable='False'/></Track.IncreaseRepeatButton></Track></ControlTemplate></Setter.Value></Setter></Style></ResourceDictionary>"));
  if(Left+Width<SystemParameters.VirtualScreenLeft+40||Left>SystemParameters.VirtualScreenLeft+SystemParameters.VirtualScreenWidth-40)Left=SystemParameters.WorkArea.Left+40;
  if(Top<SystemParameters.VirtualScreenTop||Top>SystemParameters.VirtualScreenTop+SystemParameters.VirtualScreenHeight-40)Top=SystemParameters.WorkArea.Top+30;
  frame=new Border{CornerRadius=new CornerRadius(18),BorderBrush=Line,BorderThickness=new Thickness(1),Background=Brush("#172131"),ClipToBounds=true};
  root=new Grid();Content=root;root.Children.Add(frame);var dock=new DockPanel();frame.Child=dock;
  var header=Row();header.Margin=new Thickness(16,12,10,12);DockPanel.SetDock(header,Dock.Top);dock.Children.Add(header);
  var options=Button("⚙ 后台选项",()=>OpenOptions());options.Foreground=Accent;options.Background=Brush("#173D55");Right(header,options);
  var hide=Button("↓",()=>Hide());hide.ToolTip="隐藏到托盘";Right(header,hide);
  layoutButton=Button("⌑",()=>{app.Store.Change(d=>d.Settings.LayoutLocked=!d.Settings.LayoutLocked);ApplyPreferences();});layoutButton.ToolTip="锁定 / 解锁面板移动和缩放";Right(header,layoutButton);
  var embed=Button("▣",()=>{app.Store.Change(d=>{d.Settings.EmbedDesktop=!d.Settings.EmbedDesktop;d.Settings.Topmost=false;});Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>app.Run(ApplyPreferences)));});embed.ToolTip="切换嵌入桌面 / 普通窗口";Right(header,embed);
  var brand=Label("●  桌面随记",12);brand.FontWeight=FontWeights.Bold;DockLeft(header,brand);completed=Label("",10,Muted);completed.Margin=new Thickness(8,0,0,0);header.Children.Add(completed);
  header.MouseLeftButtonDown+=BeginDrag;header.MouseMove+=Drag;header.MouseLeftButtonUp+=(s,e)=>EndDrag();header.LostMouseCapture+=(s,e)=>{dragging=false;};
  var footer=Row();footer.Margin=new Thickness(18,7,14,11);DockPanel.SetDock(footer,Dock.Bottom);dock.Children.Add(footer);Right(footer,Button("＋ 便签",()=>{filter="便签";Refresh();Edit(null,true);}));modeLabel=Label("",10,Muted);footer.Children.Add(modeLabel);
  var content=new StackPanel{Margin=new Thickness(20,5,20,0)};scroll=new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};dock.Children.Add(scroll);
  var dateRow=Row();dateRow.Margin=new Thickness(0,10,0,0);weekNumber=Label("",11,Muted);Right(dateRow,weekNumber);dateLabel=Label("",12);dateLabel.FontWeight=FontWeights.SemiBold;dateRow.Children.Add(dateLabel);content.Children.Add(dateRow);
  var clockRow=Row();clockRow.Margin=new Thickness(0,4,0,0);focusButton=Button("专注番茄",FocusDialog);focusButton.VerticalAlignment=VerticalAlignment.Center;Right(clockRow,focusButton);
  var clock=new StackPanel{Orientation=Orientation.Horizontal};hours=Label("00",48,Accent);hours.FontWeight=FontWeights.Bold;minutes=Label("00",48);minutes.FontWeight=FontWeights.Bold;seconds=Label("00",24,Muted);seconds.VerticalAlignment=VerticalAlignment.Bottom;seconds.Margin=new Thickness(0,0,0,9);clock.Children.Add(hours);clock.Children.Add(Label(" : ",40,Muted));clock.Children.Add(minutes);secondsSeparator=Label(" : ",26,Muted);clock.Children.Add(secondsSeparator);clock.Children.Add(seconds);clockRow.Children.Add(clock);content.Children.Add(clockRow);
  progressRow=Row();progressRow.Margin=new Thickness(0,5,0,6);timePercent=Label("",10,Accent);Right(progressRow,timePercent);progressRow.Children.Add(Label("今日时间流逝",11,Muted));content.Children.Add(progressRow);
  timeTrack=new Canvas{Height=6,Background=Brush("#344052"),Margin=new Thickness(0,0,0,14)};timeFill=new Border{Height=6,Background=Accent,CornerRadius=new CornerRadius(3)};timeTrack.Children.Add(timeFill);timeTrack.SizeChanged+=(s,e)=>UpdateClock();content.Children.Add(timeTrack);
  var weatherRow=Row();Right(weatherRow,Button("↻",()=>UpdateWeather(true)));weatherValue=Label(weatherText,23);weatherValue.FontWeight=FontWeights.Bold;weatherValue.Margin=new Thickness(0,0,14,0);Right(weatherRow,weatherValue);
  var cityButton=Button("☀",WeatherDialog);cityButton.Foreground=Brush("#FACC15");cityButton.FontSize=20;cityButton.BorderThickness=new Thickness(0);DockLeft(weatherRow,cityButton);
  weatherCity=Label("选择天气城市",12);weatherCity.Cursor=Cursors.Hand;weatherCity.MouseLeftButtonUp+=(s,e)=>WeatherDialog();weatherRow.Children.Add(weatherCity);var weatherCard=Card(weatherRow,"#263142",12);weatherCard.MinHeight=68;weatherCard.Margin=new Thickness(0,0,0,20);content.Children.Add(weatherCard);
  var calendar=new StackPanel();var weekRow=Row();weekRow.Margin=new Thickness(0,0,0,14);Right(weekRow,Button("今天",()=>SelectWeek(DateTime.Today)));Right(weekRow,Button("›",()=>SelectWeek(weekStart.AddDays(7))));Right(weekRow,Button("‹",()=>SelectWeek(weekStart.AddDays(-7))));var weekInfo=new StackPanel();weekLabel=Label("",12);weekLabel.FontWeight=FontWeights.SemiBold;weekProgress=Label("",10,Muted);weekProgress.Margin=new Thickness(0,3,0,0);weekInfo.Children.Add(weekLabel);weekInfo.Children.Add(weekProgress);weekRow.Children.Add(weekInfo);calendar.Children.Add(weekRow);
  weekDays=new UniformGrid{Columns=7};calendar.Children.Add(weekDays);content.Children.Add(Card(calendar,"#202A3A",13));
  var filterRow=Row();filterRow.Margin=new Thickness(0,2,0,15);Right(filterRow,Button("＋ 待办",()=>Edit(null,false,selected),true));
  var filterButtons=new WrapPanel();foreach(string name in new[]{"全部","未办","已完成","高优","便签"}){string value=name;var b=Button(value,()=>{filter=value;Refresh();});b.FontSize=10;b.Padding=new Thickness(5,4,5,4);b.BorderThickness=new Thickness(0);b.Tag=value;filterButtons.Children.Add(b);}filterRow.Children.Add(filterButtons);content.Children.Add(filterRow);
  days=new StackPanel();content.Children.Add(days);
  var grip=new Thumb{Width=18,Height=18,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Cursor=Cursors.SizeNWSE,Opacity=.35};grip.ToolTip="调整面板大小";root.Children.Add(grip);grip.DragDelta+=(s,e)=>{if(app.Store.Data.Settings.LayoutLocked)return;var bounds=DesktopHost.ScreenBoundsPixels(this);var source=PresentationSource.FromVisual(root);double scale=source==null?1:source.CompositionTarget.TransformToDevice.M11;bounds.Width=Math.Max(MinWidth*scale,bounds.Width+e.HorizontalChange*scale);bounds.Height=Math.Max(MinHeight*scale,bounds.Height+e.VerticalChange*scale);DesktopHost.Move(this,bounds);QueuePlacement();};
  Closing+=(s,e)=>{if(!app.Exiting){e.Cancel=true;SavePlacement();Hide();}};
  Closed+=(s,e)=>{clockTimer.Stop();placement.Stop();desktop.Dispose();};
  placement=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(700)};placement.Tick+=(s,e)=>{placement.Stop();app.Run(SavePlacement);};LocationChanged+=(s,e)=>QueuePlacement();SizeChanged+=(s,e)=>QueuePlacement();
  clockTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};clockTimer.Tick+=(s,e)=>{UpdateClock();if(DateTime.Now.Second%5==0&&app.Store.Data.Settings.EmbedDesktop&&IsVisible)ApplyWindowMode();if(DateTime.Now.Second==0)UpdateWeather(false);};clockTimer.Start();
  Loaded+=(s,e)=>{EnsureOnScreen();ApplyPreferences();UpdateWeather(false);};IsVisibleChanged+=(s,e)=>desktop.SyncVisibility(this);Refresh();
 }
 void BeginDrag(object sender,MouseButtonEventArgs e){if(app.Store.Data.Settings.LayoutLocked||e.OriginalSource is Button||FindButton(e.OriginalSource as DependencyObject))return;dragging=true;dragStart=root.PointToScreen(e.GetPosition(root));dragBounds=DesktopHost.ScreenBoundsPixels(this);((UIElement)sender).CaptureMouse();e.Handled=true;}
 static bool FindButton(DependencyObject child){while(child!=null){if(child is Button)return true;child=VisualTreeHelper.GetParent(child);}return false;}
 void Drag(object sender,MouseEventArgs e){if(!dragging||e.LeftButton!=MouseButtonState.Pressed)return;Point p=root.PointToScreen(e.GetPosition(root));var next=dragBounds;next.X+=p.X-dragStart.X;next.Y+=p.Y-dragStart.Y;DesktopHost.Move(this,next);QueuePlacement();}
 void EndDrag(){if(!dragging)return;dragging=false;Mouse.Capture(null);QueuePlacement();}
 void QueuePlacement(){if(placement!=null&&IsLoaded){placement.Stop();placement.Start();}}
 public void SavePlacement(){var bounds=DesktopHost.ScreenBoundsDips(this);app.Store.Change(d=>{d.Settings.Left=bounds.Left;d.Settings.Top=bounds.Top;d.Settings.Width=bounds.Width;d.Settings.Height=bounds.Height;});}
 public void SetAppearance(double background,double overall){previewBackground=background;previewOverall=overall;Opacity=Math.Max(.35,Math.Min(1,overall));desktop.SetOpacity(Opacity);frame.Background=Tint("#172131",background);foreach(var b in tinted)b.Background=Tint(Convert.ToString(b.Tag),background);}
 Brush Tint(string color,double opacity){var c=ThemePalette.Color(app.Store.Data.Settings.SurfaceColor,"#0F172A");if(color!="#172131")c=ThemePalette.Blend(c,Colors.White,.065);c.A=(byte)Math.Round(Math.Max(.1,Math.Min(1,opacity))*255);return new SolidColorBrush(c);}
 public void ApplyPreferences(){var p=app.Store.Data.Settings;bool light=ThemePalette.IsLight(p.SurfaceColor);((SolidColorBrush)Accent).Color=ThemePalette.Color(p.AccentColor,"#38BDF8");((SolidColorBrush)White).Color=ThemePalette.Color(light?"#182539":"#EEF3FB","#EEF3FB");((SolidColorBrush)Muted).Color=ThemePalette.Color(light?"#52627A":"#94A3B8","#94A3B8");((SolidColorBrush)Line).Color=ThemePalette.Color(light?"#CCD6E4":"#344052","#344052");SetAppearance(p.BackgroundOpacity,p.Opacity);layoutButton.Content=p.LayoutLocked?"▣":"⌑";UpdateClock();ApplyWindowMode();}
 public void ApplyWindowMode(){if(!IsLoaded||!IsVisible)return;if(app.Store.Data.Settings.EmbedDesktop){desktop.Attach(this);Topmost=false;}else{desktop.Detach(this);Topmost=app.Store.Data.Settings.Topmost;}modeLabel.Text=app.Store.Data.Settings.EmbedDesktop?desktop.Status:Topmost?"置顶窗口":"普通窗口 · 拖动标题移动";if(app.Store.Data.Settings.LayoutLocked)modeLabel.Text+=" · 布局已锁定";}
 void SelectWeek(DateTime day){weekStart=StartOfWeek(day);selected=day;Refresh();if(dayCards.ContainsKey(day.Date))dayCards[day.Date].BringIntoView();}
 public void Refresh(){
  if(days==null)return;app.EnsureTaskRange(weekStart,weekStart.AddDays(6));var data=app.Store.Data;var tasks=data.Items.Where(x=>!x.Archived&&!x.Note&&x.Due.HasValue&&x.Due.Value.Date>=weekStart&&x.Due.Value.Date<weekStart.AddDays(7)).ToList();int done=tasks.Count(x=>x.Done);completed.Text=done+"/"+tasks.Count+"完成";
  weekLabel.Text="▦ 本周日程  "+weekStart.ToString("M月d日")+" — "+weekStart.AddDays(6).ToString("M月d日");weekProgress.Text="周任务完成度  "+(tasks.Count==0?0:(int)Math.Round(done*100d/tasks.Count))+"%  ("+done+"/"+tasks.Count+")";
  weekDays.Children.Clear();for(int n=0;n<7;n++){DateTime day=weekStart.AddDays(n);var p=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};bool today=day==DateTime.Today;bool chosen=day==selected.Date;p.Children.Add(Label(today?"今天":new[]{"周一","周二","周三","周四","周五","周六","周日"}[n],10,chosen?Accent:Muted));var number=Label(day.Day.ToString(),16,chosen?Accent:White);number.FontWeight=FontWeights.Bold;number.HorizontalAlignment=HorizontalAlignment.Center;p.Children.Add(number);int count=data.Items.Count(x=>!x.Note&&!x.Done&&x.Due.HasValue&&x.Due.Value.Date==day);var badge=Label(count==0?"空":count.ToString(),10,count==0?Brush("#19C599"):Accent);badge.HorizontalAlignment=HorizontalAlignment.Center;p.Children.Add(badge);var tile=new Border{Child=p,Padding=new Thickness(2,8,2,8),Margin=new Thickness(2),CornerRadius=new CornerRadius(12),BorderBrush=chosen?Accent:Brushes.Transparent,BorderThickness=new Thickness(chosen?2:1),Background=chosen?new SolidColorBrush(ThemePalette.Blend(ThemePalette.Color(app.Store.Data.Settings.SurfaceColor,"#0F172A"),ThemePalette.Color(app.Store.Data.Settings.AccentColor,"#38BDF8"),.18)):Brushes.Transparent,Cursor=Cursors.Hand};tile.MouseLeftButtonUp+=(s,e)=>{selected=day;Refresh();if(dayCards.ContainsKey(day))dayCards[day].BringIntoView();};weekDays.Children.Add(tile);}
  // Remove dynamic cards from opacity tracking to avoid retaining obsolete task views.
  tinted.RemoveAll(x=>x.Parent==null&&x!=frame);days.Children.Clear();tinted.RemoveAll(x=>x.Parent==null);dayCards.Clear();
  if(filter=="便签")RenderNotes();else{
   for(int n=0;n<7;n++){DateTime day=weekStart.AddDays(n);var all=data.Items.Where(x=>!x.Archived&&!x.Note&&x.Due.HasValue&&x.Due.Value.Date==day).OrderBy(x=>x.Due).ToList();RenderDay(day,all);}
   var unscheduled=data.Items.Where(x=>!x.Archived&&!x.Note&&!x.Due.HasValue).ToList();if(unscheduled.Count>0)RenderGroup("待安排",unscheduled);
   var overdue=data.Items.Where(x=>!x.Archived&&!x.Note&&!x.Done&&x.Due.HasValue&&x.Due.Value.Date<weekStart).OrderBy(x=>x.Due).ToList();if(overdue.Count>0)RenderGroup("更早未完成",overdue);
  }
  if(data.Settings.PrivateOnDesktop){int count=data.Bills.Count(x=>!x.Paid&&x.Due.AddDays(-x.Advance)<=DateTime.Now);if(count>0){var row=Row();Right(row,Button("解锁查看",()=>app.OpenFinance()));row.Children.Add(Label("◈ 有 "+count+" 项私人提醒",12,Accent));days.Children.Insert(0,Card(row,"#173448",12));}}
  foreach(var b in FindFilterButtons(root)) {string value=b.Tag as string;if(new[]{"全部","未办","已完成","高优","便签"}.Contains(value)){b.Foreground=value==filter?Accent:Muted;b.Background=value==filter?Brush("#194258"):Brushes.Transparent;}}
  SetAppearance(AppearancePreview?previewBackground:data.Settings.BackgroundOpacity,AppearancePreview?previewOverall:data.Settings.Opacity);UpdateClock();
 }
 static IEnumerable<Button> FindFilterButtons(DependencyObject parent){if(parent is Button)yield return (Button)parent;for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)foreach(var child in FindFilterButtons(VisualTreeHelper.GetChild(parent,i)))yield return child;}
 IEnumerable<Item> Filter(IEnumerable<Item> source){return source.Where(x=>filter=="未办"?!x.Done:filter=="已完成"?x.Done:filter=="高优"?x.HighPriority:true);}
 void RenderDay(DateTime day,List<Item> all){bool today=day==DateTime.Today;var p=new StackPanel();var head=Row();head.Margin=new Thickness(0,0,0,10);Right(head,Button("＋ 添加",()=>Edit(null,false,day)));Right(head,Label(all.Count(x=>x.Done)+"/"+all.Count+" 完成  ",10,Muted));var title=Label((today?"✦ 今天  ":"")+day.ToString("ddd · M月d日")+(day<DateTime.Today?"  已过":""),12);title.FontWeight=FontWeights.SemiBold;head.Children.Add(title);var body=new StackPanel();p.Children.Add(head);p.Children.Add(body);var visible=Filter(all).ToList();if(visible.Count==0){var empty=Label(all.Count==0?"◎ 该日暂无待办事项，点击快速添加":"没有符合筛选的事项",11,Muted);empty.HorizontalAlignment=HorizontalAlignment.Center;var box=new Border{Child=empty,Padding=new Thickness(5,14,5,14),BorderBrush=Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Cursor=Cursors.Hand};box.MouseLeftButtonUp+=(s,e)=>Edit(null,false,day);body.Children.Add(box);}else foreach(var item in visible)body.Children.Add(TaskRow(item));if(day.Date<DateTime.Today){body.Visibility=Visibility.Collapsed;head.Margin=new Thickness(0);var toggle=Button("▶ 展开",()=>{bool open=body.Visibility==Visibility.Visible;body.Visibility=open?Visibility.Collapsed:Visibility.Visible;head.Margin=new Thickness(0,0,0,open?0:10);});toggle.Click+=(s,e)=>{toggle.Content=body.Visibility==Visibility.Visible?"▼ 收起":"▶ 展开";};Right(head,toggle);}var card=Card(p,today?"#163044":"#202A3A",12);if(today){card.BorderBrush=Accent;card.BorderThickness=new Thickness(2);}days.Children.Add(card);dayCards[day]=card;}
 void RenderGroup(string title,List<Item> items){var p=new StackPanel();p.Children.Add(Label(title,13,Accent));foreach(var item in Filter(items))p.Children.Add(TaskRow(item));days.Children.Add(Card(p,"#202A3A",12));}
 UIElement TaskRow(Item item){string id=item.Id;var row=Row();row.Margin=new Thickness(0,6,0,1);var menu=Button("···",()=>ItemMenu(id,item.Note));menu.FontSize=15;Right(row,menu);var check=new CheckBox{IsChecked=item.Done,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0),ToolTip=item.Done?"恢复":"完成"};check.Click+=(s,e)=>app.Run(()=>{app.Store.Change(d=>Logic.Complete(d,id));Refresh();});DockLeft(row,check);var p=new StackPanel();var text=Label((item.HighPriority?"! ":"")+item.Text,12,item.Done?Muted:White);if(item.Done)text.TextDecorations=TextDecorations.Strikethrough;p.Children.Add(text);if(item.Due.HasValue)p.Children.Add(Label(item.Due.Value.ToString("HH:mm")+(item.Repeat!="不重复"?" · "+item.Repeat:""),10,Muted));row.Children.Add(p);return row;}
 void ItemMenu(string id,bool note){var menu=new ContextMenu();Action<string,Action> add=(title,action)=>{var m=new MenuItem{Header=title};m.Click+=(s,e)=>app.Run(action);menu.Items.Add(m);};add("编辑",()=>Edit(id,note));add("归档本条",()=>{app.Store.Change(d=>d.Items.Single(x=>x.Id==id).Archived=true);Refresh();});add("删除本次",()=>{if(UI.Confirm("删除这条内容？重复待办仅删除本次。")){app.Store.Change(d=>TaskSchedule.Delete(d,id));Refresh();}});if(!note&&!String.IsNullOrEmpty(app.Store.Data.Items.Single(x=>x.Id==id).SeriesId))add("停止本次及以后的重复",()=>{if(UI.Confirm("停止此系列从本次开始的重复？已完成的记录保留。")){app.Store.Change(d=>TaskSchedule.Stop(d,id));Refresh();}});menu.IsOpen=true;}
 void RenderNotes(){var notes=app.Store.Data.Items.Where(x=>!x.Archived&&x.Note).ToList();var head=Row();Right(head,Button("＋ 便签",()=>Edit(null,true)));head.Children.Add(Label("随手记",15));days.Children.Add(head);if(notes.Count==0)days.Children.Add(Label("记录灵感、备忘与想法。",12,Muted));foreach(var note in notes)days.Children.Add(Card(TaskRow(note),"#202A3A",12));}
 void OpenOptions(){var menu=new ContextMenu();Action<string,Action> add=(title,action)=>{var m=new MenuItem{Header=title};m.Click+=(s,e)=>app.Run(action);menu.Items.Add(m);};add("搜索与归档…",OpenLibrary);add("找回面板",()=>app.RecoverPanel());add("导出桌面诊断…",ExportDesktopDiagnostics);add("个人财务…",()=>app.OpenFinance());add("个性化设置…",()=>app.Settings());add("加密备份…",()=>app.Backup());add("锁定财务",()=>app.Lock());add("隐藏面板",()=>Hide());add("退出（停止提醒）",()=>app.Quit());menu.IsOpen=true;}
 public void UpdateClock(){if(hours==null)return;DateTime now=DateTime.Now;if(displayDate!=now.Date){displayDate=now.Date;Refresh();return;}dateLabel.Text="▢ "+now.ToString("yyyy年M月d日 dddd");weekNumber.Text="第 "+IsoWeek(now)+" 周";hours.Text=now.ToString("HH");minutes.Text=now.ToString("mm");seconds.Text=now.ToString("ss");seconds.Visibility=secondsSeparator.Visibility=app.Store.Data.Settings.ShowSeconds?Visibility.Visible:Visibility.Collapsed;timeTrack.Visibility=progressRow.Visibility=app.Store.Data.Settings.ShowDayProgress?Visibility.Visible:Visibility.Collapsed;double portion=now.TimeOfDay.TotalSeconds/86400;timePercent.Text=(int)(portion*100)+"%";timeFill.Width=Math.Max(0,timeTrack.ActualWidth*portion);if(focusDeadline.HasValue){focusRemaining=focusDeadline.Value-now;if(focusRemaining<=TimeSpan.Zero){focusDeadline=null;focusRemaining=TimeSpan.FromMinutes(25);app.Notify("专注结束","一轮专注已完成，休息一下吧。");}}focusButton.Content=focusDeadline.HasValue?"专注 "+focusRemaining.ToString(@"mm\:ss"):"专注番茄";}
 void FocusDialog(){var w=UI.Window("专注番茄",360,330);var p=UI.Body(w);p.Children.Add(UI.Text("给当下25分钟",23));p.Children.Add(UI.Text(focusDeadline.HasValue?"当前专注进行中。":"开始一轮专注，结束后提醒休息。",12,UI.Muted));p.Children.Add(UI.Button(focusDeadline.HasValue?"暂停":"开始 / 继续",()=>{if(focusDeadline.HasValue){focusRemaining=focusDeadline.Value-DateTime.Now;focusDeadline=null;}else focusDeadline=DateTime.Now+focusRemaining;UpdateClock();w.Close();}));p.Children.Add(UI.Button("重置为25分钟",()=>{focusDeadline=null;focusRemaining=TimeSpan.FromMinutes(25);UpdateClock();w.Close();}));w.ShowDialog();}
 void WeatherDialog(){var w=UI.Window("天气城市",400,350);var p=UI.Body(w);p.Children.Add(UI.Text("选择天气城市",23));var c=UI.Select(new[]{"关闭天气"}.Concat(Weather.Cities.Keys),String.IsNullOrEmpty(app.Store.Data.Settings.WeatherCity)?"关闭天气":app.Store.Data.Settings.WeatherCity);UI.Field(p,"城市",c);p.Children.Add(UI.Text("启用后按城市向 Open-Meteo 请求天气，不获取精确定位。天气每15分钟刷新；断网显示离线状态。",12,UI.Muted));p.Children.Add(UI.Button("保存并刷新",()=>{app.Store.Change(d=>d.Settings.WeatherCity=UI.Value(c)=="关闭天气"?"":UI.Value(c));UpdateWeather(true);w.Close();}));w.ShowDialog();}
 public async void UpdateWeather(bool force){string city=app.Store.Data.Settings.WeatherCity;if(String.IsNullOrEmpty(city)){weatherCity.Text="选择天气城市 ▾";weatherValue.Text="--°C";return;}weatherCity.Text=city+" ▾";if(weatherBusy||(!force&&DateTime.Now-lastWeatherAttempt<TimeSpan.FromMinutes(15)&&loadedCity==city))return;weatherBusy=true;loadedCity=city;lastWeatherAttempt=DateTime.Now;weatherValue.Text="更新中";try{string result=await Weather.Load(city);if(app.Store.Data.Settings.WeatherCity==city){weatherText=result;weatherValue.Text=result;weatherValue.ToolTip="Open-Meteo · 更新于 "+DateTime.Now.ToString("HH:mm");}}catch{if(app.Store.Data.Settings.WeatherCity==city){weatherValue.Text="--°C · 离线";weatherValue.ToolTip="天气获取失败，稍后重试。来源：Open-Meteo";}}finally{weatherBusy=false;}}
}
}







