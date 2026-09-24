using Sandbox;
namespace PaintballBaddies;
public sealed class SlideCoverReview : Component
{
    private PlayerController player;
    private CoverController cover;
    private GameObject obstacle,blocker;
    private float elapsed;
    private int stage;
    private float maxRise;
    private int standingFrames;
    private void Check(string name,bool pass)=>Log.Info($"SLIDE_COVER {(pass ? "PASS" : "FAIL")} {name}");
    protected override void OnStart()
    {
        player=Scene.GetAllComponents<PlayerController>().First();cover=player.Components.Get<CoverController>();
        player.UseLookControls=false;player.EyeAngles=Angles.Zero;player.WorldPosition=new Vector3(-710,70,2);
        player.WishVelocity=Vector3.Zero;player.Body.Velocity=Vector3.Zero;cover.TestInput=true;
        obstacle=TrainingRange.Box(null,"Slide low cover",new Vector3(-550,70,24),new Vector3(20,180,48),Color.Gray,true);
        obstacle.Components.Create<CoverSurface>();
    }
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;
        if((stage==0 && elapsed>.3f) || stage==3){player.WorldPosition=new Vector3(-710,70,2);player.Body.Velocity=Vector3.Forward*177;}
        if(stage==0 && elapsed>.7f)
        {
            player.Body.Velocity=Vector3.Forward*177;
            Check("running forward action starts slide",cover.Activate(true) && cover.Sliding && !cover.Attached);
            Check("firing blocked during slide",!player.Components.Get<PaintballMarker>().FireAt(new Vector3(-550,70,24)));
            stage=1;
        }
        if(stage==1 || stage==2)
        {
            maxRise=System.MathF.Max(maxRise,player.WorldPosition.z);
            if(cover.Attached && !player.IsDucking)standingFrames++;
        }
        if(stage==1 && elapsed>1.1f)
        {
            Check("native movement advances",player.WorldPosition.x>-700 && player.WorldPosition.x<-575);
            Check("slide remains active",cover.Sliding);stage=2;
        }
        if(stage==2 && elapsed>2.5f)
        {
            Check("arrives attached without vault",cover.Attached && !cover.Sliding && !player.Components.Get<VaultController>().IsVaulting);
            Check("stops before wall",player.WorldPosition.x<-575);
            Check("no physical hop",maxRise<3);
            Check("no standing frame at cover handoff",standingFrames==0);
            cover.Leave();Check("input restored",player.UseInputControls);
            player.WorldPosition=new Vector3(-710,70,2);stage=3;
        }
        if(stage==3 && elapsed>3)
        {
            player.Body.Velocity=Vector3.Forward*177;
            Check("second slide starts",cover.TrySlide());cover.Enabled=false;
            Check("disable interrupts and restores",!cover.Sliding && player.UseInputControls);
            cover.Enabled=true;
            blocker=TrainingRange.Box(null,"Slide obstruction",new Vector3(-640,70,35),new Vector3(20,80,70),Color.Gray,true);stage=4;
        }
        if(stage==4 && elapsed>3.5f)
        {
            player.Body.Velocity=Vector3.Forward*177;
            Check("blocked path rejects slide",!cover.TrySlide());stage=5;
        }
    }
    protected override void OnDestroy()
    {
        if(cover.IsValid()){cover.Leave();cover.TestInput=false;cover.Enabled=true;}
        if(player.IsValid())player.UseLookControls=true;
        if(obstacle.IsValid())obstacle.Destroy();if(blocker.IsValid())blocker.Destroy();
    }
}
