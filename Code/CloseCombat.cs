using Sandbox;
using Sandbox.Citizen;
namespace PaintballBaddies;

/// <summary>A fresh primary press strikes at close range; focus overrides to fire. Five focus taps recover.</summary>
public sealed class CloseCombat : Component
{
    public const float Reach=47; // 1.19 m between roots; body capsules leave a small striking gap.
    public const float FacingThreshold=.82f;
    public const float AttackCooldown=.62f, InputBuffer=.20f;
    public const int RequiredRecoveryPresses=5;
    public const float RecoveryPressGap=.42f;
    public const float AttackDuration=.44f, ImpactTime=.12f, FallDuration=1.30f, GetUpDuration=1.25f;
    public const string ImpactSound="sounds/contact/elbow_impact.sound";
    public bool Downed { get; private set; }
    public bool Recovering => recovery>0;
    public bool Incapacitated => Downed || Recovering;
    public bool Attacking => attackTime>0;
    public float CooldownRemaining => cooldown;
    public bool ReadyToRecover => Downed && downTime>=FallDuration;
    public int RecoveryPresses { get; private set; }
    public int Contacts { get; private set; }
    public int Knockdowns { get; private set; }
    public int ImpactSounds { get; private set; }
    public int ImpactFeedbacks { get; private set; }
    public int CameraImpulses { get; private set; }
    public float LastShakeAmplitude { get; private set; }
    public float FeedbackRemaining { get; private set; }
    public string FeedbackText { get; private set; }="";
    private float bufferedPress,requestWait;
    private enum PrimaryAction { None, Fire, Melee }
    private PrimaryAction primaryAction;
    public bool PrimaryCommittedToMelee => primaryAction==PrimaryAction.Melee;
    public bool MeleeAvailable => !MenuOpen && !Incapacitated && !Attacking && requestWait<=0 && cooldown<=0
        && Components.Get<PaintballMarker>()?.AcceptInput==true
        && Components.Get<VaultController>()?.IsVaulting!=true && Components.Get<CoverController>()?.Sliding!=true
        && FindContactTarget().IsValid();
    private int receivedContacts,lastReceivedContacts;
    public float FallProgress => (downTime/FallDuration).Clamp(0,1);
    public float GetUpProgress => Recovering ? (1-recovery/GetUpDuration).Clamp(0,1) : 0;
    // Older editor pose probes still compile; production uses the actual fall clip.
    public CitizenAnimationHelper.SittingStyle KnockPose { get; set; }=CitizenAnimationHelper.SittingStyle.Floor4;
    private float cooldown,attackTime,downTime,recovery,knockImmunity,aiDecision,pressGap,aiRecovery;
    private bool impactPending,inputBefore,movementHeld;
    private PaintballCombatant pendingTarget;
    private Vector3 push;
    private Vector3 stepDirection;
    private float stepDistance,stepApplied;
    private Rotation motionFacing;
    private PlayerController player;
    private PaintballCombatant actor;
    private Rotation Facing => player.IsValid() ? Rotation.FromYaw(player.EyeAngles.yaw) : WorldRotation;
    private bool MenuOpen => player.IsValid() && !IsProxy && Scene.GetAllComponents<PaintballControls>().FirstOrDefault()?.Open==true;
    protected override void OnStart(){player=Components.Get<PlayerController>();actor=Components.Get<PaintballCombatant>();}
    protected override void OnUpdate()
    {
        FeedbackRemaining=System.MathF.Max(0,FeedbackRemaining-Time.Delta);
        requestWait=System.MathF.Max(0,requestWait-Time.Delta);
        if(MultiplayerSession.Online && !MultiplayerSession.Authority)
        {
            float delta=Time.Delta;
            attackTime=System.MathF.Max(0,attackTime-delta);recovery=System.MathF.Max(0,recovery-delta);
            if(Downed)downTime+=delta;
            if(!IsProxy)
            {
                if(Incapacitated || Attacking)HoldMovement();else RestoreMovement();
                if(!MenuOpen)ObserveAimInput(Input.Pressed("attack2"),Input.Down("attack2"),Input.Released("attack2"),delta);
            }
            return;
        }
        if(!actor.IsValid())actor=Components.Get<PaintballCombatant>();
        if(actor?.Eliminated==true || actor?.AcceptHits==false){ResetState();return;}
        // A network menu gates local input, never authoritative combat clocks.
        // Offline settings already pause the scene itself.
        float dt=Time.Delta;
        cooldown=System.MathF.Max(0,cooldown-dt);knockImmunity=System.MathF.Max(0,knockImmunity-dt);
        if(Downed)
        {
            downTime+=dt;HoldMovement();pressGap+=dt;
            if(RecoveryPresses>0 && pressGap>RecoveryPressGap)RecoveryPresses=0;
            if(player.IsValid() && !IsProxy)ObserveAimInput(Input.Pressed("attack2"),Input.Down("attack2"),Input.Released("attack2"),dt);
            else if(ReadyToRecover && Components.Get<ArenaOpponent>() is {Enabled:true,CombatEnabled:true})
            {
                aiRecovery-=dt;
                if(aiRecovery<=0){RegisterRecoveryPress();aiRecovery=Game.Random.Float(.13f,.23f);}
            }
            return;
        }
        if(Recovering)
        {
            recovery=System.MathF.Max(0,recovery-dt);HoldMovement();
            if(recovery==0){knockImmunity=1.5f;RestoreMovement();}
            return;
        }
        if(Attacking)
        {
            attackTime=System.MathF.Max(0,attackTime-dt);HoldMovement();
            if(impactPending && AttackDuration-attackTime>=ImpactTime)
            {
                impactPending=false;
                if(CanContact(pendingTarget,Reach+10))
                {
                    var victim=pendingTarget.Components.Get<CloseCombat>();
                    if(victim.IsValid() && !victim.Incapacitated)
                    {
                        var shieldHit=ArenaShield.TryAbsorbMelee(pendingTarget,WorldPosition);
                        bool knock=shieldHit!=ArenaShield.MeleeResult.Absorbed && victim.knockImmunity<=0;
                        victim.ReceiveContact(WorldPosition,knock);
                        Contacts++;
                        if(knock){Knockdowns++;pendingTarget.RegisterKnockdown(actor);}
                        ConfirmFeedback(false,knock);
                        string sound=shieldHit==ArenaShield.MeleeResult.None ? ImpactSound : "sounds/contact/shield_bash.sound";
                        if(MultiplayerSession.Online)NetworkEffects.Sound(sound,pendingTarget.WorldPosition+Vector3.Up*42,System.Guid.Empty);
                        else Sound.Play(sound,pendingTarget.WorldPosition+Vector3.Up*42);
                        ImpactSounds++;
                    }
                }
            }
            if(!Attacking)RestoreMovement();
        }
        if(player.IsValid())
        {
            if(IsProxy)return;
            if(Components.Get<PaintballMarker>()?.AcceptInput!=true){return;}
            ObserveAimInput(Input.Pressed("attack2"),Input.Down("attack2"),Input.Released("attack2"),dt);
        }
        else
        {
            aiDecision-=dt;
            if(aiDecision<=0)
            {
                aiDecision=Game.Random.Float(.35f,.6f);
                if(Components.Get<ArenaOpponent>() is {Enabled:true,CombatEnabled:true,UsingCover:false} && Game.Random.Float(0,1)<.45f)TryAttack();
            }
        }
    }
    /// <summary>Action edges count; a held button never produces repeated taps.</summary>
    public void ObserveAimInput(bool justPressed,bool held,bool released,float dt)
    {
        if(MenuOpen || Recovering)return;
        if(Downed && justPressed)RegisterRecoveryPress();
    }

