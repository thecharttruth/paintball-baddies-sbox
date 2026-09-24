using Sandbox;
using Sandbox.Citizen;

namespace PaintballBaddies;

/// <summary>Opt-in native opponent presentation driven by existing AI movement and magazine state.</summary>
public sealed class CitizenOpponentPresentation : Component
{
    [Property] public string ModelPath { get; set; } = "models/characters/citizen_out_signal_review/character.vmdl";
    [Property] public string OutfitPath { get; set; } = "models/gear/citizen_curvy_suit_review/suit.vmdl";
    [Property] public bool UseRosterSuit { get; set; } = true;
    [Property] public string GlovesPath { get; set; } = "models/gear/citizen_female_gloves/gloves.vmdl";
    [Property] public string BootsPath { get; set; } = "models/citizen_clothes/shoes/boots/models/boots_m_human.vmdl";
    private ArenaOpponent opponent;
    private OpponentMagazine weapon;
    [Property] public string HelmetPath { get; set; } = "models/gear/citizen_closed_helmet_review/helmet.vmdl";
    private GameObject subject, marker, support, previousAnchor;
    private SkinnedModelRenderer body, previousBody;
    private bool previousVisible;
    private CitizenLocomotion locomotion;
    private CitizenMarkerGrip grip;
    private CitizenPodReload pod;
    private float shieldPoseWeight;
    private CharacterEquipment equipment;
    public string EquippedSuit => equipment?.Outfit?.Model?.Name;
    public string EquippedHelmet => equipment?.Helmet?.Model?.Name;
    public bool ProtectiveGearReady => equipment.IsValid()
        && GearReady(equipment.Outfit) && GearReady(equipment.Helmet)
        && GearReady(equipment.Gloves) && GearReady(equipment.Boots);
    private bool GearReady(SkinnedModelRenderer piece) => piece.IsValid()
        && piece.Model.IsValid() && piece.Enabled && piece.GameObject.Active
        && piece.BoneMergeTarget==body;
    public bool VisualActive => subject.IsValid() && subject.Active;
    private readonly CitizenReloadClock clock = new();
    private bool reloading, cancelled, gesture;
    private float time, posture;
    private float outTime;
    private float stationaryAimTime;
    public float HeadHeight => body.IsValid() && body.TryGetBoneTransform("head",out var head) ? head.Position.z-subject.WorldPosition.z : -1;
    public float AnimationSpeed { get; private set; }
    public float PeakAnimationSpeed { get; private set; }
    public bool ShowingOutSignal => outTime > 0 && outTime < 1.5f;
    public float OutSignalWeight { get; private set; }
    public float SignalHandAboveHead => body.IsValid()
        && body.TryGetBoneTransform("hand_R",out var hand)
        && body.TryGetBoneTransform("head",out var head) ? hand.Position.z-head.Position.z : -999;
    private Transform cancelHand;
    private Rotation cancelPod;
    public bool IsReady => body.IsValid() && body.Model.IsValid();
    public SkinnedModelRenderer FootstepRenderer => body;
    public bool PodHeld => pod.IsValid() && pod.podHeld;
    public bool ReloadPropsVisible => pod.IsValid() && pod.HasActiveReloadProps;

    protected override void OnStart()
    {
        TryInitialize();
    }

