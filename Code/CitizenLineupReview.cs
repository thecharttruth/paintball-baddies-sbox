using Sandbox;
using System.Collections.Generic;
namespace PaintballBaddies;
/// <summary>Opt-in six-character comparison using real Citizen opponent equipment.</summary>
public sealed class CitizenLineupReview : Component
{
    [Property] public bool ReviewNativeVest { get; set; }
    [Property] public bool ReviewNativeBoots { get; set; }
    [Property] public bool SingleCharacter { get; set; }
    [Property] public int CharacterIndex { get; set; }
    [Property] public string CaptureId { get; set; } = "";
    [Property] public bool PreviewReload { get; set; }
    [Property] public bool ReviewCrouch { get; set; }
    [Property] public float ReviewRightGripWeight { get; set; } = 1;
    private float elapsed;
    private bool reloadStarted;
    private bool reloadReported;
    private readonly HashSet<GameObject> fitted = new();
    private readonly List<GameObject> actors=new();
    private GameObject floor;
    private PlayerController player;
    private Vector3 previousPlayerPosition;
    protected override void OnStart()
    {
        player=Scene.GetAllComponents<PlayerController>().First();previousPlayerPosition=player.WorldPosition;
        floor=TrainingRange.Box(null,"Roster comparison floor",new Vector3(0,3000,997),new Vector3(500,600,6),new Color(.35f,.35f,.35f),false);
        for(int i=0;i<(SingleCharacter ? 1 : 6);i++)
        {
            var character=SingleCharacter ? CharacterIndex.Clamp(0,5) : i;
            var actor=new GameObject {Name="Roster comparison "+RosterSelection.Names[character]};
            actor.WorldPosition=new Vector3(0,2800+i*80,1000);
            var presentation=actor.Components.Create<CitizenOpponentPresentation>();
            if(ReviewNativeBoots && i % 2 == 0)
                presentation.BootsPath="models/citizen_clothes/shoes/boots/models/boots_m_human.vmdl";
            var bot=actor.Components.Create<ArenaOpponent>();bot.Character=RosterSelection.Names[character];bot.CombatEnabled=false;
            actors.Add(actor);
        }
    }
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;
        if(PreviewReload && !reloadReported && elapsed>5)
        {
            foreach(var pod in Scene.GetAllComponents<CitizenPodReload>())
                Log.Info("MARKER_SIDE_RELOAD "+Json.Serialize(new {capture_id=CaptureId,character=CharacterIndex,subject=pod.GameObject.Name,pickup=pod.pickupError,closest=pod.MinimumPickupGap,returned=pod.returnError,return_gap=pod.minimumReturnGap,pour_samples=pod.pourSamples,aligned=pod.pourAlignedSamples}));
            reloadReported=true;
        }
        player.WorldPosition=new Vector3(-700,3000,1000);
        for(int i=0;i<actors.Count;i++)
        {
            var actor=actors[i];
            actor.Components.Get<ArenaOpponent>().ReviewCrouch=ReviewCrouch;
            foreach(var grip in actor.Components.GetAll<CitizenMarkerGrip>(FindMode.EverythingInSelfAndDescendants))
                grip.RightGripStrength=ReviewRightGripWeight;
            if (ReviewNativeVest && i % 2 == 0 && !fitted.Contains(actor))
            {
                var presentation=actor.Components.Get<CitizenOpponentPresentation>();
                if (presentation.IsReady)
                {
                    var target=presentation.FootstepRenderer;
                    var gear=new GameObject(target.GameObject) { Name="Native tactical vest fit review" };
                    var renderer=gear.Components.Create<SkinnedModelRenderer>();
                    renderer.Model=Model.Load("models/citizen_clothes/vest/tactical_vest/models/tactical_vest_m_human.vmdl");
                    renderer.BoneMergeTarget=target;
                    fitted.Add(actor);
                }
            }
            if(actor.Components.Get<NavMeshAgent>() is {} agent){agent.UpdatePosition=false;agent.MaxSpeed=0;}
            actor.WorldPosition=new Vector3(0,2800+i*80,1000);
            actor.WorldRotation=Rotation.FromYaw(180);
            if(PreviewReload && !reloadStarted && elapsed>2 && actor.Components.Get<ArenaOpponent>()?.Magazine is {} magazine)
            {
                magazine.TryFire();magazine.BeginReload();reloadStarted=true;
            }
        }
    }
    protected override void OnDestroy()
    {
        if(player.IsValid())player.WorldPosition=previousPlayerPosition;
        foreach(var actor in actors)if(actor.IsValid())actor.Destroy();
        if(floor.IsValid())floor.Destroy();
    }
}


