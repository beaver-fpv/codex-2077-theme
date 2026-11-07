using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using Codex2077;

class BackendTests {
 static int passed;
 static object Call(string method, params object[] args) {
  return typeof(Backend).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,args);
 }
 static void Assert(bool result,string name) { if(!result)throw new Exception(name); Console.WriteLine("PASS " + name); passed++; }
 static void RejectBool(Dictionary<string,object> data,string name) {
  try { Call("Bool",data,"installed"); throw new Exception("Accepted malformed bool: " + name); }
  catch(TargetInvocationException e) { Assert(e.InnerException is InvalidOperationException,name); }
 }
 static void Main(string[] args) {
  try {
   Assert((bool)Call("Bool",new Dictionary<string,object>{{"installed",true}},"installed"),"installed true readback");
   Assert(!(bool)Call("Bool",new Dictionary<string,object>{{"installed",false}},"installed"),"installed false readback");
   RejectBool(new Dictionary<string,object>(),"missing state fails closed");
   RejectBool(new Dictionary<string,object>{{"installed","false"}},"string state fails closed");
   RejectBool(new Dictionary<string,object>{{"installed",null}},"null state fails closed");
   Assert((bool)Call("IsLoopbackEndpoint","127.0.0.1:9341"),"IPv4 loopback accepted");
   Assert((bool)Call("IsLoopbackEndpoint","[::1]:9341"),"IPv6 loopback accepted");
   foreach(var endpoint in new[]{"0.0.0.0:9341","[::]:9341","192.168.1.4:9341","127.0.0.1:9342","127.0.0.1.evil:9341"})
    Assert(!(bool)Call("IsLoopbackEndpoint",endpoint),"reject nonlocal or wrong port " + endpoint);
   var targets=new List<TargetInfo>{new TargetInfo{Id="first",Title="A"},new TargetInfo{Id="second",Title="B"}};
   Assert(Call("SelectTarget",targets,null)==null,"multiple windows require selection");
   Assert(Call("SelectTarget",targets,"missing")==null,"stale selection refused");
   Assert(Object.ReferenceEquals(Call("SelectTarget",targets,"second"),targets[1]),"explicit second window respected");
   Assert(Object.ReferenceEquals(Call("SelectTarget",new List<TargetInfo>{targets[0]},null),targets[0]),"single window auto-selected");
   string[] urls={"app://-/index.html","app://-/index.html?initialRoute=/thread/abc","app://-/index.html?initialRoute=/spotlight","app://-/index.html?initialRoute=/avatar-overlay","app://-/index.html?initialRoute=/detached-window/a","https://example.com/index.html","app://evil/index.html","app://user@-/index.html","app://-:80/index.html","app://-/index.html#x","app://-/index.html?initialRoute=/thread/a&initialRoute=/thread/b"};
   var items=urls.Select((url,i)=>new Dictionary<string,object>{{"type","page"},{"id","id"+i},{"title","title"},{"url",url}}).ToArray();
   var selected=(IList<TargetInfo>)Call("ParseTargets",new JavaScriptSerializer().Serialize(items));
   Assert(selected.Count==2 && selected[0].Id=="id0" && selected[1].Id=="id1","target parser excludes other windows and invalid URLs");
   string sample = Path.Combine(Path.GetTempPath(), "codex-backend-path-test", "sample.exe");
   Assert((bool)Call("SamePath",sample,sample.ToUpperInvariant()),"path comparison ignores Windows path case");
   Assert(!(bool)Call("SamePath",sample,Path.Combine(Path.GetDirectoryName(sample),"other.exe")),"different files are not equivalent");
   if (args.Length == 2) {
    Assert(File.Exists(args[0]) && File.Exists(args[1]),"junction fixture files exist");
    Assert((bool)Call("SamePath",args[0],args[1]),"directory junction identity regression");
   } else if (args.Length == 0) {
    Console.WriteLine("SKIP junction integration check: run VerifyBackend.ps1 to create the isolated fixture.");
   } else {
    throw new ArgumentException("Expected zero arguments, or the real and junction fixture file paths.");
   }
   Console.WriteLine("Passed " + passed + " checks.");
  }catch(Exception ex){Console.Error.WriteLine(ex);Environment.ExitCode=1;}
 }
}
