using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
namespace DeskNote {
public static partial class RenderTest {
 static void ExtraUiTests(DeskApp app,string folder,List<string> report){
  var tabs=Find<TabControl>(app.Finance).First();tabs.SelectedIndex=1;app.Finance.UpdateLayout();var grid=Find<DataGrid>(app.Finance).First();string id=app.Store.Data.Entries.First().Id;grid.SelectedItem=grid.Items.Cast<FinanceWindow.Row>().Single(x=>x.Id==id);Click(app.Finance,"核对 / 取消");Require(app.Store.Data.Entries.Single(x=>x.Id==id).Reconciled,"Ledger reconciliation persists through UI",report);
  Modal(app,()=>Click(app.Finance,"修改记录"),w=>{Capture(w,Path.Combine(folder,"dialog-audit.png"));w.Close();});
  tabs.SelectedIndex=4;app.Finance.UpdateLayout();Modal(app,()=>Click(app.Finance,"＋ 添加账户"),w=>{var fields=Fields(w).Children.OfType<TextBox>().ToList();fields[0].Text="UI savings";fields[1].Text="500.00";Capture(w,Path.Combine(folder,"dialog-account.png"));Click(w,"保存账户");});Require(app.Store.Data.Accounts.Any(x=>x.Name=="UI savings"&&x.OpeningBalance==500),"Account editor saves opening balance",report);Capture(app.Finance,Path.Combine(folder,"accounts.png"));
  tabs.SelectedIndex=5;app.Finance.UpdateLayout();Modal(app,()=>Click(app.Finance,"＋ 设置分类预算"),w=>{Fields(w).Children.OfType<TextBox>().Single().Text="800";Click(w,"保存预算");});Require(app.Store.Data.Budgets.Any(x=>x.Amount==800),"Budget editor saves monthly category limit",report);Capture(app.Finance,Path.Combine(folder,"budget-forecast.png"));tabs.SelectedIndex=0;
  Modal(app,()=>app.Panel.OpenLibrary(),w=>{Fields(w).Children.OfType<TextBox>().Single().Text="UI-created task";w.UpdateLayout();var list=Find<DataGrid>(w).Single();list.SelectedIndex=0;Click(w,"归档 / 恢复本条");Require(app.Store.Data.Items.Single(x=>x.Text=="UI-created task").Archived,"Library archives only selected item",report);Find<ComboBox>(w).Last().SelectedItem="已归档";w.UpdateLayout();Require(list.Items.Count==1,"Library search finds archived contents",report);list.SelectedIndex=0;Capture(w,Path.Combine(folder,"library.png"));Click(w,"归档 / 恢复本条");Require(!app.Store.Data.Items.Single(x=>x.Text=="UI-created task").Archived,"Library restores archived item",report);w.Close();});
 }
}
}
