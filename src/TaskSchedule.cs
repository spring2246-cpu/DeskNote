using System;
using System.Linq;
using System.Collections.Generic;
namespace DeskNote {
public static class TaskSchedule {
 static bool Repeats(string value){return new[]{"每天","每周","每月","每年"}.Contains(value);}
 public static DateTime EditorTime(DateTime now,DateTime? day,DateTime? existing){var value=existing??(day??now).Date.AddHours(now.Hour).AddMinutes(now.Minute);return new DateTime(value.Year,value.Month,value.Day,value.Hour,value.Minute,0);}
 public static bool Generate(State state,DateTime from,DateTime through){
  bool changed=false;
  // Previous versions linked occurrences by their shared anchor, text and interval.
  var legacy=state.Items.Where(x=>!x.Note&&x.Due.HasValue&&Repeats(x.Repeat)&&String.IsNullOrEmpty(x.SeriesId)).GroupBy(x=>new{Anchor=x.RepeatAnchor??x.Due.Value,x.Text,x.Repeat}).ToList();
  foreach(var group in legacy){var first=group.OrderBy(x=>x.Due).First();var series=new TaskSeries{Text=first.Text,Start=group.Key.Anchor,Repeat=first.Repeat,HighPriority=first.HighPriority};state.TaskSeries.Add(series);foreach(var item in group)item.SeriesId=series.Id;changed=true;}
  var families=state.TaskSeries.ToDictionary(x=>x.Id,x=>Family(x));
  var dates=new HashSet<string>(state.Items.Where(x=>!String.IsNullOrEmpty(x.SeriesId)&&x.Due.HasValue).Select(x=>Key(families.ContainsKey(x.SeriesId)?families[x.SeriesId]:x.SeriesId,x.Due.Value)));
  foreach(var series in state.TaskSeries.Where(x=>x.Active&&Repeats(x.Repeat))){
   int guess=0;double days=(from.Date-series.Start.Date).TotalDays;
   if(series.Repeat=="每天")guess=(int)days;else if(series.Repeat=="每周")guess=(int)Math.Floor(days/7);else if(series.Repeat=="每月")guess=(from.Year-series.Start.Year)*12+from.Month-series.Start.Month;else guess=from.Year-series.Start.Year;
   for(int n=Math.Max(0,guess-1);;n++){
    DateTime due;try{due=Logic.Occurrence(series.Start,series.Repeat,n);}catch(ArgumentOutOfRangeException){break;}
    if(due.Date>through.Date||(series.Until.HasValue&&due>=series.Until.Value)||(series.RepeatEnd.HasValue&&due>series.RepeatEnd.Value))break;if(due.Date<from.Date||(series.From.HasValue&&due<series.From.Value)||series.Excluded.Contains(due))continue;
    if(dates.Add(Key(Family(series),due))){state.Items.Add(new Item{Text=series.Text,Due=due,Repeat=series.Repeat,RepeatAnchor=series.Start,SeriesId=series.Id,HighPriority=series.HighPriority});changed=true;}
   }
  }return changed;
 }
 static string Family(TaskSeries series){return String.IsNullOrEmpty(series.FamilyId)?series.Id:series.FamilyId;}
 static string Key(string id,DateTime date){return id+":"+date.Ticks;}
 public static bool CanRemind(State state,Item item,DateTime now){var series=state.TaskSeries.FirstOrDefault(x=>x.Id==item.SeriesId);return !item.Archived&&(series==null||(series.Active&&(!series.Until.HasValue||!item.Due.HasValue||item.Due<series.Until)&&(!series.RepeatEnd.HasValue||now<series.RepeatEnd.Value.AddMinutes(1))));}
 public static void Save(State state,string id,bool note,string text,DateTime? due,string repeat,bool high,bool future,DateTime? repeatEnd=null){
  if(!note&&Repeats(repeat)&&due.HasValue&&repeatEnd.HasValue&&repeatEnd<due)throw new Exception("重复截止时间不能早于首次提醒时间。");
  Item target=id==null?new Item{Note=note}:state.Items.Single(x=>x.Id==id);if(id==null)state.Items.Add(target);
  var series=state.TaskSeries.FirstOrDefault(x=>x.Id==target.SeriesId);DateTime? oldDue=target.Due;string oldRepeat=target.Repeat;
  if(series!=null&&!future){
   if(due!=oldDue&&oldDue.HasValue){if(due.HasValue&&state.Items.Any(x=>x.Id!=target.Id&&x.SeriesId==series.Id&&x.Due==due))throw new Exception("该时间已有同系列待办，请选择其它时间。");if(!series.Excluded.Contains(oldDue.Value))series.Excluded.Add(oldDue.Value);}
   repeat=oldRepeat;
  }else{
   string family=series==null?null:Family(series);DateTime cut=oldDue??(due??DateTime.Today);if(due.HasValue&&due<cut)cut=due.Value;
   var previous=series==null?new List<TaskSeries>():state.TaskSeries.Where(x=>Family(x)==family).ToList();
   var excluded=previous.SelectMany(x=>x.Excluded).Where(x=>x>=cut).Distinct().ToList();
   if(series!=null){
    var familyIds=new HashSet<string>(previous.Select(x=>x.Id));
    state.Items.RemoveAll(x=>x.Id!=target.Id&&familyIds.Contains(x.SeriesId)&&!x.Done&&!x.Archived&&x.Due>=cut);
    foreach(var rule in previous)if(!rule.Until.HasValue||rule.Until>cut)rule.Until=cut;
   }
   if(!note&&due.HasValue&&Repeats(repeat)){
    DateTime anchor=series!=null&&due==oldDue&&repeat==oldRepeat?series.Start:due.Value;
    series=new TaskSeries{Text=text,Start=anchor,From=due,Repeat=repeat,HighPriority=high,Excluded=excluded,RepeatEnd=repeatEnd};
    series.FamilyId=family??series.Id;state.TaskSeries.Add(series);target.SeriesId=series.Id;
   }else target.SeriesId="";
  }
  target.Text=text;target.Note=note;target.HighPriority=!note&&high;
  if(target.Due!=due||target.Repeat!=repeat){target.LastNotice=null;target.RepeatAnchor=due;}
  target.Due=due;target.Repeat=note?"不重复":repeat;
  Generate(state,DateTime.Today,DateTime.Today.AddDays(30));
 }
 public static void Delete(State state,string id){var item=state.Items.Single(x=>x.Id==id);var series=state.TaskSeries.FirstOrDefault(x=>x.Id==item.SeriesId);if(series!=null&&item.Due.HasValue&&!series.Excluded.Contains(item.Due.Value))series.Excluded.Add(item.Due.Value);state.Items.Remove(item);}
 public static void Stop(State state,string id){var item=state.Items.Single(x=>x.Id==id);var series=state.TaskSeries.FirstOrDefault(x=>x.Id==item.SeriesId);if(series==null)return;DateTime cutoff=item.Due??DateTime.Today;var rules=state.TaskSeries.Where(x=>Family(x)==Family(series)).ToList();foreach(var rule in rules)if(!rule.Until.HasValue||rule.Until>cutoff)rule.Until=cutoff;var ids=new HashSet<string>(rules.Select(x=>x.Id));state.Items.RemoveAll(x=>ids.Contains(x.SeriesId)&&!x.Done&&!x.Archived&&x.Due>=cutoff);}
}
}