    /// <summary>Called once by the marker before firing; latches intent until release.</summary>
    public bool ObservePrimaryInput(bool justPressed,bool held,bool focus,float dt)
    {
        if(MenuOpen || Incapacitated || Components.Get<PaintballMarker>()?.AcceptInput!=true)
        {primaryAction=PrimaryAction.None;bufferedPress=0;return false;}
        if(!held)primaryAction=PrimaryAction.None;
        if(focus)bufferedPress=0;
        if(justPressed)
        {
            bool close=!focus && Components.Get<VaultController>()?.IsVaulting!=true
                && Components.Get<CoverController>()?.Sliding!=true && FindContactTarget().IsValid();
            primaryAction=close ? PrimaryAction.Melee : PrimaryAction.Fire;
            bufferedPress=close ? InputBuffer : 0;
        }
        if(bufferedPress>0)
        {
            if(TryAttack())bufferedPress=0;
            else bufferedPress=System.MathF.Max(0,bufferedPress-dt);
        }
        // Holding fire never changes to melee when somebody approaches. Holding
        // a melee click never repeats the strike or releases an unexpected shot.
        return held && primaryAction==PrimaryAction.Fire && !Attacking;
    }
    public bool CanContact(PaintballCombatant other)
        => CanContact(other,Reach);
    private bool CanContact(PaintballCombatant other,float reach)
    {
        if(!actor.IsValid())actor=Components.Get<PaintballCombatant>();
        if(!actor.IsValid() || !other.IsValid() || other==actor || other.Team==actor.Team || !other.AcceptHits || other.Eliminated)return false;
        var offset=other.WorldPosition-WorldPosition;
        var facing=Attacking ? motionFacing : Facing;
        if(offset.WithZ(0).Length>reach || System.MathF.Abs(offset.z)>30 || Vector3.Dot(facing.Forward,offset.WithZ(0).Normal)<FacingThreshold)return false;
        var obstruction=Scene.Trace.Sphere(2,WorldPosition+Vector3.Up*38,other.WorldPosition+Vector3.Up*38)
            .IgnoreGameObjectHierarchy(GameObject).IgnoreGameObjectHierarchy(other.GameObject).WithoutTags("paintball_debris").Run();
        return !obstruction.Hit;
    }
    private PaintballCombatant FindContactTarget(float reach=Reach)=>Scene.GetAllComponents<PaintballCombatant>().Where(x=>CanContact(x,reach))
        .Where(x=>x.Components.Get<CloseCombat>()?.Incapacitated!=true)
        .OrderBy(x=>(x.WorldPosition-WorldPosition).Length).FirstOrDefault();
    public bool TryAttack()
    {
        if(MultiplayerSession.Online && !MultiplayerSession.Authority)
        {
            if(IsProxy || MenuOpen || Incapacitated || Attacking || requestWait>0 || !FindContactTarget().IsValid()
                || Components.Get<VaultController>()?.IsVaulting==true || Components.Get<CoverController>()?.Sliding==true)return false;
            Components.Get<CoverController>()?.Leave();
            requestWait=AttackCooldown;
            Components.Get<NetworkPawn>()?.RequestElbow();return true;
        }
        return TryAttackAuthoritative();
    }
    internal bool TryAttackAuthoritative()
    {
        if(!actor.IsValid())actor=Components.Get<PaintballCombatant>();
        if(MenuOpen || actor?.AcceptHits!=true || cooldown>0 || Incapacitated || Attacking || Components.Get<VaultController>()?.IsVaulting==true || Components.Get<CoverController>()?.Sliding==true)return false;
        pendingTarget=FindContactTarget();
        if(!pendingTarget.IsValid())return false;
        Components.Get<CoverController>()?.Leave();Components.Get<PaintballMarker>()?.CancelReload();
        cooldown=AttackCooldown;attackTime=AttackDuration;impactPending=true;motionFacing=Facing;
        var offset=(pendingTarget.WorldPosition-WorldPosition).WithZ(0);
        stepDirection=offset.Normal;stepDistance=(offset.Length-30).Clamp(0,30);stepApplied=0;
        HoldMovement();Components.Get<NetworkPawn>()?.PublishContactState();return true;
    }
    public void ReceiveContact(Vector3 source,bool knock)
    {
        if(Incapacitated)return;
        receivedContacts++;
        ConfirmFeedback(true,knock);
        Components.Get<CoverController>()?.Leave();Components.Get<VaultController>()?.Cancel();
        Components.Get<ArenaOpponent>()?.InterruptForContact();
        attackTime=0;impactPending=false;pendingTarget=null;
        var away=(WorldPosition-source).WithZ(0).Normal;
        var to=WorldPosition+away*(knock ? 20 : 12);
        var clear=Scene.Trace.Sphere(15,WorldPosition+Vector3.Up*25,to+Vector3.Up*25).IgnoreGameObjectHierarchy(GameObject).Run();
        if(!clear.Hit)push=away*(knock ? 190 : 105);
        var voice=Components.Get<CharacterVoice>();
        if(knock)voice?.PlayKnockdown();else voice?.TryPlay();
        if(knock)
        {
            ArenaShield.ReleaseForKnockdown(Components.Get<PaintballCombatant>());
            motionFacing=Facing;Downed=true;downTime=0;RecoveryPresses=0;pressGap=0;aiRecovery=Game.Random.Float(.25f,.55f);
            HoldMovement();if(player.IsValid())player.UpdateDucking(true);
        }
        else RestoreMovement();
        Components.Get<NetworkPawn>()?.PublishContactState();
    }
    private void ConfirmFeedback(bool received,bool knocked)
    {
        if(MultiplayerSession.Online)Components.Get<NetworkPawn>()?.ContactFeedback(received,knocked);
        else PlayContactFeedback(received,knocked);
    }
    internal void PlayContactFeedback(bool received,bool knocked)
    {
        if(IsProxy || !Components.Get<PlayerController>().IsValid())return;
        ImpactFeedbacks++;
        bool heist=MultiplayerSession.Find(Scene)?.IsHeist==true;
        FeedbackText=knocked ? heist ? (received ? "KNOCKED DOWN" : "KNOCKDOWN")
            : (received ? "KNOCKED DOWN -2" : "KNOCKDOWN +2") : (received ? "CONTACT" : "GUN BUTT HIT");
        FeedbackRemaining=1.1f;
        if(MenuOpen || !Scene.Camera.IsValid())return;
        // Native render-only effects leave the crosshair's logical aim untouched.
        // A confirmed knockdown has a heavier, short kick than an ordinary shield/body strike.
        // Apply once on the affected owner's camera, at the host-confirmed impact.
        CameraImpulses++;
        LastShakeAmplitude=knocked ? (received ? 2.4f : 1.6f) : (received ? .85f : .55f);
        Scene.Camera.AddShake(LastShakeAmplitude,32f,knocked ? .28f : .16f);
        Scene.Camera.AddPunch(new Angles(received ? (knocked ? 4f : 1f) : (knocked ? -2.7f : -.65f),0,
            received ? (knocked ? -1.4f : -.4f) : (knocked ? .8f : .25f)),1f,knocked ? .32f : .18f,0f);
    }
    protected override void OnFixedUpdate()
    {
        if(IsProxy)return;
        if(Attacking && stepApplied<stepDistance)
        {
            float desired=stepDistance*Ease((AttackDuration-attackTime)/ImpactTime);
            var stepTarget=WorldPosition+stepDirection*(desired-stepApplied);
            var bounds=new BBox(new Vector3(-14,-14,4),new Vector3(14,14,62));
            var move=Scene.Trace.Box(bounds,WorldPosition,stepTarget).IgnoreGameObjectHierarchy(GameObject).WithoutTags("paintball_debris").Run();
            var ground=Scene.Trace.Ray(stepTarget+Vector3.Up*12,stepTarget-Vector3.Up*16).IgnoreGameObjectHierarchy(GameObject).Run();
            if(!move.Hit && ground.Hit)ApplyContactPosition(stepTarget);
            stepApplied=desired;
        }
        if(push.Length<1)return;
        var to=WorldPosition+push*Time.Delta;
        var trace=Scene.Trace.Sphere(15,WorldPosition+Vector3.Up*22,to+Vector3.Up*22).IgnoreGameObjectHierarchy(GameObject).Run();
        if(!trace.Hit)ApplyContactPosition(to);
        push*=System.MathF.Exp(-7*Time.Delta);
    }
    private void ApplyContactPosition(Vector3 position)
    {
        WorldPosition=position;
        // Since 26.09.15, moving a GameObject no longer moves its simulated
        // navigation agent. Keep an AI's lunge/knockback from snapping back.
        var agent=Components.Get<NavMeshAgent>();
        if(agent.IsValid() && agent.UpdatePosition)agent.SetAgentPosition(position);
    }
    public bool RegisterRecoveryPress()
    {
        if(MultiplayerSession.Online && !MultiplayerSession.Authority)
        {
            if(IsProxy || MenuOpen || !ReadyToRecover)return false;
            Components.Get<NetworkPawn>()?.RequestRecovery();return true;
        }
        return RegisterRecoveryAuthoritative();
    }
    internal bool RegisterRecoveryAuthoritative()
    {
        if(MenuOpen || !ReadyToRecover || (RecoveryPresses>0 && pressGap<.055f))return false;
        if(pressGap>RecoveryPressGap)RecoveryPresses=0;
        RecoveryPresses++;pressGap=0;
        if(RecoveryPresses>=RequiredRecoveryPresses)GetUp();
        return true;
    }
    public bool GetUp()
    {
        if(!ReadyToRecover || RecoveryPresses<RequiredRecoveryPresses)return false;
        Downed=false;recovery=GetUpDuration;push=Vector3.Zero;return true;
    }
    private void HoldMovement()
    {
        if(!player.IsValid() || IsProxy)return;
        if(!movementHeld){inputBefore=player.UseInputControls;movementHeld=true;}
        player.UseInputControls=false;player.WishVelocity=Vector3.Zero;
        if(player.Body.IsValid())player.Body.Velocity=player.Body.Velocity.WithX(0).WithY(0);
    }
    private void RestoreMovement()
    {
        if(player.IsValid() && movementHeld)
        {
            player.UpdateDucking(false);
            player.UseInputControls=inputBefore && Components.Get<PaintballMarker>()?.AcceptInput!=false;
            player.WishVelocity=Vector3.Zero;
        }
        movementHeld=false;
    }
    public void ResetState()
    {
        RestoreMovement();Downed=false;
        recovery=attackTime=downTime=cooldown=knockImmunity=pressGap=bufferedPress=requestWait=FeedbackRemaining=0;RecoveryPresses=0;
        FeedbackText="";
        primaryAction=PrimaryAction.None;
        stepDistance=stepApplied=0;
        impactPending=false;pendingTarget=null;push=Vector3.Zero;
    }
    private sealed class ContactSnapshot
    {
        public bool Down {get;set;}
        public float Attack {get;set;}
        public float Fall {get;set;}
        public float Recovery {get;set;}
        public int Presses {get;set;}
        public Vector3 Push {get;set;}
        public Rotation Facing {get;set;}
        public Vector3 Step {get;set;}
        public float Distance {get;set;}
        public int HitSequence {get;set;}
    }
    private string lastNetworkState;
    internal string CaptureNetworkState()=>Json.Serialize(new ContactSnapshot{Down=Downed,Attack=attackTime,Fall=downTime,Recovery=recovery,
        Presses=RecoveryPresses,Push=push,Facing=motionFacing,Step=stepDirection,Distance=stepDistance,HitSequence=receivedContacts});
    internal void ApplyNetworkState(string json)
    {
        if(string.IsNullOrEmpty(json) || json==lastNetworkState)return;
        var state=Json.Deserialize<ContactSnapshot>(json);lastNetworkState=json;
        if(lastReceivedContacts!=state.HitSequence){lastReceivedContacts=state.HitSequence;push=state.Push;}
        if(state.Attack>0 && !Attacking)stepApplied=0;
        if(state.Down && !Downed){push=state.Push;if(!IsProxy){Components.Get<CoverController>()?.Leave();Components.Get<VaultController>()?.Cancel();}}
        Downed=state.Down;attackTime=state.Attack;downTime=state.Fall;recovery=state.Recovery;
        RecoveryPresses=state.Presses;motionFacing=state.Facing;stepDirection=state.Step;stepDistance=state.Distance;
        impactPending=false;
    }
    private static float Ease(float t){t=t.Clamp(0,1);return t*t*(3-2*t);}
    public void ApplyPresentation(SkinnedModelRenderer body,CitizenAnimationHelper animation,GameObject marker)
    {
        animation.Sitting=CitizenAnimationHelper.SittingStyle.None;
        float elapsed=AttackDuration-attackTime;
        // Frame 16 drives the rear of the marker into contact. Keep the legacy
        // graph channel name so existing network snapshots and rigs stay compatible.
        const float strikePhase=16f/30f;
        const float windup=.045f,impactHold=.035f;
        // Brief anticipation, a fast rear-first drive, then a short contact hold.
        float attackPhase=elapsed<windup ? elapsed/windup*.25f
            : elapsed<ImpactTime ? .25f+(elapsed-windup)/(ImpactTime-windup)*(strikePhase-.25f)
            : elapsed<ImpactTime+impactHold ? strikePhase
            : strikePhase+(elapsed-ImpactTime-impactHold)/(AttackDuration-ImpactTime-impactHold)*(1-strikePhase);
        float recoverPhase=GetUpProgress;
        float fallWeight=Incapacitated ? Ease(downTime/.18f)*(Recovering ? Ease(recovery/.22f) : 1) : 0;
        body.Set("pb_elbow_phase",attackPhase);
        body.Set("pb_elbow_weight",Attacking ? Ease((AttackDuration-attackTime)/.09f)*Ease(attackTime/.12f) : 0);
        body.Set("pb_fall_phase",FallProgress);body.Set("pb_fall_weight",fallWeight);
        body.Set("pb_getup_phase",recoverPhase);body.Set("pb_getup_weight",Recovering ? Ease(recoverPhase/.18f)*Ease(recovery/.16f) : 0);
        marker.Enabled=!Incapacitated;
        if(!Incapacitated && !Attacking)return;
        body.WorldRotation=motionFacing;
        animation.IkLeftHand=animation.IkRightHand=null;animation.HoldType=CitizenAnimationHelper.HoldTypes.None;
        // Native procedural look/turn can otherwise bend the downed player
        // toward a freely moving camera after the full-body fall pose is mixed.
        animation.WithLook(motionFacing.Forward,0,0,0);animation.MoveRotationSpeed=0;
        animation.Handedness=CitizenAnimationHelper.Hand.Both;animation.IsGrounded=true;
        body.Set("pb_grip_weight",0f);body.Set("pb_left_grip",0f);body.Set("pb_shield_carry",0f);
        animation.WithVelocity(Vector3.Zero);animation.WithWishVelocity(Vector3.Zero);animation.DuckLevel=0;
    }
    protected override void OnDisabled()=>ResetState();
    protected override void OnDestroy()=>ResetState();
}

