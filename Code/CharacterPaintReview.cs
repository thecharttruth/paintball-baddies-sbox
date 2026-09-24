using Sandbox;
namespace PaintballBaddies;

/// <summary>Opt-in physical front/back/side impacts above the arena; never takes player input.</summary>
public sealed class CharacterPaintReview : Component
{
    private GameObject actorObject,shooterObject;
    private CitizenOpponentPresentation presentation;
    private ArenaOpponent bot;
    private PaintballCombatant actor,attacker;
    private float elapsed,next=1;
    private int shot;
    public bool Done { get; private set; }
    public List<string> Results { get; }=new();
    public bool Crouch { get; set; }
    protected override void OnPreRender()
    {
        if(presentation.IsValid() && presentation.IsReady && !actor.Components.Get<CloseCombat>().Incapacitated)
            presentation.FootstepRenderer.Components.Get<CitizenLocomotion>().Animation.DuckLevel=Crouch ? 1 : 0;
    }
    protected override void OnStart()
    {
        actorObject=new GameObject(GameObject){Name="Clothing hit review",WorldPosition=new Vector3(0,0,1500)};
        presentation=actorObject.Components.Create<CitizenOpponentPresentation>();
        bot=actorObject.Components.Create<ArenaOpponent>();bot.Character="viper";bot.CombatEnabled=false;
        actor=actorObject.Components.Create<PaintballCombatant>();actor.Team=91;actor.StayInMatch=true;
        var box=actorObject.Components.Create<BoxCollider>();box.Scale=new(28,28,66);box.Center=new(0,0,33);
        shooterObject=new GameObject(GameObject){Name="Clothing review shooter",WorldPosition=new Vector3(120,0,1500)};
        attacker=shooterObject.Components.Create<PaintballCombatant>();attacker.Team=92;attacker.StayInMatch=true;
    }
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;
        if(!presentation.IsReady)return;
        bot.Enabled=false;
        bot.ReviewCrouch=Crouch;
        var agent=actorObject.Components.Get<NavMeshAgent>();if(agent.IsValid()){agent.Stop();agent.UpdatePosition=false;}
        actorObject.WorldPosition=new(0,0,1500);actorObject.WorldRotation=Rotation.Identity;
        var body=presentation.FootstepRenderer;
        body.Components.Get<CitizenLocomotion>().UpdateMotion(Vector3.Zero,Vector3.Zero,true,Crouch ? 1 : 0,0,Rotation.Identity,0,false,Time.Delta);
        if(Done || elapsed<next)return;
        next=elapsed+.45f;
        if(shot>0)
        {
            var paint=actor.Components.Get<CharacterPaint>();
            bool pass=actor.PaintHits==shot && attacker.HitsLanded==shot && paint?.Count==System.Math.Min(shot,CharacterPaint.Capacity)
                && !paint.LastBone.Contains("IK") && !paint.LastBone.Contains("ikrule");
            var result=$"{(pass ? "PASS" : "FAIL")} shot={shot} hits={actor.PaintHits} score={attacker.HitsLanded} paint={paint?.Count} bone={paint?.LastBone}";
            Results.Add(result);Log.Info("CHARACTER_PAINT "+result);
        }
        if(shot>=12){Done=true;return;}
        var direction=new[]{Vector3.Forward,Vector3.Backward,Vector3.Left,Vector3.Right}[shot%4];
        var height=new[]{34f,47f,61f}[shot/4];
        var target=actorObject.WorldPosition+Vector3.Up*height;
        var start=target+direction*120;
        var ray=PaintballFlight.Trace(Scene,start,target-direction*12,shooterObject);
        Log.Info($"CHARACTER_PAINT_TRACE shot={shot+1} hit={ray.GameObject?.Name} hitbox={ray.Hitbox is not null} position={ray.HitPosition}");
        var ballObject=new GameObject(GameObject){Name="Clothing test paintball",WorldPosition=start};
        var ball=ballObject.Components.Create<OpponentPaintball>();ball.Shooter=shooterObject;ball.Team=92;ball.Velocity=PaintballFlight.LaunchVelocity(start,target);
        shot++;
    }
}
