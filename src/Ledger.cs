using System;
using System.Linq;
using System.Collections.Generic;
namespace DeskNote {
public class LedgerAccount {
 public string Id=Guid.NewGuid().ToString("N"),Name="",Type="日常账户";
 public decimal? OpeningBalance;
 // Opening balance is the balance before transactions on this day.
 public DateTime OpeningDate=new DateTime(1900,1,1);
 public bool Disabled;
}
public class LedgerAudit {public DateTime At=DateTime.Now;public string EntryId="",Action="",Details="";}
public class MonthlyBudget {public string Category="其他";public decimal Amount;}
public static class Ledger {
 public static LedgerAccount Resolve(State s,string name) {
  name=(name??"").Trim();if(name.Length==0)return null;
  var a=s.Accounts.SingleOrDefault(x=>x.Name==name);
  if(a==null){a=new LedgerAccount{Name=name};s.Accounts.Add(a);}return a;
 }
 public static bool Normalize(State s) {
  string before=Convert.ToBase64String(Codec.Serialize(s));
  foreach(var e in s.Entries) {
   var a=String.IsNullOrEmpty(e.AccountId)?Resolve(s,e.Account):s.Accounts.SingleOrDefault(x=>x.Id==e.AccountId);
   if(a==null)throw new InvalidOperationException("流水账户不存在。");e.AccountId=a.Id;e.Account=a.Name;
   if(e.Kind=="转账"){var b=String.IsNullOrEmpty(e.ToAccountId)?Resolve(s,e.ToAccount):s.Accounts.SingleOrDefault(x=>x.Id==e.ToAccountId);if(b==null||b.Id==a.Id)throw new InvalidOperationException("转账需要两个不同账户。");e.ToAccountId=b.Id;e.ToAccount=b.Name;}
   else {e.ToAccountId="";e.ToAccount="";}
  }
  foreach(var r in s.Rules){Resolve(s,r.Account);if(r.Kind=="转账")Resolve(s,r.ToAccount);}
  foreach(var b in s.Bills){Resolve(s,b.Account);if(b.Kind=="转账")Resolve(s,b.ToAccount);}
  return before!=Convert.ToBase64String(Codec.Serialize(s));
 }
 public static decimal Effect(Entry e,string accountId) {
  if(e.Voided)return 0;
  if(e.Kind=="转账")return (e.ToAccountId==accountId?e.Amount:0)-(e.AccountId==accountId?e.Amount:0);
  return e.AccountId==accountId?(e.Kind=="收入"?e.Amount:-e.Amount):0;
 }
 public static decimal? Balance(State s,LedgerAccount a,DateTime day) {
  if(!a.OpeningBalance.HasValue||day.Date<a.OpeningDate.Date)return null;
  return a.OpeningBalance.Value+s.Entries.Where(x=>x.Date.Date>=a.OpeningDate.Date&&x.Date.Date<=day.Date).Sum(x=>Effect(x,a.Id));
 }
 public static void Rename(State s,string id,string name) {
  name=(name??"").Trim();if(name.Length==0||s.Accounts.Any(x=>x.Id!=id&&x.Name==name))throw new Exception("账户名称不能为空或重复。");
  var a=s.Accounts.Single(x=>x.Id==id);string old=a.Name;a.Name=name;
  foreach(var e in s.Entries){if(e.AccountId==id)e.Account=name;if(e.ToAccountId==id)e.ToAccount=name;}
  foreach(var r in s.Rules){if(r.Account==old)r.Account=name;if(r.ToAccount==old)r.ToAccount=name;}
  foreach(var b in s.Bills){if(b.Account==old)b.Account=name;if(b.ToAccount==old)b.ToAccount=name;}
 }
 public static void AuditChange(State s,Entry e,string action){s.Audit.Add(new LedgerAudit{EntryId=e.Id,Action=action,Details=e.Date.ToString("yyyy-MM-dd HH:mm")+" · "+e.Kind+" ¥"+e.Amount.ToString("0.00")+" · "+e.Account+(e.Kind=="转账"?" → "+e.ToAccount:"")+" · "+e.Category+" · "+e.Memo});}
 public static void Void(State s,string id) {
  var e=s.Entries.Single(x=>x.Id==id);
  if(e.Voided&&!String.IsNullOrEmpty(e.BillId)&&s.Bills.Any(b=>b.Id==e.BillId&&!String.IsNullOrEmpty(b.EntryId)&&b.EntryId!=id))throw new Exception("该账单已有有效流水，无法恢复重复入账。");
  e.Voided=!e.Voided;e.Reconciled=false;
  if(e.Voided)foreach(var b in s.Bills.Where(x=>x.EntryId==id))b.EntryId="";
  else if(!String.IsNullOrEmpty(e.BillId)){
   var b=s.Bills.SingleOrDefault(x=>x.Id==e.BillId);
   if(b!=null){if(!String.IsNullOrEmpty(b.EntryId)&&b.EntryId!=id)throw new Exception("该账单已有有效流水，无法恢复重复入账。");b.EntryId=id;}
  }
  AuditChange(s,e,e.Voided?"作废":"恢复有效");
 }
 public static IEnumerable<Entry> Filter(State s,string account,string month,string category,string status,string query) {
  return s.Entries.Where(e=>(account=="全部账户"||e.Account==account||e.ToAccount==account)&&(month=="全部月份"||e.Date.ToString("yyyy-MM")==month)&&(category=="全部分类"||e.Category==category)&&(status=="全部状态"||(status=="有效"?!e.Voided:status=="已作废"?e.Voided:status=="已核对"?!e.Voided&&e.Reconciled:!e.Voided&&!e.Reconciled))&&(String.IsNullOrWhiteSpace(query)||(e.Memo+" "+e.Account+" "+e.Category).IndexOf(query.Trim(),StringComparison.OrdinalIgnoreCase)>=0));
 }
 public static decimal PlannedNet(State s,DateTime today,DateTime through) {
  var copy=Codec.Parse(Codec.Serialize(s));Logic.Generate(copy,through);
  decimal result=copy.Entries.Where(x=>!x.Voided&&x.Date.Date>today.Date&&x.Date.Date<=through.Date).Sum(x=>x.Kind=="收入"?x.Amount:x.Kind=="支出"?-x.Amount:0);
  return result+copy.Bills.Where(x=>!x.Paid&&x.Due.Date<=through.Date&&!copy.Entries.Any(e=>!e.Voided&&e.BillId==x.Id)).Sum(x=>x.Kind=="收入"?x.Amount:x.Kind=="支出"?-x.Amount:0);
 }
}
}
