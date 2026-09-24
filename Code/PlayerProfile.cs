using Sandbox;
using System.Linq;

namespace PaintballBaddies;

/// <summary>A player's chosen name, independent of their selected Baddie.</summary>
public sealed class PlayerProfile : Component
{
    [Sync] public string DisplayName { get; set; } = "Player";
    public const int MaxNameLength = 24;
    public static string CleanName(string value)
    {
        var clean = new string((value ?? "").Where(c => char.IsLetterOrDigit(c) || c is ' ' or '_' or '-' or '.').Take(MaxNameLength).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(clean) ? "Player" : clean;
    }
    protected override void OnStart()
    {
        if (!IsProxy) DisplayName = CleanName(Game.Cookies.Get("paintball-player-name", "Player"));
    }
    public void SaveName(string value)
    {
        if (IsProxy) return;
        DisplayName = CleanName(value);
        Game.Cookies.Set("paintball-player-name", DisplayName);
    }
}
