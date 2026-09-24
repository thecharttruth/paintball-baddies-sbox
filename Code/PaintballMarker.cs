using System;
using Sandbox;

namespace PaintballBaddies;

/// <summary>Finite-ammo training marker with swept, gravity-driven paintballs.</summary>
public sealed class PaintballMarker : Component
{
    public const float ReloadDuration = 1.65f;
    // Source barrel end is -Y 0.434m with its centre at Z 0.09519m.
    public static readonly Vector3 MuzzleOffset = new(17.1f,0,3.75f);
    public int Ammo { get; private set; } = 40;
    public int Reserve { get; private set; } = 160;
    public bool UnlimitedAmmo => Components.Get<ArenaMatch>()?.TimedContest == true;
    public int ActivePaintballs => balls.Count;
    public int Shots { get; private set; }
    public int Hits { get; private set; }
    public int Score => Hits * 100;
    public float ReloadRemaining { get; private set; }
    public float ActiveReloadDuration { get; private set; } = ReloadDuration;
    public float ReloadProgress => 1 - (ReloadRemaining / ActiveReloadDuration).Clamp( 0, 1 );
    public float HitFlash { get; private set; }
    public float EliminationFlash { get; private set; }
    public bool Aiming { get; private set; }
    public bool CoverFireRequested { get; private set; }
    private float pendingCoverShot;
    private CoverSurface pendingCoverSurface;
    public bool? ReviewAimOverride { get; set; }
    public bool LeftShoulder { get; private set; }
    public bool AcceptInput { get; set; } = true;
    public Vector3 Muzzle { get; private set; }
    // Optional renderer-owned pose; firing, collision and ammunition remain owned here.
    public GameObject PresentationAnchor { get; set; }
    private PlayerController player;
    private GameObject marker;
    private float cooldown;
    private int networkSequence;
    internal void NetworkHit()
    {
        Hits++;HitFlash=.15f;Sound.Play("sounds/paintball/hit_confirm.sound",WorldPosition);
    }
    private float renderClock, simulationClock, physicsStep=.02f;
    private float coveredAimPitch;
    private ReloadPresentationTrack reloadPresentation;
    private string presentationModel;
    private readonly List<Ball> balls = new();
    private sealed class Ball { public GameObject Visual,Bead; public Vector3 Velocity, Position, Previous, Origin; public float Life; public bool VisualOnly; public Color Tint; public readonly HashSet<ArenaOpponent> Alerted=new(); }

    protected override void OnStart()
    {
        player = Components.Get<PlayerController>();
        marker = new GameObject( GameObject, true, "VX-9 training marker" ){NetworkMode=NetworkMode.Never};
        var renderer = marker.Components.Create<ModelRenderer>();
        renderer.Model = Model.Load( "models/weapons/vx9_full_frame/vx9_full_frame.vmdl" );
    }

