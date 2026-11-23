using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using CodeMarket.IDE.Models;

namespace CodeMarket.IDE.Views;
public partial class ReferencePane : UserControl
{
    private readonly List<ReferenceItem> entries = [];
    private bool displayingPage;
    public event Action<string>? PageRequested;
    public IReadOnlyList<ReferenceItem> Entries => entries;
    public ReferencePane() { InitializeComponent();ShowDisconnected(); }
    public void ShowDisconnected()
    {
        entries.Clear();displayingPage=false;BackButton.Visibility=Visibility.Collapsed;ContentPanel.Children.Clear();
        ContentPanel.Children.Add(Label("Connect to a game world to see its unlocked reference.","#A9BAC9",14));
    }
    public void ShowMessage(string message)
    {
        if(entries.Count!=0 || displayingPage)return;
        ContentPanel.Children.Clear();ContentPanel.Children.Add(Label(message,"#A9BAC9",14));
    }
    public void UpdateIndex(IEnumerable<ReferenceItem> items)
    {
        entries.Clear();entries.AddRange(items);
        if(!displayingPage)RenderIndex();
    }
    public void MarkRead(string id)
    {
        var item=entries.FirstOrDefault(x=>x.Id==id);if(item is not null)item.Unread=false;
    }
    private void SearchChanged(object sender,TextChangedEventArgs e) { if(!displayingPage && ContentPanel is not null)RenderIndex(); }
    private void BackClick(object sender,RoutedEventArgs e) { displayingPage=false;BackButton.Visibility=Visibility.Collapsed;SearchBar.Visibility=Visibility.Visible;RenderIndex();Scroll.ScrollToTop(); }
    private void RenderIndex()
    {
        ContentPanel.Children.Clear();string query=Search.Text.Trim();
        var visible=entries.Where(x=>query.Length==0 || (x.Chip??"").Contains(query,StringComparison.OrdinalIgnoreCase) || (x.Title??"").Contains(query,StringComparison.OrdinalIgnoreCase) || (x.Section??"").Contains(query,StringComparison.OrdinalIgnoreCase)).ToList();
        foreach(var section in visible.GroupBy(x=>x.Section))
        {
            ContentPanel.Children.Add(Label(section.Key,"#F0F4F8",21,true,new Thickness(0,10,0,8)));
            string note=section.First().Note;
            if(!string.IsNullOrEmpty(note))ContentPanel.Children.Add(Label(note,"#ACBCCB",12,false,new Thickness(0,0,0,9)));
            var chips=new WrapPanel { Margin=new Thickness(0,0,0,14) };
            foreach(var entry in section) chips.Children.Add(Chip(entry));
            ContentPanel.Children.Add(chips);
        }
        if(visible.Count==0)ContentPanel.Children.Add(Label(entries.Count==0 ? "Waiting for the game reference…" : "No unlocked reference entries match your search.","#A9BAC9",14));
    }
    private Button Chip(ReferenceItem item)
    {
        string color=item.Highlight switch { "Help"=>"#F27D6E", "Challenge"=>"#F5A04A", _=>item.Kind switch { "Guide"=>"#BCE5C3", "Language"=>"#A7D7F2", "Method"=>"#F7DF7C", "Type"=>"#D5C5F4", "Entity"=>"#F4B6C1", "Goods"=>"#F7CB9E", _=>"#CBD5E1" } };
        var label=new StackPanel { Orientation=Orientation.Horizontal };
        label.Children.Add(new TextBlock {Text=string.IsNullOrEmpty(item.Chip)?item.Title:item.Chip,Foreground=Brush("#2B2F36"),FontWeight=FontWeights.SemiBold,FontSize=12});
        if(item.Unread)label.Children.Add(new TextBlock {Text="  ●",Foreground=Brush("#E8553E"),FontWeight=FontWeights.Bold,FontSize=12});
        var button=new Button { Content=label, Background=Brush(color), Foreground=Brush("#2B2F36"),BorderBrush=Brush(color),Padding=new Thickness(9,5,9,5),Margin=new Thickness(0,0,5,6),ToolTip=item.Title };
        button.Click+=(_,_)=>PageRequested?.Invoke(item.Id);return button;
    }
    public void ShowPage(ReferenceItem item,string? subtitle,string text,IEnumerable<string> related)
    {
        displayingPage=true;MarkRead(item.Id);BackButton.Visibility=Visibility.Visible;SearchBar.Visibility=Visibility.Collapsed;ContentPanel.Children.Clear();
        ContentPanel.Children.Add(Label(item.Section,"#ACBCCB",12,false,new Thickness(0,0,0,8)));
        ContentPanel.Children.Add(Label(item.Title,"#F4F7FA",22,true,new Thickness(0,0,0,8)));
        if(!string.IsNullOrWhiteSpace(subtitle))ContentPanel.Children.Add(Label(subtitle,"#F2C14E",13,false,new Thickness(0,0,0,12),"Consolas"));
        RenderBlocks(ContentPanel,text,Regex.IsMatch(text,@"[А-Яа-яЁё]"));
        var links=related.Select(id=>entries.FirstOrDefault(x=>x.Id==id)).Where(x=>x is not null).Cast<ReferenceItem>().ToList();
        if(links.Count>0) { ContentPanel.Children.Add(Label("See also","#ACBCCB",15,true,new Thickness(0,8,0,8)));var panel=new WrapPanel();foreach(var link in links)panel.Children.Add(Chip(link));ContentPanel.Children.Add(panel); }
        Scroll.ScrollToTop();
    }
    private static void RenderBlocks(Panel parent,string text,bool russian)
    {
        var lines=text.Replace("\r","").Split('\n');var paragraph=new List<string>();
        void Flush() { if(paragraph.Count==0)return;parent.Children.Add(Paragraph(string.Join(" ",paragraph)));paragraph.Clear(); }
        for(int i=0;i<lines.Length;i++)
        {
            string line=lines[i].Trim();
            if(line.StartsWith("```",StringComparison.Ordinal))
            {
                Flush();var code=new List<string>();while(++i<lines.Length && !lines[i].Trim().StartsWith("```",StringComparison.Ordinal))code.Add(lines[i]);
                var box=new TextBox { Text=string.Join("\n",code),IsReadOnly=true,Background=Brush("#18212B"),Foreground=Brush("#D9E3EB"),BorderThickness=new Thickness(0),FontFamily=new FontFamily("Consolas"),FontSize=12,TextWrapping=TextWrapping.Wrap,Padding=new Thickness(10),Margin=new Thickness(0,0,0,12) };parent.Children.Add(box);
            }
            else if(line.StartsWith(">>>",StringComparison.Ordinal))
            {
                Flush();string title=line[3..].Trim().TrimStart('!').Trim();var inside=new List<string>();while(++i<lines.Length && lines[i].Trim()!="<<<")inside.Add(lines[i]);
                var body=new StackPanel {Margin=new Thickness(8)};RenderBlocks(body,string.Join("\n",inside),russian);
                parent.Children.Add(new Expander { Header=title,Content=body,Background=Brush("#33404F"),Foreground=Brush("#E5EDF4"),Padding=new Thickness(8),Margin=new Thickness(0,0,0,10) });
            }
            else if(line.StartsWith("@map ",StringComparison.Ordinal)) { Flush();parent.Children.Add(ReferenceMap.Create(line[5..].Trim(),russian)); }
            else if(line.Length==0)Flush();else paragraph.Add(line);
        }
        Flush();
    }
    private static TextBlock Paragraph(string text)
    {
        var block=Label("","#D3DCE6",13,false,new Thickness(0,0,0,12));
        int last=0;foreach(Match match in Regex.Matches(text,@"\*\*.+?\*\*|`[^`]+`"))
        {
            if(match.Index>last)block.Inlines.Add(new Run(text[last..match.Index]));
            string value=match.Value;
            if(value.StartsWith("**",StringComparison.Ordinal))block.Inlines.Add(new Bold(new Run(value[2..^2])));
            else block.Inlines.Add(new Run(value[1..^1]) {Foreground=Brush("#FFD27A"),FontFamily=new FontFamily("Consolas")});
            last=match.Index+match.Length;
        }
        if(last<text.Length)block.Inlines.Add(new Run(text[last..]));return block;
    }
    private static TextBlock Label(string text,string color,double size,bool bold=false,Thickness? margin=null,string? font=null) => new() {Text=text,Foreground=Brush(color),FontSize=size,FontWeight=bold?FontWeights.Bold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap,Margin=margin??new Thickness(0),FontFamily=new FontFamily(font??"Segoe UI")};
    private static Brush Brush(string value)=>(Brush)new BrushConverter().ConvertFromString(value)!;
}
