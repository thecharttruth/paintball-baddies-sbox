using Sandbox;
namespace PaintballBaddies;
public sealed class ContextCoverReview : Component
{
    private PlayerController player;
    private CoverController cover;
    private VaultController vault;
    private GameObject obstacle,blocker;
    private float elapsed;
    private int stage;
    protected override void OnStart()
    {
        player=Scene.GetAllComponents<PlayerController>().First();cover=player.Components.Get<CoverController>();vault=player.Components.Get<VaultController>();
        player.UseLookControls=false;player.EyeAngles=Angles.Zero;player.WorldPosition=new Vector3(-595,70,2);
        player.WishVelocity=Vector3.Zero;player.Body.Velocity=Vector3.Zero;cover.TestInput=true;
        obstacle=TrainingRange.Box(null,"Context low cover",new Vector3(-550,70,24),new Vector3(20,180,48),Color.Gray,true);
        obstacle.Components.Create<CoverSurface>();
        blocker=TrainingRange.Box(null,"Blocked vault landing",new Vector3(-510,70,35),new Vector3(30,80,70),Color.Gray,true);
    }
    private void Check(string name,bool pass)=>Log.Info($"CONTEXT_COVER {(pass ? "PASS" : "FAIL")} {name}");
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;
        if(stage==0 && elapsed>.7f)
        {
            Check("space alone attaches",cover.Activate(false) && cover.Attached && !vault.IsVaulting);
            Check("space alone releases",cover.Activate(false) && !cover.Attached);
            Check("blocked approach keeps protective cover",cover.Activate(true) && cover.Attached && !vault.IsVaulting);
            Check("space releases even with forward held",cover.Activate(true) && !cover.Attached && !vault.IsVaulting);
            blocker.Destroy();stage=1;
        }
        if(stage==1 && elapsed>1.1f)
        {
            Check("explicit vault from cover",cover.Activate(false) && vault.TryBegin() && vault.IsVaulting && !cover.Attached);stage=2;
        }
        if(stage==2 && elapsed>2.2f)
        {
            Check("vault lands and restores movement",!vault.IsVaulting && vault.Outcome=="Landed" && player.UseInputControls && player.Body.MotionEnabled);
            Check("forward space in open ground does not jump",!cover.Activate(true) && !vault.IsVaulting);
            stage=3;
        }
    }
    protected override void OnDestroy()
    {
        if(vault.IsValid())vault.Cancel();
        if(cover.IsValid()){cover.Leave();cover.TestInput=false;}
        if(player.IsValid())player.UseLookControls=true;
        if(obstacle.IsValid())obstacle.Destroy();if(blocker.IsValid())blocker.Destroy();
    }
}
