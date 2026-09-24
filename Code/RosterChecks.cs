using Sandbox;

namespace PaintballBaddies;

public sealed class RosterChecks : Component
{
    private float elapsed;
    private int step = -1;
    private bool checkedStep;
    protected override void OnUpdate()
    {
        elapsed += Time.Delta;
        if ( elapsed < 2 ) return;
        var roster = Components.Get<RosterSelection>();
        var player = Components.Get<PlayerController>();
        if ( roster is null || player is null ) return;
        int next = (int)((elapsed - 2) / 2);
        if ( next >= RosterSelection.Names.Length )
        {
            roster.Select( 0 );
            Log.Info( "ROSTER_CHECK COMPLETE" );
            Destroy();
            return;
        }
        if ( step != next )
        {
            step = next;
            checkedStep = false;
            roster.Select( step );
        }
        if ( !checkedStep && (elapsed - 2) % 2 > 1 )
        {
            var renderer = player.Renderer;
            bool hand = renderer.TryGetBoneTransform( "RightHand", out var transform );
            bool pass = roster.Selected == step && renderer.Sequence.Name == "idle" && renderer.Sequence.Time > 0.1f
                && renderer.Model.Bounds.Size.z > 50 && renderer.Model.Bounds.Size.z < 85 && hand;
            Log.Info( $"ROSTER_CHECK {(pass ? "PASS" : "FAIL")} {roster.CharacterName}: height={renderer.Model.Bounds.Size.z} sequence={renderer.Sequence.Name} time={renderer.Sequence.Time} hand={hand}" );
            checkedStep = true;
        }
    }
}
