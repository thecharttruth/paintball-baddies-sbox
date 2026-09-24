using Sandbox;
using System;

namespace PaintballBaddies;

/// <summary>Short, swept kinematic traversal of a prevalidated low obstacle.</summary>
public sealed class VaultController : Component
{
    public bool IsVaulting { get; private set; }
    internal void ApplyNetwork(NetworkPawn state){IsVaulting=state.Vaulting;Progress=state.VaultProgress;Facing=state.VaultFacing;}
    private bool routeAvailable;
    private float feedbackTime;
    public string BlockReason { get; private set; } = "";
    public bool Available => Enabled && routeAvailable && !IsVaulting && player.IsValid() && !player.IsAirborne;
    public string Hint => IsVaulting ? "VAULTING" : Available ? $"{PaintballControls.Display(Scene,"vault")} VAULT" : feedbackTime>0 ? BlockReason : "";
    public float Progress { get; private set; }
    public int Completed { get; private set; }
    public string Outcome { get; private set; } = "";
    public Vector3 VisualBasePosition { get; private set; }
    public Rotation Facing { get; private set; }
    private PlayerController player;
    private VaultPlanner.Plan plan;
    private bool inputBefore, motionBefore;
    private float probeTime;
    public const float Duration = .8f;
    protected override void OnStart() => player = Components.Get<PlayerController>();
    public bool TryBegin()
    {
        // Revalidate on the press; attached cover supplies its inward direction
        // even when the camera is looking along the wall.
        var cover = Components.Get<CoverController>();
        if ( !Enabled || IsVaulting || !player.IsValid() || Components.Get<CloseCombat>()?.Incapacitated==true ) return false;
        if(player.IsAirborne){BlockReason="Land before vaulting";feedbackTime=2;return false;}
        if(!VaultPlanner.TryPlan(player,out plan,out var reason)){BlockReason=reason;Outcome=reason;feedbackTime=2;return false;}
        feedbackTime=0;BlockReason="";
        cover?.Leave();
        inputBefore = player.UseInputControls; motionBefore = player.Body.MotionEnabled;
        VisualBasePosition = player.Renderer.LocalPosition;
        Facing = Rotation.LookAt( (plan.Landing - plan.Start).WithZ( 0 ) );
        Progress = 0; IsVaulting = true; routeAvailable = false; Outcome = "Traversing";
        Components.Get<PaintballMarker>()?.CancelReload();
        player.UseInputControls = false; player.WishVelocity = Vector3.Zero; player.Body.Velocity = Vector3.Zero;
        player.Body.MotionEnabled = false;
        return true;
    }
    public void Cancel()
    {
        if(IsProxy)return;
        routeAvailable = false;
        if ( !IsVaulting ) return;
        IsVaulting = false; Outcome = "Interrupted";
        if ( !player.IsValid() ) return;
        player.UseInputControls = inputBefore;
        player.WishVelocity = Vector3.Zero;
        if ( player.Body.IsValid() )
        {
            player.Body.MotionEnabled = motionBefore;
            player.Body.Velocity = Vector3.Zero;
        }
        if ( player.Renderer.IsValid() ) player.Renderer.LocalPosition = VisualBasePosition;
    }
    protected override void OnDisabled() => Cancel();
    protected override void OnDestroy() => Cancel();
    protected override void OnUpdate()
    {
        if(IsProxy)return;
        feedbackTime=MathF.Max(0,feedbackTime-Time.Delta);
        if ( IsVaulting || player is null ) return;
        var cover = Components.Get<CoverController>();
        probeTime -= Time.Delta;
        if ( probeTime <= 0 )
        {
            routeAvailable = VaultPlanner.TryPlan( player, out _, out var reason );
            if(feedbackTime<=0)BlockReason=routeAvailable ? "" : reason;
            probeTime = .1f;
        }
        if ( Components.Get<PaintballMarker>()?.AcceptInput == true && Input.Pressed( "vault" ) ) TryBegin();
    }
    private static float Smooth( float value ) { value = value.Clamp( 0, 1 ); return value * value * (3 - 2 * value); }
    protected override void OnFixedUpdate()
    {
        if(IsProxy)return;
        if ( !IsVaulting ) return;
        Progress = (Progress + Time.Delta / Duration).Clamp( 0, 1 );
        var destination = Progress < .2f ? Vector3.Lerp( plan.Start, plan.AboveStart, Smooth( Progress / .2f ) )
            : Progress < .65f ? Vector3.Lerp( plan.AboveStart, plan.AboveLanding, Smooth( (Progress - .2f) / .45f ) )
            : Vector3.Lerp( plan.AboveLanding, plan.Landing, MathF.Pow( ((Progress - .65f) / .35f).Clamp( 0, 1 ), 2 ) );
        var body = new Capsule(Vector3.Up*17,Vector3.Up*(player.BodyHeight-16),16);
        var sweep = Scene.Trace.Capsule( body, WorldPosition, destination ).IgnoreGameObjectHierarchy( GameObject ).WithoutTags( "paintball_debris" ).Run();
        if ( sweep.Hit ) { Cancel(); Outcome = "Route became blocked"; return; }
        WorldPosition = destination;
        if ( Progress < 1 ) return;
        Cancel(); Completed++; Outcome = "Landed";
    }
}