    private void TryInitialize()
    {
        if (subject.IsValid()) return;
        opponent = Components.Get<ArenaOpponent>();
        if (!opponent.IsValid() || !opponent.Body.IsValid()) return;
        weapon = opponent.Magazine;
        previousBody = opponent.Body;
        previousVisible = previousBody.IsValid() && previousBody.Enabled;
        subject = new GameObject(GameObject) { Name = "Citizen opponent presentation", NetworkMode=NetworkMode.Never };
        subject.WorldPosition = opponent.WorldPosition;
        subject.WorldRotation = opponent.WorldRotation;
        body = subject.Components.Create<SkinnedModelRenderer>();
        body.Model = Model.Load(ModelPath);
        body.UseAnimGraph = true;
        equipment=subject.Components.Create<CharacterEquipment>();
        equipment.Configure(body,
            UseRosterSuit ? CitizenRosterAssets.Suit(opponent.CharacterIndex) : OutfitPath,
            GlovesPath, BootsPath, UseRosterSuit ? CitizenRosterAssets.Helmet(opponent.CharacterIndex) : HelmetPath);
        subject.Components.Create<CitizenSuitFollowThrough>().Configure(body,equipment.Outfit);
        locomotion = subject.Components.Create<CitizenLocomotion>();
        locomotion.Configure(body, CitizenAnimationHelper.HoldTypes.Rifle);
        grip = subject.Components.Create<CitizenMarkerGrip>();
        pod = subject.Components.Create<CitizenPodReload>();
        pod.CreateGear();
        marker = new GameObject(subject) { Name = "VX-9 native opponent presentation" };
        marker.Components.Create<ModelRenderer>().Model = Model.Load("models/weapons/vx9_speed_feed/vx9_speed_feed.vmdl");
        support = new GameObject(subject) { Name = "Marker support hand" };
        previousAnchor = opponent.PresentationAnchor;
        opponent.PresentationAnchor = marker;
        weapon.ReloadStarted += StartReload;
        weapon.ReloadCancelled += CancelReload;
        if (previousBody.IsValid()) previousBody.Enabled = false;
        if (weapon.ReloadRemaining > 0) StartReload();
    }

    private void StartReload()
    {
        reloading=true;cancelled=false;gesture=false;time=.8f;
        clock.Reset();grip.ResetRecovery();pod.ResetCycle();
    }

    private void CancelReload()
    {
        if (!reloading || cancelled || !body.IsValid()) return;
        cancelled=true;
        if (body.TryGetBoneTransform("hand_L",out var hand)) cancelHand=subject.WorldTransform.ToLocal(hand);
        if (pod.refillPod.IsValid()) cancelPod=subject.WorldRotation.Inverse*pod.refillPod.WorldRotation;
    }

