using Godot;

namespace Gwota.Game.Ui;

/// <summary>A panel whose tooltip is built by <see cref="TooltipContent"/> instead of plain text.</summary>
public sealed partial class RichTooltipPanel : PanelContainer
{
    public Func<Control>? TooltipContent { get; set; }

    public override GodotObject _MakeCustomTooltip(string forText) =>
        TooltipContent is { } make ? make() : base._MakeCustomTooltip(forText);
}
