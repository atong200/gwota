using Godot;
using Gwota.Core.Cards;

namespace Gwota.Game.Ui;

/// <summary>Where a card view was drawn, and the strength it showed, when a command was applied.</summary>
public readonly record struct CardOrigin(Rect2 Rect, int? Strength);

/// <summary>
/// Card views of one size kept by card id across renders, so cards that did not change are not rebuilt. A render
/// calls <see cref="Begin"/>, takes the views it shows, places them, then calls <see cref="Sweep"/>.
/// </summary>
public sealed class CardViewCache(Vector2 size, Action<CardView> configure)
{
    private readonly Dictionary<int, CardView> _views = [];
    private readonly HashSet<int> _taken = [];

    public IEnumerable<CardView> Views => _views.Values;

    public void Begin() => _taken.Clear();

    /// <summary>The view of <paramref name="card"/>; rebuilt when the kept one shows another card or strength.</summary>
    public CardView Take(CardInstance card, int? strength = null)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (_views.TryGetValue(card.Id, out var kept))
        {
            if (GodotObject.IsInstanceValid(kept) && kept.Card == card && kept.CurrentStrength == strength)
            {
                _taken.Add(card.Id);
                return kept;
            }

            Free(kept);
        }

        var view = new CardView(card, size, strength);
        configure(view);
        _views[card.Id] = view;
        _taken.Add(card.Id);
        return view;
    }

    /// <summary>Frees the views not taken since <see cref="Begin"/>.</summary>
    public void Sweep()
    {
        foreach (var id in _views.Keys.Where(id => !_taken.Contains(id)).ToList())
        {
            Free(_views[id]);
            _views.Remove(id);
        }
    }

    /// <summary>The view showing card <paramref name="cardId"/>, when it is on screen.</summary>
    public bool TryGet(int cardId, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out CardView? view) =>
        _views.TryGetValue(cardId, out view) && GodotObject.IsInstanceValid(view) && view.IsInsideTree();

    private static void Free(CardView view)
    {
        if (GodotObject.IsInstanceValid(view))
        {
            view.GetParent()?.RemoveChild(view);
            view.QueueFree();
        }
    }
}
