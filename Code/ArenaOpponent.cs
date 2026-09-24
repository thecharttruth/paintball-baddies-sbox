using Sandbox;
using System;

namespace PaintballBaddies;

/// <summary>Navigation and perception layer for an arena opponent.</summary>
public sealed partial class ArenaOpponent : Component
{
    [Property] public string Character { get; set; } = "imani";
    [Property] public bool CombatEnabled { get; set; }
    public bool SeesPlayer { get; private set; }
    public float DistanceTravelled { get; private set; }
    public int ObstructedMoves { get; private set; }
    public Vector3 LastKnownPlayerPosition { get; private set; }
    private NavMeshAgent agent;
    private SkinnedModelRenderer body;
    public SkinnedModelRenderer Body => body;
    public int CharacterIndex { get; private set; }
    private PaintballCombatant target;
    public bool ContestMode { get; set; }
    private Vector3 previous;
    private float decisionTime;
    private float lostSight;
    private int patrol;
    private Vector3[] patrolPoints;
    private PaintballCombatant combatant;
    private GameObject marker;
    public GameObject PresentationAnchor { get; set; }
    private float visibleTime;
    private float coveredAimPitch;
    private ReloadPresentationTrack reloadPresentation;
    private string presentationModel;
    private int shots;
    private bool reloadAudioActive;
    public bool UsingCover { get; private set; }
    public bool CoverPeeking { get; private set; }
    public bool CoverIsLow { get; private set; } = true;
    public bool CoverCrouched => ReviewCrouch || (UsingCover && CoverIsLow);
    public int CoverBursts { get; private set; }
    public int ShotsFromCover { get; private set; }
    public int ShotsWhileSheltered { get; private set; }
    private bool holdingCover;
    private float heldCoverTime;
    public bool HasCoverPlan => seekingCover && combatant?.Eliminated != true;
    public Vector3 CoverDestination { get; private set; }
    public CoverSurface ReservedCover { get; private set; }
    public int InvalidatedCoverPlans { get; private set; }
    public int CoverSelections { get; private set; }
    public int SpacingMoves { get; private set; }
    public int FlankSelections { get; private set; }
    public bool Flanking => flankRemaining>0;
    private Vector3 flankDestination;
    private float flankRemaining,flankCooldown;
    private bool seekingCover,hasSeenPlayer;
    private float coverTime,coverRest,cooldownCover;
    private int observedHits;
    private int reactedHits;
    private float evadeAfterHit;
    public int HitCoverReactions { get; private set; }
    public bool EvadingAfterHit => evadeAfterHit>0;
    private float impactLookTime;
    private Vector3 impactLookDirection;
    private float eliminatedTime;
    private bool hasOutSignal;
    private string outSignalClip="out_signal";
    internal bool ReviewCrouch { get; set; }
    public bool ShowingOutSignal => combatant?.Eliminated == true && hasOutSignal && eliminatedTime < 1.5f;
    public int ShotsFired => shots;
    public int ShotsAtPlayers { get; private set; }
    public string ShotBlockReason { get; private set; }
    public float MovementSpeed { get; private set; }
    public Vector3 NavigationVelocity => IsProxy && Components.Get<NetworkBot>() is {} net ? net.Velocity : agent.IsValid() && agent.UpdatePosition
        ? agent.Velocity : Vector3.Zero;
    public Vector3 NavigationWishVelocity => IsProxy && Components.Get<NetworkBot>() is {} net ? net.Velocity : agent.IsValid() && agent.UpdatePosition
        ? agent.WishVelocity : Vector3.Zero;
    private float locomotionSpeed;
    public float MovementFacingDot { get; private set; } = 1;
    public float MinimumShotAlignment { get; private set; } = 1;
    public OpponentMagazine Magazine { get; } = new();
    // Search both flanks and ends of the expanded arena without reading an unseen target.
    internal static Vector3[] CreatePatrolRoute() => new Vector3[] {
        new(-900,-540,0), new(-450,-520,0), new(450,-520,0), new(900,-540,0),
        new(900,540,0), new(650,1100,0), new(650,1400,0),
        new(-650,1400,0), new(-650,1100,0),
        new(450,520,0), new(-450,520,0), new(-900,540,0)
    };

