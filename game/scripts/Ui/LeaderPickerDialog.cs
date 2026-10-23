using Godot;
using Gwota.Core.Cards;
using Gwota.Core.Presentation;

namespace Gwota.Game.Ui;

/// <summary>
/// Chooses a deck's leader among its faction's leaders, shown as cards with their ability underneath; the current one
/// is highlighted. Clicking a leader picks it; Escape or Cancel keeps the current one.
/// </summary>
public sealed partial class LeaderPickerDialog : Modal
{
    private static readonly Vector2 CardSize = new(170, 241);
    private const int MaxColumns = 4;
    private const int Gap = 22;

    private LeaderPickerDialog(IReadOnlyList<LeaderDefinition> leaders, string currentId, Action<string> onChosen)
        : base(Width(leaders.Count), ModalStyle.Game)
    {
        Content.AddChild(Title("Choose a leader", 26));

        var grid = new GridContainer { Columns = Math.Clamp(leaders.Count, 1, MaxColumns), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        grid.AddThemeConstantOverride("h_separation", Gap);
        grid.AddThemeConstantOverride("v_separation", Gap);
        foreach (var leader in leaders)
        {
            grid.AddChild(Cell(leader, leader.Id == currentId, () =>
            {
                QueueFree();
                onChosen(leader.Id);
            }));
        }

        Content.AddChild(grid);
        Content.AddChild(MenuButton("Cancel", Cancel));
    }

    public static void Show(Node parent, IReadOnlyList<LeaderDefinition> leaders, string currentId, Action<string> onChosen)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(leaders);
        ArgumentNullException.ThrowIfNull(onChosen);
        parent.AddChild(new LeaderPickerDialog(leaders, currentId, onChosen));
    }

    protected override void Cancel() => QueueFree();

    private static int Width(int count) =>
        (int)((Math.Clamp(count, 1, MaxColumns) * (CardSize.X + Gap)) - Gap);

    private static VBoxContainer Cell(LeaderDefinition leader, bool isCurrent, Action onPicked)
    {
        var view = new LeaderView(leader, CardSize) { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        view.SetSelected(isCurrent);
        view.Clicked += _ => onPicked();

        var ability = Widgets.Label(CardText.Leader(leader), 16, isCurrent ? Palette.Highlight : Palette.Text, HorizontalAlignment.Center);
        ability.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        ability.CustomMinimumSize = new Vector2(CardSize.X, 0);

        var cell = new VBoxContainer();
        cell.AddThemeConstantOverride("separation", 8);
        cell.AddChild(view);
        cell.AddChild(ability);
        if (isCurrent)
        {
            cell.AddChild(Widgets.Label("Current", 15, Palette.Highlight, HorizontalAlignment.Center));
        }

        return cell;
    }
}