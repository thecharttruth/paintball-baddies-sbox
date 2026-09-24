using Sandbox;
using Sandbox.Citizen;

namespace PaintballBaddies;

/// <summary>Opt-in native character presentation driven by existing gameplay components.</summary>
public sealed class CitizenPlayerPresentation : Component
{
    [Property] public string ModelPath { get; set; } = "models/characters/viper_citizen_neck_review/viper_graph.vmdl";
    [Property] public string OutfitPath { get; set; } = "models/gear/citizen_curvy_suit_review/suit.vmdl";
    [Property] public bool UseRosterSuit { get; set; } = true;
    private float shieldPoseWeight;
    private CharacterEquipment equipment;
    private int outfitIndex=-1;
    public string EquippedSuit => equipment?.Outfit?.Model?.Name;
    public string EquippedHelmet => equipment?.Helmet?.Model?.Name;
    [Property] public string GlovesPath { get; set; } = "models/gear/citizen_female_gloves/gloves.vmdl";
    [Property] public string BootsPath { get; set; } = "models/citizen_clothes/shoes/boots/models/boots_m_human.vmdl";
    private PlayerController player;
    private PaintballMarker weapon;
    private CoverController cover;
    private VaultController vault;
    [Property] public string HelmetPath { get; set; } = "models/gear/citizen_closed_helmet_review/helmet.vmdl";
    private GameObject subject, marker, support, previousAnchor;
    private SkinnedModelRenderer body, previousBody;
    private bool previousVisible;
    private CitizenLocomotion locomotion;
    private CitizenMarkerGrip grip;
    private CitizenPodReload pod;
    private readonly CitizenReloadClock clock = new();
    private bool reloading, cancelled, gesture;
    private float time, posture;
    private GameObject raisedHand;
    private Transform coverHand;
    private bool coverHandCaptured;
    private float coverAimSettled;
    private ArenaShield previousShield;
    private float holdTransition;
    private bool previousCoverPeek;
    public float CoverGunLift { get; private set; }
    private Transform cancelHand;
    private Rotation cancelPod;
    public bool IsReady => body.IsValid() && body.Model.IsValid();
    public SkinnedModelRenderer FootstepRenderer => body;
    public bool PodHeld => pod.IsValid() && pod.podHeld;

