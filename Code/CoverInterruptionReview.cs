using Sandbox;
namespace PaintballBaddies;

/// <summary>Opt-in checks of actual cover interruption and input ownership.</summary>
public sealed class CoverInterruptionReview : Component
{
    [Property] public string CaptureId { get; set; } = "";
    private PlayerController player;
    private CoverController cover;
    private GameObject obstacle;
    private bool input,look;
    private Vector3 position;
    private Angles angles;
    private float elapsed;
    private int stage;
    protected override void OnStart()
    {
        player=Scene.GetAllComponents<PlayerController>().First();
        cover=player.Components.Get<CoverController>();
        input=player.UseInputControls;look=player.UseLookControls;
        position=player.WorldPosition;angles=player.EyeAngles;
        player.UseLookControls=false;cover.TestInput=true;
        Place();
    }
    private void Place()
    {
        player.WorldPosition=new Vector3(-595,70,2);
        player.EyeAngles=Angles.Zero;player.Body.Velocity=Vector3.Zero;
        obstacle=TrainingRange.Box(null,"Cover interruption review",new Vector3(-550,70,24),new Vector3(20,180,48),Color.Gray,true);
        obstacle.Components.Create<CoverSurface>();
    }
    private void Check(string name,bool passed) => Log.Info("COVER_INTERRUPTION "+Json.Serialize(new {capture_id=CaptureId,name,passed}));
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;
        if(elapsed<.6f)return;
        elapsed=0;
        switch(stage++)
        {
            case 0:
                Check("entry before disable",cover.TryEnter() && !player.UseInputControls);
                cover.Enabled=false;break;
            case 1:
                Check("disable releases controls",!cover.Attached && player.UseInputControls==input && !cover.LowCover);
                Check("disabled controller rejects entry",!cover.TryEnter() && !cover.CanEnter);
                cover.Enabled=true;break;
            case 2:
                Check("entry before surface removal",cover.TryEnter());
                obstacle.Destroy();break;
            case 3:
                Check("removed surface releases controls",!cover.Attached && player.UseInputControls==input);
                Place();player.UseInputControls=false;break;
            case 4:
                var surface=obstacle.Components.Get<CoverSurface>();
                surface.Enabled=false;
                Check("disabled surface rejects entry",!cover.TryEnter());
                surface.Enabled=true;
                Check("entry with external input owner",cover.TryEnter());
                cover.Enabled=false;break;
            case 5:
                Check("preserves external input owner",!cover.Attached && !player.UseInputControls);
                cover.Enabled=true;player.UseInputControls=input;break;
            case 6:
                Check("entry before component removal",cover.TryEnter());
                cover.Destroy();break;
            case 7:
                Check("component removal restores controls",player.UseInputControls==input);
                break;
        }
    }
    protected override void OnDestroy()
    {
        if(cover.IsValid()){cover.Leave();cover.TestInput=false;cover.Enabled=true;}
        if(obstacle.IsValid())obstacle.Destroy();
        if(player.IsValid())
        {
            player.UseInputControls=input;player.UseLookControls=look;
            player.WorldPosition=position;player.EyeAngles=angles;
        }
    }
}
