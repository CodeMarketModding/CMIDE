using System.Text;
using System.Text.RegularExpressions;
using CodeMarket.IDE.Models;

namespace CodeMarket.IDE.Services;

/// <summary>Lightweight outline for incomplete C# and Java documents, without compiling them.</summary>
public static class ClassMapParser
{
    private sealed record Scope(string Kind, ClassMapType? Type);
    private static readonly Regex TypePattern = new(@"\b(?<kind>class|interface|enum|record|struct)\s+(?<name>[A-Za-z_]\w*)(?:\s*<[^{};]+?>)?(?<bases>[^{};]*)$", RegexOptions.Compiled);
    private static readonly Regex MethodPattern = new(@"^(?<prefix>[\w\s.<>,?\[\]]+?)\s+(?<name>[A-Za-z_]\w*)\s*\((?<args>[^()]*)\)\s*(?:throws\s+[\w.,\s]+)?$", RegexOptions.Compiled);
    private static readonly Regex FieldPattern = new(@"^(?:(?:public|private|protected|internal|static|final|readonly|const|volatile|transient|abstract|virtual|override|sealed|new)\s+)*(?<type>[A-Za-z_]\w*(?:\s*<[^;=]+?>)?(?:\[\])?\??)\s+(?<name>[A-Za-z_]\w*)\s*(?:=.*)?$", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly HashSet<string> NotTypes = new(StringComparer.Ordinal) { "return", "throw", "new", "using", "import", "package", "else", "case", "break", "continue" };

    public static IReadOnlyList<ClassMapType> Parse(string text)
    {
        string code=Mask(text); var result=new List<ClassMapType>();
        var script=new ClassMapType("Script", "script", "", 0, []);
        var scopes=new Stack<Scope>();scopes.Push(new Scope("container",null));int start=0;
        for(int i=0;i<code.Length;i++)
        {
            char c=code[i];if(c!='{' && c!='}' && c!=';')continue;
            string header=code[start..i];int leading=header.Length-header.TrimStart().Length;
            string trimmed=header.Trim();int offset=start+leading;
            Scope scope=scopes.Peek();
            if(c=='{')
            {
                Scope next=new("other",null);
                if(scope.Kind is "container" or "type")
                {
                    var match=TypePattern.Match(trimmed);
                    if(match.Success)
                    {
                        var type=new ClassMapType(match.Groups["name"].Value,match.Groups["kind"].Value,match.Groups["bases"].Value.Trim(),offset+match.Groups["name"].Index,[]);
                        result.Add(type);next=new Scope("type",type);
                    }
                    else if(trimmed.StartsWith("namespace ",StringComparison.Ordinal))next=new Scope("container",null);
                    else if(TryMethod(trimmed,offset,scope.Type ?? script,out _))next=new Scope("method",null);
                    else if(scope.Kind=="type" && TryField(trimmed,offset,scope.Type!,"property"))next=new Scope("property",null);
                }
                scopes.Push(next);start=i+1;
            }
            else if(c=='}') { if(scopes.Count>1)scopes.Pop();start=i+1; }
            else
            {
                if(scope.Kind is "container" or "type")
                {
                    var owner=scope.Type ?? script;
                    if(!TryMethod(trimmed,offset,owner,out _))TryField(trimmed,offset,owner,"field");
                }
                start=i+1;
            }
        }
        if(script.Members.Count>0)result.Insert(0,script);
        return result;
    }
    private static bool TryMethod(string header,int offset,ClassMapType owner,out ClassMapMember? member)
    {
        member=null;int arrow=header.IndexOf("=>",StringComparison.Ordinal);if(arrow>=0)header=header[..arrow].TrimEnd();
        int open=header.IndexOf('(');if(open<0 || header[..open].Contains('='))return false;
        var match=MethodPattern.Match(header);
        if(!match.Success && owner.Kind!="script")
        {
            var ctor=Regex.Match(header,@"^(?:(?:public|private|protected|internal)\s+)*(?<name>"+Regex.Escape(owner.Name)+@")\s*\((?<args>[^()]*)\)$");
            if(ctor.Success) { string ctorArgs=Regex.Replace(ctor.Groups["args"].Value,@"\s+"," ").Trim();member=new ClassMapMember(owner.Name,"method",owner.Name+"("+ctorArgs+")",offset+ctor.Groups["name"].Index);owner.Members.Add(member);return true; }
        }
        if(!match.Success)return false;
        string prefix=match.Groups["prefix"].Value.Trim(), name=match.Groups["name"].Value;
        if(NotTypes.Contains(prefix.Split(' ',StringSplitOptions.RemoveEmptyEntries)[0]))return false;
        string args=Regex.Replace(match.Groups["args"].Value,@"\s+"," ").Trim();
        member=new ClassMapMember(name,"method",name+"("+args+")",offset+match.Groups["name"].Index);owner.Members.Add(member);return true;
    }
    private static bool TryField(string header,int offset,ClassMapType owner,string kind)
    {
        var match=FieldPattern.Match(header);if(!match.Success || NotTypes.Contains(match.Groups["type"].Value))return false;
        string name=match.Groups["name"].Value, type=Regex.Replace(match.Groups["type"].Value,@"\s+"," ");
        owner.Members.Add(new ClassMapMember(name,kind,name+" : "+type,offset+match.Groups["name"].Index));return true;
    }
    private static string Mask(string text)
    {
        var result=new StringBuilder(text);int state=0;
        for(int i=0;i<text.Length;i++)
        {
            char c=text[i], next=i+1<text.Length?text[i+1]:'\0';
            if(state==0)
            {
                if(c=='/' && next=='/') { state=1;result[i]=result[++i]=' '; }
                else if(c=='/' && next=='*') { state=2;result[i]=result[++i]=' '; }
                else if(c=='@' && next=='"') { state=4;result[i]=result[++i]=' '; }
                else if(c=='"' || c=='\'') { state=c=='"'?3:5;result[i]=' '; }
            }
            else
            {
                if(c!='\n' && c!='\r')result[i]=' ';
                if(state==1 && c=='\n')state=0;
                else if(state==2 && c=='*' && next=='/') { result[++i]=' ';state=0; }
                else if(state==4 && c=='"') { if(next=='"')result[++i]=' ';else state=0; }
                else if(state is 3 or 5) { if(c=='\\' && i+1<text.Length) { if(text[i+1]!='\n' && text[i+1]!='\r')result[i+1]=' ';i++; } else if((state==3 && c=='"') || (state==5 && c=='\''))state=0; }
            }
        }
        return result.ToString();
    }
}
