using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodeMarket.IDE.Models;

namespace CodeMarket.IDE.Services;

/// <summary>Offline reference completion for local documents; it never claims game unlocks.</summary>
public sealed class LocalReferenceIndex
{
    private readonly Dictionary<string,Dictionary<string,List<CompletionItem>>> methods = new(StringComparer.OrdinalIgnoreCase);
    public LocalReferenceIndex()
    {
        LoadCSharp();LoadJava();
    }
    private void LoadCSharp()
    {
        string dir=Path.Combine(AppContext.BaseDirectory,"SDK","CSharp");
        if(!Directory.Exists(dir))return;
        string text=string.Join("\n",Directory.EnumerateFiles(dir,"*.cs").OrderBy(path=>path,StringComparer.Ordinal).Select(File.ReadAllText));
        var map=new Dictionary<string,List<CompletionItem>>(StringComparer.Ordinal);
        foreach(Match type in Regex.Matches(text,@"public class (Robot|Store)\s*\{(?<body>[\s\S]*?)\n\}"))
        {
            var items=new List<CompletionItem>();
            foreach(Match method in Regex.Matches(type.Groups["body"].Value,@"public\s+(?<ret>[\w<>?]+)\s+(?<name>\w+)\((?<args>[^)]*)\)"))
                items.Add(Item(method.Groups["name"].Value,method.Groups["ret"].Value,method.Groups["args"].Value));
            map[type.Groups[1].Value=="Robot"?"robot":"store"]=items;
        }
        foreach(Match type in Regex.Matches(text,@"public enum (Product|Direction|Entity|Season)\s*\{(?<body>[^}]+)\}"))
            map[type.Groups[1].Value]=type.Groups["body"].Value.Split(',').Select(x=>x.Trim()).Where(x=>x.Length>0).Select(x=>new CompletionItem{Label=x,Insert=x,Detail=type.Groups[1].Value,Caret=-1}).ToList();
        map["Console"]=[Item("WriteLine","void","value"),Item("Write","void","value")];
        methods["csharp"]=map;
    }
    private void LoadJava()
    {
        string dir=Path.Combine(AppContext.BaseDirectory,"SDK","Java");
        if(!Directory.Exists(dir))return;
        var map=new Dictionary<string,List<CompletionItem>>(StringComparer.Ordinal);
        foreach(string type in new[]{"Robot","Store"})
        {
            string file=Path.Combine(dir,type+".java");if(!File.Exists(file))continue;
            string text=File.ReadAllText(file);var items=new List<CompletionItem>();
            foreach(Match method in Regex.Matches(text,@"public\s+(?<ret>[\w<>?]+)\s+(?<name>\w+)\((?<args>[^)]*)\)"))
                items.Add(Item(method.Groups["name"].Value,method.Groups["ret"].Value,method.Groups["args"].Value));
            map[type=="Robot"?"robot":"store"]=items;
        }
        foreach(string type in new[]{"Product","Direction","Entity","Season"})
        {
            string file=Path.Combine(dir,type+".java");if(!File.Exists(file))continue;
            string text=File.ReadAllText(file);int start=text.IndexOf('{')+1;int end=text.IndexOf(';',start);if(end<0)end=text.IndexOf('}',start);
            if(start<=0||end<start)continue;
            map[type]=text[start..end].Split(',').Select(x=>x.Trim()).Where(x=>Regex.IsMatch(x,@"^\w+$")).Select(x=>new CompletionItem{Label=x,Insert=x,Detail=type,Caret=-1}).ToList();
        }
        map["System.out"]=[Item("println","void","value"),Item("print","void","value"),Item("printf","void","value")];
        methods["java"]=map;
    }
    private static CompletionItem Item(string name,string returns,string args) => new() { Label=name,Insert=name+"()",Caret=args.Length==0 ? -1 : name.Length+1,Detail=returns,Doc=returns+" "+name+"("+args+")\nOffline reference; unlocks are not checked." };
    public BridgeResponse Complete(string language,string code,int caret,bool forced)
    {
        if(caret<0||caret>code.Length)return new BridgeResponse();
        if(language is "csharp" or "java" && SdkIndex.IsInsideCommentOrString(code,caret))return new BridgeResponse();
        string text=code[..caret];var match=Regex.Match(text,@"(?<![\w.\)])\b(robot|store|Direction|Product|Entity|Season|Console|System\.out)\.([\w]*)$");
        List<CompletionItem> found=[];int from=caret;
        if(match.Success && methods.TryGetValue(language,out var map) && map.TryGetValue(match.Groups[1].Value,out var entries))
        { string prefix=match.Groups[2].Value;from=caret-prefix.Length;found=entries.Where(x=>x.Label.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)).ToList(); }
        if(!forced && found.Count==1 && found[0].Label==text[from..])found.Clear();
        return new BridgeResponse { Ok=true,From=from,Items=found.Select(x=>JsonSerializer.Serialize(x)).ToArray() };
    }
}
