using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace EmulatorHub {
    public static class CompactMenus {
        public static void Group(ToolStripMenuItem parent, Func<string,string> category, params string[] order) {
            var items=parent.DropDownItems.Cast<ToolStripItem>().ToArray();
            parent.DropDownItems.Clear();
            var groups=new Dictionary<string,ToolStripMenuItem>();
            foreach(var name in order) groups[name]=new ToolStripMenuItem(name);
            foreach(var item in items) {
                if(item is ToolStripSeparator) {item.Dispose(); continue;}
                string key=category(item.Text);
                if(string.IsNullOrEmpty(key)) parent.DropDownItems.Add(item);
                else {if(!groups.ContainsKey(key)) groups[key]=new ToolStripMenuItem(key); groups[key].DropDownItems.Add(item);}
            }
            foreach(var group in groups.Values) if(group.DropDownItems.Count>0) parent.DropDownItems.Add(group); else group.Dispose();
        }
        public static void Arrange(ToolStripMenuItem library,ToolStripMenuItem tools,ToolStripMenuItem emulators,ToolStripMenuItem view) {
            var selected=tools.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(i=>i.Text=="Selected emulator");
            var target=emulators.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(i=>i.Text=="Selected emulator");
            if(selected!=null && target!=null) {tools.DropDownItems.Remove(selected); selected.Text="Edit and folders"; target.DropDownItems.Insert(0,selected);}
            var transfer=tools.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(i=>i.Text=="Profile transfer");
            if(transfer!=null) {tools.DropDownItems.Remove(transfer); emulators.DropDownItems.Add(transfer);}
            foreach(var item in view.DropDownItems.OfType<ToolStripMenuItem>().Where(i=>i.Text.StartsWith("Controller launcher") || i.Text=="Reopen last game").ToArray()) {view.DropDownItems.Remove(item); tools.DropDownItems.Add(item);}
            foreach(var container in tools.DropDownItems.OfType<ToolStripMenuItem>().Where(i=>i.Text=="FishBowl" || i.Text=="Maintenance").ToArray()) {
                var children=container.DropDownItems.Cast<ToolStripItem>().ToArray();
                container.DropDownItems.Clear(); tools.DropDownItems.Remove(container);
                tools.DropDownItems.AddRange(children); container.Dispose();
            }
            Group(library,LibraryCategory,"Play","Organize","Saves","Artwork and activity","Backup and transfer");
            Group(tools,text=> {
                string t=text.ToLowerInvariant();
                if(t=="controllers" || t=="multiplayer") return null;
                if(t.Contains("search") || t.Contains("command palette")) return "Search and commands";
                if(t.Contains("launcher") || t.Contains("reopen")) return "Launchers";
                if(t.Contains("settings") || t.Contains("preferences") || t.Contains("shortcut") || t.Contains("portable mode")) return "Settings";
                return "Maintenance";
            },"Search and commands","Launchers","Maintenance","Settings");
            Group(view,text=>text.Contains("appearance") || text.Contains("Customize Home") || text.Contains("Cosmetic styles") ? "Appearance and layout" : null,"Appearance and layout");
        }
        public static string LibraryCategory(string text) {
            string t=text.ToLowerInvariant();
            if(t.StartsWith("game library")) return null;
            if(t.Contains("queue") || t.Contains("surprise")) return "Play";
            if(t.Contains("backup") || t.Contains("restore point") || t.Contains("export") || t.Contains("import fishbowl")) return "Backup and transfer";
            if(t.Contains("save")) return "Saves";
            if(t.Contains("artwork") || t.Contains("screenshot") || t.Contains("session journal")) return "Artwork and activity";
            return "Organize";
        }
        public static ContextMenuStrip Menu() {return new ContextMenuStrip {Renderer=new FishBowlMenuRenderer(),BackColor=FishBowlPalette.ThemeSurface,ForeColor=FishBowlPalette.ThemeInk};}
    }
    public partial class GameLibraryDialog {
        private void AddActionGroup(FlowLayoutPanel bar,string title, Action<ContextMenuStrip> build) {
            var menu=CompactMenus.Menu();
            var button=ExperienceUi.Button(title+" ▾",()=>{});
            button.Tag=menu;
            button.Click+=(s,e)=> {foreach(var old in menu.Items.Cast<ToolStripItem>().ToArray()) old.Dispose(); menu.Items.Clear(); menu.BackColor=FishBowlPalette.ThemeSurface; menu.ForeColor=FishBowlPalette.ThemeInk; build(menu); menu.Show(button,new Point(0,button.Height));};
            button.Disposed+=(s,e)=>menu.Dispose(); bar.Controls.Add(button);
        }
        private void GroupAction(ContextMenuStrip menu,string text,Action action,bool selected=false) {
            var item=new ToolStripMenuItem(text) {Enabled=!selected || SelectedGame()!=null,ForeColor=FishBowlPalette.ThemeInk};
            item.Click+=(s,e)=> {try {action();} catch(Exception error) {MessageBox.Show(this,error.Message,"FishBowl",MessageBoxButtons.OK,MessageBoxIcon.Exclamation);}};
            menu.Items.Add(item);
        }
        private void BuildCompactActions(FlowLayoutPanel bar) {
            AddButton(bar,"Add game",AddGame); AddButton(bar,"Launch",LaunchGame); AddButton(bar,"Details",ShowDetails);
            AddActionGroup(bar,"Game actions",menu=> {
                GroupAction(menu,"Edit game",EditGame,true); GroupAction(menu,"Game extensions...",()=>{Hub.GameOptions(this,library,SelectedGame());RefreshGames();},true); GroupAction(menu,"Online metadata and cover...",()=>{Hub.Metadata(this,library,SelectedGame());RefreshGames();},true); GroupAction(menu,"Open game folder",OpenGameFolder,true);
                GroupAction(menu,"Saves and history",ShowSaves,true); GroupAction(menu,"Progress and rating",EditProgress,true);
                menu.Items.Add(new ToolStripSeparator());
                GroupAction(menu,"Pin / unpin",TogglePinned,true); GroupAction(menu,"Favorite / unfavorite",ToggleFavorite,true);
                GroupAction(menu,"Bulk edit selected games",BulkEditGames,true); GroupAction(menu,"Preview metadata",PreviewMetadata,true);
                GroupAction(menu,"Add to play queue",()=> {LibraryAdditions.Enqueue(library,SelectedGame()); Store.Save(library);},true);
            });
            AddActionGroup(bar,"Library tools",menu=> {
                GroupAction(menu,"List / artwork view",ToggleGameView);
                GroupAction(menu,"Library extensions...",()=>{Hub.Show(this,library);ReloadLibrary();});
                GroupAction(menu,"Collections",CreateCollection); GroupAction(menu,"Smart lists",()=>LibraryAdditions.SmartLists(this,library));
                GroupAction(menu,"Play queue",()=>LibraryAdditions.Queue(this,library,SelectedGame())); GroupAction(menu,"Surprise me",()=>LibraryAdditions.Surprise(this,library));
                menu.Items.Add(new ToolStripSeparator());
                GroupAction(menu,"Sync folders",SyncFolders); GroupAction(menu,"Find duplicates",ShowDuplicates); GroupAction(menu,"Repair game paths",RepairGames);
                GroupAction(menu,"Export current view to CSV",()=>LibraryAdditions.Export(this,VisibleGames()));
                GroupAction(menu,"User tools...",()=>{UserTools.Show(this,library,()=>{foreach(var main in Application.OpenForms.OfType<MainForm>())main.RefreshUserToolsViews(library);FishBowlPalette.StyleWindow(this);RefreshGames();});RefreshGames();});
            });
            foreach(Button button in bar.Controls.OfType<Button>()){button.Height=Math.Max(38,button.Height);button.Font=new Font("Bahnschrift",10f,FontStyle.Regular);}

        }
    }
}