    protected override void OnUpdate()
    {
        if ( player is null ) return;
        if(!IsProxy&&Components.Get<CloseCameraVisibility>() is null)Components.Create<CloseCameraVisibility>();
        renderClock+=Time.Delta;
        var markerVisual = marker?.Components.Get<ModelRenderer>();
        if (markerVisual.IsValid()) markerVisual.Enabled = !PresentationAnchor.IsValid();
        cooldown = MathF.Max( 0, cooldown - Time.Delta );
        HitFlash = MathF.Max( 0, HitFlash - Time.Delta );
        EliminationFlash = MathF.Max( 0, EliminationFlash - Time.Delta );
        if ( ReloadRemaining > 0 )
        {
            ReloadRemaining = MathF.Max( 0, ReloadRemaining - Time.Delta );
            if ( ReloadRemaining == 0 )
            {
                int count = Math.Min( 40 - Ammo, Reserve );
                Ammo += count;
                Reserve -= count;
                Sound.Play("sounds/paintball/reload_ready.sound",WorldPosition);
            }
        }
        var cover = Components.Get<CoverController>();
        var contact = Components.Get<CloseCombat>();
        bool primary = false;
        bool canAim = !RoundEnded && contact?.Incapacitated!=true && contact?.Attacking!=true
            && Components.Get<VaultController>()?.IsVaulting!=true && cover?.Sliding!=true;
        bool focus = ReviewAimOverride ?? (AcceptInput && !IsProxy && Input.Down("attack2"));
        if (!IsProxy)
        {
            // A covered attack always means firing; nearby bodies must not steal
            // the click and turn it into a strike that leaves cover.
            primary = contact.IsValid()
                ? contact.ObservePrimaryInput(Input.Pressed("attack1"),Input.Down("attack1"),Input.Down("attack2") || cover?.Attached==true,Time.Delta)
                : Input.Down("attack1");
            pendingCoverShot = MathF.Max(0,pendingCoverShot-Time.Delta);
            if (!AcceptInput || !canAim || cover?.Attached!=true || pendingCoverSurface!=cover.ReservedSurface)
                pendingCoverShot=0;
            if (AcceptInput && canAim && cover?.Attached==true && primary && Input.Pressed("attack1"))
            {
                // Preserve one quick click while the native pose/edge movement
                // exposes the barrel. Never carry it into another cover object.
                pendingCoverShot=1.1f;
                pendingCoverSurface=cover.ReservedSurface;
            }
        }
        CoverFireRequested = !IsProxy && AcceptInput && canAim && cover?.Attached==true && (primary || pendingCoverShot>0);
        Aiming = IsProxy ? Components.Get<NetworkPawn>()?.Aiming==true : canAim && (focus || CoverFireRequested);
        var pitchTarget = RoundEnded || ReloadRemaining > 0 || Components.Get<VaultController>()?.IsVaulting == true
            ? 0 : player.EyeAngles.pitch;
        coveredAimPitch += (pitchTarget.Clamp( -45, 45 ) - coveredAimPitch) * (1 - MathF.Exp( -12 * Time.Delta ));
        if ( AcceptInput && !IsProxy && Input.Pressed( "shoulder" ) ) LeftShoulder = !LeftShoulder;
        var shoulder = LeftShoulder ? -1f : 1f;
        var peekCamera=Components.Get<CoverController>()?.PeekCameraSide ?? 0;
        if(Aiming && peekCamera!=0)shoulder=peekCamera;
        var cameraTarget = focus && canAim ? new Vector3( 55, 22 * shoulder, 8 ) : new Vector3( 98, 28 * shoulder, 8 );
        player.CameraOffset = Vector3.Lerp( player.CameraOffset, cameraTarget, 1 - MathF.Exp( -16 * Time.Delta ) );
        var rotation = player.EyeAngles.ToRotation();
        var grip = player.EyePosition + rotation.Forward * 12 + rotation.Right * 9 - Vector3.Up * 16;
        if ( player.Renderer.TryGetBoneTransform( "RightHand", out var hand ) )
        {
            grip = hand.Position;
            if ( CoveredRosterAssets.TryMarkerOffset( player.Renderer.Model?.Name, out var offset ) )
                grip += player.Renderer.WorldRotation * offset;
        }
        marker.WorldPosition = grip;
        marker.WorldRotation = rotation;
        Muzzle = grip + rotation * MuzzleOffset;
        UpdateCoveredMarkerPose( false );
        if ( AcceptInput && !IsProxy && Input.Pressed( "reload" ) ) Reload();
        if(!IsProxy)
        {
            if(AcceptInput && (primary || pendingCoverShot>0) && Fire()) pendingCoverShot=0;
        }
        if ( AcceptInput && !IsProxy && Input.Pressed( "use" ) && Components.Get<ArenaMatch>()?.InRound != true ) ResetTraining();
    }

    private bool RoundEnded => Components.Get<ArenaMatch>() is { InRound: false } match && match.Status == "TIME UP";

