using Sandbox;

namespace PaintballBaddies;

public sealed class VaultChecks : Component
{
    private PlayerController player;
    private GameObject obstacle, obstruction;
    private float elapsed;
    private int phase;
    private void CreateObstacle( float depth, float height )
    {
        obstacle?.Destroy();
        obstacle = TrainingRange.Box( null, "Vault check barrier", new Vector3( -550, 70, height / 2 ), new Vector3( depth, 180, height ), Color.Gray, true );
        obstacle.Components.Create<CoverSurface>();
    }
    protected override void OnStart()
    {
        player = Components.Get<PlayerController>(); player.UseInputControls = player.UseLookControls = false;
        Components.Get<PaintballMarker>().AcceptInput = false;
        WorldPosition = new Vector3( -595, 70, 2 ); player.EyeAngles = new Angles( 0, 0, 0 );
        CreateObstacle( 20, 48 );
    }
    protected override void OnUpdate()
    {
        elapsed += Time.Delta;
        if ( elapsed < phase + 1 ) return;
        bool possible = VaultPlanner.TryPlan( player, out var plan, out var reason );
        bool pass = phase == 0 ? possible && plan.Depth > 19 && plan.Depth < 21 : !possible;
        var name = new[] { "clear low barrier", "reject tall barrier", "reject deep barrier", "reject blocked headroom", "reject blocked landing" }[phase];
        Log.Info( $"VAULT_PLAN {(pass ? "PASS" : "FAIL")} {name}: {reason} depth={plan.Depth}" );
        if ( phase == 0 ) CreateObstacle( 20, 90 );
        if ( phase == 1 ) CreateObstacle( 80, 48 );
        if ( phase == 2 )
        {
            CreateObstacle( 20, 48 );
            obstruction = TrainingRange.Box( null, "Vault check ceiling", new Vector3( -550, 70, 100 ), new Vector3( 150, 200, 10 ), Color.Gray, true );
        }
        if ( phase == 3 )
        {
            obstruction.Destroy();
            obstruction = TrainingRange.Box( null, "Vault landing blocker", new Vector3( -516, 70, 30 ), new Vector3( 25, 30, 60 ), Color.Gray, true );
        }
        phase++;
        if ( phase < 5 ) return;
        obstacle.Destroy(); obstruction.Destroy(); player.UseInputControls = player.UseLookControls = true;
        Components.Get<PaintballMarker>().AcceptInput = true; Log.Info( "VAULT_PLAN COMPLETE" ); Destroy();
    }
}
