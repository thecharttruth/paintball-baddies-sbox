using Sandbox;

/// <summary>Slow background cloud drift using the native skybox orientation.</summary>
public sealed class FoundrySkyDrift : Component
{
    [Property] public float DegreesPerSecond { get; set; } = 0.15f;
    protected override void OnUpdate()
    {
        WorldRotation = Rotation.FromYaw( DegreesPerSecond * Time.Delta ) * WorldRotation;
    }
}
