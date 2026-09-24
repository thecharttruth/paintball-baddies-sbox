using Sandbox;
namespace PaintballBaddies;
public sealed class AiTacticsReview : Component
{
    PlayerController player;ArenaOpponent bot;float elapsed,next;int hiddenShots,peekShots,lastShots;bool sheltered,shifted;ArenaShield shield;bool stowed,exposed;float firstShot=-1;
    protected override void OnStart()
    {
        player=Scene.GetAllComponents<PlayerController>().First();player.UseInputControls=player.UseLookControls=false;
        player.WorldPosition=new(-630,100,1);
        Scene.GetAllComponents<TrainingRange>().First().SetTargetsVisible(false);
        Scene.NavMesh.IsEnabled=true;Scene.NavMesh.IncludeStaticBodies=true;Scene.NavMesh.IncludeKeyframedBodies=false;Scene.NavMesh.AgentHeight=67;Scene.NavMesh.AgentRadius=17;Scene.NavMesh.SetDirty();
        var go=new GameObject(true,"AI tactics review opponent");go.WorldPosition=new(-190,-40,1);go.WorldRotation=Rotation.LookAt(player.WorldPosition-go.WorldPosition);
        go.Components.Create<CitizenOpponentPresentation>();bot=go.Components.Create<ArenaOpponent>();bot.Character="imani";bot.CombatEnabled=true;bot.ContestMode=true;
    }
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;
        if(elapsed>next){next=elapsed+1;Log.Info($"AI_TRACE t={elapsed} pos={bot.WorldPosition} cover={bot.UsingCover} peek={bot.CoverPeeking} sees={bot.SeesPlayer} plan={bot.HasCoverPlan} destination={bot.CoverDestination} nav={bot.Components.Get<NavMeshAgent>()?.TargetPosition} block={bot.ShotBlockReason}");}
        if(shield is null && elapsed>1){shield=Scene.GetAllComponents<ArenaShield>().First();shield.WorldPosition=bot.WorldPosition+new Vector3(0,0,24);}
        if(shield is not null && shield.Owner.IsValid()){stowed|=bot.UsingCover&&shield.Stowed;exposed|=bot.CoverPeeking&&!shield.Stowed;}
        if(bot.ShotsFired>0&&firstShot<0)firstShot=elapsed;
        var state=player.Components.Get<PaintballCombatant>();if(state.IsValid())state.StayInMatch=true;
        if(bot.UsingCover){sheltered=true;hiddenShots+=bot.ShotsFired-lastShots;}
        if(bot.CoverPeeking)peekShots+=bot.ShotsFired-lastShots;
        lastShots=bot.ShotsFired;
        if(elapsed>17&&!shifted){shifted=true;player.WorldPosition=new(180,0,1);}
        if(elapsed>31)
        {
            Log.Info("AI_TACTICS "+Json.Serialize(new{bot.CoverSelections,firstShot,ShieldCollected=shield?.Owner?.GameObject==bot.GameObject,stowed,exposed,sheltered,bot.CoverBursts,bot.ShotsFromCover,bot.ShotsWhileSheltered,bot.FlankSelections,bot.ObstructedMoves,bot.ShotsFired,bot.DistanceTravelled,PlayerHits=state?.PaintHits}));Destroy();
        }
    }
    protected override void OnDestroy(){if(bot.IsValid())bot.GameObject.Destroy();if(player.IsValid())player.UseInputControls=player.UseLookControls=true;}
}
