using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using EmulatorHub;
class RemotePlayTests
{
    static int checks;
    static void Check(bool value,string name) { checks++; if(!value) throw new Exception(name); }
    static int Request(string address,string method,string token,string body,out string response)
    {
        var request=(HttpWebRequest)WebRequest.Create(address); request.Method=method; request.Timeout=4000;
        if(token!=null) request.Headers[HttpRequestHeader.Authorization]="Bearer "+token;
        if(body!=null) { request.ContentType="application/json"; byte[] bytes=Encoding.UTF8.GetBytes(body); request.ContentLength=bytes.Length; using(var stream=request.GetRequestStream()) stream.Write(bytes,0,bytes.Length); }
        try { using(var result=(HttpWebResponse)request.GetResponse()) { using(var reader=new StreamReader(result.GetResponseStream())) response=reader.ReadToEnd(); return (int)result.StatusCode; } }
        catch(WebException ex) { using(var result=(HttpWebResponse)ex.Response) { using(var reader=new StreamReader(result.GetResponseStream())) response=reader.ReadToEnd(); return (int)result.StatusCode; } }
    }
    [STAThread] static int Main()
    {
        Check(RemotePlayTools.Service("https://service.example/api") == "https://service.example", "service authority normalized");
        bool blocked=false;try {RemotePlayTools.Service("http://service.example");}catch(IOException){blocked=true;} Check(blocked,"remote plain HTTP blocked");
        Check(RemotePlayTools.Service("http://127.0.0.1:8787") == "http://127.0.0.1:8787","loopback development supported");
        Check(RemotePlayTools.ValidKeys(new[]{"KeyZ","ArrowUp"}),"approved key vocabulary");
        Check(!RemotePlayTools.ValidKeys(new[]{"ControlLeft","Delete"}),"system shortcuts rejected");
        Check(!RemotePlayTools.ValidKeys(null),"null key list rejected");
        using(var own=Process.GetCurrentProcess()) using(var bridge=new RemotePlayBridge(new RemoteWindow {Pid=own.Id,Started=own.StartTime.ToUniversalTime().Ticks,Handle=IntPtr.Zero},"https://service.example"))
        {
            bridge.Start(); var uri=new Uri(bridge.Address); string root=uri.GetLeftPart(UriPartial.Authority), token=uri.Fragment.Substring(8), response;
            Check(token.Length==64,"random authorization has 256 bits");
            Check(Request(root+"/","GET",null,null,out response)==200 && response.Contains("Share game window"),"embedded client page available");
            Check(Request(root+"/livekit.js","GET",null,null,out response)==200 && response.Length>500000,"official pinned media SDK embedded");
            Check(Request(root+"/config","GET",null,null,out response)==401,"configuration requires session authorization");
            Check(Request(root+"/config","GET",token,null,out response)==200 && response.Contains("https://service.example"),"authorized config has service and palette");
            Check(Request(root+"/input","POST","invalid","{\"keys\":[]}",out response)==401,"unauthorized input rejected");
            Check(Request(root+"/input","POST",token,"{\"keys\":[\"Delete\"]}",out response)==400,"unsupported input rejected");
            Check(Request(root+"/input","POST",token,"{\"keys\":[]}",out response)==200,"release input accepted");
            Check(Request(root+"/state","POST",token,"{}",out response)==200 && response.Contains("false"),"unapproved or unfocused window cannot receive input");
            bridge.Allow(true);
            Check(Request(root+"/state","POST",token,"{}",out response)==200 && response.Contains("false"),"approval cannot bypass exact foreground window validation");
        }
        using(var dialog=new RemotePlayDialog(new LibraryData(),null)) Check(dialog.Text=="Remote couch play" && dialog.Body.AutoScroll,"existing styled scrollable dialog");
        Console.WriteLine("Remote play: "+checks+" checks passed."); return 0;
    }
}
