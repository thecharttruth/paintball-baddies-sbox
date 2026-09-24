using Sandbox;
using System;

namespace PaintballBaddies;

/// <summary>Physical cover attachment; native player collision remains responsible for movement.</summary>
public sealed class CoverController : Component
{
    private bool remoteAttached;
    public bool Attached => IsProxy && Components.Get<NetworkPawn>().IsValid() ? remoteAttached : surface is not null;
    public CoverSurface ReservedSurface => Attached ? surface : Sliding ? slideSurface : null;
    internal void ApplyNetwork(NetworkPawn state)
    {
        remoteAttached=state.Covered;LowCover=state.Low;Peeking=state.Peek;Sliding=state.Sliding;
        SlideDirection=state.SlideDirection;Normal=state.CoverNormal;
        surface=Scene.GetAllComponents<CoverSurface>().FirstOrDefault(x=>x.IsValid() && x.GameObject.IsValid() && x.GameObject.Name==state.CoverName);
    }
    public bool Sliding { get; private set; }
    public bool FreeCrouched { get; private set; }
    public Vector3 SlideDirection { get; private set; }
    private CoverSurface slideSurface;
    private Vector3 slideTarget;
    private float slideElapsed;
    private bool slideInput;
    private float pendingCover;
    private bool pendingForward;
    private Vector3 pendingDirection;
    private CoverSurface preferredCandidate;
    public const float InputBuffer=.15f;
    public const float MaxPeekStep=56;
    public bool TrySlide()
    {
        if(!Enabled || Attached || Sliding || !MovementAllowsEntry || !player.UseInputControls || weapon?.AcceptInput!=true || player.Velocity.WithZ(0).Length<135)return false;
        var found=FindCandidate(out var hit,190);
        if(!found.IsValid() || !found.Enabled || MathF.Abs(hit.Normal.z)>.35f || hit.Distance<=70)return false;
        var destination=(hit.EndPosition+hit.Normal.WithZ(0).Normal*StandOff).WithZ(WorldPosition.z);
        if(!CornerClear(destination))return false;
        slideSurface=found;slideTarget=destination;SlideDirection=(destination-WorldPosition).WithZ(0).Normal;
        FreeCrouched=false;
        slideInput=player.UseInputControls;slideElapsed=0;Sliding=true;
        player.UseInputControls=false;weapon.CancelReload();player.UpdateDucking(true);
        return true;
    }
    public void CancelSlide(bool keepCrouched=false,bool preserveMomentum=false)
    {
        if(!Sliding)return;
        Sliding=false;slideSurface=null;
        if(!player.IsValid())return;
        player.UseInputControls=slideInput;player.WishVelocity=Vector3.Zero;if(!keepCrouched)player.UpdateDucking(false);
        if(player.Body.IsValid() && !preserveMomentum)player.Body.Velocity=Vector3.Zero;
    }
    private void UpdateSlide()
    {
        slideElapsed+=Time.Delta;
        var requested=TestInput ? TestMovement : Rotation.FromYaw(player.EyeAngles.yaw)*Input.AnalogMove;
        if(slideElapsed>.08f && requested.Length>.5f && Vector3.Dot(requested.Normal,SlideDirection)<-.45f)
        {CancelSlide(preserveMomentum:true);return;}
        if(!slideSurface.IsValid() || !slideSurface.Enabled || !slideSurface.GameObject.Active || slideElapsed>1.5f || player.IsAirborne){CancelSlide();return;}
        var offset=(slideTarget-WorldPosition).WithZ(0);
        if(offset.Length<28)
        {
            CancelSlide(true);
            if(TryEnter(SlideDirection))player.UpdateDucking(LowCover);
            else player.UpdateDucking(false);
            return;
        }
        var step=WorldPosition+offset.Normal*MathF.Min(offset.Length,240*Time.Delta+2);
        if(!CornerClear(step)){CancelSlide();return;}
        player.UpdateDucking(true);
        player.WishVelocity=offset.Normal*MathF.Min(240,80+offset.Length*1.4f);
    }
    public bool LowCover { get; private set; }
    public bool Peeking { get; private set; }
    private bool candidateAvailable;
    private bool MovementAllowsEntry => player is not null && !player.IsAirborne && Components.Get<CloseCombat>() is not {Incapacitated:true} and not {Attacking:true} && Components.Get<VaultController>()?.IsVaulting != true;
    public bool CanEnter => Enabled && candidateAvailable && !Attached && MovementAllowsEntry && (TestInput || weapon?.AcceptInput == true);
    public bool CornerAvailable { get; private set; }
    public bool TestInput { get; set; }
    public Vector3 TestMovement { get; set; }
    public bool TestAim { get; set; }
    public Vector3 Normal { get; private set; }
    public string Hint => Sliding ? "SLIDING INTO COVER" : Attached ? (LowCover
        ? $"{Key("attack1")} RISE & FIRE · RELEASE TO HIDE · {Key("cover")} LEAVE · {Key("vault")} VAULT"
        : CornerAvailable ? $"{Key("attack1")} PEEK & FIRE · RELEASE TO HIDE · {Key("cover")} LEAVE"
        : $"{Key("cover")} LEAVE COVER · {Key("left")}/{Key("right")} SLIDE · {Key("shoulder")} SHOULDER") : CanEnter ? $"{Key("cover")} TAKE COVER" : "";
    private string Key(string action)=>PaintballControls.Display(Scene,action);
    private PlayerController player;
    private PaintballMarker weapon;
    private CoverSurface surface;
    private Vector3 facePoint;
    private float originalDuckHeight;
    private bool originalInput;
    private float probeTime;
    private Vector3 edgeDirection;
    private Vector3? cornerOrigin;
    private Vector3 cornerTarget;
    private Vector3 cachedPeek;
    private float cornerProbeTime;
    private const float StandOff = 20;
    // Camera follows the exposed edge; the marker always remains right-handed.
    public float PeekCameraSide => !LowCover && cornerOrigin.HasValue && Peeking
        ? (Vector3.Dot(edgeDirection,Rotation.FromYaw(player.EyeAngles.yaw).Right)>=0 ? 1 : -1) : 0;

