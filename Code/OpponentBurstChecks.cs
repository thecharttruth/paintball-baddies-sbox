using Sandbox;
using System.Collections.Generic;
namespace PaintballBaddies;
public sealed class OpponentBurstChecks : Component
{
    [Property] public bool UseCitizenPresentation { get; set; }
    [Property] public string CaptureId { get; set; } = "";
    [Property] public float Duration { get; set; } = 6;
    [Property] public bool EliminateDuringBurst { get; set; }
    [Property] public bool CloseStart { get; set; }
    private int shotsAtElimination;
    private bool eliminated,signalObserved;
    private float eliminatedAt;
    private CitizenOpponentPresentation presentation;
    private ArenaOpponent bot;
    private PlayerController player;
    private float elapsed;
    private readonly List<float> times=new();
    private int previous,hitLimit;
    private bool input,look,durability;
    protected override void OnStart()
    {
        player=Scene.GetAllComponents<PlayerController>().First();input=player.UseInputControls;look=player.UseLookControls;
        player.UseInputControls=player.UseLookControls=false;player.WorldPosition=new Vector3(-950,-500,2);

        var go=new GameObject(true,"Burst cadence check");go.WorldPosition=new Vector3(-750,-500,2);go.WorldRotation=Rotation.FromYaw(180);
        bot=go.Components.Create<ArenaOpponent>();bot.CombatEnabled=true;
        if(CloseStart)
        {
            go.WorldPosition=new Vector3(-920,-500,2);
            Scene.NavMesh.IsEnabled=true;Scene.NavMesh.IncludeStaticBodies=true;
            Scene.NavMesh.IncludeKeyframedBodies=false;Scene.NavMesh.AgentHeight=67;Scene.NavMesh.AgentRadius=17;
            Scene.NavMesh.SetDirty();
        }
    }
    protected override void OnUpdate()
    {
        var state=player.Components.Get<PaintballCombatant>();
        if(state is null)return;
        if(UseCitizenPresentation && !presentation.IsValid() && bot.Body.IsValid())
            presentation=bot.Components.Create<CitizenOpponentPresentation>();
        if(!durability){hitLimit=state.HitLimit;state.HitLimit=1000;durability=true;}
        elapsed+=Time.Delta;
        if(bot.Components.Get<NavMeshAgent>() is {} agent)agent.MaxSpeed=CloseStart ? 140 : 0;
        if(bot.ShotsFired>previous){times.Add(elapsed);previous=bot.ShotsFired;}
        if(EliminateDuringBurst && !eliminated && bot.ShotsFired>=7)
        {
            shotsAtElimination=bot.ShotsFired;eliminatedAt=elapsed;
            var combatant=bot.Components.Get<PaintballCombatant>();
            for(var hit=0;hit<combatant.HitLimit;hit++)combatant.RegisterHit(0);
            eliminated=true;
        }
        if(eliminated && presentation.IsValid() && presentation.ShowingOutSignal)signalObserved=true;
        if(elapsed<Duration)return;
        bool pass=times.Count>=6;
        if(pass)for(int i=1;i<6;i++){float gap=times[i]-times[i-1];pass &= i%3==0 ? gap>=.8f && gap<1.1f : gap>=.17f && gap<.26f;}
        Log.Info($"AI_BURST {(pass?"PASS":"FAIL")} native six-shot cadence: times={string.Join(",",times)}");
        if(UseCitizenPresentation)
            Log.Info("CITIZEN_AI_FIRE "+Json.Serialize(new {capture_id=CaptureId,pass,
                ready=presentation.IsReady,anchor=bot.PresentationAnchor.IsValid(),shots=bot.ShotsFired,
                hits=state.PaintHits,alignment=bot.MinimumShotAlignment,reloads=bot.Magazine.Reloads,
                ammo=bot.Magazine.Ammo,reserve=bot.Magazine.Reserve,pod_held=presentation.PodHeld,
                eliminated,shots_at_elimination=shotsAtElimination,signal_observed=signalObserved,
                signal_finished=eliminated && !presentation.ShowingOutSignal,
                seconds_after_elimination=eliminated ? elapsed-eliminatedAt : 0,times}));
        if(CloseStart)Log.Info("CITIZEN_CLOSE_RANGE "+Json.Serialize(new {capture_id=CaptureId,
            shots=bot.ShotsFired,hits=state.PaintHits,distance=(bot.WorldPosition-player.WorldPosition).WithZ(0).Length,
            travelled=bot.DistanceTravelled}));
        Destroy();
    }
    protected override void OnDestroy()
    {
        if(bot.IsValid())bot.GameObject.Destroy();
        if(player.IsValid()){player.UseInputControls=input;player.UseLookControls=look;if(durability)player.Components.Get<PaintballCombatant>().HitLimit=hitLimit;}
    }
}
