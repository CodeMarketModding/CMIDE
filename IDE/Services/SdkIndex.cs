using System.Text.Json;
using System.Text.RegularExpressions;
using CodeMarket.IDE.Models;

namespace CodeMarket.IDE.Services;

/// <summary>Local, world-specific projection of the game's completion metadata.</summary>
public sealed class SdkIndex
{
    private static readonly string[] CSharpTargets = ["robot", "store", "Direction", "Product", "Entity", "Season", "Console"];
    private static readonly string[] JavaTargets = ["robot", "store", "Direction", "Product", "Entity", "Season", "System.out"];
    private string[] targets = CSharpTargets;
    private Dictionary<string, List<CompletionItem>> active = new(StringComparer.Ordinal);
    private Dictionary<string, List<CompletionItem>> staging = new(StringComparer.Ordinal);
    private int next;
    public bool Ready { get; private set; }
    public bool Refreshing { get; private set; }
    public DateTime RefreshAfter { get; private set; } = DateTime.MinValue;
    public void Reset() { active.Clear(); staging.Clear(); next=0;Ready=false;Refreshing=false;RefreshAfter=DateTime.MinValue; }
    public void SetLanguage(string? language) { targets=language == "java" ? JavaTargets : CSharpTargets;Reset(); }
    public void BeginRefresh() { staging=new(StringComparer.Ordinal);next=0;Refreshing=true; }
    public string NextSample => targets[next];
    public void AddSample(BridgeResponse response)
    {
        var items = (response.Items ?? []).Select(x => JsonSerializer.Deserialize<CompletionItem>(x)).Where(x => x is not null).Cast<CompletionItem>().ToList();
        staging[targets[next++]]=items;
        if(next==targets.Length) { active=staging;Ready=true;Refreshing=false;RefreshAfter=DateTime.UtcNow.AddSeconds(10); }
    }
    public bool TryComplete(string code,int caret,bool forced,out BridgeResponse response)
    {
        response=new BridgeResponse();
        if(!Ready || caret<0 || caret>code.Length || IsInsideCommentOrString(code,caret)) return false;
        string before=code[..caret];
        var match=Regex.Match(before,@"(?<![\w.\)])\b(robot|store|Direction|Product|Entity|Season|Console|System\.out)\.([\w]*)$");
        if(!match.Success || !active.TryGetValue(match.Groups[1].Value,out var all))return false;
        string prefix=match.Groups[2].Value;
        var result=all.Where(x=>x.Label.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))
            .OrderBy(x=>x.Label.StartsWith(prefix,StringComparison.Ordinal)?0:1).ToList();
        if(!forced && result.Count==1 && result[0].Label==prefix)result.Clear();
        response=new BridgeResponse { Ok=true, From=caret-prefix.Length, Items=result.Select(x=>JsonSerializer.Serialize(x)).ToArray() };
        return true;
    }
    // Keep ambiguous syntax on the game path instead of suggesting code inside comments/strings.
    internal static bool IsInsideCommentOrString(string text,int end)
    {
        int mode=0;
        for(int i=0;i<end;i++)
        {
            char c=text[i],n=i+1<end ? text[i+1] : '\0';
            if(mode==0)
            {
                if(c=='/' && n=='/') { mode=1;i++; }
                else if(c=='/' && n=='*') { mode=2;i++; }
                else if(c=='@' && n=='"') { mode=4;i++; }
                else if(c=='"')mode=3;
                else if(c=='\'')mode=5;
            }
            else if(mode==1) { if(c=='\n')mode=0; }
            else if(mode==2) { if(c=='*' && n=='/') { mode=0;i++; } }
            else if(mode==4) { if(c=='"' && n=='"')i++;else if(c=='"')mode=0; }
            else if(c=='\\')i++;
            else if((mode==3 && c=='"') || (mode==5 && c=='\''))mode=0;
        }
        return mode!=0;
    }
}
