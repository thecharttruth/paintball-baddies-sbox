using Sandbox;
using Sandbox.Navigation;
using System;

namespace PaintballBaddies;

public sealed partial class ArenaOpponent
{
    private float stalledTime,navigationRetry,searchRemaining,searchPause;
    private int searchStep;
    private Vector3 searchOrigin,searchDestination;
    public int NavigationRecoveries { get; private set; }
    public int SearchesStarted { get; private set; }
    public bool Searching => searchRemaining>0;
    public Vector3 FlankDestination => flankDestination;
    public string Tactic => EvadingAfterHit ? "Evade" : RelocatingCover ? "Relocate" : CoverPeeking ? "Peek"
        : UsingCover ? "Shelter" : HasCoverPlan ? "Seek cover" : Flanking ? "Flank" : Searching ? "Search"
        : SeesPlayer ? "Engage" : "Patrol";

    internal bool Reachable(Vector3 candidate,float maxLength,out Vector3 point,out float length)
    {
        point=default;length=0;
        var nearest=Scene.NavMesh.GetClosestPoint(candidate,40);
        if(nearest is not Vector3 snapped || MathF.Abs(snapped.z-WorldPosition.z)>36)return false;
        var path=Scene.NavMesh.CalculatePath(new CalculatePathRequest{Start=WorldPosition,Target=snapped,Agent=agent});
        if(path.Status!=NavMeshPathStatus.Complete || path.Points.Count<1)return false;
        var previous=WorldPosition;
        foreach(var step in path.Points){length+=(step.Position-previous).Length;previous=step.Position;}
        if(length>maxLength)return false;
        point=snapped;return true;
    }

    private bool TryStartFlank()
    {
        // Perpendicular flank with forward bias adapted from Facepunch's
        // CombatEngageSchedule (MIT). We additionally validate complete paths,
        // destination space, firing angle and other bots' destinations.
        var toTarget=(LastKnownPlayerPosition-WorldPosition).WithZ(0).Normal;
        if(toTarget.Length<.5f)return false;
        var perpendicular=new Vector3(-toTarget.y,toTarget.x,0);
        float preferred=Game.Random.Float()>.5f ? 1 : -1,best=float.MaxValue;
        Vector3 selected=default;
        foreach(var sign in new[]{preferred,-preferred})
        foreach(var radius in new[]{220f,360f})
        {
            var direction=(perpendicular*sign+toTarget*.3f).Normal;
            if(!Reachable(WorldPosition+direction*radius,750,out var point,out var length))continue;
            if((point-WorldPosition).Length<100 || (point-LastKnownPlayerPosition).Length<110)continue;
            var space=Scene.Trace.Sphere(18,point+Vector3.Up*25,point+Vector3.Up*48).IgnoreGameObjectHierarchy(GameObject).Run();
            if(space.Hit || Scene.GetAllComponents<ArenaOpponent>().Any(x=>x!=this &&
                ((x.WorldPosition-point).Length<85 || (x.Flanking && (x.flankDestination-point).Length<120))))continue;
            var lane=Scene.Trace.Ray(point+Vector3.Up*55,LastKnownPlayerPosition+Vector3.Up*40)
                .IgnoreGameObjectHierarchy(GameObject).IgnoreGameObjectHierarchy(target.GameObject).WithoutTags("paintball_debris").Run();
            float cost=length+(lane.Hit ? 180 : 0)+Game.Random.Float(0,60);
            if(cost>=best)continue;
            best=cost;selected=point;
        }
        if(best==float.MaxValue){flankCooldown=2;return false;}
        flankDestination=selected;flankRemaining=6;flankCooldown=Game.Random.Float(7,10);FlankSelections++;
        return true;
    }

    private bool SearchLastContact()
    {
        if(!hasSeenPlayer || SeesPlayer || lostSight<4)return false;
        if(searchRemaining<=0)
        {
            searchOrigin=LastKnownPlayerPosition;searchStep=0;searchPause=0;searchRemaining=6;SearchesStarted++;
            if(!Reachable(searchOrigin,1600,out searchDestination,out _))searchDestination=WorldPosition;
        }
        if((searchDestination-WorldPosition).WithZ(0).Length>35){agent.MoveTo(searchDestination);return true;}
        agent.Stop();searchPause+=BotCombatTuning.DecisionSeconds;
        var direction=Rotation.FromYaw(WorldRotation.Angles().yaw+55).Forward;
        impactLookDirection=direction;impactLookTime=.35f;
        if(searchPause<.65f)return true;
        searchPause=0;searchStep++;
        if(searchStep>=3){hasSeenPlayer=false;searchRemaining=0;patrol=(patrol+1)%patrolPoints.Length;return false;}
        var offset=Rotation.FromYaw(CharacterIndex*61+searchStep*137).Forward*(120+searchStep*60);
        if(!Reachable(searchOrigin+offset,700,out searchDestination,out _))searchDestination=WorldPosition;
        return true;
    }

    private void AdvanceNavigationRecovery(float delta)
    {
        navigationRetry=MathF.Max(0,navigationRetry-delta);
        if(searchRemaining>0)
        {
            searchRemaining=MathF.Max(0,searchRemaining-delta);
            if(searchRemaining==0){hasSeenPlayer=false;patrol=(patrol+1)%patrolPoints.Length;}
        }
        if(!agent.UpdatePosition || agent.MaxSpeed<=0 || UsingCover || CoverPeeking || navigationRetry>0){stalledTime=0;return;}
        bool wantsTravel=agent.TargetPosition is Vector3 destination && (destination-WorldPosition).WithZ(0).Length>45;
        stalledTime=wantsTravel && MovementSpeed<8 ? stalledTime+delta : 0;
        if(stalledTime<1.5f)return;
        NavigationRecoveries++;stalledTime=0;navigationRetry=2;
        RememberCurrentCover();ReleaseTacticalCover();flankRemaining=0;flankCooldown=0;
        if(hasSeenPlayer && TryStartFlank())agent.MoveTo(flankDestination);
        else {patrol=(patrol+1)%patrolPoints.Length;agent.MoveTo(patrolPoints[patrol]);}
        decisionTime=.5f;
    }
}
