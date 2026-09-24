using Sandbox;
namespace PaintballBaddies;

/// <summary>Opt-in projectile integration check, not a human difficulty assessment.</summary>
public sealed class PhysicalOpponentHitChecks : Component
{
    [Property] public bool CheckFeedbackReset {get;set;}
    private float doneTime;
    private bool resetChecked;
    [Property] public bool FullRound { get; set; }
    private PlayerController player;
    private PaintballMarker marker;
    private ArenaMatch match;
    private float elapsed;
    private bool started,done;
    private int movingHits;
    private bool sawHitFeedback,sawEliminationFeedback;
    private readonly Dictionary<ArenaOpponent,(int Hits,float Speed)> samples=new();
    protected override void OnStart()
    {
        player=Scene.GetAllComponents<PlayerController>().First();
        marker=player.Components.Get<PaintballMarker>();
        match=player.Components.Get<ArenaMatch>();
    }
    protected override void OnUpdate()
    {
        if(done)
        {
            doneTime+=Time.Delta;
            if(CheckFeedbackReset && !resetChecked && doneTime>.6f)
            {
                marker.ResetTraining();resetChecked=true;
                Log.Info($"FEEDBACK_RESET {(marker.HitFlash==0 && marker.EliminationFlash==0 ? "PASS":"FAIL")} after accepted physical elimination");
            }
            return;
        }
        elapsed+=Time.Delta;
        if(!started)
        {
            if(elapsed<1)return;
            match.StartRound();
            // Only the unattended player's durability changes. Opponent health,
            // navigation, cover, shooting and projectile collisions stay live.
            match.PlayerState.HitLimit=1000;
            player.UseInputControls=false;player.UseCameraControls=false;
            marker.AcceptInput=false;started=true;
            return;
        }
        sawHitFeedback |= marker.HitFlash>0;
        sawEliminationFeedback |= marker.EliminationFlash>0;
        var allBots=Scene.GetAllComponents<ArenaOpponent>().ToArray();
        foreach(var bot in allBots)
        {
            var hits=bot.Components.Get<PaintballCombatant>().PaintHits;
            if(samples.TryGetValue(bot,out var prior) && hits>prior.Hits && prior.Speed>20)
                movingHits+=hits-prior.Hits;
            samples[bot]=(hits,bot.MovementSpeed);
        }
        var bots=allBots.Where(b=>b.Components.Get<PaintballCombatant>()?.Eliminated==false).ToArray();
        if((FullRound ? !match.InRound : match.OpponentsRemaining<3) || elapsed>(FullRound ? 55 : 35))
        {
            done=true;
            var success=FullRound ? match.Status=="VICTORY" && marker.Hits==9 : match.OpponentsRemaining<3 && marker.Hits>=3;
            Log.Info($"PHYSICAL_BOT {(success && movingHits>0 && sawHitFeedback && sawEliminationFeedback ? "PASS":"FAIL")} fullRound={FullRound} status={match.Status} remaining={match.OpponentsRemaining} hits={marker.Hits} shots={marker.Shots} movingTargetHits={movingHits} hitFeedback={sawHitFeedback} eliminationFeedback={sawEliminationFeedback} elapsed={elapsed}");
            return;
        }
        if(marker.Ammo==0)marker.Reload();
        foreach(var bot in bots.OrderBy(b=>(b.WorldPosition-player.WorldPosition).Length))
        {
            var shape=bot.Components.Get<BoxCollider>();
            var aim=bot.WorldPosition+Vector3.Up*shape.Center.z;
            var ray=Scene.Trace.Ray(marker.Muzzle,aim).IgnoreGameObjectHierarchy(player.GameObject).Run();
            if(!ray.Hit || PaintballCombatant.Find(ray.GameObject)!=bot.Components.Get<PaintballCombatant>())continue;
            var flight=(aim-marker.Muzzle).Length/2283;
            aim+=bot.WorldRotation.Forward*bot.MovementSpeed*flight;
            player.EyeAngles=Rotation.LookAt(aim-player.EyePosition).Angles();
            marker.FireAt(aim);
            break;
        }
    }
}