    protected override void OnStart()
    {
        target = Scene.GetAllComponents<PlayerController>().FirstOrDefault()?.Components.Get<PaintballCombatant>();
        agent = Components.Get<NavMeshAgent>() ?? Components.Create<NavMeshAgent>();
        agent.Height = 67;
        agent.Radius = 17;
        agent.MaxSpeed = 166;
        agent.Acceleration = 600;
        agent.UpdatePosition = !IsProxy;
        agent.UpdateRotation = false;
        body = Components.Create<SkinnedModelRenderer>();
        CharacterIndex = Array.FindIndex(RosterSelection.Names, name => string.Equals(name, Character, StringComparison.OrdinalIgnoreCase));
        if (CharacterIndex < 0) CharacterIndex = 2;
        body.Model = Model.Load( RosterSelection.GetModelPath(CharacterIndex) );
        hasOutSignal = body.Model.AnimationNames.Contains( "out_signal" );
        Components.Create<RosterHelmet>();
        Components.Create<MovementFootsteps>();
        body.UseAnimGraph = false;
        body.Sequence.Name = "idle";
        body.Sequence.Looping = true;
        body.Sequence.Blending = true;
        previous = WorldPosition;
        if ( CombatEnabled )
        {
            combatant = Components.Get<PaintballCombatant>() ?? Components.Create<PaintballCombatant>();
            if(MultiplayerSession.Authority)
            {
                combatant.Team = MultiplayerSession.Online ? CharacterIndex+10 : ContestMode ? CharacterIndex+1 : 1;
                combatant.StayInMatch=ContestMode;
            }
            Magazine.UnlimitedAmmo=ContestMode || MultiplayerSession.Online;
            var collider = Components.Create<BoxCollider>();
            collider.Scale = new Vector3( 28, 28, 66 );
            collider.Center = new Vector3( 0, 0, 33 );
            collider.Static = false;
            marker = new GameObject( GameObject, true, "Opponent VX-9" ){NetworkMode=NetworkMode.Never};
            marker.Components.Create<ModelRenderer>().Model = Model.Load( "models/weapons/vx9_full_frame/vx9_full_frame.vmdl" );
        }
        patrolPoints = CreatePatrolRoute();
        patrol = (CharacterIndex * 3) % patrolPoints.Length;
        LastKnownPlayerPosition = patrolPoints[patrol];
        decisionTime=CharacterIndex*BotCombatTuning.DecisionSeconds/6;
    }

