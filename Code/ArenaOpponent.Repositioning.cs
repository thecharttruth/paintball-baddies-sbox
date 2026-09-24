using Sandbox;
using System;
namespace PaintballBaddies;

public sealed partial class ArenaOpponent
{
    public int IncomingPaintEvents { get; private set; }
    public int CoverRelocations { get; private set; }
    public int PressureRelocations { get; private set; }
    public bool RelocatingCover { get; private set; }
    public bool RepositionPending { get; private set; }
    public string LastRelocationReason { get; private set; }
    public CoverSurface LastVacatedCover { get; private set; }
    public float IncomingPressure { get; private set; }
    public float CoverStayLimit { get; private set; }=10;
    private float sinceIncomingPaint=100,pressureThreshold=4.5f,repositionDelay,repositionRetry;
    private float coverRestDuration=.85f,coverCycleDuration=2.6f,coverTravelLimit=4.5f;
    private readonly List<CoverMemory> recentlyUsedCover=new();
    private sealed class CoverMemory { public CoverSurface Surface; public float Remaining; }

    private void AdvanceCoverAwareness(float delta)
    {
        sinceIncomingPaint+=delta;
        IncomingPressure=MathF.Max(0,IncomingPressure-delta*.55f);
        repositionDelay=MathF.Max(0,repositionDelay-delta);
        repositionRetry=MathF.Max(0,repositionRetry-delta);
        foreach(var memory in recentlyUsedCover)memory.Remaining-=delta;
        recentlyUsedCover.RemoveAll(x=>x.Remaining<=0 || !x.Surface.IsValid());
    }

    // Only actual nearby projectile segments/impacts report this, once per ball.
    // The origin is captured at launch, not read from a hidden shooter's position.
    internal void ObserveIncomingPaint(GameObject shooter,Vector3 launchOrigin)
    {
        var attacker=PaintballCombatant.Find(shooter);
        if(!Enabled || !CombatEnabled || !combatant.IsValid() || combatant.Eliminated
            || !attacker.IsValid() || attacker==combatant || attacker.Team==combatant.Team)return;
        IncomingPaintEvents++;
        IncomingPressure=MathF.Min(10,IncomingPressure+1);
        sinceIncomingPaint=0;
        if(target==attacker || (!SeesPlayer && !HasCoverPlan && targetCommitment<=0))
        {
            ChangeTarget(attacker);LastKnownPlayerPosition=launchOrigin.WithZ(WorldPosition.z);
            hasSeenPlayer=true;lostSight=0;
        }
    }

    internal bool RecentlyUsed(CoverSurface surface)=>recentlyUsedCover.Any(x=>x.Surface==surface);

    private void RememberCurrentCover()
    {
        if(!ReservedCover.IsValid())return;
        recentlyUsedCover.RemoveAll(x=>x.Surface==ReservedCover);
        recentlyUsedCover.Add(new CoverMemory{Surface=ReservedCover,Remaining=Game.Random.Float(10,16)});
        if(recentlyUsedCover.Count>3)recentlyUsedCover.RemoveAt(0);
        LastVacatedCover=ReservedCover;
    }

    private void VaryCoverCycle()
    {
        coverRestDuration=Game.Random.Float(.55f,.9f);
        coverCycleDuration=coverRestDuration+Game.Random.Float(1.25f,1.9f);
    }

    private void BeginCoverStay()
    {
        RelocatingCover=false;RepositionPending=false;
        CoverStayLimit=Game.Random.Float(7,11);
        pressureThreshold=Game.Random.Float(3.5f,5.5f);
        repositionRetry=0;VaryCoverCycle();
    }

    private bool UpdateRepositionDecision()
    {
        bool underFire=IncomingPressure>=pressureThreshold && sinceIncomingPaint<1.2f;
        bool blockedLane=blockedLaneTime>1;
        if(!RepositionPending && heldCoverTime>1 && (underFire || blockedLane || heldCoverTime>CoverStayLimit))
        {
            RepositionPending=true;repositionDelay=Game.Random.Float(.25f,.65f);
            LastRelocationReason=underFire ? "incoming paint" : blockedLane ? "blocked firing lane" : "change firing angle";
        }
        if(!RepositionPending || repositionDelay>0 || repositionRetry>0)return false;
        repositionRetry=Game.Random.Float(.75f,1.2f);
        // Keep the current shelter until an unoccupied, reachable alternative
        // exists. A failed search must not send her back into the firing lane.
        if(!OpponentCoverPlanner.TryFind(GameObject,target.GameObject,LastKnownPlayerPosition,out var point,
            requirePeek:LastRelocationReason!="incoming paint",relocating:true))return false;
        var next=OpponentCoverPlanner.ShelteringSurface(GameObject,target.GameObject,LastKnownPlayerPosition,point);
        if(!next.IsValid() || next==ReservedCover)return false;
        RememberCurrentCover();
        CoverDestination=point;ReservedCover=next;CoverSelections++;CoverRelocations++;
        if(LastRelocationReason=="incoming paint")PressureRelocations++;
        seekingCover=true;holdingCover=UsingCover=CoverPeeking=false;
        RelocatingCover=true;RepositionPending=false;
        coverTime=heldCoverTime=coverRest=blockedLaneTime=0;
        coverTravelLimit=8;IncomingPressure*=.2f;
        evadeAfterHit=MathF.Max(evadeAfterHit,1.2f);flankRemaining=0;
        agent.MoveTo(point);
        return true;
    }
}
