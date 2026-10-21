using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
namespace DeskNote {
public partial class FinanceWindow {
 ComboBox accountFilter,monthFilter,categoryFilter,statusFilter;TextBox searchFilter;
 StackPanel accountBody,budgetBody;ComboBox horizon;bool updatingFilters;
 string[] AccountNames(){return app.Store.Data.Accounts.Where(x=>!x.Disabled).Select(x=>x.Name).Concat(accounts.Where(n=>!app.Store.Data.Accounts.Any(a=>a.Name==n&&a.Disabled))).Distinct().ToArray();}
 void CreateFilters(StackPanel p) {
  accountFilter=UI.Select(new[]{"全部账户"},"全部账户");monthFilter=UI.Select(new[]{"全部月份"},"全部月份");categoryFilter=UI.Select(new[]{"全部分类"},"全部分类");statusFilter=UI.Select(new[]{"全部状态","有效","待核对","已核对","已作废"},"全部状态");
  searchFilter=UI.Input("");searchFilter.Width=160;searchFilter.ToolTip="搜索备注、账户、分类";
  foreach(var c in new[]{accountFilter,monthFilter,categoryFilter,statusFilter}){c.MinWidth=110;c.SelectionChanged+=(s,e)=>{if(!updatingFilters&&entries!=null)Refresh();};}
  searchFilter.TextChanged+=(s,e)=>{if(entries!=null)Refresh();};
  p.Children.Add(UI.Bar(accountFilter,monthFilter,categoryFilter,statusFilter,UI.Text("搜索",12),searchFilter));
 }
 void Options(ComboBox c,IEnumerable<string> items) {string previous=UI.Value(c);var list=items.ToList();if(c.ItemsSource==null)c.Items.Clear();c.ItemsSource=list;c.SelectedItem=list.Contains(previous)?previous:list[0];}
 void UpdateFilters(){updatingFilters=true;try{var d=app.Store.Data;Options(accountFilter,new[]{"全部账户"}.Concat(d.Accounts.Select(x=>x.Name)));Options(monthFilter,new[]{"全部月份"}.Concat(d.Entries.Select(x=>x.Date.ToString("yyyy-MM")).Distinct().OrderByDescending(x=>x)));Options(categoryFilter,new[]{"全部分类"}.Concat(d.Entries.Select(x=>x.Category).Distinct().OrderBy(x=>x)));}finally{updatingFilters=false;}}
 IEnumerable<Entry> FilteredEntries(){return Ledger.Filter(app.Store.Data,UI.Value(accountFilter),UI.Value(monthFilter),UI.Value(categoryFilter),UI.Value(statusFilter),searchFilter.Text);}
 void ReconcileEntry(){app.RequireUnlocked();string id=SelectedId(entries);app.Store.Change(d=>{var e=d.Entries.Single(x=>x.Id==id);if(e.Voided)throw new Exception("作废流水不能核对。");e.Reconciled=!e.Reconciled;Ledger.AuditChange(d,e,e.Reconciled?"标记已核对":"取消核对");});app.Refresh();}
 void ShowAudit(){app.RequireUnlocked();string id=SelectedId(entries);var w=UI.Window("流水修改记录",530,500);app.Track(w);var p=UI.Body(w);p.Children.Add(UI.Text("加密保存的操作记录",22));var records=app.Store.Data.Audit.Where(x=>x.EntryId==id).OrderByDescending(x=>x.At).ToList();foreach(var r in records)p.Children.Add(UI.Text(r.At.ToString("yyyy-MM-dd HH:mm:ss")+"  "+r.Action+"\n"+r.Details,13));if(records.Count==0)p.Children.Add(UI.Text("此历史流水暂无修改记录。",13,UI.Muted));w.ShowDialog();}
 void CreateLedgerTabs(TabControl tabs) {
  accountBody=new StackPanel{Margin=new Thickness(22)};AddTab(tabs,"账户",new ScrollViewer{Content=accountBody,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
  budgetBody=new StackPanel{Margin=new Thickness(22)};AddTab(tabs,"预算与预测",new ScrollViewer{Content=budgetBody,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
 }
 void RefreshLedger() {
  if(accountBody==null)return;var d=app.Store.Data;
  accountBody.Children.Clear();accountBody.Children.Add(UI.Text("账户与余额",25));accountBody.Children.Add(UI.Text("期初余额指起算日当天流水发生前的余额。早于起算日的流水保留，但不重复计入余额。停用账户仍保留历史及余额。",12,UI.Muted));accountBody.Children.Add(UI.Button("＋ 添加账户",()=>EditAccount(null)));
  foreach(var a in d.Accounts){string id=a.Id;decimal? balance=Ledger.Balance(d,a,DateTime.Today);accountBody.Children.Add(UI.Bar(UI.Text(a.Name+(a.Disabled?" · 已停用":"")+"   "+(balance.HasValue?"¥"+balance.Value.ToString("N2"):"未设置期初余额"),17),UI.Button("编辑",()=>EditAccount(id))));accountBody.Children.Add(UI.Text(a.Type+" · 起算 "+a.OpeningDate.ToString("yyyy-MM-dd"),11,UI.Muted));}
  if(d.Accounts.Count==0)accountBody.Children.Add(UI.Text("添加账户，或记账时输入账户名称。",13,UI.Muted));
  budgetBody.Children.Clear();budgetBody.Children.Add(UI.Text("每月分类预算",25));budgetBody.Children.Add(UI.Text("预算按自然月统计有效实际支出；不含作废流水、转账和未付计划账单。",12,UI.Muted));budgetBody.Children.Add(UI.Button("＋ 设置分类预算",EditBudget));
  var first=new DateTime(DateTime.Today.Year,DateTime.Today.Month,1);
  foreach(var b in d.Budgets){decimal used=d.Entries.Where(e=>!e.Voided&&e.Kind=="支出"&&e.Category==b.Category&&e.Date>=first&&e.Date.Date<=DateTime.Today).Sum(e=>e.Amount);string category=b.Category;budgetBody.Children.Add(UI.Bar(UI.Text(b.Category+"   已用 ¥"+used.ToString("N2")+" / ¥"+b.Amount.ToString("N2")+"   "+(used>b.Amount?"超出":"剩余")+" ¥"+Math.Abs(b.Amount-used).ToString("N2"),14),UI.Button("移除",()=>{app.RequireUnlocked();app.Store.Change(s=>s.Budgets.RemoveAll(x=>x.Category==category));app.Refresh();})));}
  budgetBody.Children.Add(UI.Text("现金流预测",23));
  string period=horizon==null?"30天":UI.Value(horizon);horizon=UI.Select(new[]{"30天","90天","365天"},period);var forecast=UI.Text("",18,UI.Green);
  Action calculate=()=>{int days=int.Parse(UI.Value(horizon).Replace("天",""));decimal net=Ledger.PlannedNet(d,DateTime.Today,DateTime.Today.AddDays(days));bool known=d.Accounts.Count>0&&d.Accounts.All(a=>Ledger.Balance(d,a,DateTime.Today).HasValue);forecast.Text="未来"+days+"天预计净变化  ¥"+net.ToString("N2")+(known?"\n预计总余额  ¥"+(d.Accounts.Sum(a=>Ledger.Balance(d,a,DateTime.Today).Value)+net).ToString("N2"):"\n有账户缺少期初余额，暂不计算预计总资产。");};horizon.SelectionChanged+=(s,e)=>calculate();budgetBody.Children.Add(horizon);budgetBody.Children.Add(forecast);calculate();
  budgetBody.Children.Add(UI.Text("预测从今天收盘余额起算，包含已录入的未来流水，以及预测期内未支付账单（含逾期）。周期账单按现有启用规则推演；已支付或已有有效关联流水的账单不重复计算，转账不改变总额。预测不会自动入账，也不包含未录入的消费或收入。",12,UI.Muted));
 }
 void EditAccount(string id) {
  app.RequireUnlocked();var a=id==null?new LedgerAccount{OpeningDate=DateTime.Today}:app.Store.Data.Accounts.Single(x=>x.Id==id);var w=UI.Window("账户设置",490,640);app.Track(w);var p=UI.Body(w);
  var name=UI.Input(a.Name);UI.Field(p,"账户名称",name);var type=UI.Select(new[]{"日常账户","储蓄账户","信用账户","其他"},a.Type);UI.Field(p,"账户类型",type);var balance=UI.Input(a.OpeningBalance.HasValue?a.OpeningBalance.Value.ToString("0.00"):"");UI.Field(p,"期初余额（可负数；留空表示尚未核实）",balance);var date=UI.DateInput(a.OpeningDate);UI.Field(p,"余额起算日期",date);var disabled=new CheckBox{Content="停用账户（保留历史与余额）",IsChecked=a.Disabled,Margin=new Thickness(0,12,0,12)};p.Children.Add(disabled);
  p.Children.Add(UI.Text("旧账户请核对起算日及当天记账前余额。修改期初余额会重新计算其后的所有余额。",12,UI.Muted));
  p.Children.Add(UI.Button("保存账户",()=>{app.RequireUnlocked();decimal amount=0;bool has=!String.IsNullOrWhiteSpace(balance.Text);if(has&&(!decimal.TryParse(balance.Text,out amount)||Math.Abs(amount)>1000000000m||decimal.Round(amount,2)!=amount))throw new Exception("余额须为最多两位小数的金额。");DateTime start=UI.Date(date).Date;if(String.IsNullOrWhiteSpace(name.Text))throw new Exception("请填写名称。");app.Store.Change(d=>{LedgerAccount target;if(id==null){if(d.Accounts.Any(x=>x.Name==name.Text.Trim()))throw new Exception("账户名称重复。");target=new LedgerAccount{Name=name.Text.Trim()};d.Accounts.Add(target);}else{Ledger.Rename(d,id,name.Text);target=d.Accounts.Single(x=>x.Id==id);}target.Type=UI.Value(type);target.OpeningBalance=has?(decimal?)amount:null;target.OpeningDate=start;target.Disabled=disabled.IsChecked==true;});w.Close();app.Refresh();}));w.ShowDialog();
 }
 void EditBudget(){app.RequireUnlocked();var w=UI.Window("每月分类预算",420,340);app.Track(w);var p=UI.Body(w);var category=Editable(categories.Concat(app.Store.Data.Entries.Select(x=>x.Category)).Distinct().ToArray(),"餐饮");UI.Field(p,"分类",category);var amount=UI.Input("");UI.Field(p,"每月预算金额",amount);p.Children.Add(UI.Button("保存预算",()=>{app.RequireUnlocked();decimal money=UI.Money(amount);string c=category.Text.Trim();if(c.Length==0)throw new Exception("请填写分类。");app.Store.Change(d=>{d.Budgets.RemoveAll(x=>x.Category==c);d.Budgets.Add(new MonthlyBudget{Category=c,Amount=money});});w.Close();app.Refresh();}));w.ShowDialog();}
}
}



