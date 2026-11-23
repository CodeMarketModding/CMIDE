using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CodeMarket.IDE.Models;

namespace CodeMarket.IDE.Views;
public partial class ClassMapPane : UserControl
{
    public event Action<int>? NavigateRequested;
    public ClassMapPane() => InitializeComponent();
    public void Update(IReadOnlyList<ClassMapType> types, bool hasDocument)
    {
        Cards.Children.Clear();
        if(!hasDocument || types.Count==0)
        {
            Cards.Children.Add(new TextBlock { Text=hasDocument ? "No classes or declarations found in this document." : "Open a script or local file to see its classes.", Foreground=Brush("#999999"), TextWrapping=TextWrapping.Wrap, Margin=new Thickness(5,4,5,0) });return;
        }
        foreach(var type in types)
        {
            var content=new StackPanel();
            var heading=Link(type.Kind.ToUpperInvariant()+"  "+type.Name,"#DCDCAA",type.Offset);
            heading.FontWeight=FontWeights.SemiBold;heading.FontSize=13;content.Children.Add(heading);
            if(!string.IsNullOrWhiteSpace(type.Bases)) content.Children.Add(new TextBlock { Text=type.Bases,Foreground=Brush("#85B9CF"),FontSize=11,Margin=new Thickness(8,0,8,6),TextWrapping=TextWrapping.Wrap });
            if(type.Members.Count>0)
            {
                content.Children.Add(new Border { Height=1,Background=Brush("#454545"),Margin=new Thickness(0,2,0,3) });
                foreach(var member in type.Members)
                {
                    var row=Link((member.Kind=="method" ? "ƒ  " : member.Kind=="property" ? "◇  " : "•  ")+member.Detail,member.Kind=="method" ? "#DCDCAA" : "#9CDCFE",member.Offset);
                    row.ToolTip=member.Kind+": "+member.Detail;content.Children.Add(row);
                }
            }
            Cards.Children.Add(new Border { Background=Brush("#2D2D30"),BorderBrush=Brush("#505058"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(4),Padding=new Thickness(4),Margin=new Thickness(0,0,0,9),Child=content });
        }
    }
    private Button Link(string text,string color,int offset)
    {
        var button=new Button { Content=new TextBlock {Text=text,Foreground=Brush(color),TextTrimming=TextTrimming.CharacterEllipsis},Background=Brush("#2D2D30"),BorderThickness=new Thickness(0),Padding=new Thickness(6,4,6,4),HorizontalContentAlignment=HorizontalAlignment.Left };
        button.Click+=(_,_)=>NavigateRequested?.Invoke(offset);return button;
    }
    private static Brush Brush(string color)=>(Brush)new BrushConverter().ConvertFromString(color)!;
}
