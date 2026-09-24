using Sandbox;

namespace PaintballBaddies;

/// <summary>Opt-in native navigation smoke test before enabling live opponents.</summary>
public sealed class OpponentNavigationChecks : Component
{
    [Property] public Vector3 StartPosition { get; set; } = new Vector3( 550, 420, 1 );
    private ArenaOpponent opponent;
    private float elapsed;
    protected override void OnStart()
    {
        Scene.NavMesh.IsEnabled = true;
        Scene.NavMesh.IncludeStaticBodies = true;
        Scene.NavMesh.IncludeKeyframedBodies = false;
        Scene.NavMesh.AgentHeight = 67;
        Scene.NavMesh.AgentRadius = 17;
        Scene.NavMesh.SetDirty();
        var go = new GameObject( true, "Navigation test opponent" );
        go.WorldPosition = StartPosition;
        opponent = go.Components.Create<ArenaOpponent>();
    }
    protected override void OnUpdate()
    {
        elapsed += Time.Delta;
        if ( elapsed < 18 ) return;
        Log.Info( $"OPPONENT_NAV {(opponent.DistanceTravelled > 100 && opponent.ObstructedMoves == 0 ? "PASS" : "FAIL")} travelled={opponent.DistanceTravelled} obstructionCrossings={opponent.ObstructedMoves} position={opponent.WorldPosition} generating={Scene.NavMesh.IsGenerating}" );
        Destroy();
    }
}