    protected override void OnStart()
    {
        player = Components.Get<PlayerController>();
        // A lobby transition can destroy the practice pawn before this queued
        // Start callback runs. It no longer has a controller to initialize.
        if ( !player.IsValid() ) { Enabled=false; return; }
        player.JumpSpeed = 0;
        weapon = Components.Get<PaintballMarker>();
    }

    private SceneTraceResult Probe( Vector3 from, Vector3 direction, float distance ) => Scene.Trace.Ray( from, from + direction * distance ).IgnoreGameObjectHierarchy( GameObject ).Run();

    private Vector3 IntentDirection
    {
        get
        {
            var view=Rotation.FromYaw(player.EyeAngles.yaw).Forward;
            var move=TestInput ? TestMovement : Rotation.FromYaw(player.EyeAngles.yaw)*Input.AnalogMove;
            return move.Length>.5f && Vector3.Dot(move.Normal,view)>.45f ? (view+move.Normal*.5f).Normal : view;
        }
    }
    private CoverSurface Candidate(out SceneTraceResult hit,Vector3? direction=null)=>FindCandidate(out hit,70,direction);
    private CoverSurface FindCandidate(out SceneTraceResult hit,float range,Vector3? direction=null)
    {
        hit=default;CoverSurface best=null;float bestScore=float.MaxValue;
        var intent=direction ?? IntentDirection;
        foreach(float angle in new[]{0f,-9,9,-18,18,-28,28})
        {
            var ray=Rotation.FromYaw(angle)*intent;
            var sample=Probe(WorldPosition+Vector3.Up*28,ray,range);
            if(!sample.Hit || MathF.Abs(sample.Normal.z)>.35f || Vector3.Dot(-ray,sample.Normal)<.4f)continue;
            var found=sample.GameObject.Components.Get<CoverSurface>();
            if(!found.IsValid() || !found.Enabled || !found.GameObject.Active || found.Top-WorldPosition.z<30)continue;
            // Attachment corrects the distance to the wall, not the sideways
            // position of an angled probe. Test the same correction we apply.
            var normal=sample.Normal.WithZ(0).Normal;
            var gap=Vector3.Dot(WorldPosition-sample.EndPosition,normal);
            var destination=WorldPosition+normal*(StandOff-gap);
            if(!CornerClear(destination))continue;
            float score=sample.Distance+MathF.Abs(angle)*.65f-(found==preferredCandidate ? 5 : 0);
            if(score>=bestScore)continue;
            bestScore=score;best=found;hit=sample;
        }
        preferredCandidate=best;
        return best;
    }

