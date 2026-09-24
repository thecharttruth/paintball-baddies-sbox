using Sandbox;

namespace PaintballBaddies;

/// <summary>Opt-in native lifecycle checks; only install after the user's playtest.</summary>
public sealed class VaultInterruptionReview : Component
{
    [Property] public string CaptureId { get; set; } = "";
    private PlayerController player;
    private CoverController cover;
    private VaultController vault;
    private GameObject obstacle;
    private bool input, look, motion;
    private Vector3 position;
    private Angles angles;
    private float elapsed;
    private int stage;

    protected override void OnStart()
    {
        player=Scene.GetAllComponents<PlayerController>().First();
        cover=player.Components.Get<CoverController>();
        vault=player.Components.Get<VaultController>();
        input=player.UseInputControls;look=player.UseLookControls;motion=player.Body.MotionEnabled;
        position=player.WorldPosition;angles=player.EyeAngles;
        player.UseLookControls=false;
        cover.TestInput=true;cover.TestAim=false;cover.TestMovement=Vector3.Zero;
        obstacle=TrainingRange.Box(null,"Vault interruption review",new Vector3(-550,70,24),new Vector3(20,180,48),Color.Gray,true);
        obstacle.Components.Create<CoverSurface>();
        Place(true);
    }

    private void Place(bool acceptsInput)
    {
        if(vault.IsValid())vault.Cancel();
        cover.Leave();
        player.WorldPosition=new Vector3(-595,70,2);
        player.EyeAngles=Angles.Zero;
        player.Body.MotionEnabled=true;player.Body.Velocity=Vector3.Zero;
        player.WishVelocity=Vector3.Zero;player.UseInputControls=acceptsInput;
    }

    private void Check(string name,bool passed) => Log.Info("VAULT_INTERRUPTION "+Json.Serialize(new {capture_id=CaptureId,name,passed}));

    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;
        // Allow the teleported body to settle, but interrupt well inside the .8s vault.
        var delay=stage is 0 or 3 or 6 ? .6f : .15f;
        if(elapsed<delay)return;
        elapsed=0;
        switch(stage++)
        {
            case 0:
                Check("explicit vault action starts traversal",!cover.Attached && vault.TryBegin() && vault.IsVaulting);
                Check("traversal owns physics and input",vault.IsVaulting && !player.UseInputControls && !player.Body.MotionEnabled);
                break;
            case 1:
                Check("disable happens during traversal",vault.IsVaulting && vault.Progress>0 && vault.Progress<1);
                vault.Enabled=false;
                break;
            case 2:
                Check("disable restores physics and input",!vault.IsVaulting && player.UseInputControls && player.Body.MotionEnabled);
                Check("disable clears stale movement",player.WishVelocity.Length<.01f);
                Check("disabled vault rejects activation",!vault.Available && !vault.TryBegin());
                vault.Enabled=true;Place(false);
                break;
            case 3:
                Check("starts with external input owner",cover.TryEnter() && vault.TryBegin());
                break;
            case 4:
                vault.Enabled=false;
                break;
            case 5:
                Check("preserves external input owner",!vault.IsVaulting && !player.UseInputControls && player.Body.MotionEnabled);
                vault.Enabled=true;Place(true);
                break;
            case 6:
                Check("starts before removal",cover.TryEnter() && vault.TryBegin());
                break;
            case 7:
                vault.Destroy();
                break;
            case 8:
                Check("removal restores physics and input",player.UseInputControls && player.Body.MotionEnabled && player.WishVelocity.Length<.01f);
                break;
        }
    }

    protected override void OnDestroy()
    {
        if(vault.IsValid()){vault.Cancel();vault.Enabled=true;}
        if(cover.IsValid()){cover.Leave();cover.TestInput=false;cover.TestMovement=Vector3.Zero;cover.TestAim=false;}
        if(obstacle.IsValid())obstacle.Destroy();
        if(!player.IsValid())return;
        player.UseInputControls=input;player.UseLookControls=look;
        player.WorldPosition=position;player.EyeAngles=angles;player.WishVelocity=Vector3.Zero;
        if(player.Body.IsValid()){player.Body.MotionEnabled=motion;player.Body.Velocity=Vector3.Zero;}
    }
}
