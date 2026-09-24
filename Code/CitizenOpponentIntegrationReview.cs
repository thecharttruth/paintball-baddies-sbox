using Sandbox;

namespace PaintballBaddies;

public sealed class CitizenOpponentIntegrationReview : Component
{
    [Property] public string CaptureId { get; set; } = "";
    [Property] public int CharacterIndex { get; set; }
    [Property] public bool CheckElimination { get; set; }
    [Property] public bool EliminateDuringReload { get; set; }
    [Property] public bool ReviewDropCamera { get; set; }
    [Property] public bool ReviewCarryCamera { get; set; }
    private float? actualEliminationAt;
    private float EliminationAt => actualEliminationAt ?? (EliminateDuringReload ? 2.5f : 5.5f);
    private float time;
    private int stage;
    private GameObject actor;
    private ArenaOpponent opponent;
    private CitizenOpponentPresentation presentation;
    private BoxCollider eliminationCollider;
    private bool consumed, reloadStarted;
    private int stepsBeforeFrozenClip;
    private PlayerController reviewPlayer;
    private bool previousCameraControl;
    private int contactSamples;
    private float maximumMarkerGap, totalMarkerGap;
    protected override void OnPreRender()
    {
        if(stage!=3 || !presentation.IsValid() || !opponent.IsValid())return;
        var body=presentation.FootstepRenderer;
        var marker=opponent.PresentationAnchor;
        if(!body.IsValid() || !marker.IsValid() || !body.TryGetBoneTransform("hold_R",out var hand))return;
        var expected=hand.Position+hand.Rotation*CitizenMarkerGrip.MarkerOffset;
        var gap=(marker.WorldPosition-expected).Length;
        contactSamples++;
        totalMarkerGap+=gap;
        maximumMarkerGap=System.MathF.Max(maximumMarkerGap,gap);
    }
    protected override void OnStart()
    {
        Scene.NavMesh.IsEnabled=true;
        Scene.NavMesh.IncludeStaticBodies=true;
        Scene.NavMesh.IncludeKeyframedBodies=false;
        Scene.NavMesh.AgentHeight=67;Scene.NavMesh.AgentRadius=17;
        Scene.NavMesh.SetDirty();
    }
    protected override void OnUpdate()
    {
        if(ReviewCarryCamera && actor.IsValid())
        {
            if(!reviewPlayer.IsValid())
            {
                reviewPlayer=Scene.GetAllComponents<PlayerController>().First();
                previousCameraControl=reviewPlayer.UseCameraControls;
            }
            reviewPlayer.UseCameraControls=false;
            WorldPosition=actor.WorldPosition+actor.WorldRotation*new Vector3(0,-100,45);
            WorldRotation=Rotation.LookAt(actor.WorldPosition+Vector3.Up*43-WorldPosition);
        }
        if(ReviewDropCamera && Scene.GetAllComponents<DroppedReloadPod>().FirstOrDefault() is {} dropped)
        {
            if(!reviewPlayer.IsValid())
            {
                reviewPlayer=Scene.GetAllComponents<PlayerController>().First();
                previousCameraControl=reviewPlayer.UseCameraControls;
            }
            reviewPlayer.UseCameraControls=false;
            WorldPosition=dropped.WorldPosition+new Vector3(28,-32,24);
            WorldRotation=Rotation.LookAt(dropped.WorldPosition+Vector3.Up*3-WorldPosition);
        }
        if(Scene.NavMesh.IsGenerating) return;
        time+=Time.Delta;
        if(stage==0 && time>.3f)
        {
            actor=new GameObject { Name="Citizen AI integration review" };
            actor.WorldPosition=new Vector3(-950,-500,2);
            // Exercise presentation startup before its AI dependency exists.
            presentation=actor.Components.Create<CitizenOpponentPresentation>();
            stage=1;
        }
        if(stage==1 && time>.6f)
        {
            opponent=actor.Components.Create<ArenaOpponent>();
            opponent.Character=RosterSelection.Names[CharacterIndex.Clamp(0,5)];opponent.CombatEnabled=true;stage=2;
        }
        if(stage==2 && time>1.3f && opponent.Body.IsValid() && presentation.IsReady)
        {
            // A stale hidden run clip must not suppress the visible Citizen's footsteps.
            opponent.Body.Sequence.Name="run";opponent.Body.PlaybackRate=0;
            stepsBeforeFrozenClip=opponent.Components.Get<MovementFootsteps>()?.StepsPlayed ?? 0;
            opponent.CombatEnabled=false;
            consumed=opponent.Magazine.TryFire();reloadStarted=opponent.Magazine.BeginReload();stage=3;
        }
        if(stage==3 && time>EliminationAt && (!EliminateDuringReload || presentation.PodHeld))
        {
            actualEliminationAt=time;
            Log.Info("CITIZEN_OPPONENT_INTEGRATION "+Json.Serialize(new {capture_id=CaptureId,
                phase="active",ready=presentation.IsReady,consumed,reload_started=reloadStarted,
                character=opponent.CharacterIndex,suit=presentation.EquippedSuit,
                ammo=opponent.Magazine.Ammo,reserve=opponent.Magazine.Reserve,reloads=opponent.Magazine.Reloads,
                pod_held=presentation.PodHeld,distance=opponent.DistanceTravelled,
                contact_samples=contactSamples,maximum_marker_gap=maximumMarkerGap,
                mean_marker_gap=contactSamples>0 ? totalMarkerGap/contactSamples : 0,
                animation_speed=presentation.AnimationSpeed,peak_animation_speed=presentation.PeakAnimationSpeed,
                footsteps=opponent.Components.Get<MovementFootsteps>()?.StepsPlayed ?? 0,
                native_contacts=opponent.Components.Get<MovementFootsteps>()?.NativeContactEvents ?? 0,
                native_steps=opponent.Components.Get<MovementFootsteps>()?.NativeStepsPlayed ?? 0,
                steps_after_frozen_clip=(opponent.Components.Get<MovementFootsteps>()?.StepsPlayed ?? 0)-stepsBeforeFrozenClip,
                anchor=opponent.PresentationAnchor.IsValid(),old_body_visible=opponent.Body.Enabled}));
            if(CheckElimination)
            {
                eliminationCollider=opponent.Components.Get<BoxCollider>();
                for(var hit=0;hit<3;hit++)opponent.Components.Get<PaintballCombatant>().RegisterHit(0);
                stage=6;
            }
            else { presentation.Destroy();stage=4; }
        }
        if(stage==6 && time>EliminationAt+.8f)
        {
            Log.Info("CITIZEN_OPPONENT_ELIMINATION "+Json.Serialize(new {capture_id=CaptureId,
                phase="signal",showing=presentation.ShowingOutSignal,weight=presentation.OutSignalWeight,
                hand_above_head=presentation.SignalHandAboveHead,
                dropped_pods=Scene.GetAllComponents<DroppedReloadPod>().Count(),
                pod_actor_filter=Scene.GetAllComponents<DroppedReloadPod>().All(x=>x.ActorFilterVerified),
                pod_shot_filter=Scene.GetAllComponents<DroppedReloadPod>().All(x=>x.ShotFilterVerified),
                pod_fall=Scene.GetAllComponents<DroppedReloadPod>().Select(x=>x.FallDistance).DefaultIfEmpty(0).Max(),
                collider_enabled=eliminationCollider.Enabled,shots=opponent.ShotsFired,
                pod_held=presentation.PodHeld,reload_props=presentation.ReloadPropsVisible,
                reload_remaining=opponent.Magazine.ReloadRemaining}));
            stage=7;
        }
        if(stage==7 && time>EliminationAt+1.8f)
        {
            Log.Info("CITIZEN_OPPONENT_ELIMINATION "+Json.Serialize(new {capture_id=CaptureId,
                phase="finished",showing=presentation.ShowingOutSignal,shots=opponent.ShotsFired}));
            presentation.Destroy();stage=4;
        }
        if(stage==4 && time>(EliminateDuringReload ? EliminationAt+5.4f : 6))
        {
            Log.Info("CITIZEN_OPPONENT_INTEGRATION "+Json.Serialize(new {capture_id=CaptureId,
                phase="removed",anchor=opponent.PresentationAnchor.IsValid(),body_restored=opponent.Body.Enabled,
                dropped_pods=Scene.GetAllComponents<DroppedReloadPod>().Count()}));stage=5;
        }
    }
    protected override void OnDestroy()
    {
        if(reviewPlayer.IsValid())reviewPlayer.UseCameraControls=previousCameraControl;
        if(actor.IsValid()) actor.Destroy();
    }
}
