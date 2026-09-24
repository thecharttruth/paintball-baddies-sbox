using Sandbox;
namespace PaintballBaddies;

/// <summary>Isolated native tests of a rendered shield with multiplayer hierarchy.</summary>
public sealed class ShieldOutgoingReview : Component
{
    GameObject actorObject,shieldObject,enemyObject;
    PaintballCombatant actor;
    ArenaOpponent bot;
    CitizenOpponentPresentation presentation;
    ArenaShield shield;
    float elapsed;
    public bool Done {get;private set;}
    public List<string> Results {get;}=new();
    void Check(bool ok,string text)=>Results.Add((ok?"PASS ":"FAIL ")+text);
    protected override void OnStart()
    {
        actorObject=new GameObject(GameObject){Name="Outgoing shield test wearer",WorldPosition=new(0,0,1500)};
        presentation=actorObject.Components.Create<CitizenOpponentPresentation>();
        bot=actorObject.Components.Create<ArenaOpponent>();bot.Character="viper";bot.CombatEnabled=false;
        actor=actorObject.Components.Create<PaintballCombatant>();actor.Team=91;actor.StayInMatch=true;
        shieldObject=new GameObject(GameObject){Name="Outgoing shield test panel"};shield=shieldObject.Components.Create<ArenaShield>();
        enemyObject=new GameObject(GameObject){Name="Outgoing shield test enemy",WorldPosition=new(250,0,1500)};
    }
    protected override void OnUpdate()
    {
        if(Done||!presentation.IsReady)return;
        bot.Enabled=false;
        var agent=actorObject.Components.Get<NavMeshAgent>();if(agent.IsValid()){agent.Stop();agent.UpdatePosition=false;}
        actorObject.WorldPosition=new(0,0,1500);actorObject.WorldRotation=Rotation.Identity;
        if(shield.Owner!=actor){shield.Collect(actor);return;}
        elapsed+=Time.Delta;if(elapsed<.8f)return;
        // Reparenting preserves local coordinates. Keep the displayed pose when
        // simulating a network shield root, or the test ray hits arena scenery.
        var pose=shieldObject.WorldTransform;
        shieldObject.Parent=null;shieldObject.WorldTransform=pose;
        var center=shield.WorldPosition;var forward=shield.WorldRotation.Forward;
        var own=PaintballFlight.Trace(Scene,center-forward*35,center+forward*80,actorObject);
        Check(!own.Hit,$"detached own shield permits outgoing paintball ({own.GameObject?.Name})");
        var incoming=PaintballFlight.Trace(Scene,center+forward*80,center,enemyObject);
        Check(ArenaShield.Find(incoming.GameObject)==shield,$"opponent shield still intercepts incoming paintball ({incoming.GameObject?.Name})");
        Check(shield.Remaining==ArenaShield.Capacity,"own trace preserves shield durability");
        Check(shield.Components.GetAll<ModelRenderer>(FindMode.EverythingInSelfAndDescendants).Any(x=>x.Enabled),"test shield is visibly held");
        Done=true;
    }
    protected override void OnDestroy(){if(shieldObject.IsValid())shieldObject.Destroy();}
}
