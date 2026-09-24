using Sandbox;
namespace PaintballBaddies;

/// <summary>Opt-in native test: physical cover impacts must cause real relocation.</summary>
public sealed class SuppressionRepositionReview : Component
{
    private ArenaOpponent bot;
    private PaintballCombatant shooter;
    private CoverSurface original,second;
    private Vector3 originalPosition;
    private float elapsed,stageTime,nextShot;
    private int stage,eventsBefore,relocationsBefore;
    private bool routeChecked;
    public bool Done { get; private set; }
    public List<string> Results { get; }=new();
    public string State=>bot.IsValid() ? Json.Serialize(new {stage,elapsed,bot.IncomingPaintEvents,
        bot.IncomingPressure,bot.PressureRelocations,bot.CoverRelocations,bot.UsingCover,
        bot.RepositionPending,bot.RelocatingCover,bot.LastRelocationReason,
        Cover=bot.ReservedCover?.GameObject.Name,Position=bot.WorldPosition.ToString(),bot.ShotsFromCover}) : "starting";

    protected override void OnStart()
    {
        // Also run from fresh Practice, before any match has built navigation.
        Scene.NavMesh.IsEnabled=true;Scene.NavMesh.IncludeStaticBodies=true;
        Scene.NavMesh.IncludeKeyframedBodies=false;Scene.NavMesh.AgentHeight=67;Scene.NavMesh.AgentRadius=17;Scene.NavMesh.SetDirty();
        var origin=new GameObject(GameObject){Name="Suppression review shooter",WorldPosition=new(-700,0,1)};
        shooter=origin.Components.Create<PaintballCombatant>();shooter.Team=90;shooter.StayInMatch=true;
        var go=new GameObject(GameObject){Name="Suppression review opponent",WorldPosition=new(-190,60,1)};
        go.Components.Create<CitizenOpponentPresentation>();
        bot=go.Components.Create<ArenaOpponent>();bot.Character="imani";bot.CombatEnabled=true;
    }
    private void Check(bool okay,string name)
    {
        var text=(okay ? "PASS " : "FAIL ")+name;
        Results.Add(text);Log.Info("SUPPRESSION_REPOSITION "+text);
    }
    private void Next(){stage++;stageTime=0;nextShot=0;}
    private void Fire(bool far=false)
    {
        var from=shooter.WorldPosition.WithZ(bot.WorldPosition.z)+Vector3.Up*(far ? 350 : 55);
        var aim=far ? from+Vector3.Forward*650 : bot.CoverDestination+Vector3.Up*30;
        var ball=new GameObject(GameObject){Name="Suppression review paintball",WorldPosition=from};
        var shot=ball.Components.Create<OpponentPaintball>();shot.Shooter=shooter.GameObject;shot.Team=shooter.Team;
        shot.Velocity=PaintballFlight.LaunchVelocity(from,aim);
    }
    protected override void OnUpdate()
    {
        if(Done)return;
        elapsed+=Time.Delta;stageTime+=Time.Delta;nextShot-=Time.Delta;
        if(elapsed>35){Check(false,"timed out: "+State);Done=true;return;}
        if(stage==0 && stageTime>.5f)
        {
            var actor=bot.Components.Get<PaintballCombatant>();
            if(!actor.IsValid())return;
            actor.StayInMatch=true;bot.Magazine.UnlimitedAmmo=true;
            actor.RegisterHit(90,shooter.WorldPosition,shooter);Next();
        }
        else if(stage==1 && bot.UsingCover)
        {
            original=bot.ReservedCover;originalPosition=bot.WorldPosition;
            Check(original.IsValid(),"entered physical shelter");
            eventsBefore=bot.IncomingPaintEvents;Fire(true);Next();
        }
        else if(stage==2 && stageTime>.8f)
        {
            Check(bot.IncomingPaintEvents==eventsBefore,"distant paint does not create pressure");
            Fire();Next();
        }
        else if(stage==3 && stageTime>1.1f)
        {
            Check(bot.IncomingPaintEvents==eventsBefore+1,$"one physical cover shot counted once (before={eventsBefore}, after={bot.IncomingPaintEvents})");
            Check(bot.CoverRelocations==0 && bot.ReservedCover==original,"one shot does not force immediate relocation");
            Next();
        }
        else if(stage==4)
        {
            if(bot.PressureRelocations>0)
            {
                second=bot.ReservedCover;
                Check(second.IsValid() && second!=original,"sustained fire selects a different structure");
                Check((bot.CoverDestination-originalPosition).WithZ(0).Length>=140,"relocation leaves the original area");
                Check(bot.RecentlyUsed(original),"recent shelter remembered");
                Next();
            }
            else if(nextShot<=0){Fire();nextShot=PaintballFlight.ShotInterval+.02f;}
        }
        else if(stage==5)
        {
            // MoveTo submits a request; GetPath may still contain the old path
            // that frame. Inspect the active route after navigation updates.
            if(!routeChecked && stageTime>.25f)
            {
                routeChecked=true;
                Check(bot.Components.Get<NavMeshAgent>().GetPath().Status==Sandbox.Navigation.NavMeshPathStatus.Complete,"new route reaches its destination");
            }
            if(bot.UsingCover)
            {
                Check(bot.ReservedCover==second && (bot.WorldPosition-originalPosition).WithZ(0).Length>100,"opponent physically reaches new shelter");
                relocationsBefore=bot.PressureRelocations;Next();
            }
        }
        else if(stage==6)
        {
            if(bot.PressureRelocations>relocationsBefore)
            {
                Check(bot.ReservedCover!=original && bot.ReservedCover!=second,"continued fire does not bounce straight back");
                Check(bot.ShotsWhileSheltered==0,"no firing through protective cover");
                Done=true;
            }
            else if(nextShot<=0){Fire();nextShot=PaintballFlight.ShotInterval+.02f;}
        }
    }
}
