using Sandbox;
namespace PaintballBaddies;
/// <summary>Opt-in native cover/reload HUD review.</summary>
public sealed class HudStateReview : Component
{
    [Property] public float MatchClockSeconds { get; set; }
    [Property] public string ResultMode { get; set; } = "";
    [Property] public string CaptureId { get; set; } = "";
    [Property] public bool AmmoReview { get; set; }
    [Property] public int AmmoTarget { get; set; } = 8;
    [Property] public bool ReviewReload { get; set; }
    private PlayerController player;
    private PaintballMarker weapon;
    private CoverController cover;
    private GameObject fixture;
    private float elapsed;
    private int phase;
    protected override void OnStart()
    {
        player=Scene.GetAllComponents<PlayerController>().First();weapon=player.Components.Get<PaintballMarker>();cover=player.Components.Get<CoverController>();
        if (MatchClockSeconds > 0) return;
        if(AmmoReview) { player.UseInputControls=false;player.UseLookControls=false;return; }
        player.UseInputControls=false;player.UseLookControls=false;cover.TestInput=true;
        player.WorldPosition=new Vector3(-595,70,2);player.EyeAngles=new Angles(0,0,0);
        fixture=TrainingRange.Box(null,"HUD review cover",new Vector3(-550,70,24),new Vector3(20,180,48),Color.Gray,true);
        fixture.Components.Create<CoverSurface>();
    }
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;
        if(AmmoReview)
        {
            if(elapsed>8)AmmoTarget=0;
            if(elapsed>10)ReviewReload=true;
            if(ReviewReload && phase==0) { weapon.Reload();phase=1; }
            if(!ReviewReload && weapon.Ammo>AmmoTarget && elapsed>.5f)
                weapon.FireAt(weapon.Muzzle+player.EyeAngles.Forward*1000);
            return;
        }
        if (MatchClockSeconds > 0)
        {
            var match = Scene.GetAllComponents<ArenaMatch>().FirstOrDefault();
            if (phase == 0)
            {
                if (match?.PlayerState is null) return;
                match.RoundDuration = MatchClockSeconds;
                match.StartRound();
                phase = 1;
            }
            else if (phase == 1 && elapsed > .5f && ResultMode != "")
            {
                var bots=Scene.GetAllComponents<ArenaOpponent>().Where(x => x.Components.Get<PaintballCombatant>() is not null).ToArray();
                if(bots.Length != match.OpponentCount) return;
                foreach(var bot in bots.Take(ResultMode == "victory" ? bots.Length : 2))
                {
                    var state=bot.Components.Get<PaintballCombatant>();
                    while(!state.Eliminated) state.RegisterHit(0);
                }
                if(ResultMode == "eliminated")
                    while(!match.PlayerState.Eliminated) match.PlayerState.RegisterHit(1);
                phase=2;
            }
            else if (phase == 2 && elapsed > 1)
            {
                Log.Info("HUD_RESULT_CHECK "+Json.Serialize(new { capture_id=CaptureId, mode=ResultMode, status=match.Status, eliminated=match.OpponentsEliminated, total=match.OpponentCount, remaining=match.OpponentsRemaining, hits=match.PlayerState.PaintHits, in_round=match.InRound }));
                phase=3;
            }
            else if (phase == 3 && elapsed > 3)
            {
                match.StartRound();
                Log.Info("HUD_RESTART_CHECK "+Json.Serialize(new { capture_id=CaptureId, status=match.Status, eliminated=match.OpponentsEliminated, total=match.OpponentCount, remaining=match.OpponentsRemaining, hits=match.PlayerState.PaintHits, in_round=match.InRound }));
                phase=4;
            }
            // Allow OnStart to create each combatant before disabling AI fire.
            foreach(var bot in Scene.GetAllComponents<ArenaOpponent>())
                if(bot.Components.Get<PaintballCombatant>() is not null) bot.CombatEnabled=false;
            return;
        }
        if(phase==0 && elapsed>.35f){weapon.FireAt(new Vector3(-450,70,65));phase=1;}
        if(phase==1 && elapsed>.7f){cover.TryEnter();weapon.Reload();phase=2;}
        if(phase==2 && elapsed>1.7f)
        {
            Log.Info($"HUD_STATE cover={cover.Attached} reloadProgress={weapon.ReloadProgress} reloadRemaining={weapon.ReloadRemaining} duration={weapon.ActiveReloadDuration}");phase=3;
        }
    }
    protected override void OnDestroy(){if(fixture.IsValid())fixture.Destroy();}
}