    public bool TryEnter(Vector3? direction=null)
    {
        if ( !Enabled || Attached || Sliding || !MovementAllowsEntry ) return false;
        var candidate = Candidate( out var hit, direction );
        if ( candidate is null ) return false;
        surface = candidate;
        FreeCrouched=false;
        CornerAvailable = false;
        cornerProbeTime=0;
        edgeDirection = Vector3.Zero;
        cornerOrigin = null;
        Normal = hit.Normal.WithZ( 0 ).Normal;
        facePoint = hit.EndPosition;
        LowCover = !surface.StandingOnly && surface.Top - WorldPosition.z < player.BodyHeight - 3;
        originalInput = player.UseInputControls;
        originalDuckHeight = player.DuckedHeight;
        player.UseInputControls = false;
        if ( LowCover ) player.DuckedHeight = MathF.Min( originalDuckHeight, MathF.Max( 30, surface.Top - WorldPosition.z - 5 ) );
        return true;
    }

    public void Leave()
    {
        if(IsProxy)return;
        FreeCrouched=false;
        pendingCover=0;
        CancelSlide();
        if ( !Attached ) return;
        surface = null;
        LowCover = false;
        Peeking = false;
        CornerAvailable = false;
        cornerOrigin = null;
        if ( !player.IsValid() ) return;
        player.DuckedHeight = originalDuckHeight;
        player.UpdateDucking( false );
        player.WishVelocity = Vector3.Zero;
        player.UseInputControls = originalInput;
    }

    // Cover and vault are separate deliberate actions. Forward intent only
    // chooses the running slide approach; it never starts a vault.
    public bool Activate(bool forward)
    {
        if(!Enabled || player is null || (!TestInput && weapon?.AcceptInput!=true))return false;
        if(Sliding){CancelSlide(preserveMomentum:true);return true;}
        if(Attached)
        {
            Leave();return true;
        }
        if(!MovementAllowsEntry)return false;
        if(TryEnter())return true;
        return forward && TrySlide();
    }

    public bool RequestCover(bool forward)
    {
        pendingCover=0;
        if(Activate(forward))return true;
        if(MovementAllowsEntry && !Attached && !Sliding && (TestInput || weapon?.AcceptInput==true))
        {pendingCover=InputBuffer;pendingForward=forward;pendingDirection=IntentDirection;}
        return false;
    }

    public bool RequestCoverOrCrouch(bool forward)
    {
        if(RequestCover(forward))return true;
        if(!MovementAllowsEntry || (!TestInput && weapon?.AcceptInput!=true))return false;
        FreeCrouched=!FreeCrouched;
        player.UpdateDucking(FreeCrouched);
        return true;
    }

    protected override void OnUpdate()
    {
        if(IsProxy)return;
        if ( player is null ) return;
        if ( weapon?.AcceptInput != true && !TestInput ) { Leave(); return; }
        if ( !TestInput && Input.Pressed( "cover" ) )
        {
            RequestCoverOrCrouch(Input.AnalogMove.x>.5f && !Input.Down("attack2"));
        }
        if(pendingCover>0)
        {
            pendingCover=MathF.Max(0,pendingCover-Time.Delta);
            if(!MovementAllowsEntry || Vector3.Dot(IntentDirection,pendingDirection)<.65f)pendingCover=0;
            else if(Activate(pendingForward))pendingCover=0;
        }
        probeTime -= Time.Delta;
        if ( probeTime <= 0 )
        {
            candidateAvailable = !Attached && MovementAllowsEntry && Candidate( out _ ) is not null;
            probeTime = .1f;
        }
    }

