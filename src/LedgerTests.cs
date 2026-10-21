using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
namespace DeskNote {
public static class LedgerTests {
 public static void Run(string folder,Action<bool,string> check){
  var s=new State();var cash=Ledger.Resolve(s,"现金");var bank=Ledger.Resolve(s,"银行卡");cash.OpeningBalance=500;bank.OpeningBalance=1000;
  var t=new Entry{Kind="转账",Amount=200,Account="银行卡",ToAccount="现金",Date=DateTime.Today};s.Entries.Add(t);Ledger.Normalize(s);
  check(Ledger.Balance(s,cash,DateTime.Today)==700&&Ledger.Balance(s,bank,DateTime.Today)==800,"Account transfer conserves total balance and moves both sides");
  Ledger.Rename(s,bank.Id,"储蓄卡");check(t.AccountId==bank.Id&&t.Account=="储蓄卡"&&Ledger.Balance(s,bank,DateTime.Today)==800,"Account rename retains stable historical identity");
  check(Ledger.Balance(s,cash,DateTime.Today.AddDays(-1))==500,"Historical balance excludes later transfers");
  var unknown=Ledger.Resolve(s,"未核对账户");check(!Ledger.Balance(s,unknown,DateTime.Today).HasValue,"Missing opening balance is unknown, never a fabricated asset total");
  Ledger.Void(s,t.Id);check(s.Entries.Contains(t)&&Ledger.Balance(s,cash,DateTime.Today)==500&&Ledger.Balance(s,bank,DateTime.Today)==1000,"Voided transfer is retained and excluded from both balances");Ledger.Void(s,t.Id);check(Ledger.Balance(s,cash,DateTime.Today)==700,"Restoring transaction restores account effects");
  var bill=new Bill{Id="void-payment",Due=DateTime.Today,Amount=20,Account="现金"};s.Bills.Add(bill);Logic.Pay(s,bill.Id,true,"");Ledger.Normalize(s);string old=bill.EntryId;Ledger.Void(s,old);Logic.Pay(s,bill.Id,true,"");Ledger.Normalize(s);check(s.Entries.Count(x=>!x.Voided&&x.BillId==bill.Id)==1&&bill.EntryId!=old,"Voided linked payment can be backfilled exactly once");bool rejected=false;try{Ledger.Void(s,old);}catch{rejected=true;}check(rejected,"Cannot restore obsolete linked payment over a replacement entry");
  // Changes are transactional in Store; direct domain test above intentionally threw after mutation.
  s.Entries.Single(x=>x.Id==old).Voided=true;
  check(Ledger.Filter(s,"现金","全部月份","全部分类","有效","").Count()==2,"Account filter includes incoming transfers and excludes voided entries");
  var future=new Entry{Kind="收入",Account="现金",Amount=100,Date=DateTime.Today.AddDays(5)};s.Entries.Add(future);s.Bills.Add(new Bill{Id="future-expense",Account="现金",Kind="支出",Amount=30,Due=DateTime.Today.AddDays(7)});s.Bills.Add(new Bill{Id="overdue-expense",Account="现金",Kind="支出",Amount=10,Due=DateTime.Today.AddDays(-1)});Ledger.Normalize(s);
  check(Ledger.PlannedNet(s,DateTime.Today,DateTime.Today.AddDays(30))==60,"Forecast separates future actual entries, unpaid plans, overdue bills and paid history");
  check(Ledger.Balance(s,cash,DateTime.Today)==680,"Future entries and unpaid plans do not change today's actual balance");
  s.Rules.Add(new BillRule{Name="future income",Kind="收入",Account="现金",Amount=9,Start=DateTime.Today.AddDays(70),Repeat="不重复"});int count=s.Bills.Count;check(Ledger.PlannedNet(s,DateTime.Today,DateTime.Today.AddDays(90))==69&&s.Bills.Count==count,"Forecast projects beyond normal bill window without mutating actual state");
  var tasks=new State();TaskSchedule.Save(tasks,null,false,"archive daily",DateTime.Today.AddHours(9),"每天",false,true,DateTime.Today.AddDays(2).AddHours(9));var first=tasks.Items.First();first.Archived=true;int before=tasks.Items.Count;TaskSchedule.Generate(tasks,DateTime.Today,DateTime.Today.AddDays(10));check(tasks.Items.Count==before&&tasks.Items.Count(x=>!x.Archived)==2,"Archived recurring occurrence is not regenerated and does not stop later dates");first.Archived=false;check(!first.Done&&tasks.Items.Count==3,"Archive restoration retains completion and cutoff state");
  first.Archived=true;check(!TaskSchedule.CanRemind(tasks,first,first.Due.Value),"Archived occurrence never consumes or sends a reminder");TaskSchedule.Stop(tasks,first.Id);check(tasks.Items.Contains(first),"Stopping recurrence preserves archived history");first.Archived=false;check(!TaskSchedule.CanRemind(tasks,first,first.Due.Value),"Restoring archive does not reactivate a stopped series");
  string path=Path.Combine(folder,"migration-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);var legacy=new State();legacy.Entries.Add(new Entry{Account="历史现金",Amount=3});byte[] original=ProtectedData.Protect(Codec.Serialize(legacy),null,DataProtectionScope.CurrentUser);File.WriteAllBytes(Path.Combine(path,"desk.dat"),original);var store=new Store(path);check(store.Data.Accounts.Count==1&&store.Data.Entries[0].AccountId==store.Data.Accounts[0].Id,"Legacy account names migrate to stable IDs");string snapshot=Directory.GetFiles(path,"before-accounts-*.dat").Single();check(File.ReadAllBytes(snapshot).SequenceEqual(original),"Migration preserves exact encrypted pre-migration snapshot");string id=store.Data.Accounts[0].Id;check(new Store(path).Data.Accounts.Single().Id==id&&Directory.GetFiles(path,"before-accounts-*.dat").Length==1,"Account migration is persistent and idempotent");
  try{store.Change(d=>{Ledger.Rename(d,id,"temporary");throw new Exception();});}catch{}check(store.Data.Accounts.Single().Name=="历史现金"&&new Store(path).Data.Accounts.Single().Name=="历史现金","Failed account mutation rolls back memory and disk");
 }
}
}
