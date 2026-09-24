using Sandbox;
using System;
namespace PaintballBaddies;

/// <summary>Opt-in editor baseline. Reports pacing, never asserts shipping performance.</summary>
public sealed class MatchPerformanceProbe : Component
{
    [Property] public float SampleSeconds { get; set; }
    [Property] public bool StressImpacts { get; set; }
    [Property] public bool MovingCamera { get; set; }
    [Property] public bool YardCamera { get; set; }
    [Property] public bool SixCharacters { get; set; }
    [Property] public bool CitizenCharacters { get; set; }
    [Property] public bool ActiveFirefight { get; set; }
    [Property] public bool MovingTarget { get; set; }
    [Property] public bool MobileOpponents { get; set; }
    private readonly HashSet<ArenaOpponent> stagedBots=new();
    private CitizenPlayerPresentation citizenPlayer;
    private RosterSelection citizenRoster;
    private bool previousCitizen;
    private readonly List<CitizenOpponentPresentation> citizenBots=new();
    private readonly List<GameObject> extraOpponents = new();
    private PlayerController player;
    private bool restoreCamera;
    private Vector3 originalPosition;
    private Rotation originalRotation;
    private double nextImpact;
    private int injectedImpacts,peakDroplets;
    private readonly List<double> frames=new();
    private readonly List<(double Elapsed, double Milliseconds)> hitches=new();
    private double start,previous;
    private bool started;
    private double firstShotAt=-1;
    protected override void OnUpdate()
    {
        var match=Scene.GetAllComponents<ArenaMatch>().FirstOrDefault();
        if(!match.IsValid() || !match.PlayerState.IsValid())return;
        var now=RealTime.NowDouble;
        if(!started)
        {
            player=Scene.GetAllComponents<PlayerController>().FirstOrDefault();
            if(MovingCamera && player.IsValid())
            {
                restoreCamera=player.UseCameraControls;player.UseCameraControls=false;
                originalPosition=WorldPosition;originalRotation=WorldRotation;
            }
            if(CitizenCharacters)
            {
                citizenRoster=player.Components.Get<RosterSelection>();
                previousCitizen=citizenRoster.UseCitizenCharacters;
                citizenRoster.UseCitizenCharacters=true;
            }
            match.StartRound();match.PlayerState.HitLimit=10000;
            if(SixCharacters && Scene.GetAllComponents<ArenaOpponent>().Count() < 5)
            {
                for(int i=0;i<2;i++)
                {
                    var go=new GameObject(true,"Six-character performance opponent");
                    go.WorldPosition=new Vector3(i==0 ? -650 : 650,1250,1);
                    var bot=go.Components.Create<ArenaOpponent>();bot.Character=i==0 ? "roxie" : "mei";bot.CombatEnabled=true;
                    extraOpponents.Add(go);
                }
            }
            start=previous=now;started=true;return;
        }
        var elapsed=now-start;
        if(firstShotAt<0 && Scene.GetAllComponents<ArenaOpponent>().Any(x=>x.ShotsFired>0))firstShotAt=elapsed;
        if(ActiveFirefight)
        {
            player.WorldPosition=new Vector3(-950,-500+(MovingTarget ? MathF.Sin((float)elapsed*1.2f)*90 : 0),2);
            foreach(var bot in Scene.GetAllComponents<ArenaOpponent>())
            {
                if(MobileOpponents || !bot.Body.IsValid())continue;
                if(stagedBots.Add(bot))
                {
                    bot.WorldPosition=new Vector3(-700,-620+(stagedBots.Count-1)*60,2);
                    bot.WorldRotation=Rotation.FromYaw(180);
                }
                if(bot.Components.Get<NavMeshAgent>() is {} agent)
                {
                    agent.MaxSpeed=0;agent.UpdatePosition=false;
                    bot.WorldPosition=new Vector3(-700,-620+stagedBots.ToList().IndexOf(bot)*60,2);
                }
            }
        }
        if(CitizenCharacters)
        {
            citizenPlayer=player.Components.Get<CitizenPlayerPresentation>();
            foreach(var bot in Scene.GetAllComponents<ArenaOpponent>())
                if(bot.Components.Get<CitizenOpponentPresentation>() is {} presentation && !citizenBots.Contains(presentation))
                    citizenBots.Add(presentation);
        }
        var end=SampleSeconds > 0 ? Math.Clamp(SampleSeconds, 10, 160)+5 : MovingCamera ? 45 : 25;
        if(MovingCamera)
        {
            var angle=(float)(elapsed*Math.PI*2/40);
            WorldPosition=YardCamera ? new Vector3(MathF.Cos(angle)*850,1180+MathF.Sin(angle)*230,110+MathF.Sin(angle*2)*30)
                : new Vector3(MathF.Cos(angle)*850,MathF.Sin(angle)*620,110+MathF.Sin(angle*2)*30);
            WorldRotation=Rotation.LookAt(new Vector3(0,YardCamera ? 1180 : 0,40)-WorldPosition);
        }
        var impacts=PaintImpactSystem.Find(Scene);
        if(StressImpacts && elapsed>=5 && now>=nextImpact)
        {
            nextImpact=now+.2;
            // Keep annex stress effects in the camera's tested area, rather
            // than behind the divider at the stationary player's spawn.
            var point=YardCamera
                ? new Vector3(MathF.Cos((float)elapsed)*180,1180+MathF.Sin((float)elapsed)*140,1)
                : match.PlayerState.WorldPosition+Vector3.Forward*100;
            var hit=Scene.Trace.Ray(point+Vector3.Up*100,point-Vector3.Up*10).Run();
            if(hit.Hit && impacts.IsValid())
                for(int i=0;i<3;i++){impacts.Spawn(hit,i%2==0 ? Color.Cyan : new Color(1,.12f,.6f));injectedImpacts++;}
        }
        if(impacts.IsValid())peakDroplets=Math.Max(peakDroplets,impacts.ActiveDroplets);
        if(elapsed>=5 && elapsed<=end)
        {
            var milliseconds=(now-previous)*1000;
            frames.Add(milliseconds);
            // Buffer observations; logging in a long frame can itself disturb pacing.
            if(milliseconds>33.333 && hitches.Count<16)hitches.Add((elapsed,milliseconds));
        }
        previous=now;
        if(elapsed<end)return;
        if(frames.Count<2){Log.Info("PB_PERF insufficient samples");Destroy();return;}
        var sorted=frames.OrderBy(x=>x).ToArray();
        double Percentile(double p)=>sorted[Math.Min(sorted.Length-1,(int)Math.Ceiling(p*sorted.Length)-1)];
        var mean=frames.Average();
        Log.Info($"PB_PERF samples={frames.Count} mean_ms={mean:F3} mean_fps={1000/mean:F1} p50_ms={Percentile(.5):F3} p95_ms={Percentile(.95):F3} p99_ms={Percentile(.99):F3} max_ms={sorted[^1]:F3} over33ms={frames.Count(x=>x>33.333)} bots={Scene.GetAllComponents<ArenaOpponent>().Count()} shots={Scene.GetAllComponents<ArenaOpponent>().Sum(x=>x.ShotsFired)} decals={PaintImpactSystem.Find(Scene)?.ActiveCount}");
        Log.Info($"PB_PERF stress={StressImpacts} injected_impacts={injectedImpacts} peak_droplets={peakDroplets} yard_camera={YardCamera} six_characters={SixCharacters}");
        Log.Info($"PB_PERF CONTACT first_shot_seconds={firstShotAt:F3} player_hits={match.PlayerState.PaintHits} shooting_bots={Scene.GetAllComponents<ArenaOpponent>().Count(x=>x.ShotsFired>0)}");
        Log.Info($"PB_PERF SPACING moves={Scene.GetAllComponents<ArenaOpponent>().Sum(x=>x.SpacingMoves)}");
        if(ActiveFirefight)Log.Info($"PB_PERF FIREFIGHT shooting_bots={Scene.GetAllComponents<ArenaOpponent>().Count(x=>x.ShotsFired>0)} reloads={Scene.GetAllComponents<ArenaOpponent>().Sum(x=>x.Magazine.Reloads)} player_hits={match.PlayerState.PaintHits}");
        if(ActiveFirefight)foreach(var bot in Scene.GetAllComponents<ArenaOpponent>())Log.Info($"PB_PERF AI position={bot.WorldPosition} sees={bot.SeesPlayer} combat={bot.CombatEnabled} cover={bot.UsingCover} player={player.WorldPosition} round={match.InRound}");
        foreach(var bot in Scene.GetAllComponents<ArenaOpponent>())Log.Info($"PB_PERF NAV character={bot.Character} distance={bot.DistanceTravelled:F1} position={bot.WorldPosition} navigating={bot.Components.Get<NavMeshAgent>()?.IsNavigating} generating={Scene.NavMesh.IsGenerating}");
        if(CitizenCharacters && !ActiveFirefight)foreach(var bot in Scene.GetAllComponents<ArenaOpponent>())
        {
            var sight=Scene.Trace.Ray(bot.WorldPosition+Vector3.Up*60,player.EyePosition).IgnoreGameObjectHierarchy(bot.GameObject).Run();
            var anchor=bot.PresentationAnchor;
            var muzzle=anchor.WorldPosition+anchor.WorldRotation.Forward*17.1f+anchor.WorldRotation.Up*4;
            var obstruction=Scene.Trace.Sphere(.35f,bot.WorldPosition+Vector3.Up*60,muzzle).IgnoreGameObjectHierarchy(bot.GameObject).Run();
            Log.Info($"PB_PERF SIGHT character={bot.Character} sees={bot.SeesPlayer} sight_hit={sight.GameObject?.Name} eye={player.EyePosition} muzzle_hit={obstruction.GameObject?.Name} cover={bot.UsingCover} plan={bot.HasCoverPlan}");
        }
        if(CitizenCharacters)Log.Info($"PB_PERF CITIZEN player_ready={citizenPlayer.IsReady} bots_ready={citizenBots.Count(x=>x.IsValid() && x.IsReady)} suits={string.Join(",",citizenBots.Where(x=>x.IsValid()).Select(x=>x.EquippedSuit))}");
        if(CitizenCharacters)
        {
            var motions=Scene.GetAllComponents<CitizenSuitFollowThrough>().ToArray();
            Log.Info($"PB_PERF SUIT_MOTION ready={motions.Count(x=>x.MorphsReady)} total={motions.Length} peak={motions.Select(x=>x.PeakWeight).DefaultIfEmpty(0).Max():F4}");
        }
        foreach(var hitch in hitches)Log.Info($"PB_PERF HITCH elapsed_seconds={hitch.Elapsed:F3} frame_ms={hitch.Milliseconds:F3}");
        Log.Info($"PB_PERF COMPLETE editor match, moving_target={MovingTarget}, mobile_opponents={MobileOpponents}, moving_camera={MovingCamera}, five-second warmup, sample_seconds={end-5}");
        Destroy();
    }
    protected override void OnDestroy()
    {
        if(citizenRoster.IsValid())citizenRoster.UseCitizenCharacters=previousCitizen;
        foreach(var opponent in extraOpponents)if(opponent.IsValid())opponent.Destroy();
        if(MovingCamera && player.IsValid())
        {
            player.UseCameraControls=restoreCamera;
            WorldPosition=originalPosition;WorldRotation=originalRotation;
        }
    }
}
