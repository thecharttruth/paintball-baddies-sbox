using Sandbox;
namespace PaintballBaddies;

/// <summary>Opt-in scoring, unlimited ammunition and actual one-minute match check.</summary>
public sealed class PaintRushReview : Component
{
    [Property] public string CaptureId { get; set; } = "";
    private ArenaMatch match;
    private PlayerController player;
    private PaintballMarker weapon;
    private float elapsed,fireClock;
    private int stage;
    private void Check(string name,bool passed) => Log.Info("PAINT_RUSH_CHECK "+Json.Serialize(new{capture_id=CaptureId,name,passed}));
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;
        if(stage==0 && elapsed>1)
        {
            match=Scene.GetAllComponents<ArenaMatch>().First();
            player=match.Components.Get<PlayerController>();weapon=match.Components.Get<PaintballMarker>();
            Check("duration settings",match.SetRoundMinutes(3) && match.RoundDuration==180 && match.SetRoundMinutes(2) && match.RoundDuration==120 && !match.SetRoundMinutes(4) && match.SetRoundMinutes(1));
            match.StartRound();player.UseInputControls=false;player.UseLookControls=false;stage=1;
        }
        if(stage==1 && elapsed>2)
        {
            var actors=Scene.GetAllComponents<PaintballCombatant>().Where(x=>x.StayInMatch).ToArray();
            Check("six distinct competitors",actors.Length==6 && actors.Select(x=>x.Team).Distinct().Count()==6);
            Check("duration locked during round",!match.SetRoundMinutes(3));
            var receiver=actors.First(x=>x!=match.PlayerState);
            var before=match.PlayerState.HitsLanded;
            for(int i=0;i<50;i++)receiver.RegisterHit(match.PlayerState.Team,player.WorldPosition,match.PlayerState);
            Check("hits score without elimination",receiver.PaintHits>=50 && !receiver.Eliminated && match.PlayerState.HitsLanded==before+50 && match.InRound);
            var current=receiver.PaintHits;
            Check("self hits rejected",!receiver.RegisterHit(receiver.Team,receiver.WorldPosition,receiver) && receiver.PaintHits==current);
            weapon.Reload();Check("no forced player reload",weapon.UnlimitedAmmo && weapon.ReloadRemaining==0);
            stage=2;
        }
        if(stage==2 && match.InRound)
        {
            fireClock-=Time.Delta;
            if(fireClock<=0){weapon.FireAt(weapon.Muzzle+Vector3.Up*1000);fireClock=.12f;}
        }
        if(stage==2 && !match.InRound)
        {
            var bots=Scene.GetAllComponents<ArenaOpponent>().Where(x=>x.ContestMode).ToArray();
            Check("one minute ends on timer",match.Status=="TIME UP" && match.Remaining==0 && elapsed>=61 && elapsed<65);
            Check("unlimited player firing",weapon.Shots>80 && weapon.Ammo==40 && weapon.Reserve==160 && weapon.ReloadRemaining==0);
            Check("unlimited AI ammunition",bots.Length==5 && bots.All(x=>x.Magazine.Ammo==30 && x.Magazine.Reserve==120 && x.Magazine.Reloads==0));
            Check("everyone remains visible",bots.All(x=>x.GameObject.Active && !x.Components.Get<PaintballCombatant>().Eliminated));
            Check("score conservation",match.Standings.Sum(x=>x.Points)==0);
            Check("rank ordering",match.Standings.Zip(match.Standings.Skip(1),(a,b)=>a.Points>b.Points || a.Points==b.Points && a.Received<=b.Received).All(x=>x));
            var hits=match.PlayerState.PaintHits;
            Check("score freezes at horn",!match.PlayerState.RegisterHit(99) && match.PlayerState.PaintHits==hits);
            Log.Info("PAINT_RUSH_RESULT "+Json.Serialize(new{capture_id=CaptureId,standings=match.Standings,shots=weapon.Shots,bot_shots=bots.Sum(x=>x.ShotsFired)}));
            stage=3;
        }
    }
}
