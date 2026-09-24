using Sandbox;
using System;

namespace PaintballBaddies;

public sealed partial class ArenaOpponent
{
    // View-cone/line-of-sight approach adapted from Facepunch's MIT NPC senses.
    // Pinned originals and license: source-assets/library-code/facepunch-npc/.
    private float targetCommitment, timeSinceVisual=100,shotLaneRetry,blockedLaneTime;
    private Vector3 observedVelocity, previousVisualPosition, observedAimPoint;
    private readonly List<Vector3> visibleThreats=new();
    public int TargetChanges { get; private set; }
    public int HeardShots { get; private set; }
    public int BlockedShotsAvoided { get; private set; }
    public int HumanTargetChoices {get;private set;}
    public int BotTargetChoices {get;private set;}
    public string TargetName => target?.GameObject.Name;
    public Vector3 CombatAimPoint => SeesPlayer ? observedAimPoint : LastKnownPlayerPosition+Vector3.Up*40;
    internal IReadOnlyList<Vector3> VisibleThreats => visibleThreats;

    internal bool CanObserve(PaintballCombatant candidate,out Vector3 aim)
    {
        aim=default;
        if(!candidate.IsValid() || !candidate.GameObject.Active || !candidate.AcceptHits || candidate.Eliminated)return false;
        var delta=candidate.WorldPosition-WorldPosition;
        // Nearby opponents are noticeable even just outside the forward cone,
        // but still require an unobstructed view.
        if(delta.Length>55 && !InSightCone(WorldPosition,WorldRotation.Forward,candidate.WorldPosition))return false;
        var eye=WorldPosition+Vector3.Up*(CoverCrouched ? 35 : 60);
        var head=candidate.EyePosition;
        var chest=candidate.WorldPosition+(head-candidate.WorldPosition)*.66f;
        foreach(var point in new[]{chest,head})
        {
            var sight=Scene.Trace.Ray(eye,point).IgnoreGameObjectHierarchy(GameObject).WithoutTags("paintball_debris").Run();
            if(!sight.Hit || PaintballCombatant.Find(sight.GameObject)==candidate){aim=point;return true;}
        }
        return false;
    }

    private void ChangeTarget(PaintballCombatant next)
    {
        if(next==target)return;
        target=next;TargetChanges++;visibleTime=0;SeesPlayer=false;
        observedVelocity=Vector3.Zero;timeSinceVisual=100;targetCommitment=1.4f;blockedLaneTime=0;
    }

    private void ObserveTarget()
    {
        bool seen=CanObserve(target,out var point);
        if(seen)
        {
            // Estimate only from consecutive observations, never hidden velocity.
            var sample=timeSinceVisual<.65f ? (target.WorldPosition-previousVisualPosition)/MathF.Max(timeSinceVisual,.05f) : Vector3.Zero;
            if(sample.Length>260)sample=Vector3.Zero; // teleport/discontinuity
            observedVelocity=Vector3.Lerp(observedVelocity,sample,.45f);
            previousVisualPosition=target.WorldPosition;timeSinceVisual=0;
            observedAimPoint=point;LastKnownPlayerPosition=target.WorldPosition;
            lostSight=0;hasSeenPlayer=true;searchRemaining=0;
        }
        else { lostSight+=BotCombatTuning.DecisionSeconds;visibleTime=0;observedVelocity=Vector3.Zero; }
        SeesPlayer=seen;
    }

    private void SelectContestTarget()
    {
        PaintballCombatant best=null,bestHuman=null,bestBot=null;
        float bestCost=float.MaxValue,currentCost=float.MaxValue,humanCost=float.MaxValue,botCost=float.MaxValue;
        visibleThreats.Clear();
        foreach(var candidate in Scene.GetAllComponents<PaintballCombatant>())
        {
            if(candidate==combatant || candidate.Team==combatant.Team || !CanObserve(candidate,out _))continue;
            visibleThreats.Add(candidate.WorldPosition);
            float cost=(candidate.WorldPosition-WorldPosition).Length;
            if(candidate==target)cost*=.7f;
            if(combatant.HitFeedbackRemaining>0 && candidate==combatant.LastAttacker)cost*=.55f;
            if(candidate.Components.Get<CloseCombat>()?.Incapacitated==true)cost*=1.5f;
            if(candidate==target)currentCost=cost;
            if(cost<bestCost){best=candidate;bestCost=cost;}
            if(candidate.Components.Get<PlayerController>().IsValid())
            {if(cost<humanCost){bestHuman=candidate;humanCost=cost;}}
            else if(cost<botCost){bestBot=candidate;botCost=cost;}
        }
        // Commitment prevents jitter between similarly placed opponents. A
        // substantially closer visible danger can still interrupt immediately.
        // Finish moving to chosen shelter unless a newly visible opponent is
        // within elbow/close marker range. Actual hits interrupt separately.
        if(HasCoverPlan && !holdingCover && !CoverPeeking && best.IsValid() && (best.WorldPosition-WorldPosition).Length>100)return;
        if(!best.IsValid())return;
        bool urgent=bestCost<currentCost*.6f && currentCost<float.MaxValue;
        if(urgent){ChangeTarget(best);return;}
        if(targetCommitment>0 && !(currentCost==float.MaxValue && lostSight>.8f))return;
        // Pick the class first, so five bots do not dilute one human's share.
        // Roll only at a commitment boundary, never every decision or shot.
        bool heist=MultiplayerSession.Find(Scene)?.IsHeist==true;
        bool human=BotCombatTuning.PreferHuman(bestHuman.IsValid(),bestBot.IsValid(),Game.Random.Float(0,1));
        var selected=heist ? best : human ? bestHuman : bestBot;
        if(heist)human=selected.Components.Get<PlayerController>().IsValid();
        ChangeTarget(selected);targetCommitment=Game.Random.Float(1.8f,2.4f);
        if(human)HumanTargetChoices++;else BotTargetChoices++;
    }

    internal void HearMarker(GameObject shooter,Vector3 origin)
    {
        if(!CombatEnabled || !combatant.IsValid() || combatant.Eliminated || timeSinceVisual<1.2f
            || EvadingAfterHit || HasCoverPlan || Components.Get<CloseCombat>()?.Incapacitated==true)return;
        var source=PaintballCombatant.Find(shooter);
        if(!source.IsValid() || source==combatant || source.Team==combatant.Team || (origin-WorldPosition).Length>650)return;
        var soundPath=Scene.Trace.Ray(origin,WorldPosition+Vector3.Up*45)
            .IgnoreGameObjectHierarchy(GameObject).IgnoreGameObjectHierarchy(shooter).WithoutTags("paintball_debris").Run();
        if(soundPath.Hit && (origin-WorldPosition).Length>340)return;
        if(impactLookTime>0)return;
        HeardShots++;ChangeTarget(source);
        // A sound gives an approximate launch location, not live tracking.
        LastKnownPlayerPosition=new Vector3(MathF.Round(origin.x/70)*70,MathF.Round(origin.y/70)*70,WorldPosition.z);
        hasSeenPlayer=true;lostSight=1.5f;
        impactLookDirection=(LastKnownPlayerPosition-WorldPosition).WithZ(0).Normal;
        impactLookTime=.6f;decisionTime=0;agent.Stop();
    }

    internal static void ReportMarkerSound(Scene scene,GameObject shooter,Vector3 origin)
    {
        foreach(var opponent in scene.GetAllComponents<ArenaOpponent>())
            if(opponent.Enabled && opponent.GameObject.Active)opponent.HearMarker(shooter,origin);
    }
}
