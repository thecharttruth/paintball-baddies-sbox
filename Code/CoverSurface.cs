using Sandbox;

namespace PaintballBaddies;

/// <summary>Explicit opt-in for geometry usable by the cover controller.</summary>
public sealed class CoverSurface : Component
{
    [Property] public float ModelHeight { get; set; }
    [Property] public bool Curved { get; set; }
    // For chest-high machinery where the marker cannot safely fire over the top.
    [Property] public bool StandingOnly { get; set; }
    public float Top
    {
        get
        {
            var box = Components.Get<BoxCollider>();
            return WorldPosition.z + (box is not null ? box.Center.z + box.Scale.z * .5f : ModelHeight) * WorldScale.z;
        }
    }
}
