using Sandbox;

namespace PaintballBaddies;

/// <summary>Opt-in lifecycle/weapon fixture; never saved in the playable scene.</summary>
public sealed class CitizenPlayerIntegrationReview : Component
{
    [Property] public string CaptureId { get; set; } = "";
    [Property] public int CharacterIndex { get; set; }
    [Property] public bool ExerciseVault { get; set; }
    [Property] public bool ExerciseHighCover { get; set; }
    [Property] public bool ExerciseSandbags { get; set; }
    [Property] public bool ExerciseSnake { get; set; }
    private SnakeBunker snake;
    private SandbagBunker sandbags;
    private float elapsed;
    private int stage;
    private PlayerController player;
    private PaintballMarker weapon;
    private CitizenPlayerPresentation presentation;
    private bool originalVisible, shot;
    private GameObject coverObject;
    private CoverController cover;
    private VaultController vault;
    private bool entered, started;
    private int contactSamples;
    private float maximumMarkerGap;
    private ArenaMatch match;
    private bool timedContest;
    protected override void OnPreRender()
    {
        if(!presentation.IsValid() || !weapon.IsValid() || !weapon.PresentationAnchor.IsValid())return;
        var body=presentation.FootstepRenderer;
        if(!body.IsValid() || !body.TryGetBoneTransform("hold_R",out var hand))return;
        var expected=hand.Position+hand.Rotation*CitizenMarkerGrip.MarkerOffset;
        maximumMarkerGap=System.MathF.Max(maximumMarkerGap,(weapon.PresentationAnchor.WorldPosition-expected).Length);
        contactSamples++;
    }
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;
        if(stage==0 && elapsed>.3f)
        {
            player=Scene.GetAllComponents<PlayerController>().First();
            match=player.Components.Get<ArenaMatch>();
            timedContest=match.TimedContest;
            match.TimedContest=false; // Exercise the retained reload animation without changing playable rules.
            player.Components.Get<RosterSelection>().Select(CharacterIndex.Clamp(0,5));
            weapon=player.Components.Get<PaintballMarker>();
            originalVisible=player.Renderer.Enabled;
            presentation=player.Components.Create<CitizenPlayerPresentation>();
            stage=1;
        }
        if(stage==1 && elapsed>1)
        {
            shot=weapon.FireAt(weapon.Muzzle+player.EyeAngles.Forward*1000);
            weapon.Reload();stage=2;
        }
        if(stage==2 && elapsed>5.5f)
        {
            Log.Info("CITIZEN_PLAYER_INTEGRATION "+Json.Serialize(new {capture_id=CaptureId,
                phase="active",ready=presentation.IsReady,shot,ammo=weapon.Ammo,reserve=weapon.Reserve,
                pod_held=presentation.PodHeld,anchor=weapon.PresentationAnchor.IsValid(),
                input=player.UseInputControls,look=player.UseLookControls,camera=player.UseCameraControls,
                old_body_visible=player.Renderer.Enabled}));
            if (ExerciseVault || ExerciseHighCover)
            {
                cover=player.Components.Get<CoverController>();
                vault=player.Components.Get<VaultController>();
                player.WorldPosition=new Vector3(-595,ExerciseHighCover ? 20 : 70,2);
                player.EyeAngles=new Angles(0,0,0);
                if(ExerciseSnake)
                {
                    snake=Components.Create<SnakeBunker>();snake.Position=new Vector3(-550,70,0);
                }
                else if(ExerciseSandbags)
                {
                    sandbags=Components.Create<SandbagBunker>();sandbags.Position=new Vector3(-550,70,0);
                }
                else
                {
                    coverObject=TrainingRange.Box(null,"Player integration cover",new Vector3(-550,70,ExerciseHighCover ? 45 : 24),new Vector3(20,180,ExerciseHighCover ? 90 : 48),Color.Gray,true);
                    coverObject.Components.Create<CoverSurface>();
                }
                cover.TestInput=true;stage=ExerciseHighCover ? 20 : 10;
            }
            else { presentation.Destroy();stage=3; }
        }
        if(stage==10 && elapsed>6.3f) { entered=cover.TryEnter();stage=11; }
        if(stage==20 && elapsed>6.3f) { entered=cover.TryEnter();stage=21; }
        if(stage==21 && elapsed>6.5f)
        {
            var protectedShot=weapon.FireAt(weapon.Muzzle+Vector3.Forward*1000);
            Log.Info("CITIZEN_PLAYER_INTEGRATION "+Json.Serialize(new {capture_id=CaptureId,
                phase="sheltered",entered,attached=cover.Attached,peeking=cover.Peeking,shot=protectedShot}));
            cover.TestMovement=Vector3.Right;stage=22;
        }
        if(stage==22 && elapsed>7.8f)
        {
            cover.TestMovement=Vector3.Zero;cover.TestAim=true;weapon.ReviewAimOverride=true;stage=23;
        }
        if(stage==23 && elapsed>9)
        {
            var before=weapon.ActivePaintballs;
            var peekShot=weapon.FireAt(weapon.Muzzle+Vector3.Forward*1000);
            Log.Info("CITIZEN_PLAYER_INTEGRATION "+Json.Serialize(new {capture_id=CaptureId,
                phase="peek",attached=cover.Attached,peeking=cover.Peeking,shot=peekShot,
                corner_available=cover.CornerAvailable,position=player.WorldPosition,
                projectile=weapon.ActivePaintballs>before,ready=presentation.IsReady,
                input=player.UseInputControls,look=player.UseLookControls,camera=player.UseCameraControls}));stage=24;
        }
        if(stage==24 && elapsed>10)
        {
            cover.TestAim=false;weapon.ReviewAimOverride=null;cover.Leave();cover.TestInput=false;
            if(coverObject.IsValid())coverObject.Destroy();
            if(sandbags.IsValid())sandbags.Destroy();
            if(snake.IsValid())snake.Destroy();
            presentation.Destroy();stage=3;
        }
        if(stage==11 && elapsed>7)
        {
            started=vault.TryBegin();stage=12;
        }
        if(stage==12 && elapsed>7.4f)
        {
            Log.Info("CITIZEN_PLAYER_INTEGRATION "+Json.Serialize(new {capture_id=CaptureId,
                phase="vault",entered,started,vaulting=vault.IsVaulting,progress=vault.Progress,
                ready=presentation.IsReady,anchor=weapon.PresentationAnchor.IsValid()}));stage=13;
        }
        if(stage==13 && elapsed>8.5f)
        {
            Log.Info("CITIZEN_PLAYER_INTEGRATION "+Json.Serialize(new {capture_id=CaptureId,
                phase="landed",vaulting=vault.IsVaulting,outcome=vault.Outcome,
                input=player.UseInputControls,look=player.UseLookControls,camera=player.UseCameraControls,
                ready=presentation.IsReady,pod_held=presentation.PodHeld}));
            cover.Leave();cover.TestInput=false;
            if(coverObject.IsValid())coverObject.Destroy();
            if(sandbags.IsValid())sandbags.Destroy();
            if(snake.IsValid())snake.Destroy();
            presentation.Destroy();stage=3;
        }
        if(stage==3 && elapsed>(ExerciseHighCover ? 10.5f : ExerciseVault ? 9 : 6))
        {
            Log.Info("CITIZEN_PLAYER_INTEGRATION "+Json.Serialize(new {capture_id=CaptureId,
                phase="removed",anchor=weapon.PresentationAnchor.IsValid(),
                character=CharacterIndex,contact_samples=contactSamples,maximum_marker_gap=maximumMarkerGap,
                body_restored=player.Renderer.Enabled==originalVisible,
                input=player.UseInputControls,look=player.UseLookControls,camera=player.UseCameraControls}));
            stage=4;
        }
    }
    protected override void OnDestroy()
    {
        if(match.IsValid())match.TimedContest=timedContest;
        if(presentation.IsValid()) presentation.Destroy();
        if(cover.IsValid()) { cover.Leave();cover.TestInput=false;cover.TestAim=false;cover.TestMovement=Vector3.Zero; }
        if(weapon.IsValid()) weapon.ReviewAimOverride=null;
        if(coverObject.IsValid()) coverObject.Destroy();
        if(sandbags.IsValid()) sandbags.Destroy();
        if(snake.IsValid()) snake.Destroy();
    }
}
