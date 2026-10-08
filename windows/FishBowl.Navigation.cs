using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EmulatorHub {
 public static class WindowsAppearance {
  public static void Open(IWin32Window owner, LibraryData data, Action refresh, Action fonts, Action cosmetics) {
   using(var dialog=new NextDialog("Appearance and accessibility",760,580)) {
    var fields=NextDialog.Fields(dialog.Body);
    NextDialog.Field(fields,"Theme, accent and Home",ExperienceUi.Button("Appearance and Home",delegate{UiPolishTools.Open(dialog,data,refresh);}));
    NextDialog.Field(fields,"Text and layout",ExperienceUi.Button("Live appearance and accessibility",fonts));
    NextDialog.Field(fields,"Cosmetic styles",ExperienceUi.Button("Cosmetic styles",cosmetics));
    NextDialog.Field(fields,"Immersion",ExperienceUi.Button("Immersion settings",delegate{using(var settings=new ImmersionSettingsDialog(data)){if(settings.ShowDialog(dialog)==DialogResult.OK){Store.Save(data);refresh();}}}));
    NextDialog.Field(fields,"General preferences",ExperienceUi.Button("General settings",delegate{General(dialog,data,refresh);}));
    NextDialog.Field(fields,"Controller navigation",ExperienceUi.Label("D-pad or left stick: move · A: activate · B: back · LB/RB: tabs · Start: menu",90));
    dialog.Action("Close",dialog.Close);dialog.ShowDialog(owner);
   }
  }
  public static void General(IWin32Window owner,LibraryData data,Action refresh) {
   NextData.Ensure(data);
   using(var dialog=new NextDialog("General settings",780,660)) {
    var fields=NextDialog.Fields(dialog.Body);var backup=new TextBox{Text=data.BackupFolder??"",Dock=DockStyle.Fill};
    NextDialog.Field(fields,"Cloud-synced backup folder (optional)",backup);
    NextDialog.Field(fields,"",ExperienceUi.Button("Browse",delegate{using(var picker=new FolderBrowserDialog()){picker.SelectedPath=backup.Text;if(picker.ShowDialog(dialog)==DialogResult.OK)backup.Text=picker.SelectedPath;}}));
    var hint=ExperienceUi.Label("Choose a OneDrive, Dropbox, or other synced folder to receive a copy whenever you export a backup.",70);
    NextDialog.Field(fields,"",hint,90);int hintRow=fields.GetRow(hint);var emptyHint=fields.Controls.OfType<Label>().Single(c=>c!=hint&&fields.GetRow(c)==hintRow);fields.Controls.Remove(emptyHint);emptyHint.Dispose();fields.SetColumn(hint,0);fields.SetColumnSpan(hint,2);
    var startup=new CheckBox{Text="Show the setup assistant when FishBowl opens",Checked=!data.Theme.StartupAssistantPreferenceSet||data.Theme.ShowStartupAssistant,AutoSize=true};
    var maximize=new CheckBox{Text="Start FishBowl maximized",Checked=data.Theme.StartMaximized,AutoSize=true};
    var sync=new CheckBox{Text="Automatically sync configured game folders",Checked=data.Theme.AutoSyncGameFolders,AutoSize=true};
    var confirm=new CheckBox{Text="Ask before launching a game from Game Library",Checked=data.Theme.ConfirmBeforeGameLaunch,AutoSize=true};
    var storage=new CheckBox{Text="Show the game storage assistant when FishBowl opens",Checked=!data.Theme.GameStorageAssistantPreferenceSet||data.Theme.ShowGameStorageAssistant,AutoSize=true};
    foreach(var check in new[]{startup,maximize,sync,confirm,storage}){
     NextDialog.Field(fields,"",check);int row=fields.GetRow(check);
     var empty=fields.Controls.OfType<Label>().Single(c=>fields.GetRow(c)==row);fields.Controls.Remove(empty);empty.Dispose();
     fields.SetColumn(check,0);fields.SetColumnSpan(check,2);fields.RowStyles[row].SizeType=SizeType.AutoSize;check.AccessibleName=check.Text;
    }
    var days=NextDialog.Number(Math.Max(0,data.Theme.AutoBackupDays),0,365);NextDialog.Field(fields,"Backup reminder interval (days)",days);
    NextDialog.Field(fields,"Web browser",ExperienceUi.Button("Browser settings",delegate{using(var browser=new BrowserSettingsDialog(data))browser.ShowDialog(dialog);}));
    dialog.Action("Save",delegate{data.BackupFolder=backup.Text.Trim();data.Theme.StartupAssistantPreferenceSet=true;data.Theme.ShowStartupAssistant=startup.Checked;data.Theme.StartMaximized=maximize.Checked;data.Theme.AutoSyncGameFolders=sync.Checked;data.Theme.ConfirmBeforeGameLaunch=confirm.Checked;data.Theme.GameStorageAssistantPreferenceSet=true;data.Theme.ShowGameStorageAssistant=storage.Checked;data.Theme.AutoBackupDays=(int)days.Value;Store.Save(data);refresh();dialog.Close();});
    dialog.Action("Cancel",dialog.Close);dialog.ShowDialog(owner);
   }
  }
 }

 // Pure input state makes held-button repeat and reconnect behavior testable without hardware.
 public sealed class ControllerInput {
  bool ready;int device=-1;ushort previous,direction;long repeatAt;
  public const int DeadZone=7849;
  public ushort Sample(bool connected,int index,ushort buttons,short x,short y,long now,bool active) {
   ushort directions=(ushort)(buttons&15);
   if(directions==0){int ax=Math.Abs((int)x),ay=Math.Abs((int)y);if(Math.Max(ax,ay)>DeadZone)directions=(ushort)(ax>ay?(x<0?4:8):(y>0?1:2));}
   ushort current=(ushort)((buttons&~15)|directions);
   if(!connected||!active){ready=false;previous=current;device=-1;return 0;}
   if(!ready||device!=index){ready=true;device=index;previous=current;direction=directions;repeatAt=now+400;return 0;}
   ushort pressed=(ushort)(current&~previous);
   if(directions!=direction){repeatAt=now+400;direction=directions;}
   else if(directions!=0&&now>=repeatAt){pressed|=directions;repeatAt=now+110;}
   previous=current;return pressed;
  }
 }
 public static class NavigationController {
  [StructLayout(LayoutKind.Sequential)]struct Pad {public ushort Buttons;public byte LeftTrigger,RightTrigger;public short X,Y,RightX,RightY;}
  [StructLayout(LayoutKind.Sequential)]struct State {public uint Packet;public Pad Pad;}
  [DllImport("xinput1_4.dll",EntryPoint="XInputGetState")]static extern uint Get(uint index,out State state);
  [DllImport("xinput1_3.dll",EntryPoint="XInputGetState")]static extern uint GetOld(uint index,out State state);
  [DllImport("xinput9_1_0.dll",EntryPoint="XInputGetState")]static extern uint GetLegacy(uint index,out State state);
  [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
  [DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr window,uint flags);
  [DllImport("user32.dll")]static extern bool IsWindowEnabled(IntPtr window);
  static readonly ControllerInput input=new ControllerInput();static ToolStripDropDown openMenu;
  static readonly HashSet<ToolStrip> attached=new HashSet<ToolStrip>();
  static readonly HashSet<ToolStripMenuItem> wired=new HashSet<ToolStripMenuItem>();
  public static void Register(ToolStripDropDown menu){if(menu==null||attached.Contains(menu))return;attached.Add(menu);menu.Opened+=delegate{openMenu=menu;};menu.Closed+=delegate{if(openMenu==menu)openMenu=menu.OwnerItem==null?null:menu.OwnerItem.Owner as ToolStripDropDown;};menu.Disposed+=delegate{attached.Remove(menu);if(openMenu==menu)openMenu=null;};WireItems(menu);}
  static void WireItems(ToolStrip strip){foreach(ToolStripMenuItem item in strip.Items.OfType<ToolStripMenuItem>()){if(!wired.Add(item))continue;item.DropDownOpening+=delegate{Register(item.DropDown);WireItems(item.DropDown);};item.Disposed+=delegate{wired.Remove(item);};}}
  public static void Attach(Form form){foreach(var strip in NextUi.Descendants(form).OfType<MenuStrip>()){if(attached.Add(strip)){WireItems(strip);strip.Disposed+=delegate{attached.Remove(strip);};}}foreach(var control in NextUi.Descendants(form))if(control.ContextMenuStrip!=null)Register(control.ContextMenuStrip);}
  static bool Read(out State state,out int device){state=default(State);device=-1;for(uint i=0;i<4;i++){uint result=1;try{result=Get(i,out state);}catch(DllNotFoundException){try{result=GetOld(i,out state);}catch(DllNotFoundException){try{result=GetLegacy(i,out state);}catch(DllNotFoundException){return false;}catch(EntryPointNotFoundException){return false;}}catch(EntryPointNotFoundException){return false;}}catch(EntryPointNotFoundException){return false;}if(result==0){device=(int)i;return true;}}return false;}
  public static void Reset(){input.Sample(false,-1,0,0,0,0,false);}
  public static bool CanNavigate(Form active,IntPtr foreground) {
   if(active==null||active.IsDisposed||!active.IsHandleCreated||active is ControllerLauncher||foreground==IntPtr.Zero||!IsWindowEnabled(active.Handle))return false;
   uint process;GetWindowThreadProcessId(foreground,out process);
   if(process!=(uint)System.Diagnostics.Process.GetCurrentProcess().Id)return false;
   IntPtr root=GetAncestor(foreground,2);
   if(root==GetAncestor(active.Handle,2))return true;
   return openMenu!=null&&!openMenu.IsDisposed&&openMenu.Visible&&openMenu.IsHandleCreated&&root==GetAncestor(openMenu.Handle,2);
  }
  public static void Poll(Form owner){State state;int device;bool connected=Read(out state,out device);Form active=Form.ActiveForm;bool allowed=CanNavigate(active,GetForegroundWindow());ushort pressed=input.Sample(connected,device,state.Pad.Buttons,state.Pad.X,state.Pad.Y,DateTime.UtcNow.Ticks/TimeSpan.TicksPerMillisecond,allowed);if(allowed&&pressed!=0)ApplyButtons(active,owner,pressed);}

  static Control Focused(Form form){Control control=form.ActiveControl;while(control is ContainerControl&&((ContainerControl)control).ActiveControl!=null)control=((ContainerControl)control).ActiveControl;return control;}
  static void MenuMove(ToolStrip strip,int offset){var items=strip.Items.Cast<ToolStripItem>().Where(i=>i.Available&&i.Enabled&&!(i is ToolStripSeparator)).ToArray();if(items.Length==0)return;int current=Array.FindIndex(items,i=>i.Selected);if(current<0)current=offset<0?0:-1;items[(current+offset+items.Length)%items.Length].Select();}
  public static void ApplyButtons(Form form,Form owner,ushort buttons){
   Attach(form);
   if(openMenu!=null&&openMenu.Visible){
    if((buttons&3)!=0)MenuMove(openMenu,(buttons&1)!=0?-1:1);
    var item=openMenu.Items.Cast<ToolStripItem>().FirstOrDefault(i=>i.Selected) as ToolStripMenuItem;
    if((buttons&0x1000)!=0&&item!=null){if(item.HasDropDownItems){Register(item.DropDown);item.ShowDropDown();MenuMove(item.DropDown,1);}else{var closing=openMenu;item.PerformClick();if(!closing.IsDisposed)closing.Close();}}
    else if((buttons&8)!=0&&item!=null&&item.HasDropDownItems){Register(item.DropDown);item.ShowDropDown();MenuMove(item.DropDown,1);}
    else if((buttons&12)!=0&&openMenu.OwnerItem!=null&&openMenu.OwnerItem.Owner is MenuStrip){var headings=openMenu.OwnerItem.Owner.Items.OfType<ToolStripMenuItem>().Where(i=>i.Available&&i.Enabled).ToArray();int index=Array.IndexOf(headings,(ToolStripMenuItem)openMenu.OwnerItem);var next=headings[(index+((buttons&8)!=0?1:headings.Length-1))%headings.Length];openMenu.Close();Register(next.DropDown);next.ShowDropDown();MenuMove(next.DropDown,1);}
    else if((buttons&4)!=0)openMenu.Close();
    if((buttons&0x2000)!=0&&openMenu!=null)openMenu.Close();return;
   }

   if((buttons&16)!=0){var menu=NextUi.Descendants(owner).OfType<MenuStrip>().FirstOrDefault();if(menu!=null){var item=menu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(i=>i.Enabled&&i.Available);if(item!=null){Register(item.DropDown);item.ShowDropDown();MenuMove(item.DropDown,1);}}return;}
   Control control=Focused(form);
   if((buttons&0x2000)!=0){var combo=control as ComboBox;if(combo!=null&&combo.DroppedDown){combo.DroppedDown=false;return;}if(form!=owner){form.Close();return;}}
   if((buttons&0x1000)!=0){var main=form as MainForm;if(main!=null&&main.ActivateControllerSelection(control))return;var button=control as Button;if(button!=null)button.PerformClick();else if(control is CheckBox)((CheckBox)control).Checked=!((CheckBox)control).Checked;else if(control is RadioButton)((RadioButton)control).Checked=true;else if(control is CheckedListBox){var list=(CheckedListBox)control;if(list.SelectedIndex>=0)list.SetItemChecked(list.SelectedIndex,!list.GetItemChecked(list.SelectedIndex));}else if(control is ComboBox)((ComboBox)control).DroppedDown=!((ComboBox)control).DroppedDown;else if(control is ListView||control is ListBox)typeof(Control).GetMethod("OnDoubleClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(control,new object[]{EventArgs.Empty});}
   int direction=(buttons&1)!=0?-1:(buttons&2)!=0?1:0;
   if(direction!=0){var list=control as ListBox;var combo=control as ComboBox;var view=control as ListView;if(list!=null&&list.Items.Count>0)list.SelectedIndex=Math.Max(0,Math.Min(list.Items.Count-1,list.SelectedIndex+direction));else if(combo!=null&&combo.Items.Count>0)combo.SelectedIndex=Math.Max(0,Math.Min(combo.Items.Count-1,combo.SelectedIndex+direction));else if(view!=null&&view.Items.Count>0){int index=view.SelectedIndices.Count==0?-1:view.SelectedIndices[0];var item=view.Items[Math.Max(0,Math.Min(view.Items.Count-1,index+direction))];view.SelectedItems.Clear();item.Selected=true;item.Focused=true;item.EnsureVisible();}else form.SelectNextControl(control,direction>0,true,true,true);}
   if((buttons&12)!=0){NumericUpDown number=control as NumericUpDown;for(Control parent=control==null?null:control.Parent;number==null&&parent!=null;parent=parent.Parent)number=parent as NumericUpDown;if(number!=null)number.Value=Math.Max(number.Minimum,Math.Min(number.Maximum,number.Value+((buttons&8)!=0?number.Increment:-number.Increment)));else form.SelectNextControl(control,(buttons&8)!=0,true,true,true);}
   if((buttons&0x300)!=0){TabControl tabs=control as TabControl;for(Control parent=control==null?null:control.Parent;tabs==null&&parent!=null;parent=parent.Parent)tabs=parent as TabControl;if(tabs==null)tabs=NextUi.Descendants(form).OfType<TabControl>().FirstOrDefault(t=>t.Visible);if(tabs!=null&&tabs.TabCount>0)tabs.SelectedIndex=(tabs.SelectedIndex+((buttons&0x200)!=0?1:tabs.TabCount-1))%tabs.TabCount;}
  }
 }
}
