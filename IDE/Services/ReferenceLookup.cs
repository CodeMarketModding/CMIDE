using System.Text.RegularExpressions;
using CodeMarket.IDE.Models;

namespace CodeMarket.IDE.Services;

/// <summary>Resolves source symbols only to reference pages currently open in the game.</summary>
public static class ReferenceLookup
{
    private static readonly Dictionary<string,string> Keywords = new(StringComparer.Ordinal)
    {
        ["if"]="lang.if",["else"]="lang.if",["while"]="lang.while",["for"]="lang.for",["foreach"]="lang.for",
        ["break"]="lang.break",["continue"]="lang.break",["class"]="lang.classes",["static"]="lang.static",
        ["using"]="lang.import",["import"]="lang.import",["string"]="lang.strings",["String"]="lang.strings",
        ["int"]="lang.variables",["bool"]="lang.variables",["boolean"]="lang.variables",["double"]="lang.variables",
        ["var"]="lang.variables",["List"]="lang.lists",["ArrayList"]="lang.lists",["Console"]="lang.print",["print"]="lang.print",["Math"]="lang.math"
    };
    private static readonly HashSet<string> Types = new(StringComparer.Ordinal) { "Robot","Store","Order","Customer","Shelf","Cell","Product","Direction","Entity","Season" };
    public static string? Find(string code,int offset,IReadOnlyList<ReferenceItem> entries)
    {
        if(code.Length==0 || offset<0 || offset>code.Length)return null;
        int p=Math.Min(offset,code.Length-1);
        if(!Word(code[p]) && p>0 && Word(code[p-1]))p--;
        if(!Word(code[p]) || SdkIndex.IsInsideCommentOrString(code,p))return null;
        int start=p,end=p+1;while(start>0&&Word(code[start-1]))start--;while(end<code.Length&&Word(code[end]))end++;
        string token=code[start..end];
        string? id=null;
        int dot=start-1;while(dot>=0&&char.IsWhiteSpace(code[dot]))dot--;
        if(dot>=0&&code[dot]=='.')
        {
            int n=dot-1;while(n>=0&&char.IsWhiteSpace(code[n]))n--;int finish=n+1;while(n>=0&&Word(code[n]))n--;
            string owner=code[(n+1)..finish];string? type=TypeOf(owner,code,start);
            if(owner=="Product") id=FindProduct(token,entries) ?? "type.Product";
            else if(owner=="Direction") id="type.Direction";
            else if(owner=="Entity") id=FindEntity(token,entries) ?? "type.Entity";
            else if(owner=="Console" || (owner=="out" && Regex.IsMatch(code[..(n+1)],@"\bSystem\s*\.\s*$"))) id="lang.print";
            else if(owner=="Math") id="lang.math";
            else if(type is "Robot" or "Store" or "Order" or "Customer" or "Shelf" or "Cell")
                id=type.ToLowerInvariant()+"."+char.ToLowerInvariant(token[0])+token[1..];
            else if(type is "Product" or "Direction" or "Entity" or "Season") id="type."+type;
            else if(type is "List" or "ArrayList") id="lang.lists";
        }
        else
        {
            if(Types.Contains(token))id="type."+token;
            else if(token=="robot")id="type.Robot";
            else if(token=="store")id="type.Store";
            else if(Keywords.TryGetValue(token,out var keyword))id=keyword;
            else
            {
                string? type=TypeOf(token,code,end);
                if(type is not null) id=Types.Contains(type) ? "type."+type : type is "List" or "ArrayList" ? "lang.lists" : "lang.variables";
            }
        }
        return id is not null ? entries.FirstOrDefault(x=>string.Equals(x.Id,id,StringComparison.OrdinalIgnoreCase))?.Id : null;
    }
    private static string? TypeOf(string name,string code,int end)
    {
        if(name=="robot")return "Robot";if(name=="store")return "Store";
        string source=code[..Math.Min(end,code.Length)];
        var declarations=Regex.Matches(source,@"\b(?<type>Robot|Store|Order|Customer|Shelf|Cell|Product|Direction|Entity|Season|(?:ArrayList|List)\s*(?:<[^>]+>)?|int|bool|boolean|string|String|double|var)\s+"+Regex.Escape(name)+@"\b(?<rest>[^;\n]*)");
        if(declarations.Count==0)return null;
        var found=declarations[^1];string type=found.Groups["type"].Value;
        if(type.StartsWith("List",StringComparison.Ordinal))return "List";
        if(type.StartsWith("ArrayList",StringComparison.Ordinal))return "ArrayList";
        if(type!="var")return type;
        string rhs=found.Groups["rest"].Value;
        if(Regex.IsMatch(rhs,@"\.\s*(TakeOrder|takeOrder)\s*\("))return "Order";
        if(Regex.IsMatch(rhs,@"\.\s*(GetShelf|getShelf)\s*\("))return "Shelf";
        if(Regex.IsMatch(rhs,@"\.\s*(GetCustomer|getCustomer|GetWaitingCustomer|getWaitingCustomer)\s*\("))return "Customer";
        if(Regex.IsMatch(rhs,@"\.\s*(GetList|getList|GetCarried|getCarried|GetDelivery|getDelivery)\s*\("))return "List";
        return null;
    }
    private static string Norm(string text)=>new string(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static string? FindProduct(string token,IReadOnlyList<ReferenceItem> entries)
    {
        string wanted=Norm(token);
        return entries.FirstOrDefault(x=>x.Id.StartsWith("goods.",StringComparison.Ordinal) && Norm(x.Id[6..])==wanted)?.Id;
    }
    private static string? FindEntity(string token,IReadOnlyList<ReferenceItem> entries)
    {
        string wanted=Norm(token);
        return entries.FirstOrDefault(x=>x.Id.StartsWith("entity.",StringComparison.Ordinal) && Norm(x.Id[7..])==wanted)?.Id;
    }
    private static bool Word(char c)=>char.IsLetterOrDigit(c)||c=='_';
}
