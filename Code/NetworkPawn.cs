using Sandbox;
using System;
namespace PaintballBaddies;

/// <summary>Owner movement/presentation state; host validates combat requests.</summary>
public sealed class NetworkPawn : Component
{
    [Property,Sync(SyncFlags.FromHost)] public int Seat { get; set; }
    [Property,Sync(SyncFlags.FromHost)] public Guid ConnectionId { get; set; }
    [Sync] public bool Ducking { get; set; }
    [Sync] public bool Grounded { get; set; }
    [Sync] public bool Covered { get; set; }
    [Sync] public bool Low { get; set; }
    [Sync] public bool Peek { get; set; }
    [Sync] public bool Sliding { get; set; }
    [Sync] public Vector3 SlideDirection { get; set; }
    [Sync] public Vector3 CoverNormal { get; set; }
    [Sync] public string CoverName { get; set; }
    [Sync] public bool Vaulting { get; set; }
    [Sync] public float VaultProgress { get; set; }
    [Sync] public Rotation VaultFacing { get; set; }
    [Sync] public bool Aiming { get; set; }
    [Sync] public Vector3 Motion { get; set; }
    [Sync] public Angles Look { get; set; }
    [Sync(SyncFlags.FromHost)] public int AcceptedShots { get; private set; }
    [Sync(SyncFlags.FromHost)] public int RejectedShots { get; private set; }
    [Sync(SyncFlags.FromHost)] public string ContactState { get; private set; }="";
    private PlayerController player;
    private float sinceShot=10,stateTime;
    public int RecoveryRequests {get;private set;}
    public int RecoveryAccepted {get;private set;}
    private int lastSequence;
    private bool requestedPaint;
    protected override void OnStart()
    {
        player=Components.Get<PlayerController>();
        if(IsProxy)player.UseInputControls=player.UseLookControls=player.UseCameraControls=false;
        if(MultiplayerSession.Authority)
        {
            var actor=Components.Get<PaintballCombatant>();actor.Team=Seat+10;actor.StayInMatch=true;
        }
        Components.Get<RosterSelection>()?.SelectNetwork(Seat);
    }
    protected override void OnUpdate()
    {
        sinceShot+=Time.Delta;
        if(!IsProxy && !requestedPaint && sinceShot>11){requestedPaint=true;RequestPaint();}
        if(!player.IsValid())return;
        if(IsProxy)
        {
            player.UseInputControls=player.UseLookControls=player.UseCameraControls=false;
            player.EyeAngles=Look;player.UpdateDucking(Ducking);
            Components.Get<CoverController>()?.ApplyNetwork(this);
            Components.Get<VaultController>()?.ApplyNetwork(this);
        }
        else
        {
            var cover=Components.Get<CoverController>();var vault=Components.Get<VaultController>();
            // A destroyed component can remain referenced until cover's next
            // fixed update, while its GameObject is already null. Release the
            // reservation before publishing state so movement is restored too.
            var reserved=cover?.ReservedSurface;
            var coverObject=reserved.IsValid() ? reserved.GameObject : null;
            if(cover.IsValid() && (cover.Attached || cover.Sliding) && !coverObject.IsValid())cover.Leave();
            Covered=cover?.Attached==true;Low=cover?.LowCover==true;Peek=cover?.Peeking==true;
            Sliding=cover?.Sliding==true;SlideDirection=cover?.SlideDirection ?? Vector3.Zero;
            CoverNormal=cover?.Normal ?? Vector3.Zero;CoverName=coverObject.IsValid() ? coverObject.Name : "";
            Vaulting=vault?.IsVaulting==true;VaultProgress=vault?.Progress ?? 0;VaultFacing=vault?.Facing ?? Rotation.Identity;
            Ducking=player.IsDucking;Grounded=player.IsOnGround;
            Aiming=Components.Get<PaintballMarker>()?.Aiming==true;Motion=player.Velocity;Look=player.EyeAngles;
        }
        if(MultiplayerSession.Authority)
        {
            stateTime-=Time.Delta;
            if(stateTime<=0)PublishContactState();
        }
        else Components.Get<CloseCombat>()?.ApplyNetworkState(ContactState);
    }
    [Rpc.Host(NetFlags.OwnerOnly)]
    public async void RequestPaint()
    {
        var caller=Rpc.Caller?.Id;
        await GameTask.MainThread();
        if(!this.IsValid() || caller!=ConnectionId)return;
        var session=MultiplayerSession.Find(Scene);ReceivePaint(session.PaintSnapshot());
        if(session.Status=="TIME UP")EndRound();
    }
    [Rpc.Owner(NetFlags.HostOnly)]
    public async void ReceivePaint(string json)
    {
        await GameTask.MainThread();
        if(!this.IsValid())return;
        MultiplayerSession.Find(Scene)?.ReplayPaint(json);
    }
    internal void PublishContactState()
    {
        if(!MultiplayerSession.Authority)return;
        var contact=Components.Get<CloseCombat>();
        stateTime=contact?.Attacking==true ? .033f : .1f;
        ContactState=contact?.CaptureNetworkState() ?? "";
    }
    [Rpc.Owner(NetFlags.HostOnly)]
    public async void ContactFeedback(bool received,bool knocked)
    {
        await GameTask.MainThread();
        if(!this.IsValid())return;
        Components.Get<CloseCombat>()?.PlayContactFeedback(received,knocked);
    }
    [Rpc.Host(NetFlags.OwnerOnly)]
    public async void RequestShot(Vector3 origin,Vector3 aim,int sequence,bool exposedFromCover=false)
    {
        // Delayed RPCs can resume on a worker thread. Preserve caller identity
        // before yielding, then validate and touch engine objects on its main thread.
        var caller=Rpc.Caller?.Id;
        await GameTask.MainThread();
        if(!this.IsValid())return;
        var session=MultiplayerSession.Find(Scene);var state=Components.Get<PaintballCombatant>();
        bool finite=float.IsFinite(origin.x)&&float.IsFinite(origin.y)&&float.IsFinite(origin.z)
            &&float.IsFinite(aim.x)&&float.IsFinite(aim.y)&&float.IsFinite(aim.z);
        if(caller!=ConnectionId || !finite || sequence<=lastSequence || sinceShot<PaintballFlight.ShotInterval-.015f
            || !state.AcceptHits || (!session.InRound && session.Status!="LOBBY") || Components.Get<CloseCombat>()?.Incapacitated==true
            || Components.Get<CloseCombat>()?.Attacking==true || Covered&&!Peek&&!exposedFromCover || Vaulting || Sliding
            || (origin-WorldPosition).Length>115 || (aim-origin).Length<1)
        {RejectedShots++;return;}
        // Owner-controlled peek sync can arrive after this RPC, especially for
        // a single-click shot. Carry its at-shot state with the request, while
        // still enforcing the physical eye-to-muzzle obstruction below.
        lastSequence=sequence;sinceShot=0;AcceptedShots++;
        var obstruction=PaintballFlight.Trace(Scene,player.EyePosition,origin,GameObject);
        var tint=CharacterPaintColor.For(GameObject);
        NetworkEffects.Sound(CharacterMarkerSound.For(GameObject),origin,ConnectionId);
        ArenaOpponent.ReportMarkerSound(Scene,GameObject,origin);
        if(obstruction.Hit){PaintImpactSystem.Find(Scene)?.Spawn(obstruction,tint);return;}
        var target=origin+(aim-origin).Normal*MathF.Min((aim-origin).Length,PaintballFlight.Speed*PaintballFlight.Lifetime);
        NetworkEffects.SpawnBall(GameObject,state.Team,origin,PaintballFlight.LaunchVelocity(origin,target));
    }
    [Rpc.Host(NetFlags.OwnerOnly)]
    public async void RequestElbow()
    {
        var caller=Rpc.Caller?.Id;
        await GameTask.MainThread();
        if(!this.IsValid())return;
        if(caller==ConnectionId && Components.Get<PaintballCombatant>().AcceptHits)Components.Get<CloseCombat>()?.TryAttackAuthoritative();
    }
    [Rpc.Host(NetFlags.OwnerOnly)]
    public async void RequestRecovery()
    {
        var caller=Rpc.Caller?.Id;
        await GameTask.MainThread();
        if(!this.IsValid())return;
        if(caller==ConnectionId){RecoveryRequests++;if(Components.Get<CloseCombat>()?.RegisterRecoveryAuthoritative()==true)RecoveryAccepted++;}
    }
    [Rpc.Owner(NetFlags.HostOnly)]
    public async void ConfirmHit()
    {
        await GameTask.MainThread();
        if(!this.IsValid())return;
        Components.Get<PaintballMarker>()?.NetworkHit();
    }
    [Rpc.Owner(NetFlags.HostOnly)]
    public async void ResetForRound(Vector3 position,int round)
    {
        await GameTask.MainThread();
        if(!this.IsValid())return;
        Components.Get<CoverController>()?.Leave();Components.Get<VaultController>()?.Cancel();Components.Get<CloseCombat>()?.ResetState();
        player ??= Components.Get<PlayerController>();
        player.WorldPosition=position;player.Body.MotionEnabled=true;player.Body.Sleeping=false;player.Body.Velocity=Vector3.Zero;player.WishVelocity=Vector3.Zero;
        player.UseInputControls=player.UseLookControls=player.UseCameraControls=true;
        Components.Get<PaintballMarker>()?.ResetTraining();Components.Get<PaintballMarker>().AcceptInput=true;
        Network.ClearInterpolation();
    }
    [Rpc.Owner(NetFlags.HostOnly)]
    public async void EndRound()
    {
        await GameTask.MainThread();
        if(!this.IsValid())return;
        Components.Get<CoverController>()?.Leave();Components.Get<VaultController>()?.Cancel();Components.Get<CloseCombat>()?.ResetState();
        Components.Get<PaintballMarker>()?.EndRound();
        player.UseInputControls=false;player.WishVelocity=Vector3.Zero;
        if(player.Body.IsValid())player.Body.Velocity=Vector3.Zero;
        var match=Components.Get<ArenaMatch>();
        bool won=match?.Standings.FirstOrDefault()?.IsPlayer==true;
        Sound.Play(won ? "sounds/paintball/round_win.sound" : "sounds/paintball/round_loss.sound",WorldPosition);
    }
}
