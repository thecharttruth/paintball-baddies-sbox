using Sandbox;
using Sandbox.Citizen;

namespace PaintballBaddies;

/// <summary>Opt-in native baseline for the built-in weapon animation graph.</summary>
public sealed class CitizenRiflePreview : Component
{
    [Property] public string CaptureId { get; set; } = "";
    [Property] public string ModelPath { get; set; } = "models/citizen/citizen.vmdl";
    [Property] public float Speed { get; set; }
    [Property] public float StopMovementAfter { get; set; }
    [Property] public bool TranslatePreview { get; set; }
    [Property] public float TurnSpeed { get; set; }
    [Property] public bool InteractiveControls { get; set; }
    [Property] public bool ExerciseInteractiveReload { get; set; }
    private bool exercisedInteractiveReload;
    [Property] public bool ExerciseInteractiveCover { get; set; }
    [Property] public bool HighCoverExercise { get; set; }
    [Property] public bool OppositeCoverEdge { get; set; }
    [Property] public bool BlockCoverCorner { get; set; }
    private GameObject cornerObstruction;
    [Property] public bool ExerciseInteractiveVault { get; set; }
    [Property] public bool UseVaultGraph { get; set; }
    private float vaultBlend;
    [Property] public string VaultSequence { get; set; } = "";
    [Property] public string ReviewSequence { get; set; } = "";
    [Property] public float ReviewSequencePhase { get; set; } = .5f;
    [Property] public float OutSignalWeight { get; set; }
    private bool citizenWasVaulting, vaultExerciseStarted;
    private int vaultReportStage;
    private int vaultMotionSamples, vaultSlabSamples;
    private float vaultMinAnkleClearance = float.MaxValue, vaultMaxLocalFootStep, vaultStepTime, vaultStepDelta;
    private Vector3 vaultPreviousLeft, vaultPreviousRight;
    private bool vaultHasPreviousFeet;
    private CoverController interactiveCover;
    private GameObject coverFixture;
    private int coverExerciseStage, coverReportStage;
    private bool sheltered;
    [Property] public bool ControllerMovement { get; set; }
    [Property] public bool WeaponReloadClock { get; set; }
    [Property] public float CancelReloadAtPhase { get; set; } = -1;
    [Property] public bool CancelByEndRound { get; set; }
    private int cancellationEvents;
    private bool reloadCancelled;
    private readonly CitizenReloadClock reloadClock = new();
    private float cancelElapsed => reloadClock.CancellationElapsed;
    private Transform cancelHand;
    private Rotation cancelPodRotation;
    private PaintballMarker reviewWeapon;
    private bool weaponInputBefore, weaponReloadRequested;
    private int ammoBeforeReload, reserveBeforeReload;
    private bool shotAccepted;
    private float shotMuzzleError=-1;
    private int shotProjectiles;
    private GameObject previousWeaponAnchor;
    private PlayerController movementController;
    private readonly List<ModelRenderer> hiddenRenderers = new();
    [Property] public float DuckLevel { get; set; }
    [Property] public bool ShowMarker { get; set; }
    [Property] public Vector3 MarkerOffset { get; set; }
    [Property] public Angles MarkerAngles { get; set; }
    [Property] public bool SupportIk { get; set; }
    [Property] public Vector3 SupportOffset { get; set; } = new( 4, 2, -2 );
    [Property] public bool CloseGrip { get; set; }
    [Property] public bool ClosePod { get; set; }
    [Property] public float AimPitch { get; set; }
    private bool aimReported;
    private CitizenLocomotion locomotion;
    [Property] public float ViewYaw { get; set; }
    [Property] public CitizenAnimationHelper.HoldTypes WeaponHold { get; set; } = CitizenAnimationHelper.HoldTypes.Rifle;
    [Property] public float HandPose { get; set; }
    [Property] public float GripWeight { get; set; } = 1;
    [Property] public bool PreviewReload { get; set; }
    [Property] public bool RepeatReload { get; set; }
    private int reloadCycle=1;
    [Property] public bool PreviewHopperReload { get; set; }
    private CitizenPodReload podReload;
    private bool hopperReported;
    private GameObject refillPod => podReload.refillPod;
    private bool podHeld => podReload.podHeld;
    private bool podReturned => podReload.podReturned;
    private float pickupError => podReload.pickupError;
    private float returnError => podReload.returnError;
    private float minimumReturnGap => podReload.minimumReturnGap;
    private float returnReachDistance => podReload.returnReachDistance;
    private float returnArmLength => podReload.returnArmLength;
    private float maximumPourError => podReload.maximumPourError;
    private float totalPourError => podReload.totalPourError;
    private int pourSamples => podReload.pourSamples;
    private int pourAlignedSamples => podReload.pourAlignedSamples;
    [Property] public bool SuppressReloadGesture { get; set; }
    private bool reloadTriggered;
    private bool reloadMotionReported;
    private Vector3 previousHand;
    private bool hasPreviousHand;
    private float maxReleaseStep, maxReturnStep, maxActionStep;
    private float maxReturnTime;
    private int motionSamples;
    private readonly Dictionary<string,float> baselineBoneHeights = new();
    private readonly Dictionary<string,float> maximumBoneRise = new();
    private float baselineHeadHeight;
    private float maximumReloadHeadHeight = float.MinValue;
    private float minimumReloadHeadHeight = float.MaxValue;
    private CitizenMarkerGrip markerGrip;
    private float currentGrip => markerGrip.Grip;
    private float reloadPosture;
    [Property] public bool ExportHandPose { get; set; }
    [Property] public bool UseSampledGrip { get; set; }
    public sealed class FingerSample
    {
        public string name { get; set; }
        public string hand { get; set; }
        public float[] position { get; set; }
        public float[] rotation { get; set; }
    }
    public sealed class FingerTrack { public FingerSample[] bones { get; set; } }
    private FingerTrack gripTrack;
    private bool handPoseReported;
    private bool gripReported;
    [Property] public bool LibraryGloves { get; set; }
    [Property] public bool SuitSecondaryMotion { get; set; }
    private CitizenSuitFollowThrough suitMotion;
    private bool suitMotionReported;
    private bool suitSettledReported;
    private float suitSettledPeak;
    [Property] public string OutfitModel { get; set; } = "";
    [Property] public string OutfitColorTexture { get; set; } = "";
    [Property] public string BootsModel { get; set; } = "";
    [Property] public string HelmetModel { get; set; } = "";
    [Property] public string GloveModel { get; set; } = "models/citizen_clothes/gloves/rubber_gloves/models/rubber_gloves_black_m_human.vmdl";
    private GameObject subject;
    private GameObject marker;
    private SkinnedModelRenderer body;
    private SkinnedModelRenderer gloveRenderer;
    private float reviewTime;
    private float wallTime;
    private float distanceTraveled;
    private bool reported;
    private bool reviewMenuClosed;
    private GameObject supportTarget;
    private Rotation supportRotation => markerGrip.SupportRotation;
    private bool returnStarted => markerGrip.Returning;
    private CitizenAnimationHelper animation;
    protected override void OnStart()
    {
        if(InteractiveControls)
        {
            ControllerMovement=true;WeaponReloadClock=true;PreviewHopperReload=true;
            ShowMarker=true;SupportIk=true;PreviewReload=false;RepeatReload=false;
        }
        var player = Scene.GetAllComponents<PlayerController>().First();
        movementController=player;
        interactiveCover=player.Components.Get<CoverController>();
        if(WeaponReloadClock)
        {
            reviewWeapon=player.Components.Get<PaintballMarker>();
            weaponInputBefore=reviewWeapon.AcceptInput;
            reviewWeapon.AcceptInput=InteractiveControls;
            if(InteractiveControls) reviewWeapon.ReloadStarted+=OnInteractiveReloadStarted;
            reviewWeapon.ReloadCancelled+=OnWeaponReloadCancelled;
        }
        if(ControllerMovement)
        {
            if(!InteractiveControls)
            {
                player.WorldPosition=new Vector3(-950,-500,2);
                player.EyeAngles=new Angles(0,90,0);
            }
            player.UseLookControls=InteractiveControls;
            foreach(var renderer in Scene.GetAllComponents<ModelRenderer>().Where(x=>x.Enabled && (x==player.Renderer || x.GameObject.Name=="VX-9 training marker")))
            {
                renderer.Enabled=false;hiddenRenderers.Add(renderer);
            }
        }
        player.UseCameraControls = InteractiveControls;
        player.UseInputControls = InteractiveControls;
        subject = new GameObject { Name = "Citizen rifle baseline" };
        // Keep static garment reviews under the arena lighting, alongside motion reviews.
        subject.WorldPosition = ControllerMovement ? player.WorldPosition : new Vector3(-950,-500,2);
        subject.WorldRotation=Rotation.FromYaw(ControllerMovement ? player.EyeAngles.yaw : 90);
        body = subject.Components.Create<SkinnedModelRenderer>();
        body.Model = Model.Load( ModelPath );
        body.UseAnimGraph = true;
        if(UseSampledGrip) gripTrack=Json.Deserialize<FingerTrack>(FileSystem.Mounted.ReadAllText("animations/citizen_hand_grip_review.json"));
        var equipment = subject.Components.Create<CharacterEquipment>();
        equipment.Configure(body, OutfitModel, LibraryGloves ? GloveModel : "", BootsModel, HelmetModel);
        if(SuitSecondaryMotion)
        {
            suitMotion=subject.Components.Create<CitizenSuitFollowThrough>();
            suitMotion.Configure(body,equipment.Outfit);
        }
        gloveRenderer = equipment.Gloves;
        if (equipment.Outfit.IsValid() && !string.IsNullOrWhiteSpace(OutfitColorTexture))
        {
            var material=Material.Load("models/citizen_clothes/shirt/jumpsuit/textures/blue_jumpsuit.vmat").CreateCopy();
            var texture=Texture.Load(OutfitColorTexture);
            var applied=material.Set("g_tColor",texture);
            equipment.Outfit.MaterialOverride=material;
            Log.Info($"CITIZEN_OUTFIT_COLOR applied={applied} valid={texture.IsValid} error={texture.IsError} size={texture.Width}x{texture.Height}");
        }
        locomotion = subject.Components.Create<CitizenLocomotion>();
        locomotion.Configure(body, WeaponHold);
        animation = locomotion.Animation;
        markerGrip = subject.Components.Create<CitizenMarkerGrip>();
        podReload = subject.Components.Create<CitizenPodReload>();
        if ( ShowMarker )
        {
            marker = new GameObject( subject ) { Name = "VX-9 Citizen grip review" };
            marker.Components.Create<ModelRenderer>().Model = Model.Load( "models/weapons/vx9_speed_feed/vx9_speed_feed.vmdl" );
            if(WeaponReloadClock)
            {
                previousWeaponAnchor=reviewWeapon.PresentationAnchor;
                reviewWeapon.PresentationAnchor=marker;
            }
            if (PreviewHopperReload)
            {
                podReload.CreateGear();
            }
            if ( SupportIk ) supportTarget = new GameObject( subject ) { Name = "Citizen support hand IK target" };
        }
        Log.Info( $"CITIZEN_RIFLE_PREVIEW valid={body.Model.IsValid()} graph={body.UseAnimGraph}" );
        if (!string.IsNullOrWhiteSpace(ReviewSequence))
            Log.Info("CITIZEN_SEQUENCE_REVIEW "+Json.Serialize(new {capture_id=CaptureId,sequence=ReviewSequence,available=body.Model.AnimationNames.Any(x=>string.Equals(x,ReviewSequence,System.StringComparison.OrdinalIgnoreCase)),phase=ReviewSequencePhase}));
    }
    protected override void OnUpdate()
    {
        if(!reviewMenuClosed && !string.IsNullOrWhiteSpace(ReviewSequence))
        {
            var controls=Scene.GetAllComponents<PaintballControls>().FirstOrDefault();
            if(controls.IsValid()) { controls.StartPractice();reviewMenuClosed=true; }
        }
        // Also hide late-created production equipment during isolated sequence reviews.
        if(ControllerMovement && !string.IsNullOrWhiteSpace(ReviewSequence))
            foreach(var renderer in Scene.GetAllComponents<ModelRenderer>().Where(x=>x.Enabled && x.GameObject.Name=="VX-9 training marker"))
            { renderer.Enabled=false;if(!hiddenRenderers.Contains(renderer))hiddenRenderers.Add(renderer); }
        if (!string.IsNullOrWhiteSpace(ReviewSequence))
        {
            body.UseAnimGraph=false;
            if(body.Sequence.Name!=ReviewSequence) body.Sequence.Name=ReviewSequence;
            body.Sequence.Looping=false;body.PlaybackRate=0;
            body.Sequence.TimeNormalized=ReviewSequencePhase.Clamp(0,1);
            if(marker.IsValid())markerGrip.Attach(body,marker,MarkerOffset,MarkerAngles);
            var sequenceCamera=Components.Get<CameraComponent>();
            var focus=subject.WorldPosition+Vector3.Up*35;
            sequenceCamera.WorldPosition=focus+subject.WorldRotation*Rotation.FromYaw(ViewYaw)*new Vector3(145,-95,13);
            sequenceCamera.WorldRotation=Rotation.LookAt(focus-sequenceCamera.WorldPosition);
            sequenceCamera.FieldOfView=40;
            return;
        }
        // The production marker may be created after this preview's OnStart.
        if(ControllerMovement)
            foreach(var renderer in Scene.GetAllComponents<ModelRenderer>().Where(x=>x.Enabled && x.GameObject.Name=="VX-9 training marker"))
            { renderer.Enabled=false;if(!hiddenRenderers.Contains(renderer))hiddenRenderers.Add(renderer); }
        wallTime+=Time.Delta;
        if(StopMovementAfter>0 && wallTime>=StopMovementAfter)Speed=0;
        if(InteractiveControls && ExerciseInteractiveReload && !exercisedInteractiveReload && wallTime>.8f)
        {
            exercisedInteractiveReload=true;
            shotAccepted=reviewWeapon.FireAt(marker.WorldPosition+marker.WorldRotation.Forward*1000);
            shotMuzzleError=(reviewWeapon.Muzzle-marker.WorldTransform.PointToWorld(PaintballMarker.MuzzleOffset)).Length;
            shotProjectiles=reviewWeapon.ActivePaintballs;
            reviewWeapon.Reload();
        }
        if(RepeatReload && reviewTime>=5) ResetReloadCycle();
        if(InteractiveControls)
        {
            if(ExerciseInteractiveCover) ExerciseCover();
            sheltered=interactiveCover is { Attached:true, Peeking:false };
            AimPitch=sheltered ? 0 : movementController.EyeAngles.pitch;
            var nativeVault=movementController.Components.Get<VaultController>();
            var vaulting=nativeVault?.IsVaulting==true;
            if(UseVaultGraph)
            {
                locomotion.UpdateVault(vaulting, nativeVault?.Progress ?? 0, Time.Delta);
                vaultBlend=locomotion.VaultWeight;
            }
            else if(!string.IsNullOrWhiteSpace(VaultSequence))
            {
                if(vaulting)
                {
                    body.UseAnimGraph=false;
                    if(body.Sequence.Name!=VaultSequence) body.Sequence.Name=VaultSequence;
                    body.Sequence.Looping=false;body.PlaybackRate=0;
                    body.Sequence.TimeNormalized=nativeVault.Progress;
                }
                else if(citizenWasVaulting) { body.UseAnimGraph=true;body.PlaybackRate=1; }
            }
            else if(vaulting && !citizenWasVaulting) animation.TriggerJump();
            citizenWasVaulting=vaulting;
            DuckLevel=vaulting || movementController.IsDucking ? 1 : 0;
            var facing=sheltered ? Rotation.LookAt(-interactiveCover.Normal) : Rotation.FromYaw(movementController.EyeAngles.yaw);
            subject.WorldRotation=Rotation.Slerp(subject.WorldRotation,facing,1-System.MathF.Exp(-9*Time.Delta));
            animation.IsWeaponLowered=sheltered;
        }
        if(WeaponReloadClock && PreviewReload)
        {
            if(!InteractiveControls && !weaponReloadRequested && reviewTime>=.8f)
            {
                shotAccepted=reviewWeapon.FireAt(marker.WorldPosition+marker.WorldRotation.Forward*1000);
                shotMuzzleError=(reviewWeapon.Muzzle-marker.WorldTransform.PointToWorld(PaintballMarker.MuzzleOffset)).Length;
                shotProjectiles=reviewWeapon.ActivePaintballs;
                ammoBeforeReload=reviewWeapon.Ammo;
                reserveBeforeReload=reviewWeapon.Reserve;
                reviewWeapon.Reload();
                weaponReloadRequested=true;
            }
            if(weaponReloadRequested && !reloadCancelled && CancelReloadAtPhase>=0
                && reviewWeapon.ReloadRemaining>0 && reviewWeapon.ReloadProgress>=CancelReloadAtPhase)
            {
                if(CancelByEndRound) reviewWeapon.EndRound();
                else reviewWeapon.CancelReload();
            }
            reviewTime=reloadClock.Sample(reviewTime, weaponReloadRequested, reloadCancelled,
                returnStarted, podHeld, reviewWeapon.ReloadRemaining, reviewWeapon.ReloadProgress, Time.Delta);
        }
        if(UseSampledGrip)body.ClearPhysicsBones();
        if(UseSampledGrip && gloveRenderer.IsValid())gloveRenderer.ClearPhysicsBones();
        if(ControllerMovement && !InteractiveControls)
        {
            movementController.UpdateDucking(DuckLevel>.5f);
            movementController.WishVelocity=subject.WorldRotation.Forward*Speed;
        }
        bool releaseGrip = PreviewReload && reviewTime >= .8f && reviewTime < 3.2f;
        var lookYaw=InteractiveControls && !sheltered ? Rotation.FromYaw(movementController.EyeAngles.yaw) : subject.WorldRotation;
        var velocity = citizenWasVaulting ? (movementController.WorldPosition-subject.WorldPosition)/System.MathF.Max(Time.Delta,.001f)
            : ControllerMovement ? movementController.Velocity : subject.WorldRotation.Forward*Speed;
        locomotion.UpdateMotion(velocity,
            InteractiveControls ? movementController.WishVelocity : subject.WorldRotation.Forward*Speed,
            ControllerMovement ? movementController.IsOnGround && !citizenWasVaulting : true,
            DuckLevel, TurnSpeed, lookYaw, releaseGrip ? 0 : AimPitch, sheltered, Time.Delta);
        body.Set("holdtype_pose_hand", HandPose);
        body.Set("pb_out_weight", OutSignalWeight);
        var postureRecovery = ((reviewTime-3.2f)/.6f).Clamp(0,1);
        postureRecovery = postureRecovery*postureRecovery*(3-2*postureRecovery);
        var postureTarget = PreviewReload && reviewTime >= .8f ? DuckLevel.Clamp(0,1)*(1-postureRecovery) : 0;
        reloadPosture += (postureTarget-reloadPosture) * (1-System.MathF.Exp(-24*Time.Delta));
        body.Set("pb_reload_posture",reloadPosture);
        markerGrip.UpdateGrip(body, GripWeight, releaseGrip, Time.Delta);
        if (PreviewReload && !SuppressReloadGesture && !reloadCancelled && !reloadTriggered && reviewTime >= 1)
        {
            body.Set("b_reload", true);
            reloadTriggered = true;
        }
        if ( markerGrip.Attach(body, marker, MarkerOffset, MarkerAngles) )
        {
            markerGrip.UpdateSupport(body, animation, marker, supportTarget, SupportOffset, releaseGrip,
                PreviewReload && reviewTime >= 3.2f ? (reviewTime-3.2f)/.4f : -1);
            podReload.Apply(body, animation, marker, supportTarget, supportRotation, currentGrip,
                reviewTime, DuckLevel, releaseGrip, reloadCancelled, cancelElapsed, cancelHand, cancelPodRotation, Time.Delta);
            if (PreviewHopperReload && releaseGrip && !hopperReported && reviewTime>2.2f
                && body.TryGetBoneTransform("hand_L",out var reloadHand))
            {
                hopperReported=true;
                var handTarget=marker.WorldPosition+marker.WorldRotation*new Vector3(.27165f,1.4f,15.19449f);
                var shoulderValid=body.TryGetBoneTransform("arm_upper_L",out var shoulder);
                var elbowValid=body.TryGetBoneTransform("arm_lower_L",out var elbow);
                var armLength=(shoulder.Position-elbow.Position).Length+(elbow.Position-reloadHand.Position).Length;
                Log.Info("CITIZEN_HOPPER_FIT " + Json.Serialize(new { capture_id=CaptureId, time=reviewTime, bones_valid=shoulderValid && elbowValid, arm_length=armLength, shoulder_to_target=(shoulder.Position-handTarget).Length, target_error=(reloadHand.Position-handTarget).Length, hand_local=marker.WorldTransform.PointToLocal(reloadHand.Position), target_local=marker.WorldTransform.PointToLocal(handTarget) }));
            }
            reviewTime += Time.Delta;
            if ( !reported && reviewTime > .5f && body.TryGetBoneTransform("hold_R", out var hold) )
            {
                reported = true;
                Log.Info( $"CITIZEN_GRIP hold={hold.Position} rotation={hold.Rotation.Angles()}" );
                foreach ( var name in new[] { "hand_R", "hand_L", "hold_L" } )
                    if ( body.TryGetBoneTransform( name, out var pose ) )
                        Log.Info( $"CITIZEN_GRIP {name} local={hold.PointToLocal(pose.Position)}" );
            }
        }
        if(InteractiveControls)
        {
            var delta=movementController.WorldPosition-subject.WorldPosition;
            subject.WorldPosition+=delta;
            distanceTraveled+=delta.WithZ(0).Length;
            return;
        }
        var camera = Components.Get<CameraComponent>();
        var cameraFocus=subject.WorldPosition+new Vector3(0,0,35);
        camera.WorldPosition = cameraFocus + subject.WorldRotation * Rotation.FromYaw(ViewYaw) * new Vector3(145,-95,13);
        camera.WorldRotation = Rotation.LookAt( cameraFocus - camera.WorldPosition );
        camera.FieldOfView = 40;
        if ( CloseGrip && marker is not null )
        {
            var focus = ClosePod && refillPod is not null ? refillPod.WorldPosition+refillPod.WorldRotation.Up*4.2f : marker.WorldPosition;
            camera.WorldPosition = focus + subject.WorldRotation * Rotation.FromYaw(ViewYaw) * new Vector3(32,-46,12);
            camera.WorldRotation = Rotation.LookAt( focus - camera.WorldPosition );
            camera.FieldOfView = 32;
        }
        if (TurnSpeed != 0)
        {
            var turn=Rotation.FromYaw(TurnSpeed*Time.Delta);
            var offset=camera.WorldPosition-subject.WorldPosition;
            subject.WorldRotation=turn*subject.WorldRotation;
            camera.WorldPosition=subject.WorldPosition+turn*offset;
            camera.WorldRotation=turn*camera.WorldRotation;
        }
        if (ControllerMovement)
        {
            var delta=movementController.WorldPosition-subject.WorldPosition;
            subject.WorldPosition+=delta;
            camera.WorldPosition+=delta;
            distanceTraveled+=delta.WithZ(0).Length;
        }
        else if (TranslatePreview)
        {
            var delta=subject.WorldRotation.Forward*Speed*Time.Delta;
            subject.WorldPosition+=delta;
            distanceTraveled+=delta.Length;
            camera.WorldPosition+=delta;
        }
    }
    protected override void OnPreRender()
    {
        if(suitMotion.IsValid() && StopMovementAfter>0 && wallTime>StopMovementAfter+1)
        {
            suitSettledPeak=System.MathF.Max(suitSettledPeak,System.MathF.Abs(suitMotion.CurrentWeight));
            if(!suitSettledReported && wallTime>StopMovementAfter+3)
            {
                suitSettledReported=true;
                Log.Info("CITIZEN_SUIT_SETTLED "+Json.Serialize(new {capture_id=CaptureId,
                    ready=suitMotion.MorphsReady,peak=suitSettledPeak,speed=movementController.Velocity.Length}));
            }
        }
        if(suitMotion.IsValid() && !suitMotionReported && wallTime>2)
        {
            suitMotionReported=true;
            Log.Info("CITIZEN_SUIT_MOTION "+Json.Serialize(new {capture_id=CaptureId,
                ready=suitMotion.MorphsReady,weight=suitMotion.CurrentWeight,peak=suitMotion.PeakWeight}));
        }
        if(ExerciseInteractiveVault && wallTime>=1.9f && wallTime<=3.4f &&
            body.TryGetBoneTransform("ankle_L",out var motionLeft) && body.TryGetBoneTransform("ankle_R",out var motionRight))
        {
            var left=motionLeft.Position-subject.WorldPosition;
            var right=motionRight.Position-subject.WorldPosition;
            if(vaultHasPreviousFeet)
            {
                var step=System.MathF.Max((left-vaultPreviousLeft).Length,(right-vaultPreviousRight).Length);
                if(step>vaultMaxLocalFootStep) { vaultMaxLocalFootStep=step;vaultStepTime=wallTime;vaultStepDelta=Time.Delta; }
            }
            vaultPreviousLeft=left;vaultPreviousRight=right;vaultHasPreviousFeet=true;vaultMotionSamples++;
            // Ankle proxy only: the fixture spans x=-560..-540 and is 48 inches high.
            // Include a six-inch margin; this does not prove boot mesh clearance.
            foreach(var foot in new[]{motionLeft.Position,motionRight.Position})
                if(foot.x>=-566 && foot.x<=-534 && foot.y>=-20 && foot.y<=160)
                { vaultSlabSamples++;vaultMinAnkleClearance=System.MathF.Min(vaultMinAnkleClearance,foot.z-48); }
        }
        if(ExerciseInteractiveVault && ((vaultReportStage==0 && wallTime>2.4f) || (vaultReportStage==1 && wallTime>3.4f)))
        {
            var nativeVault=movementController.Components.Get<VaultController>();
            var leftValid=body.TryGetBoneTransform("ankle_L",out var leftFoot);
            var rightValid=body.TryGetBoneTransform("ankle_R",out var rightFoot);
            if(vaultReportStage==1) Log.Info("CITIZEN_VAULT_MOTION "+Json.Serialize(new { capture_id=CaptureId, samples=vaultMotionSamples, slab_samples=vaultSlabSamples, minimum_ankle_clearance=vaultSlabSamples>0 ? vaultMinAnkleClearance : (float?)null, maximum_local_foot_step=vaultMaxLocalFootStep, maximum_step_time=vaultStepTime, maximum_step_delta=vaultStepDelta }));
            Log.Info("CITIZEN_VAULT_CHECK "+Json.Serialize(new { capture_id=CaptureId, phase=vaultReportStage++, selected_sequence=VaultSequence, sequence_duration=!body.UseAnimGraph ? body.Sequence.Duration : 0, sequence_time=!body.UseAnimGraph ? body.Sequence.Time : 0, vault_blend=vaultBlend, started=vaultExerciseStarted, vaulting=nativeVault.IsVaulting, outcome=nativeVault.Outcome, progress=nativeVault.Progress, graph=body.UseAnimGraph, left_valid=leftValid, right_valid=rightValid, left_z=leftFoot.Position.z, right_z=rightFoot.Position.z, body_z=subject.WorldPosition.z, input_restored=movementController.UseInputControls }));
        }
        if(ExerciseInteractiveCover && !ExerciseInteractiveVault && ((coverReportStage==0 && wallTime>1.6f) || (coverReportStage==1 && wallTime>3.2f) || (coverReportStage==2 && wallTime>(HighCoverExercise ? 5.3f : 4.3f)) || (HighCoverExercise && coverReportStage==3 && wallTime>6.3f)))
        {
            var phase=coverReportStage++;
            var before=reviewWeapon.ActivePaintballs;
            var fired=reviewWeapon.FireAt(marker.WorldPosition+marker.WorldRotation.Forward*1000);
            Log.Info("CITIZEN_COVER_CHECK "+Json.Serialize(new { capture_id=CaptureId, phase, projectile_spawned=reviewWeapon.ActivePaintballs>before, blocked_corner=BlockCoverCorner, opposite_edge=OppositeCoverEdge, high_cover=HighCoverExercise, position_y=movementController.WorldPosition.y, corner_available=interactiveCover.CornerAvailable, attached=interactiveCover.Attached, peeking=interactiveCover.Peeking, duck=movementController.IsDucking, lowered=animation.IsWeaponLowered, shot_accepted=fired, input_restored=movementController.UseInputControls }));
        }
        if(!aimReported && wallTime>=1.8f && marker.IsValid())
        {
            aimReported=true;
            var desired=(subject.WorldRotation*Rotation.FromPitch(AimPitch)).Forward;
            var angle=System.MathF.Acos(Vector3.Dot(desired,marker.WorldRotation.Forward).Clamp(-1,1))*180/System.MathF.PI;
            var hasHand=body.TryGetBoneTransform("hand_L",out var aimHand);
            Log.Info("CITIZEN_AIM_FIT "+Json.Serialize(new { interactive=InteractiveControls, native_input=movementController.UseInputControls, native_look=movementController.UseLookControls, native_camera=movementController.UseCameraControls, weapon_input=reviewWeapon?.AcceptInput, capture_id=CaptureId, requested_pitch=AimPitch, applied_pitch=locomotion.AimPitch, barrel_angle_error=angle, marker_angles=marker.WorldRotation.Angles(), support_error=hasHand && supportTarget.IsValid() ? (aimHand.Position-supportTarget.WorldPosition).Length : -1 }));
        }
        if (PreviewReload)
        {
            foreach(var name in new[]{"pelvis","spine_0","spine_1","spine_2","neck_0"})
            {
                if(!body.TryGetBoneTransform(name,out var pose)) continue;
                var height=pose.Position.z-subject.WorldPosition.z;
                if(reviewTime<.75f) baselineBoneHeights[name]=height;
                if(reviewTime>=1 && reviewTime<3.2f && baselineBoneHeights.TryGetValue(name,out var baseline))
                    maximumBoneRise[name]=System.MathF.Max(maximumBoneRise.GetValueOrDefault(name,float.MinValue),height-baseline);
            }
        }
        if (PreviewReload && body.TryGetBoneTransform("head", out var headPose))
        {
            var height = headPose.Position.z - subject.WorldPosition.z;
            if (reviewTime < .75f) baselineHeadHeight = height;
            if (reviewTime >= 1 && reviewTime < 3.2f)
            {
                maximumReloadHeadHeight = System.MathF.Max(maximumReloadHeadHeight, height);
                minimumReloadHeadHeight = System.MathF.Min(minimumReloadHeadHeight, height);
            }
        }
        if (PreviewReload && body.TryGetBoneTransform("hand_L", out var motionHand))
        {
            if (hasPreviousHand && reviewTime > .5f && reviewTime < 4.5f)
            {
                var step = (motionHand.Position - previousHand).Length;
                motionSamples++;
                if (reviewTime < 1) maxReleaseStep = System.MathF.Max(maxReleaseStep, step);
                else if (reviewTime >= 3.15f)
                {
                    if (step > maxReturnStep) { maxReturnStep = step; maxReturnTime = reviewTime; }
                }
                else maxActionStep = System.MathF.Max(maxActionStep, step);
            }
            previousHand = motionHand.Position;
            hasPreviousHand = true;
            if (!reloadMotionReported && (reviewTime >= 4.5f || wallTime>=6))
            {
                reloadMotionReported = true;
                Log.Info("CITIZEN_RELOAD_MOTION " + Json.Serialize(new { interactive=InteractiveControls, requested_pitch=AimPitch, final_pitch=locomotion.AimPitch, final_barrel_error=System.MathF.Acos(Vector3.Dot((subject.WorldRotation*Rotation.FromPitch(AimPitch)).Forward,marker.WorldRotation.Forward).Clamp(-1,1))*180/System.MathF.PI, shot_accepted=shotAccepted, shot_muzzle_error=shotMuzzleError, shot_projectiles=shotProjectiles, cancellation_events=cancellationEvents, end_round_cancel=CancelByEndRound, capture_id=CaptureId, presentation_time=reviewTime, pod_held=podHeld, cancelled=reloadCancelled, cancel_phase=CancelReloadAtPhase, weapon_clock=WeaponReloadClock, ammo_before=ammoBeforeReload, reserve_before=reserveBeforeReload, ammo_after=reviewWeapon?.Ammo, reserve_after=reviewWeapon?.Reserve, weapon_reload_remaining=reviewWeapon?.ReloadRemaining, controller_movement=ControllerMovement, cycle=reloadCycle, samples=motionSamples, translated=TranslatePreview, turn_speed=TurnSpeed, travel=distanceTraveled, displacement_x=subject.WorldPosition.x, release_step=maxReleaseStep, action_step=maxActionStep, return_step=maxReturnStep, return_time=maxReturnTime, grip=currentGrip, duck=DuckLevel, baseline_head=baselineHeadHeight, reload_head_min=minimumReloadHeadHeight, reload_head_max=maximumReloadHeadHeight, bone_rise=maximumBoneRise, posture_weight=reloadPosture, pod_picked=pickupError>=0, pod_returned=podReturned, pickup_error=pickupError, return_error=returnError, minimum_return_gap=minimumReturnGap, return_reach=returnReachDistance, return_arm_length=returnArmLength, pour_samples=pourSamples, pour_aligned_samples=pourAlignedSamples, pour_max_error=maximumPourError, pour_mean_error=pourSamples>0 ? totalPourError/pourSamples : 0 }));
            }
        }
        if(gripTrack is not null)
        {
            float maximumError=0, maximumChange=0;
            foreach(var sample in gripTrack.bones)
            {
                var handBone=body.Model.Bones.GetBone(sample.hand);
                if(!body.TryGetBoneTransformAnimation(handBone,out var hand))continue;
                var p=sample.position;var q=sample.rotation;
                var local=new Transform(new Vector3(p[0],p[1],p[2]),new Rotation(q[0],q[1],q[2],q[3]));
                var world=hand.ToWorld(local);
                body.TryGetBoneTransform(sample.name,out var before);
                body.SetBoneTransform(body.Model.Bones.GetBone(sample.name),body.WorldTransform.ToLocal(world));
                if(gloveRenderer.IsValid())gloveRenderer.SetBoneTransform(gloveRenderer.Model.Bones.GetBone(sample.name),gloveRenderer.WorldTransform.ToLocal(world));
                if(body.TryGetBoneTransform(sample.name,out var after))
                {
                    maximumError=System.MathF.Max(maximumError,(after.Position-world.Position).Length);
                    maximumChange=System.MathF.Max(maximumChange,(after.Position-before.Position).Length);
                }
            }
            if(!gripReported && reviewTime>1.5f) { Log.Info($"CITIZEN_GRIP_OVERRIDE bones={gripTrack.bones.Length} max_error={maximumError} max_change={maximumChange}");gripReported=true; }
        }
        if (!ExportHandPose || handPoseReported || reviewTime<1.5f) return;
        var samples = new List<object>();
        foreach(var bone in body.Model.Bones.AllBones.Where(x=>x.Name.StartsWith("finger_")))
        {
            var handName=bone.Name.EndsWith("_L") ? "hand_L" : "hand_R";
            if (!body.TryGetBoneTransform(handName,out var hand) || !body.TryGetBoneTransform(bone.Name,out var pose)) continue;
            var local=hand.ToLocal(pose);
            samples.Add(new { name=bone.Name, hand=handName, position=new[]{local.Position.x,local.Position.y,local.Position.z}, rotation=new[]{local.Rotation.x,local.Rotation.y,local.Rotation.z,local.Rotation.w} });
        }
        Log.Info("CITIZEN_HAND_POSE " + Json.Serialize(new { source="Citizen HoldItem_HandPoses", phase=HandPose, bones=samples }));
        handPoseReported=true;
    }
    private void ExerciseCover()
    {
        if(coverExerciseStage==0 && wallTime>.3f)
        {
            movementController.WorldPosition=new Vector3(-595,HighCoverExercise ? (OppositeCoverEdge ? 120 : 20) : 70,2);
            movementController.EyeAngles=new Angles(0,0,0);
            coverFixture=TrainingRange.Box(null,"Citizen cover review",new Vector3(-550,70,HighCoverExercise ? 45 : 24),new Vector3(20,180,HighCoverExercise ? 90 : 48),Color.Gray,true);
            coverFixture.Components.Create<CoverSurface>();
            if(BlockCoverCorner)
                cornerObstruction=TrainingRange.Box(null,"Citizen blocked corner",new Vector3(-580,OppositeCoverEdge ? 175 : -35,35),new Vector3(28,25,70),Color.Gray,true);
            interactiveCover.TestInput=true;coverExerciseStage=1;
        }
        if(coverExerciseStage==1 && wallTime>.7f) { interactiveCover.TryEnter();if(HighCoverExercise)interactiveCover.TestMovement=OppositeCoverEdge ? Vector3.Left : Vector3.Right;coverExerciseStage=2; }
        if(coverExerciseStage==2 && wallTime>2)
        {
            interactiveCover.TestMovement=Vector3.Zero;
            if(ExerciseInteractiveVault) vaultExerciseStarted=movementController.Components.Get<VaultController>().TryBegin();
            else { interactiveCover.TestAim=true;reviewWeapon.ReviewAimOverride=true; }
            coverExerciseStage=3;
        }
        if(coverExerciseStage==3 && wallTime>4)
        {
            interactiveCover.TestAim=false;reviewWeapon.ReviewAimOverride=false;
            if(!HighCoverExercise) { interactiveCover.Leave();interactiveCover.TestInput=false;reviewWeapon.ReviewAimOverride=null; }
            coverExerciseStage=4;
        }
        if(HighCoverExercise && coverExerciseStage==4 && wallTime>6)
        { interactiveCover.Leave();interactiveCover.TestInput=false;reviewWeapon.ReviewAimOverride=null;coverExerciseStage=5; }
    }
    private void ResetReloadCycle()
    {
            reviewTime=0;wallTime=0;weaponReloadRequested=false;reloadCancelled=false;reloadClock.Reset();cancellationEvents=0;
            reloadCycle++;
            reloadTriggered=false;reloadMotionReported=false;hopperReported=false;
            markerGrip.ResetRecovery();podReload.ResetCycle();
            maxReleaseStep=0;maxActionStep=0;maxReturnStep=0;maxReturnTime=0;motionSamples=0;
            baselineBoneHeights.Clear();maximumBoneRise.Clear();
            maximumReloadHeadHeight=float.MinValue;minimumReloadHeadHeight=float.MaxValue;
            hasPreviousHand=false;
        
    }
    private void OnInteractiveReloadStarted()
    {
        ResetReloadCycle();
        PreviewReload=true;weaponReloadRequested=true;reviewTime=.8f;
        ammoBeforeReload=reviewWeapon.Ammo;reserveBeforeReload=reviewWeapon.Reserve;
    }
    private void OnWeaponReloadCancelled()
    {
        cancellationEvents++;
        if(reloadCancelled || !subject.IsValid() || !body.IsValid()) return;
        reloadCancelled=true;
        if(body.TryGetBoneTransform("hand_L",out var interruptedHand))
            cancelHand=subject.WorldTransform.ToLocal(interruptedHand);
        if(refillPod.IsValid()) cancelPodRotation=subject.WorldRotation.Inverse*refillPod.WorldRotation;
    }
    protected override void OnDestroy()
    {
        if(reviewWeapon.IsValid())
        {
            reviewWeapon.PresentationAnchor=previousWeaponAnchor;
            reviewWeapon.ReloadStarted-=OnInteractiveReloadStarted;
            reviewWeapon.ReloadCancelled-=OnWeaponReloadCancelled;
            reviewWeapon.CancelReload();
            reviewWeapon.AcceptInput=weaponInputBefore;
        }
        if(ExerciseInteractiveCover && interactiveCover.IsValid())
        {
            interactiveCover.Leave();interactiveCover.TestInput=false;interactiveCover.TestAim=false;interactiveCover.TestMovement=Vector3.Zero;
            if(reviewWeapon.IsValid()) reviewWeapon.ReviewAimOverride=null;
            coverFixture?.Destroy();cornerObstruction?.Destroy();
        }
        subject?.Destroy();
        foreach(var renderer in hiddenRenderers) if(renderer.IsValid()) renderer.Enabled=true;
        if(ControllerMovement && movementController.IsValid())
        {
            movementController.WishVelocity=Vector3.Zero;
            movementController.UpdateDucking(false);
            movementController.UseInputControls=movementController.UseLookControls=movementController.UseCameraControls=true;
        }
    }
}

