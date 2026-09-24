using Sandbox;
using System;
namespace PaintballBaddies;

public enum ArenaGameMode { PaintRush, PaintHeist }

/// <summary>Optional map-authored bank location. Position on a walkable floor.</summary>
public sealed class HeistBankPoint : Component { }

public sealed partial class MultiplayerSession
{
    [Sync(SyncFlags.FromHost)] public ArenaGameMode Mode {get;private set;}
    [Sync(SyncFlags.FromHost)] public ArenaGameMode ActiveMode {get;private set;}
    [Sync(SyncFlags.FromHost)] public Vector3 BankPosition {get;private set;}
    [Sync(SyncFlags.FromHost)] public int BankIndex {get;private set;}=-1;
    [Sync(SyncFlags.FromHost)] public float BankSeconds {get;private set;}
    public const float BankRadius=86, DepositSeconds=1.25f, BankInterval=30;
    public bool IsHeist=>ActiveMode==ArenaGameMode.PaintHeist;
    public static string ModeTitle(ArenaGameMode mode)=>mode==ArenaGameMode.PaintHeist ? "PAINT HEIST" : "PAINT RUSH";
    public static string ModeKey(ArenaGameMode mode)=>mode==ArenaGameMode.PaintHeist ? "paint_heist" : "paint_rush";
    public string ModeHelp=>HelpFor(Mode);
    public static string HelpFor(ArenaGameMode mode)=>mode==ArenaGameMode.PaintHeist
        ? "Hit opponents to earn chips. Hits spill 1; knockdowns spill half. Walk over chips to collect. Stand in the bank ring for 1.25s to secure them. Most banked wins."
        : "Land paint. Avoid hits. Most points wins. Everyone stays in the fight.";
    private Vector3[] bankLocations=Array.Empty<Vector3>();
    private GameObject bankVisual;
    private int shownBank=-1;
    public string BankNotice {get;private set;}="";
    private float bankNoticeTime;
    private bool bankReady;
    private int visualRound=-1;

    public void SelectMode(ArenaGameMode mode)
    {
        if(!Enum.IsDefined(mode))return;
        if(Online){RequestMode(mode);return;}
        Mode=mode;
    }
    [Rpc.Host]
    private async void RequestMode(ArenaGameMode mode)
    {
        var caller=Rpc.Caller;
        await GameTask.MainThread();
        if(!IsValid || caller!=Connection.Host || !Enum.IsDefined(mode))return;
        Mode=mode;
        if(!InRound)Networking.SetData("pb_mode",ModeKey(Mode));
    }

    private void StartHeistRound()
    {
        foreach(var chip in Scene.GetAllComponents<HeistChip>().ToArray())chip.GameObject.Destroy();
        BankIndex=-1;BankSeconds=0;bankReady=false;bankLocations=Array.Empty<Vector3>();
        if(Online)Networking.SetData("pb_mode",ModeKey(ActiveMode));
    }

