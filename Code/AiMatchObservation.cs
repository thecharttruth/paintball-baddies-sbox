using Sandbox;
using System;
namespace PaintballBaddies;

/// <summary>Opt-in observation of the ordinary five-bot match; no injected hits.</summary>
public sealed class AiMatchObservation : Component
{
    private float elapsed;
    private double previous;
    private PlayerController player;
    private readonly List<double> frames=new();
    private readonly HashSet<string> seenTactics=new();
    public bool Done { get; private set; }
    public object Result { get; private set; }
    protected override void OnStart()
    {
        Scene.GetAllComponents<PaintballControls>().First().StartMatch();
        player=Scene.GetAllComponents<PlayerController>().First();
        player.UseInputControls=player.UseLookControls=false;
        previous=RealTime.NowDouble;
    }
    protected override void OnUpdate()
    {
        if(Done)return;
        elapsed+=Time.Delta;
        var now=RealTime.NowDouble;
        if(elapsed>3)frames.Add((now-previous)*1000);
        previous=now;
        var bots=Scene.GetAllComponents<ArenaOpponent>().ToArray();
        foreach(var bot in bots)seenTactics.Add(bot.Tactic);
        if(elapsed<63)return;
        var sorted=frames.OrderBy(x=>x).ToArray();
        Result=new{Seconds=elapsed,BotCount=bots.Length,ShootingBots=bots.Count(x=>x.ShotsFired>0),
            Shots=bots.Sum(x=>x.ShotsFired),ShotsAtPlayers=bots.Sum(x=>x.ShotsAtPlayers),PlayerPaint=player.Components.Get<PaintballCombatant>()?.PaintHits,CoverShots=bots.Sum(x=>x.ShotsFromCover),
            ShelteredShots=bots.Sum(x=>x.ShotsWhileSheltered),CoverChoices=bots.Sum(x=>x.CoverSelections),
            Relocations=bots.Sum(x=>x.CoverRelocations),Flanks=bots.Sum(x=>x.FlankSelections),
            Searches=bots.Sum(x=>x.SearchesStarted),HeardShots=bots.Sum(x=>x.HeardShots),
            Recoveries=bots.Sum(x=>x.NavigationRecoveries),AvoidedBlockedShots=bots.Sum(x=>x.BlockedShotsAvoided),
            Tactics=seenTactics.ToArray(),Frames=frames.Count,MeanMs=frames.Average(),P99Ms=sorted[(int)(sorted.Length*.99)],MaxMs=sorted.Last(),
            Over33Ms=frames.Count(x=>x>33.333),Standings=Scene.GetAllComponents<ArenaMatch>().First().Standings};
        Done=true;Log.Info("AI_MATCH_OBSERVATION "+Json.Serialize(Result));
    }
    protected override void OnDestroy()
    {
        if(player.IsValid())player.UseInputControls=player.UseLookControls=true;
    }
}
