using System;using System.Linq;using System.Text.Json;using EmulatorHub;
class SchemaTests {
 static int checks;static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
 static void Preserved(JsonElement before,JsonElement after,string path){
  if(before.ValueKind==JsonValueKind.Object){foreach(var field in before.EnumerateObject()){JsonElement retained;Check(after.TryGetProperty(field.Name,out retained),"missing field "+path+"."+field.Name);Preserved(field.Value,retained,path+"."+field.Name);}}
  else if(before.ValueKind==JsonValueKind.Array){Check(before.GetArrayLength()==after.GetArrayLength(),"array length "+path);for(int i=0;i<before.GetArrayLength();i++)Preserved(before[i],after[i],path+"["+i+"]");}
  else Check(before.ToString()==after.ToString(),"changed field "+path);
 }
 static int Main(){try{
  const string fixture=@"{""Version"":2,""Enhancements"":{""TextPercent"":150,""Shortcuts"":{""Home"":""Control, H""}},""Cosmetics"":{""CornerRadius"":8},""Experience"":{""HomeCards"":[""Continue playing""]},""Immersion"":{""Roomier"":true},""Theme"":{""Name"":""Deep Ocean"",""AppIconColor"":""Match accent"",""LastSeenBuild"":""1.24"",""FutureTheme"":{""Shade"":7}},""Games"":[{""Id"":""g1"",""Title"":""Fixture game"",""Pinned"":true,""PlayStatus"":""Playing"",""SaveAssociations"":[{""Path"":""fixture.sav""}]}],""Emulators"":[{""Id"":""e1"",""Name"":""Fixture emulator"",""FutureEmulator"":{""Enabled"":true},""LaunchProfiles"":[{""Id"":""p1"",""Name"":""Default"",""FutureProfile"":42}],""Builds"":[{""Id"":""b1"",""FutureBuild"":""preserve""}]}],""Collections"":[{""Id"":""c1"",""FutureCollection"":true}],""Links"":[{""Id"":""l1"",""FutureLink"":""x""}],""Converters"":[{""Id"":""v1"",""FutureConverter"":3}]}";
  var data=Json.Deserialize<LibraryData>(fixture);var roundtrip=Json.Serialize(data);
  using(var before=JsonDocument.Parse(fixture))using(var after=JsonDocument.Parse(roundtrip))Preserved(before.RootElement,after.RootElement,"library");
  data.Games[0].Title="Linux edit";data.Emulators[0].Favorite=true;
  using(var edited=JsonDocument.Parse(Json.Serialize(data))){var doc=edited.RootElement;JsonElement unwanted;Check(doc.GetProperty("Games")[0].GetProperty("Title").GetString()=="Linux edit","known game edits lost");Check(doc.GetProperty("Games")[0].GetProperty("Pinned").GetBoolean(),"unknown nested game field lost during edit");Check(doc.GetProperty("Theme").GetProperty("LastSeenBuild").GetString()=="1.24","unknown theme field lost during edit");Check(doc.GetProperty("Emulators")[0].GetProperty("Favorite").GetBoolean(),"known emulator edit lost");Check(doc.GetProperty("Enhancements").GetProperty("TextPercent").GetInt32()==150,"unknown top-level settings lost during edit");Check(!doc.TryGetProperty("AdditionalFields",out unwanted),"extension data leaked into library format");}
  var typed=Json.Deserialize<LibraryData>(fixture);Check(typed.Games[0].Pinned&&typed.Games[0].PlayStatus=="Playing","Linux reads newer Windows game fields as typed data");
  var old=Json.Deserialize<LibraryData>("{\"Version\":1,\"Games\":[],\"Emulators\":[],\"Theme\":{\"Name\":\"Forest\"}}");Check(old.Version==1&&old.Theme.Name=="Forest","old library fixture rejected");
  Console.WriteLine("PASS: "+checks+" schema preservation checks");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