    protected override void OnUpdate()
    {
        if(IsProxy)return;
        // Review/practice actors may acquire combat after their presentation starts.
        if(!combatant.IsValid())combatant=Components.Get<PaintballCombatant>();
        // Match setup may create the player's combatant after this component starts.
        if(!target.IsValid())
            target=Scene.GetAllComponents<PlayerController>().FirstOrDefault()?.Components.Get<PaintballCombatant>();
        if ( !target.IsValid() || !agent.IsValid() || !body.IsValid() ) return;
        if(Components.Get<CloseCombat>() is {} contact && (contact.Incapacitated || contact.Attacking))
        {
            agent.Stop();MovementSpeed=0;previous=WorldPosition;return;
        }
        var markerVisual=marker?.Components.Get<ModelRenderer>();
        if(markerVisual.IsValid()) markerVisual.Enabled=!PresentationAnchor.IsValid();
        Magazine.ReloadDuration = CoveredRosterAssets.ReloadTrack( body.Model?.Name ) is not null ? 2.4f : 2.2f;
        if (combatant?.Eliminated != true && Magazine.ReloadRemaining > 0 && !reloadAudioActive)
        {
            Sound.Play("sounds/paintball/reload_start.sound",WorldPosition);
            reloadAudioActive=true;
        }
        if (combatant?.Eliminated == true)
        {
            Magazine.CancelReload();
            reloadAudioActive = false;
        }
        else Magazine.Advance( Time.Delta );
        if (reloadAudioActive && Magazine.ReloadRemaining <= 0)
        {
            if(combatant?.Eliminated != true) Sound.Play("sounds/paintball/reload_ready.sound",WorldPosition);
            reloadAudioActive=false;
        }
        if(agent.IsValid() && agent.MaxSpeed>0)agent.MaxSpeed=166;
        cooldownCover=MathF.Max(0,cooldownCover-Time.Delta);
        flankRemaining=MathF.Max(0,flankRemaining-Time.Delta);flankCooldown=MathF.Max(0,flankCooldown-Time.Delta);
        impactLookTime=MathF.Max(0,impactLookTime-Time.Delta);
        evadeAfterHit=MathF.Max(0,evadeAfterHit-Time.Delta);
        AdvanceCoverAwareness(Time.Delta);
        targetCommitment=MathF.Max(0,targetCommitment-Time.Delta);
        timeSinceVisual+=Time.Delta;
        shotLaneRetry=MathF.Max(0,shotLaneRetry-Time.Delta);
        AdvanceNavigationRecovery(Time.Delta);
        if(combatant is not null && combatant.PaintHits>reactedHits)
        {
            reactedHits=combatant.PaintHits;
            // React to the shot's observed origin immediately, including a hit
            // from behind. Do not wait for the next sight-cone decision.
            if(combatant.LastAttacker.IsValid())ChangeTarget(combatant.LastAttacker);
            LastKnownPlayerPosition=combatant.LastHitOrigin.WithZ(WorldPosition.z);
            hasSeenPlayer=true;lostSight=0;decisionTime=0;cooldownCover=0;
            evadeAfterHit=3;flankRemaining=0;coverRest=0;impactLookTime=0;
            HitCoverReactions++;
            if(seekingCover && !OpponentCoverPlanner.IsSheltered(GameObject,target.GameObject,LastKnownPlayerPosition,CoverDestination))
                ReleaseTacticalCover();
            cooldownCover=0;
        }
        if(seekingCover)coverTime+=Time.Delta;
        if(holdingCover){coverRest+=Time.Delta;heldCoverTime+=Time.Delta;}
        if ( combatant?.Eliminated == true )
        {
            agent.Stop();
            MovementSpeed = 0;
            if(eliminatedTime==0 && body.Sequence.Name=="crouch" && body.Model.AnimationNames.Contains("out_signal_crouch"))outSignalClip="out_signal_crouch";
            eliminatedTime += Time.Delta;
            body.Enabled = ShowingOutSignal && !PresentationAnchor.IsValid();
            if ( ShowingOutSignal && !PresentationAnchor.IsValid() )
            {
                body.ClearPhysicsBones();
                if ( body.Sequence.Name != outSignalClip ) body.Sequence.Name = outSignalClip;
                body.Sequence.Blending = false;
                body.Sequence.Looping = false; body.PlaybackRate = 0;
                body.Sequence.TimeNormalized = (eliminatedTime / 1.5f).Clamp( 0, 1 );
            }
            if ( marker.IsValid() ) marker.Enabled = false;
            var eliminatedCollider = Components.Get<BoxCollider>();
            if ( eliminatedCollider.IsValid() ) eliminatedCollider.Enabled = false;
            return;
        }
        if (ReviewCrouch)
        {
            agent.Stop();
            // Citizen presentation reads CoverCrouched through its native graph.
            // Only sample the legacy renderer when it actually has this clip.
            if (!PresentationAnchor.IsValid() && body.Model.AnimationNames.Contains("crouch"))
            {
                body.ClearPhysicsBones();
                body.Sequence.Name="crouch";
                body.Sequence.Blending=false;
                body.PlaybackRate=0;
                body.Sequence.TimeNormalized=.5f;
            }
            return;
        }
        if ( !PresentationAnchor.IsValid() && marker is not null && body.TryGetBoneTransform( "RightHand", out var hand ) )
        {
            marker.WorldPosition = hand.Position;
            if ( CoveredRosterAssets.TryMarkerOffset( body.Model?.Name, out var offset ) )
                marker.WorldPosition += body.WorldRotation * offset;
            marker.WorldRotation = Rotation.LookAt( CombatAimPoint - marker.WorldPosition );
            var targetPitch = Magazine.ReloadRemaining > 0 ? 0 : marker.WorldRotation.Angles().pitch;
            coveredAimPitch += (targetPitch.Clamp( -45, 45 ) - coveredAimPitch) * (1 - MathF.Exp( -12 * Time.Delta ));
            UpdateCoveredMarkerPose( false );
        }
        var delta = WorldPosition - previous;
        if ( delta.Length > .1f )
        {
            var clearance = Scene.Trace.Sphere( 12, previous + Vector3.Up * 30, WorldPosition + Vector3.Up * 30 )
                .IgnoreGameObjectHierarchy( GameObject ).WithoutTags( "paintball_debris" ).Run();
            if ( clearance.Hit ) ObstructedMoves++;
        }
        DistanceTravelled += delta.Length;
        previous = WorldPosition;
        var speed = delta.WithZ( 0 ).Length / MathF.Max( Time.Delta, 0.001f );
        MovementSpeed = speed;
        // Forward locomotion follows the path; turn to engage once movement stops.
        var travel=NavigationVelocity.WithZ(0);
        var facing = travel.Length>20 ? travel : SeesPlayer ? (LastKnownPlayerPosition - WorldPosition).WithZ(0) : Vector3.Zero;
        // Citizen supports directional locomotion: back away or sidestep while
        // watching the recently observed threat, rather than turning our back
        // and immediately losing it from the sight cone.
        var rememberedThreat=(LastKnownPlayerPosition-WorldPosition).WithZ(0);
        // Citizen already supplies strafing and backwards locomotion. Keep a
        // visible opponent in front while crossing a firing lane; facing only
        // the path made us turn away and repeatedly reset visual acquisition.
        if(PresentationAnchor.IsValid() && CombatEnabled && SeesPlayer && rememberedThreat.Length<850
            && !RelocatingCover)
            facing=rememberedThreat;
        if(travel.Length<=20 && PresentationAnchor.IsValid() && CombatEnabled && hasSeenPlayer && lostSight<2
            && !seekingCover && rememberedThreat.Length<350)
            facing=rememberedThreat;
        if(holdingCover && travel.Length<=20 && rememberedThreat.Length>.5f)facing=rememberedThreat;
        if(speed<=5 && hasSeenPlayer && lostSight<2 && rememberedThreat.Length>.5f)facing=rememberedThreat;
        if(impactLookTime>0 && !SeesPlayer && speed<=5)facing=impactLookDirection;
        if ( facing.Length > .01f )
        {
            var yaw=WorldRotation.Angles().yaw;
            var turningWhileMoving=travel.Length>20;
            var step=MathF.IEEERemainder(Rotation.LookAt(facing).Angles().yaw-yaw,360)*(1-MathF.Exp(-(turningWhileMoving ? 25 : 12)*Time.Delta));
            var turnLimit=(turningWhileMoving ? 720 : 360)*Time.Delta;
            WorldRotation=Rotation.FromYaw(yaw+step.Clamp(-turnLimit,turnLimit));
        }
        MovementFacingDot = speed > 5 ? Vector3.Dot(WorldRotation.Forward,delta.WithZ(0).Normal) : 1;
        locomotionSpeed += (speed-locomotionSpeed)*(1-MathF.Exp(-12*Time.Delta));
        bool moving=locomotionSpeed>(body.Sequence.Name=="idle" ? 9 : 5);
        bool running=locomotionSpeed>(body.Sequence.Name=="run" ? 90 : 115);
        string clip = CoverCrouched ? "crouch" : running ? "run" : moving ? "walk" : "idle";
        // Authored reloads layer over idle as well as moving locomotion.
        if ( !UsingCover && !moving && Magazine.ReloadRemaining > 0 && CoveredRosterAssets.ReloadTrack( body.Model?.Name ) is null ) clip = "reload_standing";
        if(combatant is not null)
        {
            var shape=Components.Get<BoxCollider>();
            if(shape.IsValid())
            {
                shape.Scale=new Vector3(28,28,CoverCrouched ? 43 : 66);
                shape.Center=new Vector3(0,0,CoverCrouched ? 21.5f : 33);
            }
        }
        if (!PresentationAnchor.IsValid())
        {
            CoveredStrideTransition.SetSequence(body, clip);
            var strideScale=RosterSelection.GetMotionScale(CharacterIndex);
            body.PlaybackRate = clip == "run" ? (locomotionSpeed / (177*strideScale)).Clamp( .65f, 1.3f ) : clip == "walk" ? (locomotionSpeed / (85*strideScale)).Clamp( .6f, 1.5f ) : 1;
            body.Sequence.Looping = clip != "reload_standing";
            if ( clip == "reload_standing" )
            {
                body.PlaybackRate = 0;
                body.Sequence.TimeNormalized = 1 - Magazine.ReloadRemaining / Magazine.ActiveReloadDuration;
            }
        }
        visibleTime=SeesPlayer ? visibleTime+Time.Delta : 0;
        TryCombatShot();
        decisionTime -= Time.Delta;
        if ( decisionTime > 0 || Scene.NavMesh.IsGenerating ) return;
        decisionTime = BotCombatTuning.DecisionSeconds;
        if(ContestMode && !EvadingAfterHit && !RelocatingCover)SelectContestTarget();
        ObserveTarget();
        if(CombatEnabled && UpdateHeistObjective())return;
        if(CombatEnabled && UpdateCoverTactic())return;
        if(blockedLaneTime>1 && flankCooldown<=0 && !EvadingAfterHit && TryStartFlank())blockedLaneTime=0;
        if(EvadingAfterHit)
        {
            // No reachable shelter: keep moving across the attack direction
            // while retrying nearby cover, rather than freezing to look back.
            var away=(WorldPosition-LastKnownPlayerPosition).WithZ(0).Normal;
            var side=Vector3.Cross(Vector3.Up,away)*(CharacterIndex%2==0 ? 1 : -1);
            foreach(var direction in new[]{(side+away*.4f).Normal,(-side+away*.4f).Normal,away})
            {
                var point=Scene.NavMesh.GetClosestPoint(WorldPosition+direction*120,40);
                if(point is not Vector3 escape || (escape-WorldPosition).Length<25)continue;
                var clear=Scene.Trace.Sphere(17,WorldPosition+Vector3.Up*32,escape+Vector3.Up*32).IgnoreGameObjectHierarchy(GameObject).Run();
                if(clear.Hit)continue;
                agent.MoveTo(escape);return;
            }
        }
        if(CombatEnabled && hasSeenPlayer && lostSight>1.4f && lostSight<5 && flankCooldown<=0)TryStartFlank();
        if(Flanking)
        {
            if((WorldPosition-flankDestination).WithZ(0).Length<35 || (SeesPlayer && visibleTime>.7f))flankRemaining=0;
            else {agent.MoveTo(flankDestination);return;}
        }
        if(impactLookTime>0 && !SeesPlayer) { agent.Stop(); return; }
        // Pursue the last observed location; never track the player through cover.
        if (SearchLastContact())return;
        if ( !hasSeenPlayer || lostSight > 10 )
        {
            if ( (WorldPosition - patrolPoints[patrol]).Length < 45 ) patrol = (patrol + 1) % patrolPoints.Length;
            LastKnownPlayerPosition = patrolPoints[patrol];
        }
        var threatDistance=(LastKnownPlayerPosition-WorldPosition).WithZ(0).Length;
        if (CombatEnabled && hasSeenPlayer && lostSight<2 && threatDistance<350 && TrySpreadFromAllies())return;
        if (SeesPlayer && threatDistance<100)
        {
            // Give the marker room instead of stopping inside the opponent's collider.
            var away=(WorldPosition-LastKnownPlayerPosition).WithZ(0).Normal;
            if(away.Length<.5f)away=-WorldRotation.Forward.WithZ(0).Normal;
            agent.MoveTo(LastKnownPlayerPosition+away*160);
        }
        else if ( SeesPlayer && threatDistance < 300 ) agent.Stop();
        else agent.MoveTo( LastKnownPlayerPosition );
    }

