using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace CodeMarket.IDE.Views;

internal sealed class ReferenceHoverRenderer : IBackgroundRenderer
{
    private static readonly Brush Highlight = new SolidColorBrush(Color.FromArgb(75, 68, 155, 227));
    private static readonly Pen Underline = new(new SolidColorBrush(Color.FromRgb(113, 190, 255)), 1);
    private readonly TextView view;
    private TextSegment? segment;
    public ReferenceHoverRenderer(TextView view) => this.view=view;
    public KnownLayer Layer => KnownLayer.Selection;
    public void Set(int start,int length)
    {
        if(segment is not null && segment.StartOffset==start && segment.Length==length)return;
        segment=new TextSegment { StartOffset=start, Length=length };view.InvalidateLayer(Layer);
    }
    public void Clear() { if(segment is null)return;segment=null;view.InvalidateLayer(Layer); }
    public void Draw(TextView textView,DrawingContext context)
    {
        if(segment is null || !textView.VisualLinesValid || segment.EndOffset>textView.Document.TextLength)return;
        foreach(Rect rect in BackgroundGeometryBuilder.GetRectsForSegment(textView,segment,false))
        {
            context.DrawRoundedRectangle(Highlight,null,rect,2,2);
            context.DrawLine(Underline,new Point(rect.Left,rect.Bottom-1),new Point(rect.Right,rect.Bottom-1));
        }
    }
}
