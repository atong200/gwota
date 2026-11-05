using Godot;

namespace Gwota.Game.Ui;

/// <summary>Previous and next arrows around a page label, such as "2/5".</summary>
public sealed partial class PagerBar : HBoxContainer
{
    private readonly Label _label;
    private readonly Button _previous;
    private readonly Button _next;

    public PagerBar(Action onPrevious, Action onNext, float labelWidth, Color labelColor, float buttonSize = 38)
    {
        ArgumentNullException.ThrowIfNull(onPrevious);
        ArgumentNullException.ThrowIfNull(onNext);

        _previous = Widgets.IconButton(Icons.PreviousIcon, "Previous page", onPrevious, size: buttonSize);
        _next = Widgets.IconButton(Icons.NextIcon, "Next page", onNext, size: buttonSize);
        _label = Widgets.Label(string.Empty, 18, labelColor, HorizontalAlignment.Center);
        _label.CustomMinimumSize = new Vector2(labelWidth, 0);

        AddChild(_previous);
        AddChild(_label);
        AddChild(_next);
    }

    public void Show(string page, bool hasPrevious, bool hasNext)
    {
        _label.Text = page;
        _previous.Disabled = !hasPrevious;
        _next.Disabled = !hasNext;
    }
}
