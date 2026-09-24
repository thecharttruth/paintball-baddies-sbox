using Sandbox;

namespace PaintballBaddies;

/// <summary>Opt-in native integration run, disabled in the saved player scene.</summary>
public sealed class TrainingChecks : Component
{
    private PlayerController player;
    private PaintballMarker weapon;
    private float elapsed;
    private int phase;
    private Vector3 start;
    private GameObject ceiling;
    private Vector3? previousFoot;
    private float footTravel;
    private float maxSequenceTime;
    private float maxRunTime;
    private Vector3? secondaryStart;
    private float secondaryTravel;
    protected override void OnStart()
    {
        player = Components.Get<PlayerController>();
        weapon = Components.Get<PaintballMarker>();
        weapon.AcceptInput = false;
        player.UseInputControls = false;
        player.UseLookControls = false;
        player.EyeAngles = new Angles( 0, 0, 0 );
    }

    private void Check( string name, bool pass, string detail ) => Log.Info( $"PB_CHECK {(pass ? "PASS" : "FAIL")} {name}: {detail}" );

    protected override void OnFixedUpdate()
    {
        elapsed += Time.Delta;
        if ( phase == 0 && elapsed > 1 )
        {
            var edge = Scene.Trace.Ray( new Vector3(-440,16,55), new Vector3(-410,16,55) ).Run();
            Check( "shaped target edge is solid", edge.Hit && edge.GameObject.Components.Get<PaintballTarget>() is not null, $"hit={edge.Hit}" );
            var corner = Scene.Trace.Ray( new Vector3(-440,16,78), new Vector3(-410,16,78) ).Run();
            Check( "chamfered target corner stays clear", !corner.Hit, $"hit={corner.Hit}" );
            weapon.FireAt( new Vector3( -420, 0, 55 ) );
            phase++;
        }
        else if ( phase == 1 && elapsed > 1.7f )
        {
            Check( "target hit", weapon.Ammo == 39 && weapon.Shots == 1 && weapon.Hits == 1, $"ammo={weapon.Ammo} shots={weapon.Shots} hits={weapon.Hits}" );
            Check( "scored shot leaves paint decal", PaintImpactSystem.Find(Scene)?.ActiveCount == 1, $"marks={PaintImpactSystem.Find(Scene)?.ActiveCount}" );
            weapon.Reload();
            Check( "reload blocks fire", !weapon.FireAt( new Vector3( -420, 0, 55 ) ), $"remaining={weapon.ReloadRemaining}" );
            phase++;
        }
        else if ( phase == 2 && elapsed > 3.6f && weapon.ReloadRemaining == 0 )
        {
            Check( "reload conservation", weapon.Ammo == 40 && weapon.Reserve == 159, $"ammo={weapon.Ammo} reserve={weapon.Reserve}" );
            elapsed = 3.6f;
            GameObject.WorldPosition = new Vector3( -630, 65, 4 );
            player.Body.Velocity = Vector3.Zero;
            start = GameObject.WorldPosition;
            phase++;
        }
        else if ( phase == 3 )
        {
            player.WishVelocity = Vector3.Forward * player.WalkSpeed;
            if ( player.Renderer.TryGetBoneTransform( "LeftFoot", out var foot ) )
            {
                var relative = foot.Position - player.WorldPosition;
                if ( previousFoot.HasValue ) footTravel += (relative - previousFoot.Value).Length;
                previousFoot = relative;
            }
            maxSequenceTime = System.MathF.Max( maxSequenceTime, player.Renderer.Sequence.Time );
            if ( elapsed > 3.9f && player.Renderer.TryGetBoneTransform( "SuitSecondary_L", out var secondary )
                && player.Renderer.TryGetBoneTransform( "Spine01", out var chest ) )
            {
                var local = chest.Rotation.Inverse * (secondary.Position - chest.Position);
                secondaryStart ??= local;
                secondaryTravel = System.MathF.Max( secondaryTravel, (local - secondaryStart.Value).Length );
            }
            else if ( elapsed > 3.9f && player.Renderer.Model.Morphs.GetIndex( "SuitFollowThroughLUp" ) >= 0 )
            {
                var value = player.Renderer.Morphs.Get( "SuitFollowThroughLUp" ) - player.Renderer.Morphs.Get( "SuitFollowThroughLDown" );
                var local = Vector3.Up * value * (.0025f / .0254f);
                secondaryStart ??= local;
                secondaryTravel = System.MathF.Max( secondaryTravel, (local - secondaryStart.Value).Length );
            }
            if ( elapsed > 4.6f )
            {
                Check( "walk movement", GameObject.WorldPosition.x > start.x + 55, $"start={start} current={GameObject.WorldPosition}" );
                Check( "walk animation", player.Renderer.Sequence.Name == "walk", player.Renderer.Sequence.Name );
                Check( "walk animation advances", maxSequenceTime > 0.15f, $"max sequence time={maxSequenceTime}" );
                Check( "walk feet move relative to body", footTravel > 8, $"foot travel={footTravel}" );
                var footsteps=Components.Get<MovementFootsteps>();
                Check("walking emits bounded footsteps",footsteps?.StepsPlayed is >= 1 and <= 5,$"steps={footsteps?.StepsPlayed}");
                Check( "minute secondary motion", secondaryStart.HasValue && secondaryTravel > 0.002f && secondaryTravel < 0.25f,
                    $"secondary excursion={secondaryTravel} inches (bone or authored morph range)" );
                phase++;
            }
        }
        else if ( phase == 4 )
        {
            player.WishVelocity = Vector3.Forward * player.RunSpeed;
            if ( player.Renderer.Sequence.Name == "run" )
                maxRunTime = System.MathF.Max( maxRunTime, player.Renderer.Sequence.Time );
            if ( elapsed > 6.3f )
            {
                Check( "run animation advances", maxRunTime > 0.15f, $"max run time={maxRunTime}" );
                Check( "cover collision", GameObject.WorldPosition.x < -295 && GameObject.WorldPosition.x > -360, $"position={GameObject.WorldPosition}" );
                player.WishVelocity = Vector3.Zero;
                GameObject.WorldPosition = new Vector3( -630, 65, 4 );
                player.Body.Velocity = Vector3.Zero;
                player.UpdateDucking( true );
                phase++;
            }
        }
        else if ( phase == 5 && elapsed > 7 )
        {
            Check( "crouch collider", player.CurrentHeight < 50, $"height={player.CurrentHeight}" );
            Check( "crouch animation", player.Renderer.Sequence.Name == "crouch", player.Renderer.Sequence.Name );
            ceiling = TrainingRange.Box( null, "Temporary clearance test", new Vector3( -630, 65, 55 ), new Vector3( 100, 100, 10 ), Color.White, true );
            phase++;
        }
        else if ( phase == 6 && elapsed > 7.6f )
        {
            player.UpdateDucking( false );
            phase++;
        }
        else if ( phase == 7 && elapsed > 8.1f )
        {
            Check( "standing clearance", player.CurrentHeight < 50, $"height under ceiling={player.CurrentHeight}" );
            ceiling.Destroy();
            phase++;
        }
        else if ( phase == 8 && elapsed > 8.5f )
        {
            player.UpdateDucking( false );
            phase++;
        }
        else if ( phase == 9 && elapsed > 9.2f )
        {
            Check( "stand after clearance", player.CurrentHeight > 60, $"height={player.CurrentHeight}" );
            weapon.ResetTraining();
            GameObject.WorldPosition = new Vector3( -630, 0, 4 );
            phase++;
        }
        else if ( phase == 10 && elapsed > 9.7f )
        {
            ceiling = TrainingRange.Box( null, "Temporary barrel obstruction", (player.EyePosition + weapon.Muzzle) * 0.5f,
                new Vector3( 8, 40, 80 ), Color.White, true );
            phase++;
        }
        else if ( phase == 11 && elapsed > 10 )
        {
            weapon.FireAt( new Vector3( -420, 0, 55 ) );
            phase++;
        }
        else if ( phase == 12 && elapsed > 10.5f )
        {
            Check( "barrel obstruction", weapon.Shots == 1 && weapon.Hits == 0, $"shots={weapon.Shots} hits={weapon.Hits}" );
            ceiling.Destroy();
            ceiling = TrainingRange.Box( null, "Temporary downrange obstruction", new Vector3( -500, 0, 55 ),
                new Vector3( 4, 100, 100 ), Color.White, true );
            phase++;
        }
        else if ( phase == 13 && elapsed > 11 )
        {
            weapon.FireAt( new Vector3( -420, 0, 55 ) );
            phase++;
        }
        else if ( phase == 14 && elapsed > 11.5f )
        {
            Check( "projectile cover obstruction", weapon.Shots == 2 && weapon.Hits == 0, $"shots={weapon.Shots} hits={weapon.Hits}" );
            ceiling.Destroy();
            weapon.ResetTraining();
            phase++;
        }
        else if ( phase == 15 )
        {
            weapon.FireAt( new Vector3( -420, 0, 55 ) );
            if ( elapsed > 17 )
            {
                Check( "empty magazine", weapon.Ammo == 0 && weapon.Shots == 40 && !weapon.FireAt( Vector3.Zero ), $"ammo={weapon.Ammo} shots={weapon.Shots}" );
                weapon.Reload();
                phase++;
            }
        }
        else if ( phase == 16 && elapsed > 19 && weapon.ReloadRemaining == 0 )
        {
            Check( "full reload", weapon.Ammo == 40 && weapon.Reserve == 120, $"ammo={weapon.Ammo} reserve={weapon.Reserve}" );
            weapon.ResetTraining();
            weapon.AcceptInput = true;
            player.UseInputControls = true;
            player.UseLookControls = true;
            GameObject.WorldPosition = new Vector3( -630, 0, 4 );
            Log.Info( "PB_CHECK COMPLETE" );
            Enabled = false;
        }
    }
}