    private void TickHeist()
    {
        if(visualRound!=RoundId){visualRound=RoundId;shownBank=-1;BankNotice="";bankNoticeTime=0;}
        bankNoticeTime=MathF.Max(0,bankNoticeTime-Time.Delta);
        if(bankNoticeTime<=0)BankNotice="";
        if(!IsHeist || !InRound)
        {
            if(bankVisual.IsValid())bankVisual.Destroy();
            shownBank=-1;
            if(Authority)foreach(var state in Scene.GetAllComponents<PaintballCombatant>())state.BankProgress=0;
            return;
        }
        if(Authority)
        {
            if(!bankReady && !Scene.NavMesh.IsGenerating)
            {
                var authored=Scene.GetAllComponents<HeistBankPoint>().Where(x=>x.GameObject.Active).OrderBy(x=>x.GameObject.Name).Select(x=>x.WorldPosition).ToArray();
                var candidates=authored.Length>0 ? authored : ArenaOpponent.CreatePatrolRoute();
                bankLocations=candidates.Select(p=>Scene.NavMesh.GetClosestPoint(p,100))
                    .Where(p=>p.HasValue).Select(p=>p.Value).Where(p=>ClearBankPoint(p)).ToArray();
                if(bankLocations.Length>0){bankReady=true;MoveBank();}
            }
            if(bankReady)
            {
                BankSeconds=MathF.Max(0,BankSeconds-Time.Delta);
                if(BankSeconds<=0)MoveBank();
                foreach(var actor in Competitors())AdvanceDeposit(actor,MathF.Min(Time.Delta,Remaining));
            }
        }
        if(BankIndex<0)return;
        if(!bankVisual.IsValid())CreateBankVisual();
        bankVisual.WorldPosition=BankPosition+Vector3.Up*.9f;
        if(shownBank!=BankIndex)
        {
            bool moved=shownBank>=0;shownBank=BankIndex;
            HeistBankBurst.Spawn(Scene,BankPosition,0);
            // The main prompt already explains earning; reserve this notice for relocation.
            BankNotice=moved ? "BANK MOVED — FOLLOW THE ARROW" : "";bankNoticeTime=4;
            if(moved)HeistAudio.Play("bank_move",Scene.Camera?.WorldPosition ?? BankPosition);
        }
    }
    private bool ClearBankPoint(Vector3 p)=>!Scene.Trace.Sphere(25,p+Vector3.Up*28,p+Vector3.Up*60)
        .WithoutTags("paintball_actor","paintball_debris").Run().Hit;
    private IEnumerable<PaintballCombatant> Competitors()=>Scene.GetAllComponents<PaintballCombatant>()
        .Where(x=>x.GameObject.Active && (x.Components.Get<NetworkPawn>().IsValid() || x.Components.Get<NetworkBot>().IsValid()));
    private void MoveBank()
    {
        // Rotate without an immediate repeat; the marked destination is shared by all peers.
        var next=bankLocations.Where(p=>BankIndex<0 || (p-BankPosition).Length>300).ToArray();
        BankPosition=next.Length>0 ? next[Game.Random.Int(0,next.Length-1)] : bankLocations[0];
        BankIndex++;BankSeconds=BankInterval;
        foreach(var actor in Competitors())actor.BankProgress=0;
    }
    public bool InBank(PaintballCombatant actor)=>BankIndex>=0 && actor.IsValid()
        && MathF.Abs(actor.WorldPosition.z-BankPosition.z)<35
        && (actor.WorldPosition-BankPosition).WithZ(0).Length<=BankRadius
        && !Scene.Trace.Ray(actor.WorldPosition+Vector3.Up*30,BankPosition+Vector3.Up*30)
            .IgnoreGameObjectHierarchy(actor.GameObject).WithoutTags("paintball_actor","paintball_debris").Run().Hit;
    internal void AdvanceDeposit(PaintballCombatant actor,float dt)
    {
        if(!Authority || !InRound || !IsHeist)return;
        if(!actor.AcceptHits || actor.Eliminated || actor.CarriedTokens<=0 || !InBank(actor)
            || actor.Components.Get<CloseCombat>()?.Incapacitated==true || actor.HitFeedbackRemaining>0)
        {actor.BankProgress=0;return;}
        actor.BankProgress=MathF.Min(1,actor.BankProgress+dt/DepositSeconds);
        if(actor.BankProgress<1)return;
        int amount=actor.CarriedTokens;actor.CarriedTokens=0;actor.BankedTokens+=amount;actor.BankProgress=0;
        actor.HeistFeedback("bank",amount);
        if(Online)NetworkEffects.BankSecure(BankPosition,amount);
        else HeistBankBurst.Spawn(Scene,BankPosition,amount);
    }
    internal void HeistHit(PaintballCombatant victim,PaintballCombatant attacker,bool knockdown)
    {
        if(!Authority || !InRound || !IsHeist || !victim.IsValid() || !attacker.IsValid() || victim==attacker)return;
        victim.BankProgress=0;
        int spill=Math.Min(victim.CarriedTokens,knockdown ? (victim.CarriedTokens+1)/2 : 1);
        if(spill>0)
        {
            victim.CarriedTokens-=spill;
            SpawnChips(victim,spill);victim.HeistFeedback("spill",spill);
        }
        if(!knockdown){attacker.CarriedTokens++;attacker.HeistFeedback("earn",1);}
    }
    internal void SpawnChips(PaintballCombatant victim,int amount)
    {
        if(!Authority || amount<=0 || !IsHeist || !InRound)return;
        // One value-bearing stack per spill, bounded to avoid hundreds of network objects.
        var nearby=Scene.GetAllComponents<HeistChip>().FirstOrDefault(c=>!c.Collected && (c.WorldPosition-victim.WorldPosition).Length<50);
        if(nearby.IsValid()){nearby.Amount+=amount;nearby.Lifetime=18;return;}
        var drops=Scene.GetAllComponents<HeistChip>().Where(c=>!c.Collected).ToArray();
        if(drops.Length>=48)
        {
            // Move the oldest stack to the new spill; preserve its value rather than erase it.
            var stack=drops.OrderBy(c=>c.Lifetime).First();stack.Amount+=amount;stack.Lifetime=18;
            stack.WorldPosition=victim.WorldPosition;stack.ResetPickupDelay(victim.GameObject.Id);return;
        }
        var go=new GameObject(false,"Paint token stack"){WorldPosition=victim.WorldPosition};
        go.Tags.Add("paintball_debris");var chip=go.Components.Create<HeistChip>();
        chip.Amount=amount;chip.Round=RoundId;chip.ResetPickupDelay(victim.GameObject.Id);
        go.Enabled=true;
        if(Online){go.NetworkMode=NetworkMode.Object;go.Network.SetOwnerTransfer(OwnerTransfer.Fixed);go.NetworkSpawn();}
    }
    private void CreateBankVisual()
    {
        bankVisual=new GameObject(true,"Paint Heist bank ring"){NetworkMode=NetworkMode.Never};
        bankVisual.Tags.Add("paintball_debris");
        bankVisual.Components.Create<HeistBankPresentation>();
    }
}
