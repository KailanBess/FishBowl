using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace EmulatorHub {
 public class ScheduledSaveResult {public List<SaveSnapshot> Snapshots=new List<SaveSnapshot>();public List<string> Messages=new List<string>();}
 public sealed class RecoveryMarker {
  public string PathName {get;private set;}public bool Interrupted {get;private set;}public bool Owned {get;private set;}string signature;
  public static bool Alive(string text){try{var parts=text.Split('|');if(parts.Length!=2)return false;int pid;long started;if(!int.TryParse(parts[0],out pid)||!long.TryParse(parts[1],out started))return false;using(var process=Process.GetProcessById(pid))return !process.HasExited&&process.StartTime.ToUniversalTime().Ticks==started;}catch{return false;}}
  public RecoveryMarker(string path){PathName=path;Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));if(File.Exists(path)){var text=File.ReadAllText(path);if(Alive(text))return;Interrupted=true;}using(var process=Process.GetCurrentProcess())signature=process.Id+"|"+process.StartTime.ToUniversalTime().Ticks;File.WriteAllText(path,signature);Owned=true;}
  public void Close(){if(Owned&&File.Exists(PathName)&&File.ReadAllText(PathName)==signature)File.Delete(PathName);Owned=false;}
 }
 public static class HubSaveSchedule {
  public static ScheduledSaveResult Capture(LibraryData copy,CancellationToken token){return Capture(copy,token,null);}
  public static ScheduledSaveResult Capture(LibraryData copy,CancellationToken token,Action<string> progress){
   var result=new ScheduledSaveResult();string managed=Path.Combine(GameStorage.Root(copy),"Game Saves");
   try {
    foreach(var g in copy.Games){
     token.ThrowIfCancellationRequested();if(!(g.Saves??new List<GameSaveEntry>()).Any(link=>!string.IsNullOrWhiteSpace(link.Path)&&!SafeFiles.Within(link.Path,managed)))continue;var emulator=NextData.LaunchEmulator(copy,g);
     if(emulator==null||!Platform.IsDirectProgram(emulator.Executable)||EmulatorRuntime.State(emulator.Executable)!=RuntimeState.Stopped){result.Messages.Add(g.Title+": skipped; a closed executable could not be verified.");continue;}
     try{SaveHistory.RequireClosed(copy,new SaveSnapshot{GameId=g.Id});}catch(Exception ex){result.Messages.Add(g.Title+": "+ex.Message);continue;}
     foreach(var link in (g.Saves??new List<GameSaveEntry>()).ToArray()){
      token.ThrowIfCancellationRequested();if(string.IsNullOrWhiteSpace(link.Path)||SafeFiles.Within(link.Path,managed))continue;
      if(!File.Exists(link.Path)&&!Directory.Exists(link.Path)){result.Messages.Add(g.Title+": linked save missing.");continue;}
      try {
       string hash=SafeFiles.Hash(link.Path,token);
       if(copy.SaveSnapshots.Any(s=>s.GameId==g.Id&&s.Source==link.Path&&s.Kind==link.Kind&&s.Hash==hash)){result.Messages.Add(g.Title+": unchanged save skipped.");continue;}
       var snapshot=SaveHistory.Capture(copy,g,link.Path,link.Kind,false,token);snapshot.Note="Scheduled linked-save capture";result.Snapshots.Add(snapshot);result.Messages.Add(g.Title+": verified save captured.");if(progress!=null)progress(g.Title+": verified save captured.");
      }catch(OperationCanceledException){throw;}catch(Exception ex){result.Messages.Add(g.Title+": "+ex.Message);}
     }
    }
    token.ThrowIfCancellationRequested();return result;
   }catch(OperationCanceledException){foreach(var created in result.Snapshots)SaveHistory.Remove(copy,created);throw;}
  }
  public static void Apply(LibraryData d,ScheduledSaveResult result){foreach(var s in result.Snapshots){if(d.SaveSnapshots.Any(x=>x.Id==s.Id))continue;d.SaveSnapshots.Add(s);var g=d.Games.FirstOrDefault(x=>x.Id==s.GameId);if(g!=null)GameSaves.Link(g,s.Path,s.Kind);}if(result.Snapshots.Count>0)d.Experience.LastSuccessfulBackup=DateTime.UtcNow.ToString("o");Hub.Ensure(d).LastCaptureReport=string.Join("\r\n",result.Messages);}
  public static void Settings(IWin32Window owner,LibraryData d){using(var f=new NextDialog("Linked-save backup schedule",870,620)){var table=NextDialog.Fields(f.Body);var interval=NextDialog.Number(Hub.Ensure(d).CaptureMinutes,0,10080);NextDialog.Field(table,"Capture every N minutes (0 = off)",interval);NextDialog.Field(table,"",new Label{Text="While FishBowl is open, capture changed original saves linked to games with a verifiably closed executable. Unchanged contents and managed snapshots are skipped. Keep sufficient backup space; use reviewed retention to remove old snapshots. Cloud uploads remain your cloud client's responsibility.",AutoSize=true},155);NextDialog.Field(table,"Last capture",new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Text=Hub.Ensure(d).LastCaptureReport??"No scheduled captures yet."},180);f.Action("Save schedule",()=>{Hub.Idle(d);Hub.Ensure(d).CaptureMinutes=(int)interval.Value;d.Hub.NextCaptureAt=DateTime.UtcNow.AddMinutes(Math.Max(1,d.Hub.CaptureMinutes)).ToString("o");Store.Save(d);f.Close();});f.Action("Capture linked saves now",()=>{Hub.Idle(d);var copy=UserTools.Copy(d);var result=BackgroundWork<ScheduledSaveResult>.Run(f,"Capture changed linked saves",(token,progress)=>Capture(copy,token,progress));if(result==null)return;Apply(d,result);Store.Save(d);UserTools.Report(f,"Linked-save capture",d.Hub.LastCaptureReport);});f.Action("Stop scheduled capture",()=>{foreach(var main in Application.OpenForms.OfType<MainForm>())main.CancelScheduledCapture();});f.Action("Snapshot export schedule",()=>NextTools.BackupPlanner(f,d));f.Action("Close",f.Close);f.ShowDialog(owner);}}
 }
 public partial class MainForm {
  RecoveryMarker recovery;System.Windows.Forms.Timer hubCaptureTimer;bool captureRunning;CancellationTokenSource hubCaptureCancellation;
  public void CancelScheduledCapture(){if(hubCaptureCancellation!=null)hubCaptureCancellation.Cancel();}
  void OfferStartupRecovery(){if(recovery!=null&&recovery.Interrupted&&library.Hub.ResumeAfterCrash&&MessageBox.Show(this,"The previous FishBowl run did not close cleanly. Reopen the remembered Library view?","Recover Library view",MessageBoxButtons.YesNo)==DialogResult.Yes){workspaceNavigation.SelectedIndex=2;embeddedLibrary.ReloadLibrary();}}
  void StartHubRuntime(){StartImmersionRuntime();StartReleaseWatch();try{recovery=new RecoveryMarker(Path.Combine(Store.DataDirectory,"open-session.json"));}catch(Exception ex){Store.Log("Recovery marker unavailable: "+ex.Message);}FormClosed+=(a,b)=>{if(recovery!=null)try{recovery.Close();}catch{}};hubCaptureTimer=new System.Windows.Forms.Timer{Interval=60000};hubCaptureTimer.Tick+=(a,b)=>{ScheduleHubCapture();CheckScheduledReleases();};hubCaptureTimer.Start();FormClosing+=(a,b)=>{if(captureRunning){b.Cancel=true;MessageBox.Show(this,"A scheduled save capture is finishing. Wait for it to complete, then close FishBowl.","Save capture in progress");}};Disposed+=(a,b)=>{hubCaptureTimer.Stop();hubCaptureTimer.Dispose();};}
  void ScheduleHubCapture(){var h=Hub.Ensure(library);DateTime due;if(h.CaptureMinutes<=0||captureRunning||UserTools.Guest||UserTools.ActiveLaunches>0||WorkGate.Busy>0||backgroundLibraryScanRunning||automaticCopyRunning)return;if(!DateTime.TryParse(h.NextCaptureAt,out due)){h.NextCaptureAt=DateTime.UtcNow.AddMinutes(h.CaptureMinutes).ToString("o");Store.Save(library);return;}if(DateTime.UtcNow<due.ToUniversalTime())return;LibraryData copy;try{copy=UserTools.Copy(library);}catch(Exception ex){h.LastCaptureReport=ex.Message;return;}captureRunning=true;hubCaptureCancellation=new CancellationTokenSource();Interlocked.Increment(ref WorkGate.Busy);Task.Factory.StartNew(()=>HubSaveSchedule.Capture(copy,hubCaptureCancellation.Token)).ContinueWith(task=>{if(IsDisposed||!IsHandleCreated){captureRunning=false;Interlocked.Decrement(ref WorkGate.Busy);return;}try{BeginInvoke((Action)(()=>{try{h.LastCaptureReport=task.IsCanceled?"Capture cancelled; new snapshots from the batch were discarded.":task.IsFaulted?task.Exception.GetBaseException().Message:string.Join("\r\n",task.Result.Messages);if(!task.IsFaulted&&!task.IsCanceled)HubSaveSchedule.Apply(library,task.Result);h.NextCaptureAt=DateTime.UtcNow.AddMinutes(Math.Max(1,h.CaptureMinutes)).ToString("o");Store.Save(library);}catch(Exception ex){Store.Log("Scheduled linked saves: "+ex.Message);}finally{captureRunning=false;hubCaptureCancellation.Dispose();hubCaptureCancellation=null;Interlocked.Decrement(ref WorkGate.Busy);}}));}catch(InvalidOperationException){captureRunning=false;Interlocked.Decrement(ref WorkGate.Busy);}});}
 }
}