    public event Action ReloadStarted;
    public void Reload()
    {
        if (UnlimitedAmmo || RoundEnded || Components.Get<VaultController>()?.IsVaulting == true) return;
        if ( ReloadRemaining <= 0 && Ammo < 40 && Reserve > 0 )
        {
            ActiveReloadDuration = CoveredRosterAssets.ReloadTrack( player?.Renderer?.Model?.Name ) is not null ? 2.4f : ReloadDuration;
            ReloadRemaining = ActiveReloadDuration;
            ReloadStarted?.Invoke();
            Sound.Play("sounds/paintball/reload_start.sound",WorldPosition);
        }
    }

    // Ammunition transfers only on completion, so cancelling preserves both counts.
    public event Action ReloadCancelled;
    public void CancelReload()
    {
        if (ReloadRemaining <= 0) return;
        ReloadRemaining = 0;
        ReloadCancelled?.Invoke();
    }

    protected override void OnPreRender()
    {
        foreach(var ball in balls)
            if(ball.Visual.IsValid())
            {
                ball.Visual.WorldPosition=Vector3.Lerp(ball.Previous,ball.Position,((renderClock-simulationClock)/physicsStep).Clamp(0,1));
                PaintballFlight.UpdateBead(ball.Bead);
            }
        UpdateReloadPresentation();
        UpdateCoveredMarkerPose( true );
        CoveredSuitMotion.Apply( player?.Renderer );
        PoseApplied?.Invoke(player?.Renderer);
    }
    public event System.Action<SkinnedModelRenderer> PoseApplied;

    private void UpdateReloadPresentation()
    {
        if(PresentationAnchor.IsValid())
        {
            if(reloadPresentation.IsValid()) { reloadPresentation.Destroy(); reloadPresentation=null; }
            return;
        }
        if ( player?.Renderer is not { } body || !marker.IsValid() ) return;
        var modelName = body.Model?.Name;
        var trackPath = CoveredRosterAssets.ReloadTrack( modelName );
        var weaponModel = trackPath is null ? "models/weapons/vx9_full_frame/vx9_full_frame.vmdl"
            : "models/weapons/vx9_speed_feed/vx9_speed_feed.vmdl";
        var visual = marker.Components.Get<ModelRenderer>();
        // Scene teardown/hotload can remove the renderer before its owning object.
        if (!visual.IsValid()) return;
        if ( visual.Model?.Name != weaponModel ) visual.Model = Model.Load( weaponModel );
        var playing = trackPath is not null && ReloadRemaining > 0 && Components.Get<VaultController>()?.IsVaulting != true;
        if ( reloadPresentation.IsValid() && (trackPath is null || presentationModel != modelName) )
        {
            reloadPresentation.Destroy(); reloadPresentation = null;
        }
        if ( trackPath is not null )
        {
            if ( !reloadPresentation.IsValid() )
            {
                reloadPresentation = GameObject.Components.Create<ReloadPresentationTrack>();
                reloadPresentation.Body = body; reloadPresentation.TrackPath = trackPath;
                presentationModel = modelName;
            }
            reloadPresentation.Phase = ReloadProgress;
            reloadPresentation.Docked = !playing;
            reloadPresentation.BodyOffset = Vector3.Zero;
            if(playing && body.Sequence.Name!="reload_standing" && CoveredRosterAssets.TryMarkerOffset(modelName,out var reloadOffset)
                && ReloadUpperBodyData.Apply(body,ReloadProgress,reloadOffset,out _,out var translation,false))
                reloadPresentation.BodyOffset=translation;
            reloadPresentation.ApplyFrame();
        }
    }