    protected override void OnFixedUpdate()
    {
        if(IsProxy)return;
        if(Sliding){UpdateSlide();return;}
        if ( !Attached || player is null ) return;
        if ( !surface.IsValid() || !surface.Enabled || !surface.GameObject.Active )
        {
            Leave(); return;
        }
        if ( surface.Curved && !cornerOrigin.HasValue )
        {
            // Follow the actual hull instead of retaining the tangent plane at entry.
            var inward = (surface.WorldPosition - WorldPosition).WithZ( 0 ).Normal;
            var face = Probe( WorldPosition + Vector3.Up * 28, inward, 85 );
            if ( !face.Hit || face.GameObject != surface.GameObject || MathF.Abs( face.Normal.z ) > .15f )
            {
                Leave(); return;
            }
            Normal = face.Normal.WithZ( 0 ).Normal;
            facePoint = face.EndPosition;
            edgeDirection = Vector3.Zero;
        }
        var movement = TestInput ? TestMovement : Rotation.FromYaw( player.EyeAngles.yaw ) * Input.AnalogMove;
        // Moving away cancels immediately; movement into the wall never clips the body through it.
        if ( Vector3.Dot( movement, Normal ) > .55f ) { Leave(); return; }
        var aim = (TestInput ? TestAim : Input.Down( "attack2" )) || weapon?.CoverFireRequested==true;
        player.UpdateDucking( LowCover && !aim );
        Peeking = LowCover && aim && !player.IsDucking;
        var tangent = Vector3.Cross( Vector3.Up, Normal ).Normal;
        var slide = tangent * Vector3.Dot( movement, tangent ) * 55;
        if ( !LowCover && cornerOrigin.HasValue )
        {
            // Recheck after an aim change, moving farther along this edge only.
            // Never switch to the other side of the obstacle while holding aim.
            cornerProbeTime-=Time.Delta;
            if(aim && cornerProbeTime<=0)
            {
                cornerProbeTime=.12f;
                bool settled=(cornerTarget-WorldPosition).WithZ(0).Length<6 && weapon.Aiming
                    && weapon.PresentationAnchor.IsValid() && Vector3.Dot(weapon.PresentationAnchor.WorldRotation.Forward,player.EyeAngles.ToRotation().Forward)>.9f;
                // Once the rifle pose has settled, the actual muzzle is the
                // clearance authority. Combining it with the approximate grip
                // corridor can reject a safe short step at the left-hand edge.
                if(!(settled ? StandingMarkerClear(cornerTarget) : StandingLaneClear(cornerTarget)))
                    for(float extra=4;extra<=32;extra+=4)
                    {
                        var next=cornerTarget+edgeDirection*extra;
                        if((next-cornerOrigin.Value).Length>MaxPeekStep)break;
                        if(CornerClear(next)&&(settled ? StandingMarkerClear(next) : StandingLaneClear(next))){cornerTarget=next;break;}
                    }
            }
            var destination = aim ? cornerTarget : cornerOrigin.Value;
            var offset = (destination - WorldPosition).WithZ( 0 );
            Peeking = aim && offset.Length < 4;
            // Repositioning is always player-controlled. Commit the current
            // spot when A/D is used rather than dragging back to an old anchor.
            if(slide.Length>5)
            {
                cornerOrigin=null;cornerProbeTime=0;CornerAvailable=false;Peeking=false;
                player.WishVelocity=slide;return;
            }
            player.WishVelocity = offset.Normal * MathF.Min( 180, offset.Length * 16 );
            if ( !aim && offset.Length < 1 ) cornerOrigin = null;
            return;
        }
        // Probe beyond the body radius so an unprompted slide stops before the end of the face.
        if ( slide.Length > 1 && !surface.Curved )
        {
            var edge = Probe( WorldPosition + Vector3.Up * 28 + slide.Normal * 22, -Normal, 80 );
            if ( !edge.Hit || edge.GameObject != surface.GameObject || Vector3.Dot( edge.Normal, Normal ) < .9f )
            {
                edgeDirection = slide.Normal;
                slide = Vector3.Zero;
            }
            else edgeDirection = Vector3.Zero;
        }
        // Detect reachable exposure even when entering directly beside an edge.
        // Rounded bunkers have no hard edge, so find a clear tangent position.
        cornerProbeTime-=Time.Delta;
        if(cornerProbeTime<=0)
        {
            CornerAvailable = !LowCover && FindStandingPeek(tangent, out cachedPeek);
            cornerProbeTime=.08f;
        }
        if ( aim && CornerAvailable )
        {
            cornerOrigin = WorldPosition;
            cornerTarget = cachedPeek;
        }
        var gap = Vector3.Dot( WorldPosition - facePoint, Normal );
        if ( gap > 85 || gap < 0 ) { Leave(); return; }
        var correction = Normal * ((StandOff - gap) * 14).Clamp( -180, 180 );
        player.WishVelocity = slide + correction;
    }

