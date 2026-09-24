using Sandbox;

namespace PaintballBaddies;

/// <summary>One paint colour per competitor, shared by flight and every impact.</summary>
public static class CharacterPaintColor
{
    public static Color For(int index)=>index switch
    {
        1=>new Color(1,.36f,.06f),       // Roxie: orange
        2=>new Color(.05f,.95f,.8f),    // Imani: turquoise
        3=>new Color(1,.09f,.16f),      // Mei: red
        4=>new Color(.58f,.95f,.12f),   // Freya: lime
        5=>new Color(.12f,.48f,1),      // Leilani: blue
        _=>new Color(.72f,.23f,1)       // Viper: violet
    };

    public static Color For(GameObject shooter)
    {
        if(!shooter.IsValid())return For(2);
        var roster=shooter.Components.Get<RosterSelection>();
        return For(roster?.Selected ?? shooter.Components.Get<ArenaOpponent>()?.CharacterIndex ?? 0);
    }
}
