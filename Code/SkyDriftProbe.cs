using Sandbox;
using System;

/// <summary>Opt-in live-scene sky rotation check; removes itself after sampling.</summary>
public sealed class SkyDriftProbe : Component
{
    private float elapsed;
    private Rotation initial;
    protected override void OnStart() { initial = WorldRotation; }
    protected override void OnUpdate()
    {
        elapsed += Time.Delta;
        if ( elapsed < 3 ) return;
        var delta = Rotation.Difference( initial, WorldRotation ).Angle();
        Log.Info( $"PB_SKY_DRIFT elapsed={elapsed:F3} rotation_delta={delta:F3}" );
        Destroy();
    }
}
