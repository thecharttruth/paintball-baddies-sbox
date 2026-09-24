using Sandbox;
namespace PaintballBaddies;
public sealed class OpponentIdleReloadChecks : Component
{
    [Property] public bool ReviewReach {get;set;}
    [Property] public string Character {get;set;}="imani";
    private ArenaOpponent bot;
    private PlayerController player;
    private bool camera;
    private float elapsed;
    private bool started,accepted;
    private int active,idle,gear;
    protected override void OnStart()
    {
        if(ReviewReach)ReloadUpperBodyData.ImaniReviewTrack="animations/imani_reload_reach.json";
        player=Scene.GetAllComponents<PlayerController>().First();camera=player.UseCameraControls;player.UseCameraControls=false;
        var go=new GameObject(true,"Idle reload review opponent");go.WorldPosition=new Vector3(-950,-500,2);
        bot=go.Components.Create<ArenaOpponent>();bot.Character=Character;bot.CombatEnabled=true;
    }
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;
        if(!bot.Body.IsValid())return;
        bot.Components.Get<NavMeshAgent>().MaxSpeed=0;
        bot.CombatEnabled=false; // Keep initialized equipment, suppress unrelated shots.
        if(!started && elapsed>.3f)
        {
            accepted=bot.Magazine.TryFire()&&bot.Magazine.BeginReload();started=true;
        }
        if(started && bot.Magazine.ReloadRemaining>0)
        {
            active++;if(bot.Body.Sequence.Name=="idle")idle++;
            if(bot.Components.Get<ReloadPresentationTrack>() is { } track && !track.Docked)gear++;
        }
        WorldPosition=bot.WorldPosition+new Vector3(110,-95,80);
        WorldRotation=Rotation.LookAt(bot.WorldPosition+Vector3.Up*45-WorldPosition);
        if(elapsed<3.4f)return;
        Check("reload starts",accepted,$"accepted={accepted}");
        Check("reload sampled",active>90,$"frames={active}");
        Check("idle retained",idle==active,$"idle={idle} active={active}");
        Check("gear active",gear>90,$"frames={gear}");
        Check("ammo conserved",bot.Magazine.Ammo==30 && bot.Magazine.Reserve==119,$"ammo={bot.Magazine.Ammo} reserve={bot.Magazine.Reserve}");
        Check("gear docked",bot.Components.Get<ReloadPresentationTrack>()?.Docked==true,"dock state");
        Log.Info("AI_IDLE_RELOAD COMPLETE");Destroy();
    }
    private void Check(string name,bool pass,string detail)=>Log.Info($"AI_IDLE_RELOAD {(pass?"PASS":"FAIL")} {name}: {detail}");
    protected override void OnDestroy(){if(ReviewReach)ReloadUpperBodyData.ImaniReviewTrack=null;if(bot.IsValid())bot.GameObject.Destroy();if(player.IsValid())player.UseCameraControls=camera;}
}
