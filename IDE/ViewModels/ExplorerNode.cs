using System.Collections.ObjectModel;
namespace CodeMarket.IDE.ViewModels;
public sealed class ExplorerNode
{
    public string Name { get; init; } = "";
    public string? Path { get; init; }
    public string FolderPath { get; init; } = "";
    public bool IsSelected { get; set; }
    public bool IsFolder => Path is null;
    public string Icon => IsFolder ? "▾" : "C#";
    public ObservableCollection<ExplorerNode> Children { get; } = new();
    public static ObservableCollection<ExplorerNode> FromPaths(IEnumerable<string> paths, IEnumerable<string>? folders = null, string? selectFolder = null)
    {
        var roots = new ObservableCollection<ExplorerNode>();
        foreach (var entry in (folders ?? []).Select(p => (path:p, file:false)).Concat(paths.Select(p => (path:p, file:true))).OrderBy(x => x.path, StringComparer.OrdinalIgnoreCase))
        {
            var segments = entry.path.Split('/'); var children = roots;
            for (var i = 0; i < segments.Length; i++)
            {
                bool file = entry.file && i == segments.Length - 1;
                var node = children.FirstOrDefault(n => n.Name == segments[i] && n.IsFolder == !file);
                string folderPath=string.Join("/",segments.Take(file ? i : i+1));
                if (node is null) { node = new ExplorerNode { Name=segments[i], Path=file ? entry.path : null, FolderPath=folderPath, IsSelected=!file && folderPath==selectFolder }; children.Add(node); }
                children = node.Children;
            }
        }
        return roots;
    }
}
