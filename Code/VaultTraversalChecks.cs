using Sandbox;

namespace PaintballBaddies;

public sealed class VaultTraversalChecks : Component
{
    public string TestModelPath { get; set; } = "";
    private PlayerController player;
    private VaultController vault;
    private GameObject obstacle;
    private float elapsed;
    private int phase;
    private void Check( string name, bool pass, string detail ) => Log.Info( $"VAULT_MOVE {(pass ? "PASS" : "FAIL")} {name}: {detail}" );
    protected override void OnStart()
    {
        player = Components.Get<PlayerController>(); vault = Components.Get<VaultController>();
        player.UseInputControls = player.UseLookControls = player.UseCameraControls = false; Components.Get<PaintballMarker>().AcceptInput = false;
        Components.Get<CoverController>().TestInput = true;
        WorldPosition = new Vector3( -595, 70, 2 ); player.EyeAngles = new Angles( 0, 0, 0 );
        obstacle = TrainingRange.Box( null, "Vault traversal fixture", new Vector3( -550, 70, 24 ), new Vector3( 20, 180, 48 ), Color.Gray, true );
        obstacle.Components.Create<CoverSurface>();
    }
    protected override void OnUpdate()
    {
        elapsed += Time.Delta;
        var camera = Scene.GetAllComponents<CameraComponent>().First();
        camera.WorldPosition = new Vector3( -550, -190, 110 );
        camera.WorldRotation = Rotation.LookAt( new Vector3( -550, 70, 45 ) - camera.WorldPosition );
        if ( phase == 0 && elapsed > 1 )
        {
            if ( !string.IsNullOrEmpty( TestModelPath ) )
            {
                var character = CoveredRosterAssets.CharacterIndex( TestModelPath );
                if ( character >= 0 ) Components.Get<RosterSelection>().Select( character );
                player.Renderer.Model = Model.Load( TestModelPath );
                Check( "protected model loaded", player.Renderer.Model.IsValid(), player.Renderer.Model.Name );
            }
            Check( "rejects vault outside cover", !vault.TryBegin(), "near a valid obstacle but unattached" );
            var weapon = Components.Get<PaintballMarker>();
            var fired = weapon.FireAt(new Vector3(-400,70,65));
            weapon.Reload(); weapon.ReviewAimOverride = true;
            Check("reload prepared before vault", fired && weapon.ReloadRemaining > 0 && weapon.Ammo == 39, "one paintball spent, reload active");
            Check( "attaches to low cover", Components.Get<CoverController>().TryEnter(), "physical fixture" );
            phase++;
        }
        else if ( phase == 1 && elapsed > 1.3f )
        {
            Check( "shows available vault prompt", vault.Available && vault.Hint == "V VAULT", vault.Hint );
            var cover = Components.Get<CoverController>();
            cover.Leave();
            Check( "clears prompt immediately after leaving", !vault.Available && vault.Hint == "", "same frame as Leave" );
            Check( "rejects stale route after leaving", !vault.TryBegin(), "no intervening route refresh" );
            Check( "reattaches before traversal", cover.TryEnter(), "valid low cover" );
            Check( "starts valid vault", vault.TryBegin(), vault.Outcome );
            var weapon = Components.Get<PaintballMarker>();
            Check("vault cancels reload without transferring paint", weapon.ReloadRemaining == 0 && weapon.Ammo == 39 && weapon.Reserve == 160, $"ammo={weapon.Ammo} reserve={weapon.Reserve}");
            Check( "shows traversal state", vault.Hint == "VAULTING", vault.Hint );
            Check("vault suppresses cover entry and prompt",!cover.CanEnter && cover.Hint=="" && !cover.TryEnter(),"no attachment during kinematic traversal");
            phase++;
        }
        else if ( phase == 2 && elapsed > 1.75f )
        {
            Check( "body crosses above barrier", vault.IsVaulting && WorldPosition.z > 48 && WorldPosition.x > -590, $"position={WorldPosition}" );
            Check( "vault animation active", player.Renderer.Sequence.Name == "vault" && player.Renderer.Sequence.Time > .1f, $"clip={player.Renderer.Sequence.Name} time={player.Renderer.Sequence.Time}" );
            Check( "firing blocked during vault", !Components.Get<PaintballMarker>().FireAt( new Vector3( 100, 70, 55 ) ), "physical traversal" );
            var weapon = Components.Get<PaintballMarker>();
            weapon.Reload();
            Check("vault blocks reload and aim", weapon.ReloadRemaining == 0 && !weapon.Aiming && weapon.Ammo == 39 && weapon.Reserve == 160, "held aim and reload request during traversal");
            if ( !string.IsNullOrEmpty( TestModelPath ) )
            {
                var sampled = CoveredVaultGroundProfiles.TrySample( player.Renderer.Model.Name, ((TestModelPath is "models/characters/mei_planted_review/mei_protected.vmdl" or "models/characters/mei_protected_morph/mei_protected.vmdl") ? player.Renderer.Sequence.TimeNormalized : player.Renderer.Sequence.Time / .8f), out var offset );
                var actual = vault.VisualBasePosition.z - player.Renderer.LocalPosition.z;
                Check( "protected grounding applied", sampled && System.MathF.Abs( actual - offset ) < .05f, $"model={player.Renderer.Model.Name} actual={actual} expected={offset}" );
                Check( "integrated helmet used", Components.Get<RosterHelmet>() is { Equipped: false }, "legacy helmet and neck seal removed" );
            }
            phase++;
        }
        else if ( phase == 3 && elapsed > 2.6f )
        {
            Check( "lands on far side", vault.Completed == 1 && !vault.IsVaulting && WorldPosition.x > -520 && WorldPosition.z < 2, $"position={WorldPosition} outcome={vault.Outcome}" );
            Check( "physics and input restored", player.Body.MotionEnabled && !player.UseInputControls, $"motion={player.Body.MotionEnabled} input={player.UseInputControls}" );
            Check( "visual offset restored", player.Renderer.LocalPosition == vault.VisualBasePosition, $"offset={player.Renderer.LocalPosition}" );
            var weapon = Components.Get<PaintballMarker>();
            weapon.Reload();
            Check("reload and held aim resume after landing", weapon.ReloadRemaining > 0 && weapon.Aiming && weapon.Ammo == 39 && weapon.Reserve == 160, "fresh reload request accepted");
            weapon.ReviewAimOverride = null;
            obstacle.Destroy(); Components.Get<CoverController>().TestInput = false;
            player.UseInputControls = player.UseLookControls = player.UseCameraControls = true; Components.Get<PaintballMarker>().AcceptInput = true;
            Log.Info( "VAULT_MOVE COMPLETE" ); Destroy();
        }
    }
    protected override void OnPreRender()
    {
        if ( TestModelPath != "models/characters/mei_planted_review/mei_protected.vmdl" || elapsed < 1.95f || elapsed > 2.45f ) return;
        var body = player.Renderer;
        foreach ( var name in new[] { "Hips", "Head", "LeftFoot", "RightFoot" } )
        {
            if ( !body.TryGetBoneTransform( name, out var bone ) ) continue;
            var p = bone.Position - player.WorldPosition;
            Log.Info( $"VAULT_LANDING_POSE t={elapsed:F6} dt={Time.Delta:F6} clip={body.Sequence.Name} bone={name} x={p.x:F6} y={p.y:F6} z={p.z:F6} clip_time={body.Sequence.Time:F6} normalized={body.Sequence.TimeNormalized:F6}" );
        }
    }

}
