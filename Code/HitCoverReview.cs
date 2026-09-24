using Sandbox;
namespace PaintballBaddies;

/// <summary>Two struck opponents must reserve different shelter and move immediately.</summary>
public sealed class HitCoverReview : Component
{
    private readonly List<ArenaOpponent> bots=new();
    private GameObject source;
    private float elapsed,next;
    private bool struck;
    public bool Done { get; private set; }
    public List<string> Results { get; }=new();
    protected override void OnStart()
    {
        source=new GameObject(GameObject){Name="Hit cover review source",WorldPosition=new Vector3(-700,0,1)};
        var attacker=source.Components.Create<PaintballCombatant>();attacker.Team=90;attacker.StayInMatch=true;
        for(int i=0;i<2;i++)
        {
            var go=new GameObject(GameObject){Name="Hit cover review "+i,WorldPosition=new Vector3(-190,60+i*100,1)};
            go.Components.Create<CitizenOpponentPresentation>();
            var bot=go.Components.Create<ArenaOpponent>();bot.Character=i==0 ? "imani" : "roxie";bot.CombatEnabled=true;bot.ContestMode=true;bots.Add(bot);
        }
    }
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;
        if(!struck && elapsed>1)
        {
            foreach(var bot in bots)
            {
                var actor=bot.Components.Get<PaintballCombatant>();actor.Team=89;actor.StayInMatch=true;
                actor.RegisterHit(90,source.WorldPosition,source.Components.Get<PaintballCombatant>());
            }
            struck=true;next=elapsed+.1f;
        }
        if(Done || !struck || elapsed<next)return;
        next=elapsed+1;
        var report=Json.Serialize(bots.Select(x=>new{x.HitCoverReactions,x.EvadingAfterHit,x.HasCoverPlan,x.UsingCover,Reserved=x.ReservedCover?.GameObject.Name,Position=x.WorldPosition.ToString(),x.DistanceTravelled,x.ShotsFromCover}));
        Results.Add(report);Log.Info("HIT_COVER "+report);
        if(elapsed>10)Done=true;
    }
}
