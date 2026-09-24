using Sandbox;
using System;

namespace PaintballBaddies;

/// <summary>Map author settings. Reuses native SpawnPoint components for all six seats.</summary>
public sealed class ArenaMap : Component
{
    [Property] public string Title { get; set; } = "Community Arena";
    [Property] public bool ShowPracticeTargets { get; set; }

    public static ArenaMap Find(Scene scene) => scene.GetAllComponents<ArenaMap>().FirstOrDefault();
    public static string DisplayTitle(Scene scene) => Find(scene)?.Title ?? "Neon Foundry";

    // Use the same ordered six native markers for solo opponents and network seats.
    // Partial sets are rejected as a whole so a map never mixes two coordinate systems.
    public static SpawnPoint[] Spawns(Scene scene) => scene.GetAllComponents<SpawnPoint>()
        .Where(x => x.GameObject.Active).OrderBy(x => x.GameObject.Name, StringComparer.Ordinal).ToArray();

    public static Vector3 PlayerSpawn(Scene scene, int seat, Vector3 legacyPosition)
    {
        var points = Spawns(scene);
        return points.Length == 6 && seat >= 0 && seat < 6 ? points[seat].WorldPosition : legacyPosition;
    }

    public static ShieldSpawnPoint[] ShieldSpawns(Scene scene) => scene.GetAllComponents<ShieldSpawnPoint>()
        .Where(x => x.GameObject.Active).ToArray();

    protected override void OnStart()
    {
        if(Spawns(Scene).Length != 6)
            Log.Warning("Paintball map requires six native SpawnPoint objects named 01 through 06. Using legacy arena positions until corrected.");
    }
}

/// <summary>Place on walkable ground to choose a possible random shield pickup location.</summary>
public sealed class ShieldSpawnPoint : Component { }