    private bool TrySpreadFromAllies()
    {
        var separation=Vector3.Zero;
        foreach(var ally in Scene.GetAllComponents<ArenaOpponent>())
        {
            if(ally==this || !ally.CombatEnabled || ally.combatant?.Eliminated==true)continue;
            var offset=(WorldPosition-ally.WorldPosition).WithZ(0);
            var distance=offset.Length;
            if(distance>=85)continue;
            var direction=distance>.1f ? offset/distance
                : Rotation.FromYaw(CharacterIndex*60).Forward;
            separation+=direction*(1-distance/85);
        }
        if(separation.Length<.15f)return false;
        // Only relocate to a locally clear spot; navigation remains responsible
        // for moving there, and existing cover tactics retain priority.
        var destination=WorldPosition+separation.Normal*90;
        var clearance=Scene.Trace.Sphere(17,WorldPosition+Vector3.Up*32,destination+Vector3.Up*32)
            .IgnoreGameObjectHierarchy(GameObject).WithoutTags("paintball_debris").Run();
        if(clearance.Hit)return false;
        agent.MoveTo(destination);
        SpacingMoves++;
        return true;
    }

    // Fire cadence advances every frame; path/perception decisions remain throttled.
    internal static bool InSightCone(Vector3 origin, Vector3 forward, Vector3 point)
    {
        var offset = point - origin;
        if (offset.Length >= 2200) return false;
        var horizontal = offset.WithZ(0);
        if (horizontal.Length < 1) return true;
        // 140 degree horizontal view; cover occlusion is checked separately.
        return Vector3.Dot(forward.WithZ(0).Normal, horizontal.Normal) >= .34202015f;
    }

