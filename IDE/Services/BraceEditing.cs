namespace CodeMarket.IDE.Services;

/// <summary>Calculates brace and newline edits without changing the source document.</summary>
public static class BraceEditing
{
    public sealed record Edit(int Offset, int Length, string Text, int CaretOffset, int SelectionLength = 0);

    public static Edit? OpeningBrace(string text, int start, int length)
    {
        if(start<0 || length<0 || start+length>text.Length || SdkIndex.IsInsideCommentOrString(text,start))return null;
        string selected=text.Substring(start,length);
        return new Edit(start,length,"{"+selected+"}",start+1,length);
    }

    public static Edit? ClosingBrace(string text, int offset, int selectionLength)
    {
        if(selectionLength!=0 || offset<0 || offset>=text.Length || text[offset]!='}' || SdkIndex.IsInsideCommentOrString(text,offset))return null;
        return new Edit(offset,0,"",offset+1);
    }

    public static Edit? NewLine(string text, int offset, int selectionLength)
    {
        if(selectionLength!=0 || offset<0 || offset>text.Length)return null;
        int lineStart=offset;
        while(lineStart>0 && text[lineStart-1]!='\n' && text[lineStart-1]!='\r')lineStart--;
        int indentEnd=lineStart;
        while(indentEnd<text.Length && (text[indentEnd]==' ' || text[indentEnd]=='\t'))indentEnd++;
        string indent=text.Substring(lineStart,Math.Min(indentEnd,offset)-lineStart);
        string newline=text.Contains("\r\n",StringComparison.Ordinal)?"\r\n":"\n";
        int before=offset-1;
        while(before>=lineStart && (text[before]==' ' || text[before]=='\t'))before--;
        bool opensBlock=before>=lineStart && text[before]=='{' && !SdkIndex.IsInsideCommentOrString(text,before);
        string inner=indent+(opensBlock ? (indent.Contains('\t')?"\t":"    ") : "");
        int after=offset;
        while(after<text.Length && (text[after]==' ' || text[after]=='\t'))after++;
        bool between=opensBlock && after<text.Length && text[after]=='}';
        string insertion=newline+inner+(between ? newline+indent : "");
        return new Edit(offset,between?after-offset:0,insertion,offset+newline.Length+inner.Length);
    }
}
