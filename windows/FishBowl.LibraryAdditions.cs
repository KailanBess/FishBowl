using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace EmulatorHub {
    public class SmartLibraryList {
        public string Name { get; set; }
        public string Search { get; set; }
        public string Platform { get; set; }
        public string Status { get; set; }
        public bool FavoritesOnly { get; set; }
        public bool UnplayedOnly { get; set; }
    }

    public static class LibraryAdditions {
        private static readonly Random random = new Random();
        public static void Ensure(LibraryData data) {
            if (data.SmartLists == null) data.SmartLists = new List<SmartLibraryList>();
            if (data.PlayQueue == null) data.PlayQueue = new List<string>();
        }
        public static List<GameEntry> Match(LibraryData data, SmartLibraryList rule) {
            return data.Games.Where(g =>
                (string.IsNullOrWhiteSpace(rule.Search) || Hub.SearchText(g).IndexOf(rule.Search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0) &&
                (string.IsNullOrWhiteSpace(rule.Platform) || rule.Platform == "Any" || string.Equals(g.ConsoleLabel, rule.Platform, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(rule.Status) || rule.Status == "Any" || string.Equals(g.PlayStatus, rule.Status, StringComparison.OrdinalIgnoreCase)) &&
                (!rule.FavoritesOnly || g.Favorite) && (!rule.UnplayedOnly || g.LaunchCount == 0)
            ).OrderBy(g => g.Title).ToList();
        }
        public static List<GameEntry> QueueGames(LibraryData data) {
            Ensure(data);
            return data.PlayQueue.Select(id => data.Games.FirstOrDefault(g => g.Id == id)).Where(g => g != null).ToList();
        }
        public static void Enqueue(LibraryData data, GameEntry game) {
            Ensure(data);
            if (game != null && !data.PlayQueue.Contains(game.Id)) data.PlayQueue.Add(game.Id);
        }
        public static bool MoveQueue(LibraryData data, string id, int delta) {
            Ensure(data);
            int index=data.PlayQueue.IndexOf(id), target=index+delta;
            if (index<0 || target<0 || target>=data.PlayQueue.Count) return false;
            data.PlayQueue.RemoveAt(index); data.PlayQueue.Insert(target,id); return true;
        }
        private static ListView GameList() {
            var list = new ListView { Dock=DockStyle.Fill, View=View.Details, FullRowSelect=true, HideSelection=false, MultiSelect=false, AccessibleName="Games" };
            list.Columns.Add("Game",300); list.Columns.Add("Platform",150); list.Columns.Add("Progress",120); list.Columns.Add("Launches",85);
            return list;
        }
        private static void Fill(ListView list, IEnumerable<GameEntry> games) {
            string selected=list.SelectedItems.Count==0 ? null : ((GameEntry)list.SelectedItems[0].Tag).Id;
            list.BeginUpdate(); list.Items.Clear();
            foreach(var game in games) {
                var item=new ListViewItem(new [] {game.Title??"Untitled", game.ConsoleLabel??"", game.PlayStatus??"", game.LaunchCount.ToString()}) {Tag=game};
                list.Items.Add(item); if(game.Id==selected) item.Selected=true;
            }
            list.EndUpdate();
        }
        private static GameEntry Selected(ListView list) {
            if (list.SelectedItems.Count==0) throw new InvalidOperationException("Select a game first.");
            return (GameEntry)list.SelectedItems[0].Tag;
        }
        public static void Show(IWin32Window owner, LibraryData data, GameEntry selected, List<GameEntry> visible) {
            using(var dialog=new NextDialog("More Library tools",720,440)) {
                dialog.Body.Controls.Add(ExperienceUi.Label("Save dynamic smart lists, organize what to play next, discover a game, or export your current Library view.",90));
                dialog.Action("Smart lists",()=>SmartLists(dialog,data));
                dialog.Action("Play queue",()=>Queue(dialog,data,selected));
                dialog.Action("Surprise me",()=>Surprise(dialog,data));
                dialog.Action("Export current view",()=>Export(dialog,visible));
                dialog.Action("Close",dialog.Close); dialog.ShowDialog(owner);
            }
        }
        public static void SmartLists(IWin32Window owner, LibraryData data) {
            Ensure(data);
            using(var dialog=new NextDialog("Smart Library lists",1000,620)) {
                var choices=new ComboBox {Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList,DisplayMember="Name",AccessibleName="Saved smart list"};
                var list=GameList(); var count=ExperienceUi.Label("",30);
                dialog.Body.Controls.Add(list); dialog.Body.Controls.Add(count); dialog.Body.Controls.Add(choices);
                Action refresh=()=> {var rule=choices.SelectedItem as SmartLibraryList; var matches=rule==null ? new List<GameEntry>() : Match(data,rule); Fill(list,matches); count.Text=matches.Count+" matching games — updates as your Library changes";};
                Action reload=()=> {var rule=choices.SelectedItem as SmartLibraryList; choices.Items.Clear(); choices.Items.AddRange(data.SmartLists.Cast<object>().ToArray()); if(rule!=null && data.SmartLists.Contains(rule)) choices.SelectedItem=rule; else if(choices.Items.Count>0) choices.SelectedIndex=0; refresh();};
                choices.SelectedIndexChanged+=(s,e)=>refresh();
                dialog.Action("New list",()=> {var rule=EditRule(dialog,data,null); if(rule!=null) {data.SmartLists.Add(rule); Store.Save(data); reload(); choices.SelectedItem=rule;}});
                dialog.Action("Edit list",()=> {var current=choices.SelectedItem as SmartLibraryList; if(current==null) throw new InvalidOperationException("Create or choose a list first."); var edited=EditRule(dialog,data,current); if(edited!=null) {data.SmartLists[data.SmartLists.IndexOf(current)]=edited; Store.Save(data); reload(); choices.SelectedItem=edited;}});
                dialog.Action("Delete list",()=> {var rule=choices.SelectedItem as SmartLibraryList; if(rule!=null && MessageBox.Show(dialog,"Delete this smart list? Games stay in your Library.","FishBowl",MessageBoxButtons.YesNo)==DialogResult.Yes) {data.SmartLists.Remove(rule); Store.Save(data); reload();}});
                dialog.Action("Add to queue",()=> {Enqueue(data,Selected(list)); Store.Save(data); count.Text="Added to play queue.";});
                dialog.Action("Launch",()=> {ExperienceTools.Launch(dialog,data,Selected(list)); refresh();});
                dialog.Action("Export list",()=> {var rule=choices.SelectedItem as SmartLibraryList; if(rule==null) throw new InvalidOperationException("Choose a smart list first."); Export(dialog,Match(data,rule));});
                dialog.Action("Refresh",refresh); dialog.Action("Close",dialog.Close); reload(); dialog.ShowDialog(owner);
            }
        }
        private static SmartLibraryList EditRule(IWin32Window owner,LibraryData data,SmartLibraryList existing) {
            var rule=existing??new SmartLibraryList {Name="New smart list",Platform="Any",Status="Any"};
            using(var editor=new NextDialog(existing==null?"New smart list":"Edit smart list",760,550)) {
                var fields=NextDialog.Fields(editor.Body);
                var name=new TextBox {Text=rule.Name}; var search=new TextBox {Text=rule.Search};
                var platforms=new [] {"Any"}.Concat(data.Games.Select(g=>g.ConsoleLabel).Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s=>s));
                var platform=NextDialog.Choice(platforms,rule.Platform);
                var status=NextDialog.Choice(new [] {"Any"}.Concat(data.Games.Select(g=>g.PlayStatus).Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct()).Concat(new [] {"Playing","Completed","Backlog"}).Distinct(),rule.Status);
                var favorite=new CheckBox {Text="Favorites only",Checked=rule.FavoritesOnly};
                var unplayed=new CheckBox {Text="Never launched through FishBowl",Checked=rule.UnplayedOnly};
                NextDialog.Field(fields,"List name",name); NextDialog.Field(fields,"Search title, genre, developer or tags",search,64);
                NextDialog.Field(fields,"Platform",platform); NextDialog.Field(fields,"Progress",status);
                NextDialog.Field(fields,"Favorites",favorite); NextDialog.Field(fields,"Unplayed",unplayed);
                SmartLibraryList result=null;
                editor.Action("Save",()=> {if(string.IsNullOrWhiteSpace(name.Text)) throw new InvalidOperationException("Enter a list name."); if(data.SmartLists.Any(x=>x!=existing && string.Equals(x.Name,name.Text.Trim(),StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("A list with that name already exists."); result=new SmartLibraryList {Name=name.Text.Trim(),Search=search.Text.Trim(),Platform=platform.Text,Status=status.Text,FavoritesOnly=favorite.Checked,UnplayedOnly=unplayed.Checked}; editor.Close();});
                editor.Action("Cancel",editor.Close); editor.ShowDialog(owner); return result;
            }
        }
        public static void Queue(IWin32Window owner, LibraryData data, GameEntry selected) {
            Ensure(data);
            using(var dialog=new NextDialog("Play queue",1000,620)) {
                var picker=NextTools.Games(data,selected); var list=GameList(); var hint=ExperienceUi.Label("Choose games for later. Launching keeps them queued until you remove them.",38);
                dialog.Body.Controls.Add(list); dialog.Body.Controls.Add(hint); dialog.Body.Controls.Add(picker);
                Action refresh=()=>Fill(list,QueueGames(data));
                dialog.Action("Add game",()=> {Enqueue(data,picker.SelectedItem as GameEntry); Store.Save(data); refresh();});
                dialog.Action("Move up",()=> {MoveQueue(data,Selected(list).Id,-1); Store.Save(data); refresh();});
                dialog.Action("Move down",()=> {MoveQueue(data,Selected(list).Id,1); Store.Save(data); refresh();});
                dialog.Action("Remove",()=> {data.PlayQueue.Remove(Selected(list).Id); Store.Save(data); refresh();});
                dialog.Action("Launch",()=>ExperienceTools.Launch(dialog,data,Selected(list)));
                dialog.Action("Details",()=> {var game=Selected(list); using(var details=new GameDetailsDialog(game,ExperienceData.Emulator(data,game))) details.ShowDialog(dialog);});
                dialog.Action("Export queue",()=>Export(dialog,QueueGames(data)));
                dialog.Action("Close",dialog.Close); refresh(); dialog.ShowDialog(owner);
            }
        }
        public static List<GameEntry> SurpriseCandidates(LibraryData data, bool unplayed) {
            return data.Games.Where(g => (!unplayed || g.LaunchCount==0) && File.Exists(g.Path)).Where(g=> {
                try {GameSessions.Validate(data,g); return true;} catch {return false;}
            }).ToList();
        }
        public static void Surprise(IWin32Window owner,LibraryData data) {
            using(var dialog=new NextDialog("Surprise me",760,460)) {
                var unplayed=new CheckBox {Text="Only games never launched through FishBowl",Dock=DockStyle.Top,Height=40,Checked=true,AccessibleName="Unplayed games only"};
                var title=ExperienceUi.Label("",80); var detail=ExperienceUi.Label("",100); GameEntry picked=null;
                dialog.Body.Controls.Add(detail); dialog.Body.Controls.Add(title); dialog.Body.Controls.Add(unplayed);
                Action roll=()=> {var candidates=SurpriseCandidates(data,unplayed.Checked); picked=candidates.Count==0 ? null : candidates[random.Next(candidates.Count)]; title.Text=picked==null?"No launch-ready games match.":picked.Title; detail.Text=picked==null?"Add games or adjust the unplayed filter. Missing game files and invalid launch settings are excluded.":(picked.ConsoleLabel??"Platform not set")+"\r\n"+(picked.Genre??"Genre not set")+"\r\n"+candidates.Count+" eligible games";};
                unplayed.CheckedChanged+=(s,e)=>roll(); dialog.Action("Pick another",roll);
                dialog.Action("Launch",()=> {if(picked==null) throw new InvalidOperationException("No game selected."); ExperienceTools.Launch(dialog,data,picked); roll();});
                dialog.Action("Add to queue",()=> {if(picked==null) throw new InvalidOperationException("No game selected."); Enqueue(data,picked); Store.Save(data); detail.Text="Added to play queue.";});
                dialog.Action("Close",dialog.Close); roll(); dialog.ShowDialog(owner);
            }
        }
        public static string CsvCell(string text) {
            text=text??"";
            // Preserve literal metadata when the CSV is opened in spreadsheet software.
            string trimmed=text.TrimStart();
            if(trimmed.Length>0 && "=+-@".IndexOf(trimmed[0])>=0) text="'"+text;
            return "\""+text.Replace("\"","\"\"")+"\"";
        }
        public static string Csv(IEnumerable<GameEntry> games) {
            var csv=new StringBuilder("Title,Platform,Genre,Developer,Year,Progress,Favorite,Launches,Play hours,Last launched,Path,Tags\r\n");
            foreach(var g in games) csv.AppendLine(string.Join(",",new [] {g.Title,g.ConsoleLabel,g.Genre,g.Developer,g.ReleaseYear,g.PlayStatus,g.Favorite?"Yes":"No",g.LaunchCount.ToString(CultureInfo.InvariantCulture),(g.TotalPlaySeconds/3600.0).ToString("0.00",CultureInfo.InvariantCulture),g.LastLaunched,g.Path,string.Join("; ",g.Tags??new List<string>())}.Select(CsvCell)));
            return csv.ToString();
        }
        public static void Export(IWin32Window owner,IEnumerable<GameEntry> games) {
            var rows=games.ToList();
            using(var save=new SaveFileDialog {Filter="CSV spreadsheet|*.csv",DefaultExt="csv",FileName="FishBowl Library.csv",OverwritePrompt=true}) {
                if(save.ShowDialog(owner)!=DialogResult.OK) return;
                File.WriteAllText(save.FileName,Csv(rows),new UTF8Encoding(true));
                MessageBox.Show(owner,"Exported "+rows.Count+" games.","FishBowl");
            }
        }
    }
}