    private void UpdateCoveredMarkerPose( bool writeBones )
    {
        if(PresentationAnchor.IsValid() && marker.IsValid())
        {
            marker.WorldPosition=PresentationAnchor.WorldPosition;
            marker.WorldRotation=PresentationAnchor.WorldRotation;
            Muzzle=PresentationAnchor.WorldTransform.PointToWorld(MuzzleOffset);
            return;
        }
        if ( player?.Renderer is not { } body || !marker.IsValid()
            || !CoveredRosterAssets.TryMarkerOffset( body.Model?.Name, out var offset ) ) return;
        var pitch = ReloadRemaining > 0 || Components.Get<VaultController>()?.IsVaulting == true ? 0 : coveredAimPitch;
        if(ReloadRemaining>0 && body.Sequence.Name=="reload_standing")
            offset=ReloadUpperBodyData.MarkerOffset(body.Model?.Name,offset,ReloadProgress);
        if(ReloadRemaining>0 && Components.Get<VaultController>()?.IsVaulting!=true && body.Sequence.Name!="reload_standing"
            && ReloadUpperBodyData.Apply(body,ReloadProgress,offset,out var reloadPose,out _,writeBones))
        {
            marker.WorldPosition=reloadPose.Position;marker.WorldRotation=reloadPose.Rotation;
            Muzzle=reloadPose.Position+reloadPose.Rotation*MuzzleOffset;
            return;
        }
        if ( CoveredAimPose.Apply( body, pitch, offset, out var pose, writeBones ) )
        {
            marker.WorldPosition = pose.Position;
            marker.WorldRotation = pose.Rotation;
            Muzzle = pose.Position + pose.Rotation * MuzzleOffset;
        }
    }

    public bool Fire()
    {
        if ( cooldown > 0 || ReloadRemaining > 0 || (!UnlimitedAmmo && Ammo <= 0) || balls.Count >= 64 || Scene.Camera is null ) return false;
        var camera = Scene.Camera;
        var sight = PaintballFlight.Trace(Scene,camera.WorldPosition,camera.WorldPosition+camera.WorldRotation.Forward*(PaintballFlight.Speed*PaintballFlight.Lifetime),GameObject);
        return FireAt( sight.EndPosition );
    }

    // Shared with native integration checks; target selection is separate from projectile physics.
    public bool FireAt( Vector3 aimPoint )
    {
        if(Components.Get<CloseCombat>() is { } contact && (contact.Incapacitated || contact.Attacking))return false;
        if (RoundEnded || Components.Get<CoverController>()?.Sliding==true) return false;
        if ( Components.Get<VaultController>()?.IsVaulting == true ) return false;
        var cover = Components.Get<CoverController>();
        if ( cover?.Attached == true && !cover.Peeking ) return false;
        if ( cooldown > 0 || ReloadRemaining > 0 || (!UnlimitedAmmo && Ammo <= 0) || balls.Count >= 64 ) return false;
        UpdateCoveredMarkerPose( false );
        if (cover?.Attached==true)
        {
            // Peeking starts before the rifle has necessarily finished rising.
            // Keep the click queued until both physical barrel segments clear.
            if (PaintballFlight.Trace(Scene,player.EyePosition,Muzzle,GameObject).Hit) return false;
            var direction=(aimPoint-Muzzle).Normal;
            var lane=PaintballFlight.Trace(Scene,Muzzle,Muzzle+direction*96,GameObject);
            if (lane.Hit && lane.GameObject==cover.ReservedSurface?.GameObject) return false;
        }
        if(!UnlimitedAmmo)Ammo--;
        Shots++; cooldown = PaintballFlight.ShotInterval;
        Sound.Play(CharacterMarkerSound.For(GameObject),Muzzle);
        if(MultiplayerSession.Online)
        {
            Components.Get<NetworkPawn>()?.RequestShot(Muzzle,aimPoint,++networkSequence,cover?.Attached==true && cover.Peeking);
            if(!MultiplayerSession.Authority)CreateLocalBall(aimPoint,true);
            return true;
        }
        ArenaOpponent.ReportMarkerSound(Scene,GameObject,Muzzle);
        // A barrel clipping through cover must not let a shot emerge on its far side.
        var obstruction = PaintballFlight.Trace(Scene,player.EyePosition,Muzzle,GameObject);
        var tint=CharacterPaintColor.For(GameObject);
        if ( obstruction.Hit ) { Impact( obstruction,tint ); return true; }
        CreateLocalBall(aimPoint,false);
        return true;
    }
    private void CreateLocalBall(Vector3 aimPoint,bool visualOnly)
    {
        if(visualOnly && PaintballFlight.Trace(Scene,player.EyePosition,Muzzle,GameObject).Hit)return;
        var tint=CharacterPaintColor.For(GameObject);
        var visual = new GameObject(false,"Paintball"){NetworkMode=NetworkMode.Never};
        visual.Tags.Add("paintball_debris");visual.WorldPosition=Muzzle;
        var bead=PaintballFlight.CreateBead(visual,tint);visual.Enabled=true;
        balls.Add(new Ball {Visual=visual,Bead=bead,Tint=tint,VisualOnly=visualOnly,Position=Muzzle,Previous=Muzzle,Origin=Muzzle,
            Velocity=PaintballFlight.LaunchVelocity(Muzzle,aimPoint),Life=PaintballFlight.Lifetime});
    }

