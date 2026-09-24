using Sandbox;
using System;
namespace PaintballBaddies;

/// <summary>Local view of the single host-owned match; all competitors use MultiplayerSession.</summary>
public sealed class ArenaMatch : Component
{
    internal static float? RoundAfterReload;
    private float startupTime;
    // Retained for marker animation review scenes; playable matches always use unlimited paint.
    [Property] public bool TimedContest {get;set;}=true;
    [Property] public float RoundDuration {get;set;}=180;
    internal static readonly int[] RoundMinuteOptions={1,2,3,5};
    private MultiplayerSession OnlineSession=>MultiplayerSession.Online ? MultiplayerSession.Find(Scene) : null;
    private PlayerController player;
    public PaintballCombatant PlayerState {get;private set;}
    public int PlayedMinutes=>OnlineSession?.ActiveMinutes ?? (int)(RoundDuration/60);
    public int RoundSerial=>OnlineSession?.RoundId ?? 0;
    public bool InRound=>OnlineSession?.InRound ?? false;
    public string Status=>OnlineSession?.Status ?? "TRAINING";
    public float Remaining=>OnlineSession?.Remaining ?? 0;
    public int MarkerSoundAssignments {get;private set;}=-1;
    public int OpponentCount=>Math.Max(0,Standings.Length-1);
    public int OpponentsRemaining=>OpponentCount;
    public int OpponentsEliminated=>0;
    public string EliminationNotice=>"";
    public sealed record Standing(string Name,int Landed,int Received,bool IsPlayer,int Knockdowns=0,int KnockedDown=0,bool IsAi=false,int Carried=0,int Banked=0,bool Heist=false)
    {
        public int Points=>Heist ? Banked : Landed-Received+PaintballCombatant.KnockdownPoints*(Knockdowns-KnockedDown);
    }
    public Standing[] Standings=>OnlineSession?.Standings ?? Array.Empty<Standing>();
    public int RankOf(Standing row)=>1+Standings.Count(x=>x.Points>row.Points || (!row.Heist && x.Points==row.Points && x.Received<row.Received));
    public int StandingsHash=>string.Join("|",Standings.Select(x=>$"{x.Name}:{x.Landed}:{x.Received}:{x.Knockdowns}:{x.KnockedDown}:{x.IsAi}:{x.IsPlayer}:{x.Carried}:{x.Banked}:{x.Heist}")).GetHashCode();
    public static bool SupportsRoundMinutes(int minutes)=>RoundMinuteOptions.Contains(minutes);
    public bool CycleRoundMinutes()=>SetRoundMinutes(RoundMinuteOptions[(Array.IndexOf(RoundMinuteOptions,(int)(RoundDuration/60))+1)%RoundMinuteOptions.Length]);
    public bool SetRoundMinutes(int minutes)
    {
        if(!SupportsRoundMinutes(minutes))return false;
        if(OnlineSession.IsValid())
        {
            if(!Networking.IsHost)return false;
            OnlineSession.SetMinutes(minutes);return true;
        }
        RoundDuration=minutes*60;return true;
    }
    protected override void OnStart()
    {
        player=Components.Get<PlayerController>();
        PlayerState=Components.Get<PaintballCombatant>() ?? Components.Create<PaintballCombatant>();
        if(OnlineSession.IsValid())return;
        MarkerSoundAssignments=CharacterMarkerSound.Roll();
        PlayerState.Team=0;PlayerState.HitLimit=5;PlayerState.StayInMatch=true;
    }
    protected override void OnUpdate()
    {
        if(IsProxy)return;
        if(OnlineSession.IsValid())RoundDuration=OnlineSession.Minutes*60;
        startupTime+=Time.Delta;
        if(RoundAfterReload is float duration && startupTime>.5f)
        {
            RoundAfterReload=null;RoundDuration=duration;StartRound();
        }
        if(Input.Pressed("flashlight"))StartRound();
        if(!InRound && Input.Pressed("roundlength"))CycleRoundMinutes();
    }
    public void StartRound()
    {
        if(OnlineSession.IsValid()){OnlineSession.StartRound();return;}
        MultiplayerSession.Find(Scene)?.QuickPlay();
    }
    // One cue per crossed boundary; a long frame must never queue a burst of ticks.
    internal static string CountdownCue(float previous, float current)
    {
        if (current <= 0 || current >= previous) return null;
        if (current <= 5 && MathF.Ceiling(current) < MathF.Ceiling(previous)) return "countdown_tick";
        return previous > 10 && current <= 10 ? "time_warning" : null;
    }
    public bool ReturnToTraining(bool endCurrentRound = false)
    {
        if(OnlineSession.IsValid())return false;
        if (player is null) return false;
        ArenaShield.ResetArena(Scene);
        Components.Get<VaultController>()?.Cancel();
        Components.Get<CoverController>()?.Leave();
        foreach(var pod in Scene.GetAllComponents<DroppedReloadPod>().ToArray())pod.GameObject.Destroy();
        foreach(var ball in Scene.GetAllComponents<OpponentPaintball>().ToArray()) ball.GameObject.Destroy();
        PlayerState.ResetPaintHits();PlayerState.HitLimit=5;
        PlayerState.StayInMatch=TimedContest;
        player.WorldPosition=ArenaMap.PlayerSpawn(Scene,0,new Vector3(-630,0,4));player.Body.Velocity=Vector3.Zero;
        player.WishVelocity=Vector3.Zero;
        player.UseInputControls=true;
        var weapon=Components.Get<PaintballMarker>();weapon.ResetTraining();weapon.AcceptInput=true;
        Scene.GetAllComponents<TrainingRange>().FirstOrDefault()?.SetTargetsVisible(true);
        return true;
    }
}
