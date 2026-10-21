using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace DeskNote {
public class Item {
 public string Id = Guid.NewGuid().ToString("N");
 public string Text = "";
 public bool Note, Done, HighPriority, Archived;
 public DateTime? Due;
 public DateTime? RepeatAnchor;
 public string Repeat = "不重复";
 public string SeriesId = "";
 public DateTime? LastNotice;
}
public class TaskSeries {
 public string Id=Guid.NewGuid().ToString("N"),Text="",Repeat="每天";
 public string FamilyId="";
 public DateTime Start;
 public DateTime? From,Until;
 public DateTime? RepeatEnd;
 public bool HighPriority,Active=true;
 public List<DateTime> Excluded=new List<DateTime>();
}
public class Entry {
 public string AccountId="",ToAccountId="";
 public bool Voided,Reconciled;
 public string Id = Guid.NewGuid().ToString("N");
 public DateTime Date = DateTime.Today;
 public string Kind = "支出", Category = "其他", Account = "现金", ToAccount = "", Memo = "", BillId = "";
 public decimal Amount;
}
public class BillRule {
 public string Id = Guid.NewGuid().ToString("N");
 public string Name = "", Repeat = "每月", Kind = "支出", Category = "居住", Account = "银行卡", ToAccount = "";
 public DateTime Start = DateTime.Today;
 public decimal Amount;
 public int Advance = 3;
 public bool Active = true;
}
public class Bill {
 public string Id = "", RuleId = "", Name = "", Kind = "支出", Category = "", Account = "", ToAccount = "", EntryId = "";
 public DateTime Due;
 public decimal Amount;
 public int Advance;
 public bool Paid;
 public DateTime? Snooze, LastNotice;
}
public class Preferences {
 public double Left = 80, Top = 80, Width = 500, Height = 820, Opacity = 1;
 public double BackgroundOpacity = 1;
 public string ThemeName="曜石暗夜", AccentColor="#38BDF8", SurfaceColor="#0F172A";
 public bool ShowSeconds=true, ShowDayProgress=true, EnableReminders=true, ReminderSound;
 public bool EmbedDesktop, LayoutLocked;
 public string WeatherCity = "";
 public bool Topmost, PrivateOnDesktop = true, LockOnBlur, AutoStart;
 public int LockMinutes = 5, Hotkey = 0x48;
 public DateTime? PauseUntil;
}
public class State {
 public List<LedgerAccount> Accounts=new List<LedgerAccount>();
 public List<LedgerAudit> Audit=new List<LedgerAudit>();
 public List<MonthlyBudget> Budgets=new List<MonthlyBudget>();
 public int Version = 1;
 public string PasswordSalt = "", PasswordHash = "";
 public List<Item> Items = new List<Item>();
 public List<TaskSeries> TaskSeries = new List<TaskSeries>();
 public List<Entry> Entries = new List<Entry>();
 public List<BillRule> Rules = new List<BillRule>();
 public List<Bill> Bills = new List<Bill>();
 public Preferences Settings = new Preferences();
}
public static class Codec {
 public static byte[] Serialize(State s) { using(var m = new MemoryStream()) { new XmlSerializer(typeof(State)).Serialize(m,s); return m.ToArray(); } }
 public static State Parse(byte[] b) {
  if(b.Length > 32*1024*1024) throw new InvalidDataException("文件过大。");
  var settings = new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 32*1024*1024 };
  using(var m = new MemoryStream(b)) using(var r = System.Xml.XmlReader.Create(m, settings)) {
   var s = (State)new XmlSerializer(typeof(State)).Deserialize(r);
   if(s.Version != 1 || s.Settings == null || s.TaskSeries == null || s.Items == null || s.Entries == null || s.Rules == null || s.Bills == null) throw new InvalidDataException("不支持的数据格式。");
   if(s.Accounts==null||s.Audit==null||s.Budgets==null||s.Accounts.Any(x=>x==null||String.IsNullOrWhiteSpace(x.Id)||String.IsNullOrWhiteSpace(x.Name)||x.OpeningDate.Year<1900||x.OpeningDate.Year>2200||(x.OpeningBalance.HasValue&&(Math.Abs(x.OpeningBalance.Value)>1000000000m||decimal.Round(x.OpeningBalance.Value,2)!=x.OpeningBalance.Value)))||s.Accounts.Select(x=>x.Id).Distinct().Count()!=s.Accounts.Count||s.Accounts.Select(x=>x.Name).Distinct().Count()!=s.Accounts.Count||s.Budgets.Any(x=>x==null||x.Amount<=0||x.Amount>1000000000m||decimal.Round(x.Amount,2)!=x.Amount))throw new InvalidDataException("账户或预算数据无效。");
   if(s.TaskSeries.Any(x=>x==null||x.Start.Year<1900||x.Start.Year>2200||x.Excluded==null||!new[]{"每天","每周","每月","每年"}.Contains(x.Repeat)))throw new InvalidDataException("重复待办规则无效。");
   if(s.Items.Any(x=>x==null) || s.Entries.Any(x=>x==null || x.Amount<=0 || x.Amount>1000000000m || decimal.Round(x.Amount,2)!=x.Amount) || s.Rules.Any(x=>x==null || x.Start.Year<1900 || x.Start.Year>2200 || x.Advance<0 || x.Advance>365 || x.Amount<=0) || s.Bills.Any(x=>x==null || x.Due.Year<1900 || x.Due.Year>2201 || x.Amount<=0)) throw new InvalidDataException("数据校验失败。");
   if(s.Items.Select(x=>x.Id).Distinct().Count()!=s.Items.Count || s.Entries.Select(x=>x.Id).Distinct().Count()!=s.Entries.Count || s.Bills.Select(x=>x.Id).Distinct().Count()!=s.Bills.Count) throw new InvalidDataException("数据中有重复记录。");
   if(!String.IsNullOrEmpty(s.PasswordHash) && (Convert.FromBase64String(s.PasswordSalt).Length!=16 || Convert.FromBase64String(s.PasswordHash).Length!=32)) throw new InvalidDataException("口令数据不完整。");
   return s;
  }
 }
 public static byte[] Random(int count) { var b = new byte[count]; using(var r=RandomNumberGenerator.Create())r.GetBytes(b); return b; }
 public static byte[] Derive(string pass,byte[] salt,int size) { using(var k=new Rfc2898DeriveBytes(pass,salt,100000))return k.GetBytes(size); }
 public static bool Equal(byte[] a,byte[] b) { int d=a.Length ^ b.Length; for(int i=0;i<Math.Min(a.Length,b.Length);i++)d|=a[i]^b[i]; return d==0; }
 public static void SetPassword(State s,string p) { var salt=Random(16); s.PasswordSalt=Convert.ToBase64String(salt); s.PasswordHash=Convert.ToBase64String(Derive(p,salt,32)); }
 public static bool CheckPassword(State s,string p) { return !String.IsNullOrEmpty(s.PasswordHash) && Equal(Convert.FromBase64String(s.PasswordHash),Derive(p,Convert.FromBase64String(s.PasswordSalt),32)); }
 // Backup envelope: magic(8), salt(16), IV(16), ciphertext, HMAC-SHA256(32).
 public static byte[] Pack(State s,string pass) {
  byte[] salt=Random(16), iv=Random(16), key=Derive(pass,salt,64), plain=Serialize(s), cipher;
  try {
   using(var aes=Aes.Create()) { aes.Key=key.Take(32).ToArray(); aes.IV=iv; using(var enc=aes.CreateEncryptor())cipher=enc.TransformFinalBlock(plain,0,plain.Length); }
   byte[] body=Encoding.ASCII.GetBytes("DSNBKP01").Concat(salt).Concat(iv).Concat(cipher).ToArray();
   using(var mac=new HMACSHA256(key.Skip(32).ToArray()))return body.Concat(mac.ComputeHash(body)).ToArray();
  } finally { Array.Clear(key,0,key.Length); Array.Clear(plain,0,plain.Length); }
 }
 public static State Unpack(byte[] file,string pass) {
  if(file.Length<88 || file.Length>40*1024*1024 || Encoding.ASCII.GetString(file,0,8)!="DSNBKP01")throw new InvalidDataException("不是有效的随记备份。");
  byte[] key=Derive(pass,file.Skip(8).Take(16).ToArray(),64);
  try {
   byte[] body=file.Take(file.Length-32).ToArray();
   using(var mac=new HMACSHA256(key.Skip(32).ToArray())) if(!Equal(mac.ComputeHash(body),file.Skip(file.Length-32).ToArray()))throw new InvalidDataException("备份口令错误或文件已损坏。");
   using(var aes=Aes.Create()) { aes.Key=key.Take(32).ToArray(); aes.IV=file.Skip(24).Take(16).ToArray(); using(var dec=aes.CreateDecryptor()) { var plain=dec.TransformFinalBlock(file,40,file.Length-72); try {return Parse(plain);} finally {Array.Clear(plain,0,plain.Length);} } }
  } finally {Array.Clear(key,0,key.Length);}
 }
}
public class Store {
 public State Data;
 public readonly string Folder, PathName;
 public Store(string folder,bool recovery=false) {
  Folder=folder; Directory.CreateDirectory(folder); PathName=Path.Combine(folder,"desk.dat");
  Data=!recovery && File.Exists(PathName) ? Codec.Parse(ProtectedData.Unprotect(File.ReadAllBytes(PathName),null,DataProtectionScope.CurrentUser)) : new State();
  if(!recovery&&File.Exists(PathName)&&Ledger.Normalize(Data)){
   File.Copy(PathName,Path.Combine(folder,"before-accounts-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".dat"));Save();
  }
 }
 public void Change(Action<State> action) {
  var previous=Codec.Serialize(Data);
  try {action(Data); Ledger.Normalize(Data); Save();} catch {Data=Codec.Parse(previous); throw;}
 }
 public void Save() {
  byte[] b=ProtectedData.Protect(Codec.Serialize(Data),null,DataProtectionScope.CurrentUser);
  string temp=PathName+".tmp", previous=PathName+".previous";
  using(var f=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){f.Write(b,0,b.Length);f.Flush(true);}
  for(int attempt=0;;attempt++){
   try{if(File.Exists(PathName))File.Replace(temp,PathName,previous);else File.Move(temp,PathName);break;}
   catch(IOException){
    // Scanners may briefly hold the previous snapshot; never replace the atomic save with a direct write.
    if(!File.Exists(temp)){if(File.Exists(PathName)&&File.ReadAllBytes(PathName).SequenceEqual(b))break;throw;}
    if(attempt>=3)throw;System.Threading.Thread.Sleep(60*(attempt+1));
   }
  }
  string backup=Path.Combine(Folder,"auto-"+DateTime.Today.ToString("yyyyMMdd")+".dat");
  // Automatic backup failure must not roll back an already committed primary file.
  try {string backupTemp=backup+".tmp";File.Copy(PathName,backupTemp,true);if(File.Exists(backup))File.Replace(backupTemp,backup,null);else File.Move(backupTemp,backup);foreach(var old in Directory.GetFiles(Folder,"auto-*.dat").OrderByDescending(x=>x).Skip(14))File.Delete(old);} catch(IOException) {} catch(UnauthorizedAccessException) {}
 }
 public void Restore(State s) {
  string recovery=Path.Combine(Folder,"before-restore-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".dat");
  if(File.Exists(PathName))File.Copy(PathName,recovery);
  State previous=Data; try{Data=s;Ledger.Normalize(Data);Save();}catch{Data=previous;throw;}
 }
}
public static class Logic {
 public static DateTime Occurrence(DateTime anchor,string repeat,int n) {
  if(repeat=="每月") {var d=new DateTime(anchor.Year,anchor.Month,1).AddMonths(n);return new DateTime(d.Year,d.Month,Math.Min(anchor.Day,DateTime.DaysInMonth(d.Year,d.Month)),anchor.Hour,anchor.Minute,0);}
  if(repeat=="每年") {int y=anchor.Year+n;return new DateTime(y,anchor.Month,Math.Min(anchor.Day,DateTime.DaysInMonth(y,anchor.Month)),anchor.Hour,anchor.Minute,0);}
  if(repeat=="每天")return anchor.AddDays(n);
  if(repeat=="每周")return anchor.AddDays(7*n);
  return anchor;
 }
 public static void Generate(State s,DateTime now) {
  var ids=new HashSet<string>(s.Bills.Select(x=>x.Id));
  foreach(var r in s.Rules.Where(x=>x.Active)) {
   for(int n=0;n<5000;n++) {
    DateTime due=Occurrence(r.Start,r.Repeat,n);
    if(due.Date>now.Date.AddDays(Math.Max(45,r.Advance+1)))break;
    string id=r.Id+":"+due.ToString("yyyyMMddHHmm");
    if(ids.Add(id))s.Bills.Add(new Bill{Id=id,RuleId=r.Id,Name=r.Name,Due=due,Amount=r.Amount,Kind=r.Kind,Category=r.Category,Account=r.Account,ToAccount=r.ToAccount,Advance=r.Advance});
    if(r.Repeat=="不重复")break;
   }
  }
 }
 public static bool Ready(Bill b,DateTime now) {
  return !b.Paid && now>=b.Due.AddDays(-b.Advance) && (!b.Snooze.HasValue || now>=b.Snooze.Value) && (!b.LastNotice.HasValue || b.LastNotice.Value.Date<now.Date || (b.Snooze.HasValue && b.Snooze.Value>b.LastNotice.Value && now>=b.Snooze.Value));
 }
 public static void Pay(State s,string id,bool create,string linkId) {
  Bill b=s.Bills.Single(x=>x.Id==id); if(b.Paid && (!String.IsNullOrEmpty(b.EntryId) || (!create && String.IsNullOrEmpty(linkId))))return;
  if(!String.IsNullOrEmpty(linkId)) {
   Entry e=s.Entries.Single(x=>x.Id==linkId);
   if(!String.IsNullOrEmpty(e.BillId) && e.BillId!=b.Id)throw new InvalidOperationException("此流水已关联其他账单。");
   if(e.Voided || e.Amount!=b.Amount || e.Kind!=b.Kind)throw new InvalidOperationException("流水已作废或类型、金额与账单不一致。");
   e.BillId=b.Id;b.EntryId=e.Id;
  } else if(create) {
   Entry e=s.Entries.FirstOrDefault(x=>!x.Voided&&x.BillId==b.Id);
   if(e==null){e=new Entry{Date=DateTime.Today,Amount=b.Amount,Kind=b.Kind,Category=b.Category,Account=b.Account,ToAccount=b.ToAccount,Memo=b.Name,BillId=b.Id};s.Entries.Add(e);}
   b.EntryId=e.Id;
  }
  b.Paid=true;b.Snooze=null;
 }
 public static void Complete(State s,string id) {
  Item i=s.Items.Single(x=>x.Id==id); i.Done=!i.Done;
  if(i.Done && !i.Note && i.Due.HasValue && i.Repeat!="不重复" && String.IsNullOrEmpty(i.SeriesId)) {
   DateTime anchor=i.RepeatAnchor??i.Due.Value;
   DateTime next=Occurrence(anchor,i.Repeat,1);
   // Skip missed task intervals; only create the next active task.
   int n=1;while((next<=DateTime.Now || next<=i.Due.Value) && n<5000)next=Occurrence(anchor,i.Repeat,++n);
   if(!s.Items.Any(x=>!x.Done && x.Text==i.Text && x.Due==next))s.Items.Add(new Item{Text=i.Text,Due=next,Repeat=i.Repeat,RepeatAnchor=anchor,HighPriority=i.HighPriority});
  }
 }
 public static string CsvCell(string value) {
  value=value??""; if(value.TrimStart().Length>0 && "=+-@\t\r".IndexOf(value.TrimStart()[0])>=0)value="'"+value;
  return "\""+value.Replace("\"","\"\"")+"\"";
 }
}
}


