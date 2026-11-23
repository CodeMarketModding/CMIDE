using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CodeMarket.IDE.Views;

/// <summary>Renders the game's compact reference-map notation as a coordinate grid.</summary>
internal static class ReferenceMap
{
    private sealed record Tile(string Color, string English, string Russian, string? Icon = null);
    private static readonly Dictionary<char, Tile> Tiles = new()
    {
        ['.'] = new("#3A4552", "floor", "пол"),
        ['T'] = new("#B5503E", "the tomato table", "стол с помидорами", "table"),
        ['S'] = new("#59636F", "the register: its screen", "касса: экран", "register"),
        ['B'] = new("#B98A55", "the register: its bag", "касса: пакет"),
        ['R'] = new("#4C6EF5", "the robot: stands here, facing the register", "робот: стоит тут, лицом к кассе", "down"),
        ['p'] = new("#7E9BF2", "here the robot packs the bag", "здесь робот кладёт в пакет", "down"),
        ['g'] = new("#8E99A6", "at the garage door: takes from the truck", "у ворот гаража: берёт из грузовика", "up"),
        ['c'] = new("#D9709A", "the customer", "покупатель")
    };

    public static FrameworkElement Create(string specification, bool russian)
    {
        string[] rows = specification.Split('/');
        int width = rows.Max(row => row.Length);
        var result = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Left };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        for (int x = 0; x < width; x++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });
        for (int y = 0; y < rows.Length; y++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(55) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });

        for (int y = 0; y < rows.Length; y++)
        {
            Add(grid, Coordinate((rows.Length - 1 - y).ToString()), y, 0);
            for (int x = 0; x < width; x++)
            {
                char symbol = x < rows[y].Length ? rows[y][x] : '.';
                Tile tile = Tiles.GetValueOrDefault(symbol, Tiles['.']);
                var box = new Border { Width = 50, Height = 50, CornerRadius = new CornerRadius(7), Background = Brush(tile.Color),
                    HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, ToolTip = russian ? tile.Russian : tile.English };
                if (tile.Icon is not null) box.Child = Icon(tile.Icon);
                Add(grid, box, y, x + 1);
            }
        }
        for (int x = 0; x < width; x++) Add(grid, Coordinate(x.ToString()), rows.Length, x + 1);
        result.Children.Add(new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Left, Child = grid });

        var legend = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        foreach (char symbol in rows.SelectMany(row => row).Distinct())
        {
            if (symbol == '.' || !Tiles.TryGetValue(symbol, out var tile)) continue;
            var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 14, 7) };
            item.Children.Add(new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(3), Background = Brush(tile.Color), Margin = new Thickness(0, 1, 7, 0), VerticalAlignment = VerticalAlignment.Top });
            item.Children.Add(new TextBlock { Text = russian ? tile.Russian : tile.English, Foreground = Brush("#D3DCE6"), FontSize = 12 });
            legend.Children.Add(item);
        }
        result.Children.Add(legend);
        return result;
    }

    private static TextBlock Coordinate(string text) => new() { Text = text, Foreground = Brush("#8494A5"), FontWeight = FontWeights.Bold,
        FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private static void Add(Grid grid, UIElement child, int row, int column) { Grid.SetRow(child, row); Grid.SetColumn(child, column); grid.Children.Add(child); }
    private static Brush Brush(string color) => (Brush)new BrushConverter().ConvertFromString(color)!;

    // The game generates 3-D item pictures at runtime. These small vector icons keep
    // the map readable without requiring an additional game bridge or asset export.
    private static Canvas Icon(string icon)
    {
        var canvas = new Canvas { Width = 50, Height = 50, IsHitTestVisible = false };
        void Shape(string geometry, string color) => canvas.Children.Add(new Path { Data = Geometry.Parse(geometry), Fill = Brush(color) });
        if (icon is "up" or "down")
        {
            Shape(icon == "up" ? "M25,11 L13,23 L20,23 L20,36 L30,36 L30,23 L37,23 Z" : "M20,14 L30,14 L30,27 L37,27 L25,39 L13,27 L20,27 Z", "#FFFFFF");
        }
        else if (icon == "table")
        {
            Shape("M11,25 L28,19 L40,25 L23,32 Z", "#D39756");
            Shape("M11,25 L23,32 L23,39 L11,32 Z", "#754325");
            Shape("M23,32 L40,25 L40,32 L23,39 Z", "#9C5A2C");
            Shape("M14,32 L17,33 L17,41 L14,40 Z M34,33 L37,32 L37,39 L34,40 Z", "#593722");
            foreach (var (x,y) in new[] { (17,23), (24,20), (30,23), (22,25), (29,27), (35,24) })
            {
                var tomato = new Ellipse { Width = 7, Height = 6, Fill = Brush("#EF4930") }; Canvas.SetLeft(tomato,x); Canvas.SetTop(tomato,y); canvas.Children.Add(tomato);
                Shape($"M{x+2},{y+1} l2,-3 l2,3 Z", "#4D963D");
            }
        }
        else if (icon == "register")
        {
            Shape("M6,28 L26,21 L43,28 L23,36 Z", "#DA9D58");
            Shape("M6,28 L23,36 L23,40 L6,32 Z", "#A46B36");
            Shape("M23,36 L43,28 L43,32 L23,40 Z", "#85512E");
            Shape("M13,18 L24,15 L29,24 L17,27 Z", "#202B35");
            Shape("M15,19 L23,17 L26,22 L18,24 Z", "#94C5CC");
            Shape("M30,22 L36,20 L39,25 L33,27 Z", "#F2D296");
        }
        return canvas;
    }
}