    protected override void OnUpdate()
    {
        // Scene components can start before ArenaOpponent creates its renderer.
        if (!subject.IsValid()) TryInitialize();
        if (!IsReady || !opponent.IsValid() || weapon is null) return;
        if (previousBody.IsValid()) previousBody.Enabled=false;
        var heldShield=Scene.GetAllComponents<ArenaShield>().FirstOrDefault(x=>x.Owner.IsValid()&&x.Owner.GameObject==GameObject);
        bool shieldActive=heldShield is not null && !heldShield.Stowed;
        shieldPoseWeight+=((shieldActive ? 1 : 0)-shieldPoseWeight)*(1-System.MathF.Exp(-12*Time.Delta));
        body.Set("pb_shield_carry",shieldPoseWeight);
        locomotion.Animation.HoldType=!shieldActive ? CitizenAnimationHelper.HoldTypes.Rifle : CitizenAnimationHelper.HoldTypes.Pistol;
        locomotion.Animation.Handedness=!shieldActive ? CitizenAnimationHelper.Hand.Both : CitizenAnimationHelper.Hand.Right;
        var eliminated=opponent.Components.Get<PaintballCombatant>()?.Eliminated==true;
        if(eliminated)
        {
            outTime+=Time.Delta;
            subject.Enabled=ShowingOutSignal;
            marker.Enabled=false;
            pod.DropHeldPod(opponent.NavigationVelocity*.5f);
            pod.StopPresentation();
            pod.Enabled=false;
            locomotion.Animation.IkLeftHand=null;
            locomotion.Animation.IkRightHand=null;
            locomotion.UpdateMotion(Vector3.Zero,Vector3.Zero,true,opponent.CoverCrouched ? 1 : 0,
                0,subject.WorldRotation,0,true,Time.Delta);
            grip.UpdateGrip(body,0,true,Time.Delta);
            var progress=(outTime/.45f).Clamp(0,1);
            OutSignalWeight=progress*progress*(3-2*progress);
            body.Set("pb_out_weight",OutSignalWeight);
            return;
        }
        var sheltered=opponent.UsingCover;
        var duck=opponent.CoverCrouched ? 1f : 0f;
        var facing=opponent.WorldRotation;
        subject.WorldRotation=Rotation.Slerp(subject.WorldRotation,facing,1-System.MathF.Exp(-9*Time.Delta));
        if (reloading) time=clock.Sample(time,true,cancelled,grip.Returning,pod.podHeld,weapon.ReloadRemaining,(1-weapon.ReloadRemaining/weapon.ActiveReloadDuration),Time.Delta);
        var release=reloading && time>=.8f && time<3.2f;
        // Native agent velocity excludes position corrections and avoids measuring
        // movement against a child that already follows the parent this frame.
        var velocity=opponent.NavigationVelocity;
        AnimationSpeed=velocity.WithZ(0).Length;
        PeakAnimationSpeed=System.MathF.Max(PeakAnimationSpeed,AnimationSpeed);
        var look=Rotation.LookAt(opponent.CombatAimPoint-(subject.WorldPosition+Vector3.Up*(sheltered ? 35 : 60)));
        // While following a route, look with the body instead of craning all
        // the way back toward a threat that is no longer visible.
        if(velocity.WithZ(0).Length>30 || (!opponent.SeesPlayer&&!opponent.CoverPeeking))look=facing;
        stationaryAimTime=velocity.WithZ(0).Length>20 || System.MathF.Abs(locomotion.RotationSpeed)>12 ? 0 : stationaryAimTime+Time.Delta;
        var torsoAim=((stationaryAimTime-.12f)/.18f).Clamp(0,1);
        locomotion.UpdateMotion(velocity,opponent.NavigationWishVelocity,true,duck,0,Rotation.FromYaw(look.Angles().yaw),
            release || sheltered ? 0 : look.Angles().pitch,sheltered,Time.Delta,torsoAim,rootPosition:opponent.WorldPosition);
        var recovery=((time-3.2f)/.6f).Clamp(0,1);
        recovery=recovery*recovery*(3-2*recovery);
        posture+=((reloading ? duck*(1-recovery) : 0)-posture)*(1-System.MathF.Exp(-24*Time.Delta));
        body.Set("pb_reload_posture",posture);
        grip.UpdateGrip(body,1,release,Time.Delta);
        if (reloading && !cancelled && !gesture && time>=1) { body.Set("b_reload",true);gesture=true; }
        if (grip.Attach(body,marker,CitizenMarkerGrip.MarkerOffset,Angles.Zero))
        {
            grip.UpdateSupport(body,locomotion.Animation,marker,support,new Vector3(1.5f,1,0),release,
                reloading && time>=3.2f ? (time-3.2f)/.4f : -1);
            pod.Apply(body,locomotion.Animation,marker,support,grip.SupportRotation,grip.Grip,time,
                duck,release,cancelled,clock.CancellationElapsed,cancelHand,cancelPod,Time.Delta);
        }
        subject.WorldPosition=opponent.WorldPosition;
        Components.Get<CloseCombat>()?.ApplyPresentation(body,locomotion.Animation,marker);
        pod.SetCarryVisible(!weapon.UnlimitedAmmo || reloading);
        heldShield?.ApplyGrip(body,locomotion.Animation);
        if (reloading)
        {
            time+=Time.Delta;
            if (time>=4.5f && !pod.podHeld) reloading=false;
        }
    }

    protected override void OnDestroy()
    {
        if (weapon is not null)
        {
            weapon.ReloadStarted-=StartReload;weapon.ReloadCancelled-=CancelReload;
            if (opponent.IsValid() && opponent.PresentationAnchor==marker) opponent.PresentationAnchor=previousAnchor;
        }
        if (previousBody.IsValid()) previousBody.Enabled=previousVisible
            && opponent.IsValid() && opponent.Components.Get<PaintballCombatant>()?.Eliminated!=true;
        if (subject.IsValid()) subject.Destroy();
    }
}
