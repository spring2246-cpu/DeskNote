using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace DeskNote {
public partial class PanelWindow {
 void Edit(string id,bool note,DateTime? day=null) {
  Item source=id==null?new Item{Note=note}:app.Store.Data.Items.Single(x=>x.Id==id);
  DateTime initial=TaskSchedule.EditorTime(DateTime.Now,id==null?day:null,source.Due);
  var w=UI.Window(note?(id==null?"新增便签":"编辑便签"):(id==null?"新增待办":"编辑待办"),510,note?420:String.IsNullOrEmpty(source.SeriesId)?650:740);var p=UI.Body(w);
  var text=UI.Input(source.Text);text.AcceptsReturn=true;text.TextWrapping=TextWrapping.Wrap;text.Height=100;UI.Field(p,note?"随手记":"要做什么",text);
  var timed=new CheckBox{Content="设置日期与到期提醒",IsChecked=id==null||source.Due.HasValue,Margin=new Thickness(0,8,0,12)};
  var year=UI.Select(Enumerable.Range(1900,301).Select(n=>n.ToString()),initial.Year.ToString());year.Name="TaskYear";
  var month=UI.Select(Enumerable.Range(1,12).Select(n=>n.ToString("00")),initial.Month.ToString("00"));month.Name="TaskMonth";
  var date=UI.Select(Enumerable.Range(1,DateTime.DaysInMonth(initial.Year,initial.Month)).Select(n=>n.ToString("00")),initial.Day.ToString("00"));date.Name="TaskDay";
  var hour=UI.Select(Enumerable.Range(0,24).Select(n=>n.ToString("00")),initial.Hour.ToString("00"));hour.Name="TaskHour";
  var minute=UI.Select(Enumerable.Range(0,60).Select(n=>n.ToString("00")),initial.Minute.ToString("00"));minute.Name="TaskMinute";
  foreach(var combo in new[]{year,month,date,hour,minute}){combo.MinWidth=0;combo.HorizontalAlignment=HorizontalAlignment.Stretch;combo.Margin=new Thickness(0,2,8,5);}
  Action updateDays=()=>{int oldDay;int.TryParse(UI.Value(date),out oldDay);int y,m;if(!int.TryParse(UI.Value(year),out y)||!int.TryParse(UI.Value(month),out m))return;date.Items.Clear();int count=DateTime.DaysInMonth(y,m);for(int n=1;n<=count;n++)date.Items.Add(n.ToString("00"));date.SelectedItem=Math.Max(1,Math.Min(oldDay,count)).ToString("00");};
  year.SelectionChanged+=(s,e)=>updateDays();month.SelectionChanged+=(s,e)=>updateDays();
  var dateGrid=new Grid();foreach(var c in new[]{year,month,date}){dateGrid.ColumnDefinitions.Add(new ColumnDefinition());Grid.SetColumn(c,dateGrid.Children.Count);dateGrid.Children.Add(c);}
  var timeGrid=new Grid();foreach(var c in new[]{hour,minute}){timeGrid.ColumnDefinitions.Add(new ColumnDefinition());Grid.SetColumn(c,timeGrid.Children.Count);timeGrid.Children.Add(c);}
  var repeat=UI.Select(new[]{"不重复","每天","每周","每月","每年"},source.Repeat);repeat.Name="TaskRepeat";
  var existingSeries=app.Store.Data.TaskSeries.FirstOrDefault(s=>s.Id==source.SeriesId);
  DateTime endInitial=existingSeries!=null&&existingSeries.RepeatEnd.HasValue?existingSeries.RepeatEnd.Value:initial.Date.AddDays(30).AddHours(23).AddMinutes(59);
  var endMode=UI.Select(new[]{"不设截止，持续重复","指定截止日期和时间"},existingSeries!=null&&existingSeries.RepeatEnd.HasValue?"指定截止日期和时间":"不设截止，持续重复");endMode.Name="TaskEndMode";
  var endYear=UI.Select(Enumerable.Range(1900,301).Select(n=>n.ToString()),endInitial.Year.ToString());endYear.Name="TaskEndYear";
  var endMonth=UI.Select(Enumerable.Range(1,12).Select(n=>n.ToString("00")),endInitial.Month.ToString("00"));endMonth.Name="TaskEndMonth";
  var endDay=UI.Select(Enumerable.Range(1,DateTime.DaysInMonth(endInitial.Year,endInitial.Month)).Select(n=>n.ToString("00")),endInitial.Day.ToString("00"));endDay.Name="TaskEndDay";
  var endHour=UI.Select(Enumerable.Range(0,24).Select(n=>n.ToString("00")),endInitial.Hour.ToString("00"));endHour.Name="TaskEndHour";
  var endMinute=UI.Select(Enumerable.Range(0,60).Select(n=>n.ToString("00")),endInitial.Minute.ToString("00"));endMinute.Name="TaskEndMinute";
  foreach(var combo in new[]{endYear,endMonth,endDay,endHour,endMinute}){combo.MinWidth=0;combo.Margin=new Thickness(0,2,8,5);}
  Action updateEndDays=()=>{int previous,y,m;int.TryParse(UI.Value(endDay),out previous);if(!int.TryParse(UI.Value(endYear),out y)||!int.TryParse(UI.Value(endMonth),out m))return;int count=DateTime.DaysInMonth(y,m);endDay.Items.Clear();for(int n=1;n<=count;n++)endDay.Items.Add(n.ToString("00"));endDay.SelectedItem=Math.Max(1,Math.Min(previous,count)).ToString("00");};endYear.SelectionChanged+=(s,e)=>updateEndDays();endMonth.SelectionChanged+=(s,e)=>updateEndDays();
  var endDateGrid=new Grid();foreach(var combo in new[]{endYear,endMonth,endDay}){endDateGrid.ColumnDefinitions.Add(new ColumnDefinition());Grid.SetColumn(combo,endDateGrid.Children.Count);endDateGrid.Children.Add(combo);}
  var endTimeGrid=new Grid();foreach(var combo in new[]{endHour,endMinute}){endTimeGrid.ColumnDefinitions.Add(new ColumnDefinition());Grid.SetColumn(combo,endTimeGrid.Children.Count);endTimeGrid.Children.Add(combo);}
  var endSection=new StackPanel();UI.Field(endSection,"重复截止",endMode);
  var endDetails=new StackPanel();UI.Field(endDetails,"截止日期（年 / 月 / 日）",endDateGrid);UI.Field(endDetails,"截止时间（小时 / 分钟）",endTimeGrid);endDetails.Children.Add(UI.Text("包含截止时刻所在分钟；此后不再生成新事项或发送重复提醒。",11,UI.Muted));endSection.Children.Add(endDetails);
  Action updateEnd=()=>{bool repeating=UI.Value(repeat)!="不重复";endSection.Visibility=repeating?Visibility.Visible:Visibility.Collapsed;bool specified=UI.Value(endMode)=="指定截止日期和时间";endDetails.Visibility=specified?Visibility.Visible:Visibility.Collapsed;if(!note)w.Height=Math.Min(SystemParameters.WorkArea.Height-30,650+(repeating?75:0)+(repeating&&specified?160:0)+(String.IsNullOrEmpty(source.SeriesId)?0:75));endSection.InvalidateMeasure();endSection.InvalidateArrange();p.InvalidateMeasure();p.InvalidateArrange();};repeat.SelectionChanged+=(s,e)=>updateEnd();endMode.SelectionChanged+=(s,e)=>updateEnd();
  var high=new CheckBox{Content="高优先级",IsChecked=source.HighPriority,Margin=new Thickness(0,10,0,10)};
  ComboBox scope=null;
  if(!note){
   p.Children.Add(timed);UI.Field(p,"提醒日期（年 / 月 / 日）",dateGrid);UI.Field(p,"提醒时间（小时 / 分钟，00 分为整点）",timeGrid);
   var now=UI.Button("使用当前日期和时间",()=>{DateTime current=DateTime.Now;year.SelectedItem=current.Year.ToString();month.SelectedItem=current.Month.ToString("00");date.SelectedItem=current.Day.ToString("00");hour.SelectedItem=current.Hour.ToString("00");minute.SelectedItem=current.Minute.ToString("00");});p.Children.Add(now);
   UI.Field(p,"重复频率",repeat);p.Children.Add(UI.Text("重复待办会显示在对应日期，每天独立完成，不必等前一天完成。",11,UI.Muted));
   p.Children.Add(endSection);
   if(!String.IsNullOrEmpty(source.SeriesId)){scope=UI.Select(new[]{"本次及以后","仅本次"},"本次及以后");UI.Field(p,"修改范围",scope);scope.SelectionChanged+=(s,e)=>{repeat.IsEnabled=endSection.IsEnabled=UI.Value(scope)!="仅本次";if(!repeat.IsEnabled)repeat.SelectedItem=source.Repeat;};}
   updateEnd();p.Children.Add(high);Action enabled=()=>{bool active=timed.IsChecked==true;dateGrid.IsEnabled=timeGrid.IsEnabled=now.IsEnabled=active;};timed.Checked+=(s,e)=>enabled();timed.Unchecked+=(s,e)=>enabled();enabled();
  }
  p.Children.Add(UI.Button("保存",()=>{
   if(String.IsNullOrWhiteSpace(text.Text))throw new Exception("请填写内容。");DateTime? due=null;
   if(!note&&timed.IsChecked==true)due=new DateTime(int.Parse(UI.Value(year)),int.Parse(UI.Value(month)),int.Parse(UI.Value(date)),int.Parse(UI.Value(hour)),int.Parse(UI.Value(minute)),0);
   if(!note&&!due.HasValue&&UI.Value(repeat)!="不重复")throw new Exception("重复待办需要设置日期与提醒时间。");
   DateTime? repeatEnd=null;if(!note&&UI.Value(repeat)!="不重复"&&UI.Value(endMode)=="指定截止日期和时间")repeatEnd=new DateTime(int.Parse(UI.Value(endYear)),int.Parse(UI.Value(endMonth)),int.Parse(UI.Value(endDay)),int.Parse(UI.Value(endHour)),int.Parse(UI.Value(endMinute)),0);
   app.Store.Change(d=>TaskSchedule.Save(d,id,note,text.Text.Trim(),due,note?"不重复":UI.Value(repeat),high.IsChecked==true,scope==null||UI.Value(scope)=="本次及以后",repeatEnd));
   w.Close();Refresh();
  }));w.Loaded+=(s,e)=>text.Focus();w.ShowDialog();
 }
}
}


