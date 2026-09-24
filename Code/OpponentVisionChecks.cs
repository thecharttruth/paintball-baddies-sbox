using Sandbox;

namespace PaintballBaddies;

/// <summary>Opt-in native perception check in the annex's clear west lane.</summary>
public sealed class OpponentVisionChecks : Component
{
    private PlayerController player;
    private GameObject opponent, blocker;
    private ArenaOpponent bot;
    private float elapsed;
    private int stage;
    private Vector3 original;
    private bool input;
    private bool impactSent;
    protected override void OnStart()
    {
        player = Scene.GetAllComponents<PlayerController>().First();
        original = player.WorldPosition; input = player.UseInputControls;
        player.UseInputControls = false;
        opponent = new GameObject { Name = "Vision check opponent" };
        opponent.WorldPosition = new Vector3(-900,1250,1);
        bot = opponent.Components.Create<ArenaOpponent>();
        bot.Character = "imani";
        bot.CombatEnabled = true;
    }
    private void Check(string name,bool pass) => Log.Info($"VISION_CHECK {(pass ? "PASS" : "FAIL")} {name}");
    protected override void OnUpdate()
    {
        elapsed += Time.Delta;
        opponent.WorldPosition = new Vector3(-900,1250,1);
        if(stage<4)opponent.WorldRotation = Rotation.Identity;
        var nav = opponent.Components.Get<NavMeshAgent>();
        if(nav.IsValid()) { nav.MaxSpeed=0; nav.Stop(); }
        player.WorldPosition = stage == 0 || stage == 4 ? new Vector3(-1050,1250,4) : stage == 2 ? new Vector3(-650,1250,4) : new Vector3(-700,1250,4);
        player.Body.Velocity=Vector3.Zero;player.WishVelocity=Vector3.Zero;
        if(stage==0 && elapsed>1.5f)
        {
            Check("rear target is not acquired",!bot.SeesPlayer);
            stage=1;elapsed=0;
        }
        else if(stage==1 && elapsed>1.5f)
        {
            var probe=Scene.Trace.Ray(opponent.WorldPosition+Vector3.Up*60,player.EyePosition).IgnoreGameObjectHierarchy(opponent).WithoutTags("paintball_debris").Run();
            Log.Info($"VISION_TRACE hit={probe.Hit} object={probe.GameObject?.Name} actor={PaintballCombatant.Find(probe.GameObject)?.GameObject.Name} eye={player.EyePosition} end={probe.EndPosition}");
            Check("front target is acquired",bot.SeesPlayer);
            blocker=new GameObject { Name="Vision check occluder" };
            // Within the old 24-inch endpoint tolerance: a wall here must still occlude.
            blocker.WorldPosition=new Vector3(-665,1250,50);
            var shape=blocker.Components.Create<BoxCollider>();shape.Scale=new Vector3(10,150,100);shape.Static=true;
            stage=2;elapsed=0;
        }
        else if(stage==2 && elapsed>1.5f)
        {
            Check("solid cover close to target interrupts sight",!bot.SeesPlayer);
            Check("unseen movement does not update memory",(bot.LastKnownPlayerPosition-new Vector3(-700,1250,4)).Length<10);
            blocker.Destroy();blocker=null;stage=3;elapsed=0;
        }
        else if(stage==3 && elapsed>1.5f)
        {
            Check("removed obstruction allows reacquisition",bot.SeesPlayer);
            stage=4;elapsed=0;
        }
        else if(stage==4 && elapsed>.5f && !impactSent)
        {
            impactSent=true;
            var state=opponent.Components.Get<PaintballCombatant>();
            if(state.PaintHits==0)state.RegisterHit(0,new Vector3(-1050,1250,4));
        }
        else if(stage==4 && elapsed>2f)
        {
            Check("rear impact turns opponent to reacquire visible attacker",bot.SeesPlayer && Vector3.Dot(opponent.WorldRotation.Forward,Vector3.Backward)>.8f);
            Log.Info("VISION_CHECK COMPLETE");Destroy();
        }
    }
    protected override void OnDestroy()
    {
        opponent?.Destroy();blocker?.Destroy();
        if(player.IsValid()) { player.WorldPosition=original;player.Body.Velocity=Vector3.Zero;player.UseInputControls=input; }
    }
}