    protected override void OnStart()
    {
        player = Components.Get<PlayerController>();
        weapon = Components.Get<PaintballMarker>();
        cover = Components.Get<CoverController>();
        vault = Components.Get<VaultController>();
        if (!player.IsValid() || !weapon.IsValid()) return;
        previousBody = player.Renderer;
        previousVisible = previousBody.IsValid() && previousBody.Enabled;
        subject = new GameObject(GameObject) { NetworkMode=NetworkMode.Never, Name = "Citizen player presentation" };
        subject.WorldPosition = player.WorldPosition;
        subject.WorldRotation = Rotation.FromYaw(player.EyeAngles.yaw);
        body = subject.Components.Create<SkinnedModelRenderer>();
        body.Model = Model.Load(ModelPath);
        body.UseAnimGraph = true;
        equipment=subject.Components.Create<CharacterEquipment>();
        var roster=Components.Get<RosterSelection>();
        outfitIndex=UseRosterSuit && roster.IsValid() ? roster.Selected : -1;
        equipment.Configure(body,
            outfitIndex>=0 ? CitizenRosterAssets.Suit(outfitIndex) : OutfitPath,
            GlovesPath, BootsPath,
            outfitIndex>=0 ? CitizenRosterAssets.Helmet(outfitIndex) : HelmetPath);
        subject.Components.Create<CitizenSuitFollowThrough>().Configure(body,equipment.Outfit);
        locomotion = subject.Components.Create<CitizenLocomotion>();
        locomotion.Configure(body, CitizenAnimationHelper.HoldTypes.Rifle);
        grip = subject.Components.Create<CitizenMarkerGrip>();
        pod = subject.Components.Create<CitizenPodReload>();
        pod.CreateGear();
        marker = new GameObject(subject) { Name = "VX-9 native player presentation" };
        marker.Components.Create<ModelRenderer>().Model = Model.Load("models/weapons/vx9_speed_feed/vx9_speed_feed.vmdl");
        support = new GameObject(subject) { Name = "Marker support hand" };
        previousAnchor = weapon.PresentationAnchor;
        weapon.PresentationAnchor = marker;
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
        if (!IsReady || !player.IsValid() || !weapon.IsValid()) return;
        if(subject.Parent!=GameObject)subject.Parent=GameObject;
        if(UseRosterSuit && Components.Get<RosterSelection>() is { } roster && outfitIndex!=roster.Selected)
        {
            var model=Model.Load(CitizenRosterAssets.Suit(roster.Selected));
            if(model.IsValid() && equipment.Outfit.IsValid())
            {
                var helmet=Model.Load(CitizenRosterAssets.Helmet(roster.Selected));
                if(helmet.IsValid() && equipment.Helmet.IsValid())
                { equipment.Outfit.Model=model;equipment.Helmet.Model=helmet;outfitIndex=roster.Selected; }
            }
        }
        if (previousBody.IsValid()) previousBody.Enabled=false;
        var heldShield=Scene.GetAllComponents<ArenaShield>().FirstOrDefault(x=>x.Owner.IsValid()&&x.Owner.GameObject==GameObject);
        if(heldShield!=previousShield){previousShield=heldShield;holdTransition=.4f;}
        holdTransition=System.MathF.Max(0,holdTransition-Time.Delta);
        bool shieldActive=heldShield is not null && !heldShield.Stowed;
        shieldPoseWeight+=((shieldActive ? 1 : 0)-shieldPoseWeight)*(1-System.MathF.Exp(-12*Time.Delta));
        body.Set("pb_shield_carry",shieldPoseWeight);
        bool coverPeek=cover is {Attached:true,Peeking:true};
        if(coverPeek!=previousCoverPeek){previousCoverPeek=coverPeek;coverHandCaptured=false;coverAimSettled=0;}
        bool oneHanded=shieldActive;
        // Keep native two-handed rifle contacts through cover/peek instead of
        // swapping to a generic object-carry pose and waiting for another blend.
        locomotion.Animation.HoldType=shieldActive ? CitizenAnimationHelper.HoldTypes.Pistol : CitizenAnimationHelper.HoldTypes.Rifle;
        locomotion.Animation.Handedness=oneHanded ? CitizenAnimationHelper.Hand.Right : CitizenAnimationHelper.Hand.Both;
        var sheltered=cover is { Attached:true, Peeking:false };
        var vaulting=vault?.IsVaulting==true;
        var duck=vaulting || player.IsDucking ? 1f : 0f;
        body.Set("special_movement_states",cover?.Sliding==true ? 3 : 0);
        var facing=cover?.Sliding==true ? Rotation.LookAt(cover.SlideDirection) : sheltered ? Rotation.LookAt(cover.LowCover ? -cover.Normal : cover.Normal) : Rotation.FromYaw(player.EyeAngles.yaw);
        // The view can reverse instantly, but the body needs a short pivot.
        // Bound its angular speed so native aim springs and feet can follow.
        var currentYaw=subject.WorldRotation.Angles().yaw;
        var yawStep=System.MathF.IEEERemainder(facing.Angles().yaw-currentYaw,360)*(1-System.MathF.Exp(-9*Time.Delta));
        subject.WorldRotation=Rotation.FromYaw(currentYaw+yawStep.Clamp(-360*Time.Delta,360*Time.Delta));
        if (reloading) time=clock.Sample(time,true,cancelled,grip.Returning,pod.podHeld,weapon.ReloadRemaining,weapon.ReloadProgress,Time.Delta);
        var release=reloading && time>=.8f && time<3.2f;
        var velocity=vaulting ? (player.WorldPosition-subject.WorldPosition)/System.MathF.Max(Time.Delta,.001f) : (IsProxy ? Components.Get<NetworkPawn>()?.Motion ?? player.Velocity : player.Velocity);
        locomotion.UpdateVault(vaulting,vault?.Progress ?? 0,Time.Delta);
        locomotion.UpdateMotion(velocity,IsProxy ? velocity : player.WishVelocity,((IsProxy ? Components.Get<NetworkPawn>()?.Grounded==true : player.IsOnGround) || cover?.Sliding==true || sheltered) && !vaulting,duck,0,
            sheltered ? subject.WorldRotation : Rotation.FromYaw(player.EyeAngles.yaw),
            release || sheltered ? 0 : player.EyeAngles.pitch,sheltered,Time.Delta,rootPosition:player.WorldPosition);
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
            if(!reloading)UpdateCoverGunLift(vaulting || holdTransition>0 || shieldActive);
            heldShield?.ApplyGrip(body,locomotion.Animation);
        }
        subject.WorldPosition=player.WorldPosition;
        Components.Get<CloseCombat>()?.ApplyPresentation(body,locomotion.Animation,marker);
        pod.SetCarryVisible(!weapon.UnlimitedAmmo || reloading);
        if (reloading)
        {
            time+=Time.Delta;
            if (time>=4.5f && !pod.podHeld) reloading=false;
        }
    }

    private void UpdateCoverGunLift(bool busy)
    {
        var bodyYaw=subject.WorldRotation.Angles().yaw;
        float yawGap=System.MathF.Abs(System.MathF.IEEERemainder(player.EyeAngles.yaw-bodyYaw,360));
        bool eligible=!busy && yawGap<=45 && (weapon.Aiming || cover is { Attached:true, Peeking:true }) && cover is not {Attached:true,Peeking:false};
        if(!eligible)
        {
            CoverGunLift=0;coverHandCaptured=false;coverAimSettled=0;
            locomotion.Animation.IkRightHand=null;
            return;
        }
        // Follow Citizen's bounded aim frame. The camera can reverse immediately,
        // but dragging a hand through that reversal pulls the arm behind the torso.
        var aim=Rotation.FromYaw(bodyYaw+locomotion.AimYaw)*Rotation.FromPitch(locomotion.AimPitch);
        var frame=new Transform(player.WorldPosition,aim);
        if(!coverHandCaptured)
        {
            // Capture the native rifle pose after the pivot has caught up, rather
            // than preserving a transient turn pose for the rest of the aim hold.
            coverAimSettled=yawGap<=12 ? coverAimSettled+Time.Delta : 0;
            locomotion.Animation.IkRightHand=null;
            if(coverAimSettled<.08f)return;
            if(!body.TryGetBoneTransform("hand_R",out var hand))return;
            coverHand=frame.ToLocal(hand);coverHandCaptured=true;
        }
        var baseHand=frame.ToWorld(coverHand);
        var muzzle=weapon.Muzzle-Vector3.Up*CoverGunLift;
        float desired=0;
        // Only lift enough to clear nearby cover. Keep the original shoulder pose
        // and let native Citizen arm IK retain elbows and the animated grip.
        float minimum=(player.EyePosition.z-5-muzzle.z).Clamp(0,12);
        for(float lift=minimum;lift<=12;lift+=2)
        {
            var from=muzzle+Vector3.Up*lift;
            var barrel=Scene.Trace.Sphere(.7f,from,from+aim.Forward*80)
                .IgnoreGameObjectHierarchy(GameObject).WithoutTags("paintball_debris").WithSurfaceMeshes().Run();
            var reach=Scene.Trace.Sphere(.7f,player.EyePosition,from)
                .IgnoreGameObjectHierarchy(GameObject).WithoutTags("paintball_debris").WithSurfaceMeshes().Run();
            if(!barrel.Hit && !reach.Hit){desired=lift;break;}
        }
        CoverGunLift+=(desired-CoverGunLift)*(1-System.MathF.Exp(-10*Time.Delta));
        if(!raisedHand.IsValid())raisedHand=new GameObject(subject){Name="Cover raised marker hand"};
        raisedHand.WorldPosition=baseHand.Position+Vector3.Up*CoverGunLift;
        raisedHand.WorldRotation=baseHand.Rotation;
        locomotion.Animation.IkRightHand=raisedHand;
    }

    protected override void OnDestroy()
    {
        if (weapon.IsValid())
        {
            weapon.ReloadStarted-=StartReload;weapon.ReloadCancelled-=CancelReload;
            if (weapon.PresentationAnchor==marker) weapon.PresentationAnchor=previousAnchor;
        }
        if (previousBody.IsValid()) previousBody.Enabled=previousVisible;
        if (subject.IsValid()) subject.Destroy();
    }
}