    private void TryCombatShot()
    {
        if(Components.Get<CloseCombat>() is {} contact && (contact.Incapacitated || contact.Attacking))return;
        ShotBlockReason=UsingCover ? "sheltered" : !SeesPlayer ? "no sight" : visibleTime<BotCombatTuning.ReactionSeconds ? "acquiring" : "aim/obstruction";
        if ( CombatEnabled && !UsingCover && SeesPlayer && visibleTime >= BotCombatTuning.ReactionSeconds && target.Components.Get<PaintballCombatant>()?.Eliminated == false
            && Scene.GetAllComponents<OpponentPaintball>().Count() < 96 && marker is not null )
        {
            if(Magazine.PauseRemaining>0 || Magazine.ReloadRemaining>0 || shotLaneRetry>0)return;
            // Recheck at the firing instant: a target may duck between scans.
            if(!CanObserve(target,out var visibleAim))
            { BlockedShotsAvoided++;SeesPlayer=false;visibleTime=0;return; }
            var eye = WorldPosition + Vector3.Up * 60;
            UpdateCoveredMarkerPose( false );
            var muzzle = marker.WorldTransform.PointToWorld( PaintballMarker.MuzzleOffset );
            var obstruction = Scene.Trace.Sphere( PaintballFlight.Radius, eye, muzzle ).IgnoreGameObjectHierarchy( GameObject ).WithoutTags( "paintball_debris" ).Run();
            ShotBlockReason=obstruction.Hit ? "muzzle: "+obstruction.GameObject?.Name : "aim alignment";
            // Seeing a helmet does not mean the barrel has cleared its cover.
            var lane=Scene.Trace.Sphere(PaintballFlight.Radius,muzzle,visibleAim).IgnoreGameObjectHierarchy(GameObject).WithoutTags("paintball_debris").Run();
            if(lane.Hit && PaintballCombatant.Find(lane.GameObject)!=target)
            {
                // A lower chest lane can be blocked while the exposed helmet
                // is reachable. Aim at that visible point before relocating.
                var head=target.EyePosition;
                var sight=Scene.Trace.Ray(eye,head).IgnoreGameObjectHierarchy(GameObject).WithoutTags("paintball_debris").Run();
                var highLane=Scene.Trace.Sphere(PaintballFlight.Radius,muzzle,head).IgnoreGameObjectHierarchy(GameObject).WithoutTags("paintball_debris").Run();
                if((!sight.Hit || PaintballCombatant.Find(sight.GameObject)==target)
                    && (!highLane.Hit || PaintballCombatant.Find(highLane.GameObject)==target))
                { visibleAim=head;observedAimPoint=head;lane=highLane; }
            }
            if(lane.Hit && PaintballCombatant.Find(lane.GameObject)!=target)
            { ShotBlockReason="blocked firing lane";BlockedShotsAvoided++;shotLaneRetry=.12f;blockedLaneTime+=.12f;return; }
            if(obstruction.Hit){BlockedShotsAvoided++;shotLaneRetry=.12f;blockedLaneTime+=.12f;return;}
            var shotAlignment = Vector3.Dot(marker.WorldRotation.Forward,(visibleAim-muzzle).Normal);
            if ( !obstruction.Hit && shotAlignment >= .966f && Magazine.TryFire() )
            {
                ShotBlockReason="fired";
                blockedLaneTime=0;
                MinimumShotAlignment = MathF.Min(MinimumShotAlignment,shotAlignment);
                shots++;
                if(target.Components.Get<PlayerController>().IsValid())ShotsAtPlayers++;
                if(CoverPeeking)ShotsFromCover++;
                if(UsingCover)ShotsWhileSheltered++;
                if(MultiplayerSession.Online)NetworkEffects.Sound(CharacterMarkerSound.For(GameObject),muzzle,System.Guid.Empty);
                else Sound.Play(CharacterMarkerSound.For(GameObject),muzzle);
                ReportMarkerSound(Scene,GameObject,muzzle);
                float flightTime=MathF.Min(.22f,(visibleAim-muzzle).Length/PaintballFlight.Speed);
                var aim=visibleAim+observedVelocity*flightTime*.7f;
                var error = BotCombatTuning.AimError(muzzle,aim,shots,CharacterIndex,MovementSpeed>65);
                if(MultiplayerSession.Online)
                {
                    NetworkEffects.SpawnBall(GameObject,combatant.Team,muzzle,PaintballFlight.LaunchVelocity(muzzle,aim+error));
                    return;
                }
                var ball = new GameObject( true, "Opponent paintball" );
                ball.WorldPosition = muzzle;
                var projectile = ball.Components.Create<OpponentPaintball>();
                projectile.Shooter = GameObject;
                projectile.Team = combatant.Team;
                projectile.Velocity = PaintballFlight.LaunchVelocity(muzzle,aim + error);
            }
        }
    }

