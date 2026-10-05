using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace EmulatorHub {
 public class ReleaseWatchResult {public string EmulatorId;public UpdateResult Release;public string Error;}
 public static class ReleaseWatch {
  public static List<ReleaseWatchResult> Check(IEnumerable<EmulatorProfile> profiles,CancellationToken token,Func<string,string> fetch=null){var results=new List<ReleaseWatchResult>();foreach(var e in profiles){token.ThrowIfCancellationRequested();if(string.IsNullOrWhiteSpace(EmulatorUpdates.Repository(e)))continue;try{results.Add(new ReleaseWatchResult{EmulatorId=e.Id,Release=EmulatorUpdates.Check(e,fetch??(url=>Encoding.UTF8.GetString(Hub.Fetch(url,2097152,token))))});}catch(OperationCanceledException){throw;}catch(Exception ex){results.Add(new ReleaseWatchResult{EmulatorId=e.Id,Error=ex.Message});}}return results;}
  public static List<string> Apply(LibraryData d,IEnumerable<ReleaseWatchResult> results){var messages=new List<string>();var h=Hub.Ensure(d);if(h.NotifiedReleases==null)h.NotifiedReleases=new Dictionary<string,string>();foreach(var result in results){var e=d.Emulators.FirstOrDefault(x=>x.Id==result.EmulatorId);if(e==null)continue;if(result.Release==null){Store.Log("Release watch for "+e.Name+": "+result.Error);continue;}var r=result.Release;e.LatestReleaseTag=r.Latest;e.LatestReleaseUrl=r.Url;e.LatestReleaseNotes=r.Notes;e.LastUpdateCheck=r.CheckedAt;string notified;h.NotifiedReleases.TryGetValue(e.Id,out notified);if(r.Status=="Newer release available."&&r.Latest!=notified){messages.Add(e.Name+": "+r.Latest);h.NotifiedReleases[e.Id]=r.Latest;}}return messages;}
 }
 public partial class MainForm {
  bool releaseWatchRunning;CancellationTokenSource releaseWatchCancellation;NotifyIcon releaseWatchIcon;
  void StartReleaseWatch(){releaseWatchIcon=new NotifyIcon{Icon=System.Drawing.SystemIcons.Information,Text="FishBowl emulator releases",Visible=false};releaseWatchIcon.BalloonTipClicked+=(a,b)=>ShowHubExtensions();Disposed+=(a,b)=>{if(releaseWatchCancellation!=null)releaseWatchCancellation.Cancel();releaseWatchIcon.Visible=false;releaseWatchIcon.Dispose();};}
  void CheckScheduledReleases(){var h=Hub.Ensure(library);if(!h.WatchReleases||releaseWatchRunning||UserTools.Guest||UserTools.ActiveLaunches>0||WorkGate.Busy>0||backgroundLibraryScanRunning||automaticCopyRunning)return;DateTime last;if(DateTime.TryParse(h.LastReleaseWatchAt,out last)&&DateTime.UtcNow-last.ToUniversalTime()<TimeSpan.FromDays(1))return;var profiles=UserTools.Copy(library.Emulators);releaseWatchRunning=true;var cancellation=new CancellationTokenSource();releaseWatchCancellation=cancellation;Interlocked.Increment(ref WorkGate.Busy);Task.Factory.StartNew(()=>ReleaseWatch.Check(profiles,cancellation.Token)).ContinueWith(task=>{if(IsDisposed||!IsHandleCreated){cancellation.Dispose();Interlocked.Decrement(ref WorkGate.Busy);return;}try{BeginInvoke((Action)(()=>{try{h.LastReleaseWatchAt=DateTime.UtcNow.ToString("o");if(!task.IsFaulted&&!task.IsCanceled){var messages=ReleaseWatch.Apply(library,task.Result);Store.Save(library);if(messages.Count>0){Hub.Record(library,"Emulator releases available: "+string.Join(", ",messages));Store.Save(library);SetStatus("Emulator releases available. Open Library extensions → Versions, updates and rollback.");releaseWatchIcon.Visible=true;releaseWatchIcon.ShowBalloonTip(8000,"Emulator releases available",string.Join("\n",messages.Take(5)),ToolTipIcon.Info);}}else{Store.Log("Release watch could not finish.");Store.Save(library);}}catch(Exception ex){Store.Log("Release watch: "+ex.Message);}finally{releaseWatchRunning=false;releaseWatchCancellation=null;cancellation.Dispose();Interlocked.Decrement(ref WorkGate.Busy);}}));}catch(InvalidOperationException){cancellation.Dispose();Interlocked.Decrement(ref WorkGate.Busy);}});}
 }
}