    private bool FindStandingPeek(Vector3 tangent, out Vector3 destination)
    {
        destination=WorldPosition;
        var preferred=edgeDirection.Length>.5f ? edgeDirection : tangent*(weapon?.LeftShoulder==true ? 1 : -1);
        for(float distance=8;distance<=MaxPeekStep;distance+=4)
        {
            // Choose the nearest reachable edge, using shoulder preference only
            // as a tie breaker. A long wall still cannot force a distant excursion.
            foreach(var side in new[]{preferred,-preferred})
            {
                var target=WorldPosition+side*distance;
                if(!CornerClear(target))continue;
                if(!StandingLaneClear(target))continue;
                destination=target;edgeDirection=side;return true;
            }
        }
        return false;
    }

    private bool StandingLaneClear(Vector3 position)
    {
        var facing=player.EyeAngles.ToRotation();
        // Test the right-hand grip and complete barrel corridor, rather than
        // a ray from the character's centre. This matters at a left corner.
        foreach(float height in new[]{46f,58f})
        {
            var eye=position+Vector3.Up*height;
            var grip=eye+facing.Right*16;
            var muzzle=grip+facing.Forward*36;
            foreach(var segment in new[]{(eye,grip),(grip,muzzle),(muzzle,muzzle+facing.Forward*160)})
                if(Scene.Trace.Sphere(2,segment.Item1,segment.Item2).IgnoreGameObjectHierarchy(GameObject)
                    .WithoutTags("paintball_debris","paintball_actor").WithSurfaceMeshes().Run().Hit)return false;
        }
        return true;
    }

    private bool StandingMarkerClear(Vector3 position)
    {
        var offset=position-WorldPosition;
        var muzzle=weapon.Muzzle+offset;
        foreach(var segment in new[]{(player.EyePosition+offset,muzzle),(muzzle,muzzle+player.EyeAngles.ToRotation().Forward*160)})
            if(Scene.Trace.Sphere(1.4f,segment.Item1,segment.Item2).IgnoreGameObjectHierarchy(GameObject)
                .WithoutTags("paintball_debris","paintball_actor").WithSurfaceMeshes().Run().Hit)return false;
        return true;
    }

    private bool CornerClear( Vector3 destination )
    {
        // Sweep a body-sized volume at both torso heights before committing to exposure.
        // The native capsule can rest a fraction inside its contact margin.
        // A full-radius sweep then reports StartedSolid even while backing away
        // from the wall. Use a tiny skin; native body collision stays unchanged.
        var radius=MathF.Max(1,player.BodyRadius-.5f);
        foreach ( var height in new[] { 18f, 50f } )
        {
            var hit = Scene.Trace.Sphere( radius, WorldPosition + Vector3.Up * height, destination + Vector3.Up * height )
                .IgnoreGameObjectHierarchy( GameObject ).Run();
            if ( hit.Hit ) return false;
        }
        return Probe( destination + Vector3.Up * 12, Vector3.Down, 24 ).Hit;
    }

    protected override void OnDisabled() => Leave();
    protected override void OnDestroy() => Leave();
}
