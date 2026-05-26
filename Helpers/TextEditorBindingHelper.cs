using Avalonia;
using AvaloniaEdit;

namespace Kapture.Helpers;

/// <summary>
/// Attached property to enable data binding on AvaloniaEdit's TextEditor,
/// which doesn't expose Text as a bindable AvaloniaProperty.
/// </summary>
public class TextEditorBindingHelper : AvaloniaObject
{
    public static readonly AttachedProperty<string?> BoundTextProperty =
        AvaloniaProperty.RegisterAttached<TextEditorBindingHelper, TextEditor, string?>("BoundText");

    static TextEditorBindingHelper()
    {
        BoundTextProperty.Changed.AddClassHandler<TextEditor>((editor, e) =>
        {
            var text = (e.NewValue as string) ?? string.Empty;
            if (editor.Document.Text != text)
            {
                editor.Document.Text = text;
                editor.InvalidateVisual();
                editor.InvalidateMeasure();
                editor.InvalidateArrange();
            }
        });
    }

    public static void SetBoundText(TextEditor editor, string? value) => editor.SetValue(BoundTextProperty, value);
    public static string? GetBoundText(TextEditor editor) => editor.GetValue(BoundTextProperty);
}
