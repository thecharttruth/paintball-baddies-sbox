using Sandbox;
namespace PaintballBaddies;
public sealed class CameraWallChecks : Component
{
    [Property] public int CharacterIndex { get; set; }
    [Property] public bool AnnexEast { get; set; }
    private float Side => AnnexEast ? -1 : 1;
    private Vector3 Position(float x) => new(x*Side,AnnexEast ? 1250 : -500,2);
    private PlayerController player;private PaintballMarker marker;
    private bool input,look,accept;private float elapsed;private int stage;
    protected override void OnStart()
    {
        player=Scene.GetAllComponents<PlayerController>().First();marker=player.Components.Get<PaintballMarker>();
        player.Components.Get<RosterSelection>().Select(CharacterIndex);
        input=player.UseInputControls;look=player.UseLookControls;accept=marker.AcceptInput;
        player.UseInputControls=player.UseLookControls=marker.AcceptInput=false;
        player.WorldPosition=Position(-900);player.EyeAngles=new Angles(0,AnnexEast ? 180 : 0,0);marker.ReviewAimOverride=false;
    }
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;player.WishVelocity=Vector3.Zero;
        if(elapsed<.8f)return;elapsed=0;
        var cam=Scene.Camera;var delta=cam.WorldPosition-player.EyePosition;
        if(stage==0){Check("open follow distance",delta.Length>90&&delta.Length<115,$"delta={delta}");player.WorldPosition=Position(-1140);}
        if(stage==1){Check("wall keeps camera inside",cam.WorldPosition.x*Side>-1172&&cam.WorldPosition.x*Side<player.WorldPosition.x*Side,$"camera={cam.WorldPosition} player={player.WorldPosition}");marker.ReviewAimOverride=true;}
        if(stage==2){Check("aim keeps camera inside",marker.Aiming&&cam.WorldPosition.x*Side>-1172&&cam.WorldPosition.x*Side<player.WorldPosition.x*Side,$"camera={cam.WorldPosition}");player.WorldPosition=Position(-900);}
        if(stage==3){Check("open aim distance",marker.Aiming&&delta.Length>35&&delta.Length<70,$"delta={delta}");Log.Info("CAMERA_WALL COMPLETE");Destroy();}
        stage++;
    }
    private void Check(string name,bool pass,string detail)=>Log.Info($"CAMERA_WALL {(pass?"PASS":"FAIL")} {name}: {detail}");
    protected override void OnDestroy(){if(player.IsValid()){player.UseInputControls=input;player.UseLookControls=look;}if(marker.IsValid()){marker.AcceptInput=accept;marker.ReviewAimOverride=null;}}
}
