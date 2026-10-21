using System;
using System.Linq;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
namespace DeskNote {
public partial class PanelWindow {
 public void EnsureOnScreen(){
  if(!IsLoaded)return;Rect b=DesktopHost.ScreenBoundsPixels(this);
  bool reachable=System.Windows.Forms.Screen.AllScreens.Any(s=>{var work=s.WorkingArea;Rect r=new Rect(work.Left,work.Top,work.Width,work.Height);r.Intersect(new Rect(b.X,b.Y,b.Width,Math.Min(70,b.Height)));return !r.IsEmpty&&r.Width>=120&&r.Height>=25;});
  if(!reachable)app.RecoverPanel();
 }
 public void OpenLibrary() {
  var w=UI.Window("待办与便签 · 搜索与归档",850,660);var p=UI.Body(w);p.Children.Add(UI.Text("所有内容，都能找回来",24));
  var search=UI.Input("");search.ToolTip="输入内容关键词";UI.Field(p,"全文搜索（含已完成与归档）",search);
  var type=UI.Select(new[]{"所有类型","待办","便签"},"所有类型");var state=UI.Select(new[]{"未归档","已归档","全部"},"未归档");p.Children.Add(UI.Bar(type,state));
  var g=new DataGrid{AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,SelectionMode=DataGridSelectionMode.Single,Height=280,Margin=new Thickness(0,12,0,12)};
  foreach(var pair in new[]{new[]{"内容","Text"},new[]{"日期","Date"},new[]{"状态","Status"}})g.Columns.Add(new DataGridTextColumn{Header=pair[0],Binding=new System.Windows.Data.Binding(pair[1]),Width=new DataGridLength(pair[1]=="Text"?3:1,DataGridLengthUnitType.Star)});p.Children.Add(g);
  Action refresh=()=>{var old=g.SelectedItem as LibraryRow;string q=search.Text.Trim();var rows=app.Store.Data.Items.Where(x=>(q.Length==0||x.Text.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0)&&(UI.Value(type)=="所有类型"||(UI.Value(type)=="便签"?x.Note:!x.Note))&&(UI.Value(state)=="全部"||(UI.Value(state)=="已归档"?x.Archived:!x.Archived))).OrderByDescending(x=>x.Due??DateTime.MinValue).Select(x=>new LibraryRow{Id=x.Id,Text=x.Text,Date=x.Due.HasValue?x.Due.Value.ToString("yyyy-MM-dd HH:mm"):"便签 / 未安排",Status=(x.Archived?"已归档 · ":"")+(x.Note?"便签":x.Done?"已完成":"未完成")}).ToList();g.ItemsSource=rows;if(old!=null)g.SelectedItem=rows.FirstOrDefault(x=>x.Id==old.Id);};
  Func<Item> selectedItem=()=>{var r=g.SelectedItem as LibraryRow;if(r==null)throw new Exception("请先选择内容。");return app.Store.Data.Items.Single(x=>x.Id==r.Id);};
  p.Children.Add(UI.Bar(UI.Button("编辑所选",()=>{var i=selectedItem();Edit(i.Id,i.Note);refresh();}),UI.Button("归档 / 恢复本条",()=>{string id=selectedItem().Id;app.Store.Change(d=>{var i=d.Items.Single(x=>x.Id==id);i.Archived=!i.Archived;});Refresh();refresh();}),UI.Button("在面板查看",()=>{var i=selectedItem();if(i.Archived)throw new Exception("请先恢复归档内容。");filter=i.Note?"便签":"全部";if(i.Due.HasValue){selected=i.Due.Value.Date;weekStart=StartOfWeek(selected);}Show();Refresh();w.Close();})));
  p.Children.Add(UI.Text("归档仅隐藏本条内容并停止其提醒，不会停止系列后续重复。停止后续重复请在面板的事项菜单中操作。恢复后仍保留原完成状态及截止规则。",12,UI.Muted));
  search.TextChanged+=(s,e)=>refresh();type.SelectionChanged+=(s,e)=>refresh();state.SelectionChanged+=(s,e)=>refresh();refresh();w.ShowDialog();
 }
 public class LibraryRow {public string Id{get;set;}public string Text{get;set;}public string Date{get;set;}public string Status{get;set;}}
 void ExportDesktopDiagnostics(){var save=new SaveFileDialog{Filter="文本诊断|*.txt",FileName="桌面显示诊断.txt"};if(save.ShowDialog()!=true)return;var b=DesktopHost.ScreenBoundsPixels(this);File.WriteAllText(save.FileName,"桌面随记 显示诊断\r\n时间："+DateTime.Now.ToString("s")+"\r\n模式："+desktop.Status+"\r\n宿主："+DesktopHost.FindHost()+"\r\n渲染句柄："+desktop.Handle+"\r\n父窗口："+desktop.Parent+"\r\n有效挂接："+desktop.Attached+"\r\n渲染目标："+desktop.HasRenderTarget+"\r\n边界（像素）："+b+"\r\n显示器数量："+System.Windows.Forms.Screen.AllScreens.Length+"\r\n此报告不含便签正文或财务内容。",System.Text.Encoding.UTF8);}
}
}