    protected override void OnFixedUpdate()
    {
        simulationClock+=Time.Delta;physicsStep=System.MathF.Max(Time.Delta,.001f);
        for ( int i = balls.Count - 1; i >= 0; i-- )
        {
            var b = balls[i];
            var start = b.Position;
            b.Previous=start;
            var end = start + b.Velocity * Time.Delta + Vector3.Down * (PaintballFlight.Gravity * .5f * Time.Delta * Time.Delta);
            var trace = PaintballFlight.Trace(Scene,start,end,GameObject);
            if(!b.VisualOnly)IncomingPaintAwareness.Report(Scene,GameObject,b.Origin,start,trace.Hit ? trace.EndPosition : end,trace.GameObject,b.Alerted);
            b.Life -= Time.Delta;
            if ( trace.Hit || b.Life <= 0 )
            {
                if ( trace.Hit && !b.VisualOnly ) Impact( trace,b.Tint,b.Origin );
                b.Visual.Destroy(); balls.RemoveAt( i ); continue;
            }
            b.Position = end;
            b.Velocity += Vector3.Down * PaintballFlight.Gravity * Time.Delta;
        }
    }

    public string LastImpactObject { get; private set; }
    private void Impact( SceneTraceResult trace,Color tint,Vector3? launchOrigin=null )
    {
        LastImpactObject=trace.GameObject?.Name;
        var source=launchOrigin ?? Muzzle;
        if(ArenaShield.TryAbsorbImpact(trace,source,tint))return;
        var target = trace.GameObject?.Components.Get<PaintballTarget>();
        if ( target is not null ) { target.Hit(); Hits++; HitFlash = 0.15f; }
        var combatant = PaintballCombatant.Find( trace.GameObject );
        if ( combatant?.RegisterHit( 0, source, Components.Get<PaintballCombatant>() ) == true )
        {
            Hits++; HitFlash = .15f;
            if(combatant.Eliminated) EliminationFlash = .4f;
            Sound.Play("sounds/paintball/hit_confirm.sound", WorldPosition);
        }
        PaintImpactSystem.Find(Scene)?.Spawn(trace,tint);
    }

    public void EndRound()
    {
        pendingCoverShot=0;CoverFireRequested=false;
        AcceptInput = false;
        CancelReload();
        Aiming = false;
        foreach (var ball in balls) if (ball.Visual.IsValid()) ball.Visual.Destroy();
        balls.Clear();
        if (reloadPresentation.IsValid()) reloadPresentation.Destroy();
    }

    public void ResetTraining()
    {
        pendingCoverShot=0;CoverFireRequested=false;
        CancelReload();
        Ammo = 40; Reserve = 160; Shots = 0; Hits = 0; cooldown = 0;
        HitFlash = EliminationFlash = 0;
        foreach ( var b in balls ) b.Visual.Destroy();
        balls.Clear();
        PaintImpactSystem.Find(Scene)?.Clear();
    }
}
