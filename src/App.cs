using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Markup;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace DeskNote {
public static partial class UI {
 public static void InstallTheme(Application app) {
  using(var stream=typeof(UI).Assembly.GetManifestResourceStream("DeskNote.DarkTheme"))app.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(stream));
 }
 public static Brush Ink = new SolidColorBrush(Color.FromRgb(241,245,249));
 public static Brush Muted = new SolidColorBrush(Color.FromRgb(160,170,188));
 public static Brush Green = new SolidColorBrush(Color.FromRgb(56,189,248));
 public static TextBlock Text(string text,double size=14,Brush brush=null) {return new TextBlock{Text=text,FontSize=size,Foreground=brush??Ink,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,5)};}
 public static Button Button(string text,Action click) {var b=new Button{Content=text,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,4,7,4),MinHeight=32};if(new[]{"保存","确认","解锁","设置并","使用新","更新口令","继续"}.Any(prefix=>text.StartsWith(prefix))){b.Background=Green;b.Foreground=new SolidColorBrush(Color.FromRgb(8,37,58));}b.Click+=(s,e)=>DeskApp.CurrentApp.Run(click);return b;}
 public static TextBox Input(string text="") {return new TextBox{Text=text??"",MinWidth=150,Padding=new Thickness(8),Margin=new Thickness(0,2,0,8)};}
 public static ComboBox Select(IEnumerable<string> list,string selected) {var c=new ComboBox{Margin=new Thickness(0,2,0,8),Padding=new Thickness(7),MinWidth=150};foreach(string x in list)c.Items.Add(x);c.SelectedItem=selected;if(c.SelectedIndex<0)c.SelectedIndex=0;return c;}
 public static string Value(ComboBox c) {return Convert.ToString(c.SelectedItem);}
 public static StackPanel Stack() {return new StackPanel();}
 public static WrapPanel Bar(params UIElement[] children) {var p=new WrapPanel();foreach(var c in children)p.Children.Add(c);return p;}
 public static void Field(Panel p,string label,UIElement input) {p.Children.Add(Text(label,12,Muted));p.Children.Add(input);}
 public static Window Window(string title,int width=470,int height=650) {return new DarkWindow{Icon=Branding.WindowIcon,Title=title,Width=width,Height=height,MinWidth=340,MinHeight=260,Background=new SolidColorBrush(Color.FromRgb(15,23,42)),FontFamily=new FontFamily("Microsoft YaHei UI"),Foreground=Ink,WindowStartupLocation=WindowStartupLocation.CenterScreen};}
 public static StackPanel Body(Window w) {var p=new StackPanel{Margin=new Thickness(24)};w.Content=new ScrollViewer{Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};return p;}
 public static bool Confirm(string text) {return UI.Message(text,"桌面随记",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;}
 public static decimal Money(TextBox b) {decimal n;if(!decimal.TryParse(b.Text.Trim(),NumberStyles.Number,CultureInfo.CurrentCulture,out n) || n<=0 || n>1000000000m || decimal.Round(n,2)!=n)throw new Exception("金额应大于0，不超过十亿元，且最多两位小数。");return n;}
 public static DateTime Date(TextBox b) {DateTime d;if(!DateTime.TryParseExact(b.Text.Trim(),"yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out d) || d.Year<1900 || d.Year>2200)throw new Exception("请按 yyyy-MM-dd HH:mm 输入日期时间，年份为1900—2200。");return d;}
 public static TextBox DateInput(DateTime d) {return Input(d.ToString("yyyy-MM-dd HH:mm"));}
}
public class DeskApp : Application {
 public static DeskApp CurrentApp;
 public Store Store;
 public PanelWindow Panel;
 public FinanceWindow Finance;
 public bool Unlocked, Exiting;
 public readonly List<Window> PrivateWindows=new List<Window>();
 public DateTime LastInput=DateTime.Now;
 DateTime nextUnlock=DateTime.MinValue;
 int failedUnlock;
 Forms.NotifyIcon tray;
 DispatcherTimer timer;
 HwndSource hook;
 Mutex mutex;
 bool registered, locking, restoredSuccessfully;
 uint shellRestartMessage;
 static readonly uint recoverMessage=RegisterWindowMessage("DeskNote.ShowPanel.v1");
 bool safeMode, openSettings;
 [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hwnd,uint message,IntPtr w,IntPtr l);
 [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);
 [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h,int id);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern uint RegisterWindowMessage(string message);
 [STAThread] public static void Main(string[] args) {
  if(args.Length>0 && args[0]=="--render-probe") {DisplayProbe.Run(args.Length>1?args[1]:"test-output-probe");return;}
  if(args.Length>0 && args[0]=="--self-test") {Environment.Exit(SelfTest.Run(args.Length>1?args[1]:"test-output"));return;}
  if(args.Length>0 && args[0]=="--render-test") {RenderTest.Run(args.Length>1?args[1]:"test-output");return;}
  if(args.Length>0 && args[0]=="--smoke-test") {SmokeTest.Run(args.Length>1?args[1]:"test-output");return;}
  if(args.Length>0 && args[0]=="--desktop-test") {DesktopTest.Run(args.Length>1?args[1]:"test-output");return;}
  if(args.Length>0 && args[0]=="--weather-test") {string folder=args.Length>1?args[1]:"test-output";Directory.CreateDirectory(folder);try{string result=Weather.Load("上海").GetAwaiter().GetResult();File.WriteAllText(Path.Combine(folder,"weather-test.txt"),"PASS Live Open-Meteo request and decoding: "+result);}catch(Exception ex){File.WriteAllText(Path.Combine(folder,"weather-test.txt"),"INFO Live weather unavailable; application shows offline state. "+ex.GetBaseException().Message);}return;}
  bool created;var app=new DeskApp();app.mutex=new Mutex(true,"Local\\DeskNote-"+System.Security.Principal.WindowsIdentity.GetCurrent().User.Value,out created);
  if(!created){PostMessage(new IntPtr(0xffff),recoverMessage,IntPtr.Zero,IntPtr.Zero);app.mutex.Dispose();return;}
  app.safeMode=Array.IndexOf(args,"--safe-mode")>=0;app.openSettings=Array.IndexOf(args,"--settings")>=0;
  CurrentApp=app;UI.InstallTheme(app);app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
  app.DispatcherUnhandledException+=(s,e)=>{LogError(e.Exception);UI.Message("操作未完成："+e.Exception.Message,"桌面随记");e.Handled=true;};
  app.Startup+=(s,e)=>app.Start();app.Exit+=(s,e)=>app.Cleanup();app.Run();
 }
 static void LogError(Exception ex){try{File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"display-errors.log"),DateTime.Now.ToString("s")+" "+ex+Environment.NewLine);}catch{}}
 public void Run(Action action) {try{action();}catch(Exception ex){LogError(ex);UI.Message(ex.Message,"操作未完成",MessageBoxButton.OK,MessageBoxImage.Warning);}}
 internal void Start(string testFolder=null) {
  string folder=testFolder??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DeskNote");
  try{Store=new Store(folder);}
  catch(Exception ex){
   if(UI.Message("无法读取本地数据。原文件将保留，只有成功恢复后才替换。\n是否选择加密备份或本机自动备份恢复？\n\n"+ex.Message,"桌面随记",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes){Shutdown();return;}
   Store=new Store(folder,true);Run(Restore);if(!restoredSuccessfully){Shutdown();return;}
  }
  if(safeMode)Store.Change(d=>{d.Settings.EmbedDesktop=false;d.Settings.Topmost=false;d.Settings.Left=SystemParameters.WorkArea.Left+40;d.Settings.Top=SystemParameters.WorkArea.Top+30;d.Settings.Opacity=1;});
  if(Store.Data.Settings.AutoStart||AutoStartEnabled())Run(()=>SetAutoStart(true));
  var menu=new Forms.ContextMenuStrip();
  menu.Items.Add("显示 / 隐藏备忘",null,(s,e)=>Dispatcher.Invoke(new Action(Toggle)));
  menu.Items.Add("个人财务…",null,(s,e)=>Dispatcher.Invoke(new Action(()=>Run(OpenFinance))));
  menu.Items.Add("锁定财务",null,(s,e)=>Dispatcher.Invoke(new Action(Lock)));
  menu.Items.Add("暂停提醒 1 小时",null,(s,e)=>Dispatcher.Invoke(new Action(()=>Run(()=>{Store.Change(d=>d.Settings.PauseUntil=DateTime.Now.AddHours(1));Panel.Refresh();}))));
  menu.Items.Add("恢复提醒",null,(s,e)=>Dispatcher.Invoke(new Action(()=>Run(()=>{Store.Change(d=>d.Settings.PauseUntil=null);Tick();}))));
  menu.Items.Add(new Forms.ToolStripSeparator());
  menu.Items.Add("加密备份…",null,(s,e)=>Dispatcher.Invoke(new Action(()=>Run(Backup))));
  menu.Items.Add("恢复备份…",null,(s,e)=>Dispatcher.Invoke(new Action(()=>Run(Restore))));
  menu.Items.Add("设置…",null,(s,e)=>Dispatcher.Invoke(new Action(()=>Run(Settings))));
  menu.Items.Add("找回面板（恢复位置与透明度）",null,(s,e)=>Dispatcher.Invoke(new Action(RecoverPanel)));
  menu.Items.Add("退出（停止提醒）",null,(s,e)=>Dispatcher.Invoke(new Action(()=>Run(Quit))));
  tray=new Forms.NotifyIcon{Text="桌面随记",Icon=Branding.TrayIcon,ContextMenuStrip=menu,Visible=true};
  tray.DoubleClick+=(s,e)=>Dispatcher.Invoke(new Action(()=>{Panel.Show();Panel.ApplyWindowMode();if(!Panel.Desktop.Attached)Panel.Activate();}));
  tray.BalloonTipClicked+=(s,e)=>Dispatcher.Invoke(new Action(()=>{Panel.Show();Panel.Activate();}));
  hook=new HwndSource(new HwndSourceParameters("DeskNoteHotkey"){Width=0,Height=0,WindowStyle=0});hook.AddHook(WndProc);RegisterKey();shellRestartMessage=RegisterWindowMessage("TaskbarCreated");
  InputManager.Current.PreProcessInput+=(s,e)=>{if(e.StagingItem.Input is KeyboardEventArgs || e.StagingItem.Input is MouseEventArgs)LastInput=DateTime.Now;};
  SystemEvents.SessionSwitch+=SessionSwitch;SystemEvents.PowerModeChanged+=PowerChanged;SystemEvents.DisplaySettingsChanged+=DisplaysChanged;
  CreatePanel();if(safeMode)RecoverPanel();if(openSettings)Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>Run(Settings)));
  timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(20)};timer.Tick+=(s,e)=>Run(Tick);timer.Start();Run(Tick);
 }
 void DisplaysChanged(object sender,EventArgs e){Dispatcher.BeginInvoke(new Action(()=>Run(()=>{if(Panel!=null)Panel.EnsureOnScreen();})));}
 void SessionSwitch(object sender,SessionSwitchEventArgs e) {if(e.Reason==SessionSwitchReason.SessionLock)Dispatcher.BeginInvoke(new Action(Lock));}
 void PowerChanged(object sender,PowerModeChangedEventArgs e) {if(e.Mode==PowerModes.Suspend)Dispatcher.BeginInvoke(new Action(Lock));if(e.Mode==PowerModes.Resume)Dispatcher.BeginInvoke(new Action(()=>Run(Tick)));}
 public void RecoverPanel(){if(Panel==null)return;Run(()=>{Store.Change(d=>{d.Settings.EmbedDesktop=false;d.Settings.Topmost=false;d.Settings.Opacity=1;});Panel.Show();Panel.ApplyPreferences();Panel.WindowState=WindowState.Normal;Panel.Width=Math.Min(Panel.Width,Math.Max(Panel.MinWidth,SystemParameters.WorkArea.Width-40));Panel.Height=Math.Min(Panel.Height,Math.Max(Panel.MinHeight,SystemParameters.WorkArea.Height-40));Panel.Left=SystemParameters.WorkArea.Left+20;Panel.Top=SystemParameters.WorkArea.Top+20;Panel.Opacity=1;Panel.Topmost=true;Panel.Activate();Panel.Topmost=false;Panel.SavePlacement();});}
 IntPtr WndProc(IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled) {if((uint)msg==recoverMessage){Dispatcher.BeginInvoke(new Action(RecoverPanel));handled=true;}if(msg==0x312){Lock();Panel.Hide();handled=true;}if(shellRestartMessage!=0&&(uint)msg==shellRestartMessage)Dispatcher.BeginInvoke(new Action(()=>{if(Panel!=null&&!Exiting)Panel.ApplyWindowMode();}));return IntPtr.Zero;}
 void CreatePanel(){var panel=new PanelWindow(this);Panel=panel;panel.Closed+=(s,e)=>{if(Exiting)return;Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{if(!Exiting&&ReferenceEquals(Panel,panel))Run(CreatePanel);}));};panel.Show();}
 public void RegisterKey() {if(hook==null)return;if(registered)UnregisterHotKey(hook.Handle,1);registered=RegisterHotKey(hook.Handle,1,0x4000|0x2|0x4,(uint)Store.Data.Settings.Hotkey);if(!registered)UI.Message("隐藏快捷键注册失败，可能已被占用。可在设置中修改，托盘隐藏功能仍可使用。","桌面随记");}
 public void Toggle() {if(Panel.IsVisible)Panel.Hide();else{Panel.Show();Panel.ApplyWindowMode();if(!Panel.Desktop.Attached)Panel.Activate();}}
 public void Notify(string title,string message){if(!Store.Data.Settings.EnableReminders)return;if(Store.Data.Settings.ReminderSound)System.Media.SystemSounds.Asterisk.Play();if(tray!=null)tray.ShowBalloonTip(8000,title,message,Forms.ToolTipIcon.Info);}
 public void Track(Window w) {PrivateWindows.Add(w);w.Closed+=(s,e)=>PrivateWindows.Remove(w);w.Deactivated+=(s,e)=>CheckBlur();}
 public void CheckBlur(){if(Store.Data.Settings.LockOnBlur)Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{uint pid;GetWindowThreadProcessId(GetForegroundWindow(),out pid);if(Unlocked && pid!=(uint)System.Diagnostics.Process.GetCurrentProcess().Id)Lock();}));}
 public void Lock() {if(locking)return;locking=true;try{Unlocked=false;foreach(var w in PrivateWindows.ToArray())if(PrivateWindows.Contains(w))w.Close();Finance=null;}finally{locking=false;}}
 public void RequireUnlocked() {if(!Unlocked)throw new Exception("财务已锁定，请重新打开。");}
 public bool Unlock() {
  if(Unlocked){LastInput=DateTime.Now;return true;}
  if(DateTime.Now<nextUnlock){UI.Message("口令尝试过多，请稍后再试。","桌面随记");return false;}
  bool setup=String.IsNullOrEmpty(Store.Data.PasswordHash), ok=false;
  var w=UI.Window(setup?"设置财务解锁口令":"解锁个人财务",430,360);var p=UI.Body(w);
  p.Children.Add(UI.Text(setup?"为你的账本设置口令":"个人财务已锁定",23));
  p.Children.Add(UI.Text(setup?"至少8个字符。忘记口令需使用加密备份恢复，无法找回原口令。":"财务明细仅在解锁后显示。",12,UI.Muted));
  var pass=new PasswordBox{Padding=new Thickness(8),Margin=new Thickness(0,8,0,8)};UI.Field(p,"解锁口令",pass);
  var confirm=new PasswordBox{Padding=new Thickness(8)};if(setup)UI.Field(p,"再次输入",confirm);
  var submit=UI.Button(setup?"设置并打开":"解锁",()=>{
   string value=pass.Password;
   if(setup){if(value.Length<8 || value!=confirm.Password)throw new Exception("口令至少8个字符，两次输入需一致。");Store.Change(d=>Codec.SetPassword(d,value));}
   else if(!Codec.CheckPassword(Store.Data,value)){failedUnlock++;pass.Clear();if(failedUnlock>=5){nextUnlock=DateTime.Now.AddSeconds(30);w.Close();}throw new Exception("口令错误。连续5次失败将暂停30秒。");}
   ok=true;failedUnlock=0;w.Close();
  });submit.IsDefault=true;p.Children.Add(submit);w.Loaded+=(s,e)=>pass.Focus();w.ShowDialog();
  Unlocked=ok;LastInput=DateTime.Now;return ok;
 }
 public void OpenFinance() {if(!Unlock())return;if(Finance==null){Finance=new FinanceWindow(this);Track(Finance);Finance.Closed+=(s,e)=>{Finance=null;Lock();};}Finance.Show();Finance.Activate();}
 public void EnsureTaskRange(DateTime from,DateTime through){var snapshot=Codec.Parse(Codec.Serialize(Store.Data));if(TaskSchedule.Generate(snapshot,from,through))Store.Change(d=>TaskSchedule.Generate(d,from,through));}
 public void Refresh() {if(Panel!=null)Panel.Refresh();if(Finance!=null)Finance.Refresh();}
 public void Tick() {Tick(DateTime.Now);}
 internal void Tick(DateTime now) {
  if(Unlocked && DateTime.Now-LastInput>TimeSpan.FromMinutes(Store.Data.Settings.LockMinutes))Lock();
  EnsureTaskRange(now.Date,now.Date.AddDays(30));
  bool needGeneration=Store.Data.Rules.Any(x=>x.Active); // Save only when there are new occurrences or notices.
  var copy=Codec.Parse(Codec.Serialize(Store.Data));if(needGeneration)Logic.Generate(copy,now);
  bool paused=!copy.Settings.EnableReminders || (copy.Settings.PauseUntil.HasValue && copy.Settings.PauseUntil>now);
  var bills=paused?new List<Bill>():copy.Bills.Where(x=>Logic.Ready(x,now)).ToList();
  var tasks=paused?new List<Item>():copy.Items.Where(x=>!x.Archived&&!x.Note && !x.Done && x.Due.HasValue && x.Due<=now && TaskSchedule.CanRemind(copy,x,now) && (!x.LastNotice.HasValue || x.LastNotice.Value.Date<now.Date)).ToList();
  if(copy.Bills.Count!=Store.Data.Bills.Count || bills.Count>0 || tasks.Count>0){
   Store.Change(d=>{Logic.Generate(d,now);foreach(var b in bills){var target=d.Bills.Single(x=>x.Id==b.Id);target.LastNotice=now;target.Snooze=null;}foreach(var t in tasks)d.Items.Single(x=>x.Id==t.Id).LastNotice=now;});
   if(bills.Count>0 || tasks.Count>0)Notify("桌面随记",(tasks.Count>0?"有待办事项已到期。 ":"")+(bills.Count>0?"有私人账单需要处理，请打开个人财务查看。":""));
  }
  Panel.Refresh();if(Finance!=null)Finance.Refresh();
 }
 public void Backup() {
  if(!Unlock())return;string pass=BackupPassword(true);if(pass==null)return;
  var save=new SaveFileDialog{Filter="随记加密备份|*.dsnb",FileName="随记备份-"+DateTime.Today.ToString("yyyyMMdd")+".dsnb"};
  if(save.ShowDialog()==true){RequireUnlocked();File.WriteAllBytes(save.FileName,Codec.Pack(Store.Data,pass));UI.Message("加密备份已保存。恢复时需要此备份口令。","桌面随记");}
 }
 string BackupPassword(bool create) {
  string result=null;var w=UI.Window(create?"加密备份口令":"恢复备份口令",430,380);Track(w);var p=UI.Body(w);
  p.Children.Add(UI.Text(create?"设置独立备份口令":"输入备份口令",22));p.Children.Add(UI.Text("该口令用于跨电脑恢复。请妥善保存，无法找回。",12,UI.Muted));
  PasswordBox a=new PasswordBox{Padding=new Thickness(8)},b=new PasswordBox{Padding=new Thickness(8)};UI.Field(p,"备份口令",a);if(create)UI.Field(p,"再次输入",b);
  p.Children.Add(UI.Button("继续",()=>{if(create && (a.Password.Length<8 || a.Password!=b.Password))throw new Exception("备份口令至少8个字符，两次输入需一致。");result=a.Password;w.Close();}));w.ShowDialog();return result;
 }
 public void Restore() {
  // Empty installations can restore by proving knowledge of the backup password.
  var open=new OpenFileDialog{Filter="随记备份|*.dsnb;*.dat;*.previous|所有文件|*.*"};if(open.ShowDialog()!=true)return;
  if(new FileInfo(open.FileName).Length>40*1024*1024)throw new Exception("备份文件过大。");byte[] raw=File.ReadAllBytes(open.FileName);
  State restored;bool portable=Path.GetExtension(open.FileName).Equals(".dsnb",StringComparison.OrdinalIgnoreCase)||(raw.Length>=8&&Encoding.ASCII.GetString(raw,0,8)=="DSNBKP01");
  if(!portable){if(!String.IsNullOrEmpty(Store.Data.PasswordHash)&&!Unlock())return;restored=Codec.Parse(System.Security.Cryptography.ProtectedData.Unprotect(raw,null,System.Security.Cryptography.DataProtectionScope.CurrentUser));}
  else{string pass=BackupPassword(false);if(pass==null)return;restored=Codec.Unpack(raw,pass);}
  if(!UI.Confirm("使用备份替换当前全部待办与财务数据？当前数据会先保存为恢复前备份。"))return;
  if(portable && !RecoveryPassword(restored))return;
  Lock(); // Preserve local window / startup preferences; restoring does not modify the registry.
  restored.Settings=Store.Data.Settings;Store.Restore(restored);restoredSuccessfully=true;Refresh();UI.Message(portable?"恢复完成。请使用刚设置的新口令打开财务。":"恢复完成。请使用本机备份中的财务解锁口令打开财务。","桌面随记");
 }
 bool RecoveryPassword(State recovered) {
  bool ok=false;var w=UI.Window("恢复后设置解锁口令",430,380);Track(w);var p=UI.Body(w);p.Children.Add(UI.Text("为恢复的账本设置新口令",22));p.Children.Add(UI.Text("备份口令已验证，无需原来的解锁口令。",12,UI.Muted));
  PasswordBox pass=new PasswordBox{Padding=new Thickness(8)},confirm=new PasswordBox{Padding=new Thickness(8)};UI.Field(p,"新口令（至少8个字符）",pass);UI.Field(p,"再次输入",confirm);p.Children.Add(UI.Button("使用新口令恢复",()=>{if(pass.Password.Length<8||pass.Password!=confirm.Password)throw new Exception("口令至少8个字符，两次输入需一致。");Codec.SetPassword(recovered,pass.Password);ok=true;w.Close();}));w.ShowDialog();return ok;
 }
 public void Settings() {new SettingsWindow(this).ShowDialog();}
 internal bool AutoStartEnabled(){using(var key=Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run"))return key!=null && !String.IsNullOrWhiteSpace(Convert.ToString(key.GetValue("DeskNote")));}
 internal void SetAutoStart(bool enabled){using(var key=Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run")){if(enabled)key.SetValue("DeskNote","\""+Forms.Application.ExecutablePath+"\"");else key.DeleteValue("DeskNote",false);}}
 public void Quit() {Panel.SavePlacement();Exiting=true;Lock();Shutdown();}
 internal void Cleanup() {Exiting=true;if(timer!=null)timer.Stop();SystemEvents.SessionSwitch-=SessionSwitch;SystemEvents.PowerModeChanged-=PowerChanged;SystemEvents.DisplaySettingsChanged-=DisplaysChanged;if(hook!=null){if(registered)UnregisterHotKey(hook.Handle,1);hook.Dispose();}if(tray!=null){tray.Visible=false;tray.Dispose();}if(mutex!=null)mutex.Dispose();}
}

}











