using System.Text.Json.Serialization;
namespace CodeMarket.IDE.Models;
public sealed class BridgeResponse
{
    [JsonPropertyName("ok")] public bool Ok { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("session")] public string? Session { get; set; }
    [JsonPropertyName("world")] public int World { get; set; }
    [JsonPropertyName("language")] public string? Language { get; set; }
    [JsonPropertyName("files")] public string[]? Files { get; set; }
    [JsonPropertyName("folders")] public string[]? Folders { get; set; }
    [JsonPropertyName("path")] public string? Path { get; set; }
    [JsonPropertyName("text")] public string? Text { get; set; }
    [JsonPropertyName("from")] public int From { get; set; }
    [JsonPropertyName("items")] public string[]? Items { get; set; }
    [JsonPropertyName("referenceItems")] public string[]? ReferenceItems { get; set; }
    [JsonPropertyName("referenceTitle")] public string? ReferenceTitle { get; set; }
    [JsonPropertyName("referenceSubtitle")] public string? ReferenceSubtitle { get; set; }
    [JsonPropertyName("referenceText")] public string? ReferenceText { get; set; }
    [JsonPropertyName("related")] public string[]? Related { get; set; }
}
public sealed class ReferenceItem
{
    [JsonPropertyName("section")] public string Section { get; set; } = "";
    [JsonPropertyName("note")] public string Note { get; set; } = "";
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("chip")] public string Chip { get; set; } = "";
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("kind")] public string Kind { get; set; } = "";
    [JsonPropertyName("highlight")] public string Highlight { get; set; } = "";
    [JsonPropertyName("unread")] public bool Unread { get; set; }
}
public sealed class CompletionItem
{
    [JsonPropertyName("label")] public string Label { get; set; } = "";
    [JsonPropertyName("insert")] public string Insert { get; set; } = "";
    [JsonPropertyName("detail")] public string Detail { get; set; } = "";
    [JsonPropertyName("doc")] public string Doc { get; set; } = "";
    [JsonPropertyName("caret")] public int Caret { get; set; } = -1;
}
