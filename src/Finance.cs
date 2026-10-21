using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DeskNote {
public partial class FinanceWindow : DarkWindow {
 DeskApp app;
 StackPanel overview;
 DataGrid entries,bills,rules;
 TextBlock summary;
 ComboBox billFilter;
 static string[] kinds={"支出","收入","转账"};
 static string[] categories={"餐饮","交通","购物","居住","医疗","娱乐","工资","其他"};
 static string[] accounts={"现金","银行卡","微信","支付宝"};
 public FinanceWindow(DeskApp a) {
  app=a;Title="桌面随记 · 私人空间";Width=1020;Height=720;MinWidth=740;MinHeight=500;FontFamily=new FontFamily("Microsoft YaHei UI");Background=new SolidColorBrush(Color.FromRgb(15,23,42));WindowStartupLocation=WindowStartupLocation.CenterScreen;
  var root=new DockPanel{Margin=new Thickness(26)};Content=root;
  var top=new DockPanel();DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
  var lockButton=UI.Button("立即锁定",()=>app.Lock());DockPanel.SetDock(lockButton,Dock.Right);top.Children.Add(lockButton);top.Children.Add(UI.Text("私人空间",27));
  var footer=UI.Text("本地加密保存 · 关闭窗口即锁定 · 不连接银行或支付平台",11,UI.Muted);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
  var tabs=new TabControl{Margin=new Thickness(0,18,0,16)};root.Children.Add(tabs);
  overview=new StackPanel{Margin=new Thickness(20)};AddTab(tabs,"概览",new ScrollViewer{Content=overview,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
  var flow=UI.Stack();flow.Margin=new Thickness(15);summary=UI.Text("",13);flow.Children.Add(summary);
  flow.Children.Add(UI.Bar(UI.Button("＋ 记一笔",()=>EditEntry(null)),UI.Button("编辑所选",()=>EditEntry(SelectedId(entries))),UI.Button("作废 / 恢复",DeleteEntry),UI.Button("核对 / 取消",ReconcileEntry),UI.Button("修改记录",ShowAudit),UI.Button("导出 CSV",ExportCsv)));
  CreateFilters(flow);entries=Grid(new[]{"日期","类型","金额","分类","账户","转入账户","备注","关联账单","状态"},new[]{"Date","Kind","Amount","Category","Account","ToAccount","Memo","Linked","Status"});flow.Children.Add(entries);AddTab(tabs,"流水",flow);
  var billPanel=new StackPanel{Margin=new Thickness(15)};
  billPanel.Children.Add(UI.Bar(UI.Button("＋ 周期 / 一次性账单",()=>EditRule(null)),UI.Button("确认支付 / 补记",Pay),UI.Button("稍后提醒",Snooze)));
  billFilter=UI.Select(new[]{"未支付","已支付","全部"},"未支付");billFilter.SelectionChanged+=(s,e)=>Refresh();billPanel.Children.Add(billFilter);
  bills=Grid(new[]{"到期时间","账单","金额","类型","账户","状态","提醒"},new[]{"Date","Name","Amount","Kind","Account","Status","Notice"});bills.Height=230;billPanel.Children.Add(bills);
  billPanel.Children.Add(UI.Text("周期规则 · 修改只影响尚未生成的期次",12,UI.Muted));
  billPanel.Children.Add(UI.Bar(UI.Button("编辑规则",()=>EditRule(SelectedId(rules))),UI.Button("启用 / 停用",ToggleRule),UI.Button("删除所选未付期次",DeleteBill)));
  rules=Grid(new[]{"名称","周期","起始时间","金额","状态"},new[]{"Name","Repeat","Date","Amount","Status"});rules.Height=140;billPanel.Children.Add(rules);AddTab(tabs,"账单",new ScrollViewer{Content=billPanel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
  var settings=new StackPanel{Margin=new Thickness(25)};settings.Children.Add(UI.Text("数据与隐私",22));settings.Children.Add(UI.Text("本地文件由当前 Windows 用户身份加密。解锁口令限制应用内访问，不能隔离同一 Windows 账户下的其他程序。加密备份使用独立口令，可跨电脑迁移。",14,UI.Muted));
  settings.Children.Add(UI.Bar(UI.Button("加密备份",()=>app.Backup()),UI.Button("恢复备份",()=>app.Restore())));
  settings.Children.Add(UI.Button("修改解锁口令",ChangePassword));settings.Children.Add(UI.Button("应用设置",()=>app.Settings()));
  settings.Children.Add(UI.Text("忘记解锁口令时，可凭独立备份口令恢复 .dsnb 备份并重设解锁口令。备份口令无法找回，请妥善保管。本机 .dat 快照保留原解锁口令。",12,UI.Muted));AddTab(tabs,"设置",settings);
  CreateLedgerTabs(tabs);Refresh();
 }
 void AddTab(TabControl t,string title,object body){t.Items.Add(new TabItem{Header=title,Content=body,Padding=new Thickness(22,10,22,10)});}
 DataGrid Grid(string[] labels,string[] paths) {
  var g=new DataGrid{AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,SelectionMode=DataGridSelectionMode.Single,SelectionUnit=DataGridSelectionUnit.FullRow,Height=410,Margin=new Thickness(0,10,0,8),RowHeaderWidth=0,GridLinesVisibility=DataGridGridLinesVisibility.Horizontal,HorizontalGridLinesBrush=new SolidColorBrush(Color.FromRgb(51,65,85)),Background=new SolidColorBrush(Color.FromRgb(18,29,48)),AlternatingRowBackground=new SolidColorBrush(Color.FromRgb(24,36,55)),BorderThickness=new Thickness(0),RowHeight=36};
  for(int i=0;i<labels.Length;i++)g.Columns.Add(new DataGridTextColumn{Header=labels[i],Binding=new System.Windows.Data.Binding(paths[i]),Width=new DataGridLength(1,DataGridLengthUnitType.Star),MinWidth=65});return g;
 }
 string SelectedId(DataGrid grid){var row=grid.SelectedItem as Row;if(row==null)throw new Exception("请先选择一条记录。");return row.Id;}
 public void Refresh() {
  if(!app.Unlocked)return;var d=app.Store.Data;DateTime month=new DateTime(DateTime.Today.Year,DateTime.Today.Month,1);var current=d.Entries.Where(x=>!x.Voided&&x.Date>=month&&x.Date.Date<=DateTime.Today);
  decimal income=current.Where(x=>x.Kind=="收入").Sum(x=>x.Amount),expense=current.Where(x=>x.Kind=="支出").Sum(x=>x.Amount);
  overview.Children.Clear();overview.Children.Add(UI.Text(DateTime.Today.ToString("yyyy年M月"),14,UI.Muted));overview.Children.Add(UI.Text("收支有数，日常从容",25));
  overview.Children.Add(UI.Text("收入  ¥"+income.ToString("N2")+"     支出  ¥"+expense.ToString("N2"),24,UI.Green));overview.Children.Add(UI.Text("本月结余  ¥"+(income-expense).ToString("N2")+"  · 转账不计入收支；余额请查看账户页",12,UI.Muted));
  overview.Children.Add(UI.Bar(UI.Button("＋ 记一笔",()=>EditEntry(null)),UI.Button("＋ 新账单",()=>EditRule(null))));
  overview.Children.Add(UI.Text("支出分类",18));foreach(var group in current.Where(x=>x.Kind=="支出").GroupBy(x=>x.Category).OrderByDescending(x=>x.Sum(y=>y.Amount)))overview.Children.Add(UI.Text(group.Key+"    ¥"+group.Sum(x=>x.Amount).ToString("N2")));
  if(expense==0)overview.Children.Add(UI.Text("本月还没有支出记录。",13,UI.Muted));
  overview.Children.Add(UI.Text("临近与逾期账单",18));var upcoming=d.Bills.Where(x=>!x.Paid&&x.Due.Date<=DateTime.Today.AddDays(30)).OrderBy(x=>x.Due).ToList();
  foreach(var b in upcoming.Take(12))overview.Children.Add(UI.Text(b.Due.ToString("MM-dd")+"  "+b.Name+"    ¥"+b.Amount.ToString("N2")+(b.Due<DateTime.Now?" · 已到期":"")));
  if(upcoming.Count==0)overview.Children.Add(UI.Text("未来30天没有待付账单。",13,UI.Muted));
  summary.Text="全部流水 "+d.Entries.Count+" 笔 · 本月收入 ¥"+income.ToString("N2")+" · 支出 ¥"+expense.ToString("N2");
  string selected=entries.SelectedItem is Row?((Row)entries.SelectedItem).Id:null;
  UpdateFilters();RefreshLedger();var entryRows=FilteredEntries().OrderByDescending(x=>x.Date).Select(x=>new Row{Id=x.Id,Date=x.Date.ToString("yyyy-MM-dd"),Kind=x.Kind,Amount=x.Amount.ToString("N2"),Category=x.Category,Account=x.Account,ToAccount=x.ToAccount,Memo=x.Memo,Status=x.Voided?"已作废":x.Reconciled?"已核对":"待核对",Linked=String.IsNullOrEmpty(x.BillId)?"":"已关联"}).ToList();entries.ItemsSource=entryRows;if(selected!=null)entries.SelectedItem=entryRows.FirstOrDefault(x=>x.Id==selected);
  if(billFilter==null)return;string filter=UI.Value(billFilter),bid=bills.SelectedItem is Row?((Row)bills.SelectedItem).Id:null,rid=rules.SelectedItem is Row?((Row)rules.SelectedItem).Id:null;
  var billRows=d.Bills.Where(x=>filter=="全部"||(filter=="已支付"?x.Paid:!x.Paid)).OrderBy(x=>x.Due).Select(x=>new Row{Id=x.Id,Date=x.Due.ToString("yyyy-MM-dd HH:mm"),Name=x.Name,Amount=x.Amount.ToString("N2"),Kind=x.Kind,Account=x.Account,Status=x.Paid?"已支付":x.Due<DateTime.Now?"已到期":"待支付",Notice=x.Snooze.HasValue?"延至 "+x.Snooze.Value.ToString("M/d HH:mm"):"提前"+x.Advance+"天"}).ToList();bills.ItemsSource=billRows;bills.SelectedItem=billRows.FirstOrDefault(x=>x.Id==bid);
  var ruleRows=d.Rules.Select(x=>new Row{Id=x.Id,Name=x.Name,Repeat=x.Repeat,Date=x.Start.ToString("yyyy-MM-dd HH:mm"),Amount=x.Amount.ToString("N2"),Status=x.Active?"启用":"停用"}).ToList();rules.ItemsSource=ruleRows;rules.SelectedItem=ruleRows.FirstOrDefault(x=>x.Id==rid);
 }
 ComboBox Editable(string[] options,string value) {var c=UI.Select(options,value);c.IsEditable=true;c.Text=value;return c;}
 void ValidateAccounts(string kind,string from,string to) {if(String.IsNullOrWhiteSpace(from))throw new Exception("请填写账户。");if(kind=="转账" && (String.IsNullOrWhiteSpace(to)||from.Trim()==to.Trim()))throw new Exception("转账需要不同的转出和转入账户。");}
 void EditEntry(string id) {
  app.RequireUnlocked();Entry source=id==null?new Entry():app.Store.Data.Entries.Single(x=>x.Id==id);
  if(source.Voided)throw new Exception("请先恢复作废流水，再编辑内容。");var w=UI.Window(id==null?"记一笔":"编辑流水",470,720);app.Track(w);var p=UI.Body(w);
  var type=UI.Select(kinds,source.Kind);UI.Field(p,"类型",type);var amount=UI.Input(source.Amount==0?"":source.Amount.ToString("0.00"));UI.Field(p,"金额（元）",amount);
  var date=UI.DateInput(source.Date);UI.Field(p,"日期时间（yyyy-MM-dd HH:mm）",date);
  var category=Editable(categories,source.Category);UI.Field(p,"分类（可直接输入）",category);var account=Editable(AccountNames(),source.Account);UI.Field(p,"账户 / 转出账户",account);
  var to=Editable(AccountNames(),source.ToAccount);UI.Field(p,"转入账户（仅转账）",to);var memo=UI.Input(source.Memo);UI.Field(p,"备注",memo);
  if(!String.IsNullOrEmpty(source.BillId))p.Children.Add(UI.Text("此流水已关联账单，修改将同步更新该期已支付账单，后续周期规则不变。",12,UI.Muted));
  p.Children.Add(UI.Button("保存流水",()=>{app.RequireUnlocked();decimal money=UI.Money(amount);DateTime when=UI.Date(date);string kind=UI.Value(type);ValidateAccounts(kind,account.Text,to.Text);
   if(source.Reconciled&&!UI.Confirm("此流水已核对。保存修改将重置为待核对，继续？"))return;app.Store.Change(d=>{Entry e=id==null?new Entry():d.Entries.Single(x=>x.Id==id);if(id==null)d.Entries.Add(e);else Ledger.AuditChange(d,e,"编辑前");e.Kind=kind;e.Amount=money;e.Date=when;e.Category=String.IsNullOrWhiteSpace(category.Text)?"其他":category.Text.Trim();e.Account=account.Text.Trim();e.AccountId=Ledger.Resolve(d,e.Account).Id;e.ToAccount=kind=="转账"?to.Text.Trim():"";e.ToAccountId=kind=="转账"?Ledger.Resolve(d,e.ToAccount).Id:"";e.Reconciled=false;e.Memo=memo.Text.Trim();Ledger.AuditChange(d,e,id==null?"新增流水":"编辑后（待核对）");if(!String.IsNullOrEmpty(e.BillId)){Bill linked=d.Bills.Single(x=>x.Id==e.BillId);linked.Kind=e.Kind;linked.Amount=e.Amount;linked.Category=e.Category;linked.Account=e.Account;linked.ToAccount=e.ToAccount;}});w.Close();app.Refresh();}));w.ShowDialog();
 }
 void DeleteEntry(){app.RequireUnlocked();string id=SelectedId(entries);if(UI.Confirm("切换作废 / 有效状态？记录保留；作废不计入余额、收支或预算。关联账单保留支付历史，可补记有效流水。")){app.Store.Change(d=>Ledger.Void(d,id));app.Refresh();}}
 void EditRule(string id) {
  app.RequireUnlocked();BillRule source=id==null?new BillRule():app.Store.Data.Rules.Single(x=>x.Id==id);var w=UI.Window(id==null?"添加账单":"编辑账单规则",490,780);app.Track(w);var p=UI.Body(w);
  var name=UI.Input(source.Name);UI.Field(p,"账单名称",name);var amount=UI.Input(source.Amount==0?"":source.Amount.ToString("0.00"));UI.Field(p,"每期金额（元）",amount);
  var repeat=UI.Select(new[]{"不重复","每月","每年"},source.Repeat);UI.Field(p,"周期",repeat);var date=UI.DateInput(source.Start.Date.AddHours(source.Start.TimeOfDay==TimeSpan.Zero?9:source.Start.Hour).AddMinutes(source.Start.Minute));UI.Field(p,"首次到期（yyyy-MM-dd HH:mm）",date);
  var advance=UI.Input(source.Advance.ToString());UI.Field(p,"提前提醒天数（0—365），当天按到期时间提醒",advance);
  var kind=UI.Select(kinds,source.Kind);UI.Field(p,"支付时流水类型（信用卡还款选择转账）",kind);
  var category=Editable(categories,source.Category);UI.Field(p,"分类",category);var account=Editable(AccountNames(),source.Account);UI.Field(p,"账户 / 转出账户",account);var to=Editable(AccountNames(),source.ToAccount);UI.Field(p,"转入账户（仅转账）",to);
  if(id!=null)p.Children.Add(UI.Text("已生成期次保留原金额和日期。需要更换近期账单时，先停用旧规则并删除未付期次，再创建新规则。",12,UI.Muted));
  if(id!=null && app.Store.Data.Bills.Any(x=>x.RuleId==id)){date.IsReadOnly=true;repeat.IsEnabled=false;}
  p.Children.Add(UI.Button("保存账单",()=>{app.RequireUnlocked();if(String.IsNullOrWhiteSpace(name.Text))throw new Exception("请填写账单名称。");decimal money=UI.Money(amount);DateTime when=UI.Date(date);int days;if(!int.TryParse(advance.Text,out days)||days<0||days>365)throw new Exception("提前天数应为0—365的整数。");ValidateAccounts(UI.Value(kind),account.Text,to.Text);
   app.Store.Change(d=>{BillRule r=id==null?new BillRule():d.Rules.Single(x=>x.Id==id);if(id==null)d.Rules.Add(r);r.Name=name.Text.Trim();r.Amount=money;r.Start=when;r.Repeat=UI.Value(repeat);r.Advance=days;r.Kind=UI.Value(kind);r.Category=String.IsNullOrWhiteSpace(category.Text)?"其他":category.Text.Trim();r.Account=account.Text.Trim();r.ToAccount=r.Kind=="转账"?to.Text.Trim():"";Logic.Generate(d,DateTime.Now);});w.Close();app.Refresh();}));w.ShowDialog();
 }
 void ToggleRule(){app.RequireUnlocked();string id=SelectedId(rules);var rule=app.Store.Data.Rules.Single(x=>x.Id==id);if(!UI.Confirm(rule.Active?"停用此规则？已生成的未付账单仍会提醒，可选择期次删除。":"重新启用此规则并补生成账单？"))return;app.Store.Change(d=>{var r=d.Rules.Single(x=>x.Id==id);r.Active=!r.Active;Logic.Generate(d,DateTime.Now);});app.Refresh();}
 void DeleteBill(){app.RequireUnlocked();string id=SelectedId(bills);var b=app.Store.Data.Bills.Single(x=>x.Id==id);if(b.Paid)throw new Exception("已支付期次保留为历史，不允许删除。");if(app.Store.Data.Rules.Any(x=>x.Id==b.RuleId&&x.Active))throw new Exception("请先停用相应规则，再删除未付期次，避免自动重新生成。");if(UI.Confirm("删除所选未付期次？")){app.Store.Change(d=>d.Bills.RemoveAll(x=>x.Id==id));app.Refresh();}}
 void Pay() {
  app.RequireUnlocked();string id=SelectedId(bills);Bill bill=app.Store.Data.Bills.Single(x=>x.Id==id);if(bill.Paid && !String.IsNullOrEmpty(bill.EntryId))throw new Exception("该期账单已经支付并记账。");var w=UI.Window(bill.Paid?"为已支付账单补记流水":"确认账单支付",470,450);app.Track(w);var p=UI.Body(w);
  p.Children.Add(UI.Text(bill.Name,23));p.Children.Add(UI.Text("¥"+bill.Amount.ToString("N2")+" · "+bill.Kind,18,UI.Green));
  var mode=UI.Select(new[]{"生成一笔流水","关联已有流水","仅标记已支付"},"生成一笔流水");UI.Field(p,"支付后如何记账",mode);
  var candidates=app.Store.Data.Entries.Where(x=>!x.Voided&&String.IsNullOrEmpty(x.BillId)&&x.Amount==bill.Amount&&x.Kind==bill.Kind).OrderByDescending(x=>x.Date).ToList();var existing=new ComboBox{Margin=new Thickness(0,8,0,8),DisplayMemberPath="Label",SelectedValuePath="Id"};existing.ItemsSource=candidates.Select(x=>new LinkOption{Id=x.Id,Label=x.Date.ToString("yyyy-MM-dd")+" · "+x.Account+" · "+x.Memo}).ToList();existing.SelectedIndex=0;UI.Field(p,"可关联的同类型、同金额流水",existing);
  p.Children.Add(UI.Button("确认已支付",()=>{app.RequireUnlocked();string link="";if(UI.Value(mode)=="关联已有流水"){if(existing.SelectedValue==null)throw new Exception("没有可关联的流水，请选择其他方式。");link=existing.SelectedValue.ToString();}app.Store.Change(d=>Logic.Pay(d,id,UI.Value(mode)=="生成一笔流水",link));w.Close();app.Refresh();}));w.ShowDialog();
 }
 void Snooze(){app.RequireUnlocked();string id=SelectedId(bills);if(app.Store.Data.Bills.Single(x=>x.Id==id).Paid)throw new Exception("已支付账单无需延后提醒。");var w=UI.Window("稍后提醒",360,300);app.Track(w);var p=UI.Body(w);foreach(string option in new[]{"1小时后","今晚20:00","明天09:00"}){string choice=option;p.Children.Add(UI.Button(choice,()=>{app.RequireUnlocked();DateTime target=choice=="1小时后"?DateTime.Now.AddHours(1):choice=="今晚20:00"?DateTime.Today.AddHours(20):DateTime.Today.AddDays(1).AddHours(9);if(target<=DateTime.Now)target=target.AddDays(1);app.Store.Change(d=>d.Bills.Single(x=>x.Id==id).Snooze=target);w.Close();app.Refresh();}));}w.ShowDialog();}
 void ExportCsv(){app.RequireUnlocked();if(!UI.Confirm("CSV 是明文文件，会包含当前筛选结果的流水金额、账户和备注。继续导出？"))return;var save=new SaveFileDialog{Filter="CSV 文件|*.csv",FileName="个人流水-"+DateTime.Today.ToString("yyyyMMdd")+".csv"};if(save.ShowDialog()!=true)return;app.RequireUnlocked();var sb=new StringBuilder("日期,类型,金额,分类,账户,转入账户,备注,状态\r\n");foreach(var x in FilteredEntries().OrderBy(e=>e.Date))sb.AppendLine(string.Join(",",new[]{x.Date.ToString("yyyy-MM-dd HH:mm"),x.Kind,x.Amount.ToString("0.00",CultureInfo.InvariantCulture),x.Category,x.Account,x.ToAccount,x.Memo,x.Voided?"已作废":x.Reconciled?"已核对":"待核对"}.Select(Logic.CsvCell)));File.WriteAllText(save.FileName,sb.ToString(),new UTF8Encoding(true));}
 void ChangePassword(){app.RequireUnlocked();var w=UI.Window("修改财务解锁口令",430,430);app.Track(w);var p=UI.Body(w);PasswordBox old=new PasswordBox{Padding=new Thickness(8)},next=new PasswordBox{Padding=new Thickness(8)},confirm=new PasswordBox{Padding=new Thickness(8)};UI.Field(p,"原口令",old);UI.Field(p,"新口令（至少8个字符）",next);UI.Field(p,"再次输入新口令",confirm);p.Children.Add(UI.Button("更新口令",()=>{app.RequireUnlocked();if(!Codec.CheckPassword(app.Store.Data,old.Password))throw new Exception("原口令错误。");if(next.Password.Length<8||next.Password!=confirm.Password)throw new Exception("新口令至少8个字符，两次输入需一致。");app.Store.Change(d=>Codec.SetPassword(d,next.Password));w.Close();}));w.ShowDialog();}
 public class Row {public string Id{get;set;}public string Date{get;set;}public string Kind{get;set;}public string Amount{get;set;}public string Category{get;set;}public string Account{get;set;}public string ToAccount{get;set;}public string Memo{get;set;}public string Linked{get;set;}public string Name{get;set;}public string Status{get;set;}public string Notice{get;set;}public string Repeat{get;set;}}
 public class LinkOption {public string Id{get;set;}public string Label{get;set;}}
}
}




