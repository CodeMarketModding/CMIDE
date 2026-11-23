using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CodeMarket.IDE.Views;

public sealed class FileNameDialog : Window
{
    private readonly TextBox input;
    public string FileName => input.Text.Trim();
    public FileNameDialog(Window owner, string title, string initial, string location)
    {
        Owner=owner;Title=title;Width=430;Height=185;MinWidth=350;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Background=(Brush)new BrushConverter().ConvertFromString("#252526")!;
        Foreground=Brushes.White;ShowInTaskbar=false;
        var panel=new StackPanel { Margin=new Thickness(16) };Content=panel;
        panel.Children.Add(new TextBlock { Text=location, Foreground=(Brush)new BrushConverter().ConvertFromString("#BBBBBB")!, Margin=new Thickness(0,0,0,7), TextTrimming=TextTrimming.CharacterEllipsis });
        input=new TextBox { Text=initial, MaxLength=32, Background=(Brush)new BrushConverter().ConvertFromString("#3C3C3C")!, Foreground=Brushes.White, CaretBrush=Brushes.White, Padding=new Thickness(7), BorderBrush=(Brush)new BrushConverter().ConvertFromString("#007ACC")! };
        panel.Children.Add(input);
        var buttons=new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Right, Margin=new Thickness(0,12,0,0) };panel.Children.Add(buttons);
        var cancel=new Button { Content="Cancel", IsCancel=true, MinWidth=75, Margin=new Thickness(0,0,8,0) };
        var ok=new Button { Content="OK", IsDefault=true, MinWidth=75 };ok.Click+=(_,_)=> { if(!string.IsNullOrWhiteSpace(input.Text)) DialogResult=true; };
        buttons.Children.Add(cancel);buttons.Children.Add(ok);
        Loaded+=(_,_)=> { input.Focus();input.SelectAll(); };
    }
}
