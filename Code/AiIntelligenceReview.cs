using Sandbox;
namespace PaintballBaddies;

/// <summary>Opt-in live perception/decision regressions, never saved in the arena.</summary>
public sealed class AiIntelligenceReview : Component
{
    private PlayerController player;
    private ArenaOpponent bot;
    private GameObject wall, rival;
    private Vector3 original,remembered;
    private bool input,look;
    private float elapsed,stageTime;
    private int stage,shots,changes,heard;
    public bool Done { get; private set; }
    public List<string> Results { get; }=new();
    public string State=>Json.Serialize(new{stage,stageTime,Target=bot?.TargetName,Sees=bot?.SeesPlayer,Memory=bot?.LastKnownPlayerPosition,Shots=bot?.ShotsFired});
    private void Check(bool okay,string label){var text=(okay ? "PASS " : "FAIL ")+label;Results.Add(text);Log.Info("AI_SMART "+text);}
    private void Next(){stage++;stageTime=0;}
    protected override void OnStart()
    {
        Scene.NavMesh.IsEnabled=true;Scene.NavMesh.IncludeStaticBodies=true;
        Scene.NavMesh.IncludeKeyframedBodies=false;Scene.NavMesh.AgentHeight=67;Scene.NavMesh.AgentRadius=17;Scene.NavMesh.SetDirty();
        player=Scene.GetAllComponents<PlayerController>().First();
        original=player.WorldPosition;input=player.UseInputControls;look=player.UseLookControls;
        player.UseInputControls=player.UseLookControls=false;
        var go=new GameObject(GameObject){Name="Smart AI review opponent",WorldPosition=new(-900,1250,1)};
        go.Components.Create<CitizenOpponentPresentation>();
        bot=go.Components.Create<ArenaOpponent>();bot.Character="imani";bot.CombatEnabled=true;
    }
    protected override void OnUpdate()
    {
        if(Done)return;
        elapsed+=Time.Delta;stageTime+=Time.Delta;
        if(elapsed>28){Check(false,"timeout: "+State);Done=true;return;}
        bot.WorldPosition=new(-900,1250,1);bot.WorldRotation=Rotation.Identity;
        var nav=bot.Components.Get<NavMeshAgent>();if(nav.IsValid()){nav.UpdatePosition=false;nav.MaxSpeed=0;nav.Stop();}
        var contact=bot.Components.Get<CloseCombat>();if(contact.IsValid())contact.Enabled=false;
        var actor=bot.Components.Get<PaintballCombatant>();if(actor.IsValid())actor.StayInMatch=true;
        var human=player.Components.Get<PaintballCombatant>();human.StayInMatch=true;
        player.WorldPosition=stage==0 ? new(-1080,1250,4) : stage==4 || stage==5 ? new(-640,1250,4) : stage==7 ? new(-940,1250,4) : new(-700,1250,4);
        player.Body.Velocity=player.WishVelocity=Vector3.Zero;
        if(stage==0 && stageTime>1){Check(!bot.SeesPlayer,"unseen rear opponent not acquired");Next();}
        else if(stage==1 && stageTime>1.2f)
        {
            Check(bot.SeesPlayer,"front opponent acquired");
            wall=new GameObject(GameObject){Name="Smart AI sight blocker",WorldPosition=new(-750,1250,24)};
            var box=wall.Components.Create<BoxCollider>();box.Static=true;box.Scale=new(12,220,48);
            Next();
        }
        else if(stage==2 && stageTime>1.2f)
        {
            Check(bot.SeesPlayer && bot.CombatAimPoint.z>54,"exposed head noticed above chest-height cover");
            wall.WorldPosition=new(-750,1250,60);wall.Components.Get<BoxCollider>().Scale=new(12,220,120);Next();
        }
        else if(stage==3 && stageTime>1)
        {Check(!bot.SeesPlayer,"full cover blocks sight");shots=bot.ShotsFired;remembered=bot.LastKnownPlayerPosition;Next();}
        else if(stage==4 && stageTime>1)
        {
            Check((bot.LastKnownPlayerPosition-remembered).Length<2,"hidden movement does not update visual memory");
            Check(bot.ShotsFired==shots,"no shots continue through fully blocking cover");
            // Hearing is deliberately lower priority than a committed cover
            // plan; release that unrelated tactic for this isolated check.
            bot.InterruptForContact();
            heard=bot.HeardShots;bot.HearMarker(human.GameObject,new(-660,1250,60));Next();
        }
        else if(stage==5 && stageTime>.1f)
        {
            Check(bot.HeardShots==heard+1 && !bot.SeesPlayer,"nearby occluded shot heard without granting sight");
            Check((bot.LastKnownPlayerPosition-new Vector3(-630,1260,1)).Length<2,"hearing stores approximate launch location");
            heard=bot.HeardShots;bot.HearMarker(human.GameObject,new(1500,1250,60));
            Check(bot.HeardShots==heard,"distant shots do not grant awareness");wall.Destroy();Next();
        }
        else if(stage==6 && stageTime>1)
        {
            Check(bot.SeesPlayer,"target reacquired after obstacle removed");Next();
        }
        else if(stage==7 && stageTime>.8f)
        {
            Check(bot.CanObserve(human,out _),"unobstructed opponent inside personal space noticed behind");Next();
        }
        else if(stage==8 && stageTime>1)
        {
            bot.ContestMode=true;
            rival=new GameObject(GameObject){Name="Smart AI alternative target",WorldPosition=new(-710,1290,1)};
            var rivalState=rival.Components.Create<PaintballCombatant>();rivalState.Team=89;rivalState.StayInMatch=true;
            var hull=rival.Components.Create<BoxCollider>();hull.Scale=new(28,28,66);hull.Center=new(0,0,33);
            changes=bot.TargetChanges;Next();
        }
        else if(stage==9)
        {
            rival.WorldPosition=new(-710+System.MathF.Sin(stageTime*12)*12,1290,1);
            if(stageTime<2)return;
            Check(bot.TargetChanges-changes<=2,"weighted choices retain target commitment without thrashing");
            Check(!bot.Reachable(new Vector3(5000,5000,1),750,out _,out _),"off-arena flank destination rejected by native navmesh");
            Check(bot.Reachable(new Vector3(-900,1400,1),750,out _,out _),"nearby clear route accepted by native navmesh");
            actor.RegisterHit(0,new Vector3(-650,1200,50),human,new Vector3(-620,1190,50));
            Check(actor.LastHitOrigin==new Vector3(-620,1190,50),"hit memory uses recorded launch origin");
            Done=true;
        }
    }
    protected override void OnDestroy()
    {
        if(player.IsValid()){player.WorldPosition=original;player.UseInputControls=input;player.UseLookControls=look;player.Body.Velocity=Vector3.Zero;}
    }
}