    protected override void OnPreRender()
    {
        if(PresentationAnchor.IsValid())
        {
            if(reloadPresentation.IsValid()) { reloadPresentation.Destroy();reloadPresentation=null; }
            UpdateCoveredMarkerPose(false);
            return;
        }
        CoveredSuitMotion.Apply( body );
        var trackPath = body.IsValid() ? CoveredRosterAssets.ReloadTrack( body.Model?.Name ) : null;
        if ( reloadPresentation.IsValid() && (trackPath is null || combatant?.Eliminated == true || presentationModel != body.Model?.Name) )
        {
            reloadPresentation.Destroy(); reloadPresentation = null;
        }
        if ( ShowingOutSignal )
        {
            body.ClearPhysicsBones();
            foreach ( var name in new[]{ "LeftGrip", "RightGrip", "LeftThumb", "RightThumb" } ) body.Morphs.Set( name, 0, 0 );
        }
        if ( trackPath is not null && combatant?.Eliminated != true && marker.IsValid() )
        {
            var visual = marker.Components.Get<ModelRenderer>();
            const string model = "models/weapons/vx9_speed_feed/vx9_speed_feed.vmdl";
            if ( visual.Model?.Name != model ) visual.Model = Model.Load( model );
            if ( !reloadPresentation.IsValid() )
            {
                reloadPresentation = Components.Create<ReloadPresentationTrack>();
                reloadPresentation.Body = body; reloadPresentation.TrackPath = trackPath; presentationModel = body.Model.Name;
            }
            reloadPresentation.Docked = Magazine.ReloadRemaining <= 0;
            reloadPresentation.Phase = 1 - Magazine.ReloadRemaining / Magazine.ActiveReloadDuration;
            reloadPresentation.BodyOffset=Vector3.Zero;
            if(!reloadPresentation.Docked && body.Sequence.Name!="reload_standing" && CoveredRosterAssets.TryMarkerOffset(body.Model.Name,out var reloadOffset)
                && ReloadUpperBodyData.Apply(body,reloadPresentation.Phase,reloadOffset,out _,out var translation,false))
                reloadPresentation.BodyOffset=translation;
            reloadPresentation.ApplyFrame();
        }
        UpdateCoveredMarkerPose( true );
    }

