using Sandbox;

namespace PaintballBaddies;

/// <summary>Opt-in physical cover checks; exercises the same public controller used by input.</summary>
public sealed class CoverChecks : Component
{
    [Property] public string CandidateModelPath { get; set; } = "";
    private PlayerController player;
    private CoverController cover;
    private PaintballMarker weapon;
    private GameObject fixture;
    private GameObject obstruction;
    private float elapsed;
    private int phase;
    private Vector3 start;
    private void Check( string label, bool pass, string detail ) => Log.Info( $"PB_COVER {(pass ? "PASS" : "FAIL")} {label}: {detail}" );
    protected override void OnStart()
    {
        player = Components.Get<PlayerController>(); cover = Components.Get<CoverController>(); weapon = Components.Get<PaintballMarker>();
        player.UseInputControls = false; player.UseLookControls = false; weapon.AcceptInput = false;
        cover.TestInput = true;
        Components.Get<RosterSelection>().Select( 2 );
        if(!string.IsNullOrEmpty(CandidateModelPath))
        {
            Components.Get<RosterSelection>().Select(CoveredRosterAssets.CharacterIndex(CandidateModelPath));
            player.Renderer.Model=Model.Load(CandidateModelPath);
        }
        WorldPosition = new Vector3( -595, 70, 2 ); player.EyeAngles = new Angles( 0, 0, 0 );
        fixture = TrainingRange.Box( null, "Cover check fixture", new Vector3( -550, 70, 24 ), new Vector3( 20, 180, 48 ), Color.Gray, true );
        fixture.Components.Create<CoverSurface>();
    }
    protected override void OnFixedUpdate()
    {
        elapsed += Time.Delta;
        if ( phase == 0 && elapsed > 1 )
        {
            Check( "entry", cover.TryEnter(), $"position={WorldPosition}" ); phase++;
        }
        else if ( phase == 1 && elapsed > 2.5f )
        {
            Check( "physical attachment", cover.Attached && WorldPosition.x < -576 && WorldPosition.x > -585, $"position={WorldPosition}" );
            Check( "low cover posture", player.IsDucking && player.CurrentHeight < 45, $"height={player.CurrentHeight}" );
            Check( "protected fire blocked", !weapon.FireAt( new Vector3( 100, 70, 55 ) ), $"ammo={weapon.Ammo}" );
            start = WorldPosition; cover.TestMovement = Vector3.Right; phase++;
        }
        else if ( phase == 2 && elapsed > 3.2f )
        {
            Check( "slide along face", (WorldPosition-start).WithZ( 0 ).Length > 15 && cover.Attached, $"start={start} position={WorldPosition}" );
            Check( "directional cover animation", player.Renderer.Sequence.Name == "crouch_right", player.Renderer.Sequence.Name );
            cover.TestMovement = Vector3.Zero; cover.TestAim = true; phase++;
        }
        else if ( phase == 3 && elapsed > 4 )
        {
            Check( "low cover peek exposes body", cover.Peeking && !player.IsDucking && player.CurrentHeight > 60, $"height={player.CurrentHeight}" );
            cover.TestAim = false; phase++;
        }
        else if ( phase == 4 && elapsed > 4.7f )
        {
            Check( "release returns to protection", !cover.Peeking && player.IsDucking, $"height={player.CurrentHeight}" );
            cover.TestMovement = cover.Normal; phase++;
        }
        else if ( phase == 5 && elapsed > 5.3f )
        {
            Check( "movement cancels cover", !cover.Attached && player.DuckedHeight == 46, $"attached={cover.Attached} duckHeight={player.DuckedHeight}" );
            player.EyeAngles = new Angles( 0, 180, 0 );
            Check( "reject empty space", !cover.TryEnter(), $"attached={cover.Attached}" );
            fixture.Destroy();
            fixture = TrainingRange.Box( null, "High cover check fixture", new Vector3( -550, 70, 45 ), new Vector3( 20, 180, 90 ), Color.Gray, true );
            fixture.Components.Create<CoverSurface>();
            WorldPosition = new Vector3( -595, 70, 2 ); player.Body.Velocity = Vector3.Zero;
            player.EyeAngles = new Angles( 0, 0, 0 ); cover.TestMovement = Vector3.Zero; phase++;
        }
        else if ( phase == 6 && elapsed > 6 )
        {
            Check( "high cover entry", cover.TryEnter() && !cover.LowCover, $"position={WorldPosition}" );
            cover.TestMovement = Vector3.Right; phase++;
        }
        else if ( phase == 7 && elapsed > 8.5f )
        {
            Check( "edge stops protected slide", cover.Attached && WorldPosition.y > -5 && WorldPosition.y < 8 && cover.CornerAvailable, $"position={WorldPosition} corner={cover.CornerAvailable}" );
            start = WorldPosition; cover.TestMovement = Vector3.Zero; cover.TestAim = true; phase++;
        }
        else if ( phase == 8 && elapsed > 9.5f )
        {
            Check( "corner physically exposes body", cover.Peeking && WorldPosition.y < start.y - 38, $"start={start} position={WorldPosition}" );
            cover.TestAim = false; phase++;
        }
        else if ( phase == 9 && elapsed > 10.5f )
        {
            Check( "corner release returns behind cover", cover.Attached && !cover.Peeking && (WorldPosition-start).WithZ(0).Length < 2, $"position={WorldPosition}" );
            obstruction = TrainingRange.Box( null, "Blocked corner fixture", new Vector3( -580, -35, 35 ), new Vector3( 28, 25, 70 ), Color.Gray, true );
            phase++;
        }
        else if ( phase == 10 && elapsed > 11 )
        {
            cover.TestAim = true; phase++;
        }
        else if ( phase == 11 && elapsed > 12 )
        {
            Check( "blocked corner rejects peek", !cover.Peeking && !cover.CornerAvailable && WorldPosition.y > -5, $"position={WorldPosition} corner={cover.CornerAvailable}" );
            Check( "blocked corner cannot fire", !weapon.FireAt( new Vector3( 100, 0, 55 ) ), $"ammo={weapon.Ammo}" );
            obstruction.Destroy();
            cover.Leave(); fixture.Destroy(); cover.TestInput = false; player.UseInputControls = true; player.UseLookControls = true; weapon.AcceptInput = true;
            Components.Get<RosterSelection>().Select( 0 );
            Log.Info( "PB_COVER COMPLETE" ); Destroy();
        }
    }
}