    private void UpdateCoveredMarkerPose( bool writeBones )
    {
        if(PresentationAnchor.IsValid() && marker.IsValid())
        {
            marker.WorldPosition=PresentationAnchor.WorldPosition;
            marker.WorldRotation=PresentationAnchor.WorldRotation;
            return;
        }
        if ( !body.IsValid() || !marker.IsValid() || combatant?.Eliminated == true
            || !CoveredRosterAssets.TryMarkerOffset( body.Model?.Name, out var offset ) ) return;
        var pitch = Magazine.ReloadRemaining > 0 ? 0 : coveredAimPitch;
        if(Magazine.ReloadRemaining>0 && body.Sequence.Name!="reload_standing"
            && ReloadUpperBodyData.Apply(body,1-Magazine.ReloadRemaining/Magazine.ActiveReloadDuration,offset,out var reloadPose,out _,writeBones))
        {
            marker.WorldPosition=reloadPose.Position;marker.WorldRotation=reloadPose.Rotation;return;
        }
        if ( CoveredAimPose.Apply( body, pitch, offset, out var pose, writeBones ) )
        {
            marker.WorldPosition = pose.Position;
            marker.WorldRotation = pose.Rotation;
        }
    }

    private bool UpdateCoverTactic()
    {
        if(!combatant.IsValid())return false;
        bool pressured=combatant.PaintHits>observedHits;
        observedHits=combatant.PaintHits;
        if(!seekingCover && hasSeenPlayer && cooldownCover<=0 && (EvadingAfterHit || pressured || visibleTime>BotCombatTuning.SeekCoverAfterSeconds || (Magazine.Ammo<=6 && Magazine.Reserve>0)))
        {
            if(!Flanking && OpponentCoverPlanner.TryFind(GameObject,target.GameObject,LastKnownPlayerPosition,out var point,requirePeek:!EvadingAfterHit))
            {
                CoverDestination=point;seekingCover=true;coverTime=coverRest=0;CoverSelections++;
                ReservedCover=OpponentCoverPlanner.ShelteringSurface(GameObject,target.GameObject,LastKnownPlayerPosition,point);
            }
            else cooldownCover=EvadingAfterHit ? .3f : 2;
        }
        if(!seekingCover)return false;
        if(OpponentCoverPlanner.IsOccupied(GameObject,ReservedCover)
            || !OpponentCoverPlanner.IsSheltered(GameObject,target.GameObject,LastKnownPlayerPosition,CoverDestination))
        {
            InvalidatedCoverPlans++;
            ReleaseTacticalCover();
            cooldownCover=0;
            return false;
        }
        if(!holdingCover && (WorldPosition-CoverDestination).WithZ(0).Length<12
            && OpponentCoverPlanner.IsSheltered(GameObject,target.GameObject,LastKnownPlayerPosition,WorldPosition))
        {
            holdingCover=true;heldCoverTime=coverRest=0;UsingCover=true;agent.Stop();Magazine.BeginReload();
            BeginCoverStay();
        }
        if(holdingCover)
        {
            if(UpdateRepositionDecision())return true;
            if(lostSight>6 && !RepositionPending){ReleaseTacticalCover();return false;}
            if(pressured)coverRest=0;
            bool hasPeek=OpponentCoverPlanner.TryPeek(GameObject,target.GameObject,LastKnownPlayerPosition,CoverDestination,out var peek,out var low);
            CoverIsLow=low;
            if(coverRest>coverCycleDuration){coverRest=0;VaryCoverCycle();}
            bool expose=hasPeek && !RepositionPending && Magazine.ReloadRemaining<=0 && coverRest>coverRestDuration;
            var destination=expose ? peek : CoverDestination;
            bool arrived=(WorldPosition-destination).WithZ(0).Length<8;
            var wasPeeking=CoverPeeking;
            CoverPeeking=expose&&arrived;
            UsingCover=!expose&&arrived && OpponentCoverPlanner.IsSheltered(GameObject,target.GameObject,LastKnownPlayerPosition,WorldPosition);
            if(CoverPeeking&&!wasPeeking)CoverBursts++;
            if(arrived)agent.Stop();else agent.MoveTo(destination);
            if(!hasPeek&&heldCoverTime>2.5f){ReleaseTacticalCover();return false;}
            return true;
        }
        if(coverTime>coverTravelLimit) { RememberCurrentCover();ReleaseTacticalCover();return false; }
        agent.MoveTo(CoverDestination);return true;
    }
    internal void ApplyNetwork(NetworkBot state)
    {
        UsingCover=state.Covered;CoverPeeking=state.Peek;CoverIsLow=state.Low;
        LastKnownPlayerPosition=state.Aim-Vector3.Up*40;SeesPlayer=state.Peek || !state.Covered;
        observedAimPoint=state.Aim;MovementSpeed=state.Velocity.Length;
        var shape=Components.Get<BoxCollider>();
        if(shape.IsValid()){shape.Scale=new(28,28,CoverCrouched ? 43 : 66);shape.Center=new(0,0,CoverCrouched ? 21.5f : 33);}
    }
    public void InterruptForContact()
    {
        ReleaseTacticalCover();
        agent?.Stop();MovementSpeed=0;flankRemaining=0;decisionTime=0;
    }
    private void ReleaseTacticalCover()
    {
        // Losing a firing lane or shelter must not immediately reselect the
        // same obstacle and restart its stay timer indefinitely.
        if(holdingCover)RememberCurrentCover();
        UsingCover=false;CoverPeeking=false;holdingCover=false;seekingCover=false;ReservedCover=null;cooldownCover=1f;
        RelocatingCover=false;RepositionPending=false;coverTravelLimit=4.5f;
        if(hasSeenPlayer && lostSight>1.4f && flankCooldown<=0)TryStartFlank();
    }
}
