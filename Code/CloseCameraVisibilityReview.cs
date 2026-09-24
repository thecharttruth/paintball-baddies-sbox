using Sandbox;
namespace PaintballBaddies;
/// <summary>Opt-in checks using the real wall-clipped native player camera.</summary>
public sealed class CloseCameraVisibilityReview : Component
{
    public bool Done {get;private set;}
    public int Stage {get;private set;}
    public List<string> Results {get;}=new();
    PlayerController player;PaintballMarker marker;CloseCameraVisibility visibility;
    Vector3 original;Angles angles;bool input,look;float elapsed;GameObject witness;
    void Check(bool ok,string text)=>Results.Add((ok?"PASS ":"FAIL ")+text);
    protected override void OnStart()
    {
        Scene.GetAllComponents<PaintballControls>().First().StartPractice();
        player=MultiplayerSession.LocalPlayer(Scene);marker=player.Components.Get<PaintballMarker>();visibility=player.Components.Get<CloseCameraVisibility>();
        original=player.WorldPosition;angles=player.EyeAngles;input=player.UseInputControls;look=player.UseLookControls;
        player.Components.Get<CoverController>()?.Leave();player.UseInputControls=player.UseLookControls=false;
        player.WorldPosition=new(-1000,1250,2);player.EyeAngles=Angles.Zero;player.Body.Velocity=Vector3.Zero;marker.ReviewAimOverride=false;
        witness=TrainingRange.Box(GameObject,"Camera visibility unrelated prop",new(-1050,1400,25),new(12,12,50),Color.Green,false);
    }
    protected override void OnUpdate()
    {
        if(Done)return;elapsed+=Time.Delta;player.WishVelocity=Vector3.Zero;
        if(elapsed<1.5f)return;elapsed=0;
        var camera=Scene.Camera;
        switch(Stage++)
        {
            case 0:
                Check(!visibility.Hidden&&visibility.EyeDistance>70,"open third-person view keeps own character visible");
                player.WorldPosition=new(-1155,1250,2);player.Body.Velocity=Vector3.Zero;break;
            case 1:
                Check(visibility.Hidden&&visibility.EyeDistance<44,"wall-compressed camera hides own obstructing geometry");
                var missing=player.Components.GetAll<ModelRenderer>(FindMode.EverythingInSelfAndDescendants).Where(x=>x.Enabled&&!visibility.Filters(camera,x.GameObject)).Select(x=>x.GameObject.Name).ToArray();
                Check(missing.Length==0,"separate body, helmet, suit and marker all use camera filter; missing: "+string.Join(", ",missing));
                Check(!visibility.Filters(camera,witness),"unrelated scenery is not hidden");
                Check(camera.WorldPosition.x>-1174,"native camera stays inside arena wall");
                Check(marker.FireAt(player.EyePosition+Vector3.Forward*400),"close camera still permits shooting");
                marker.ReviewAimOverride=true;break;
            case 2:
                Check(marker.Aiming&&visibility.Hidden,"focused wall camera remains unobstructed");
                player.UpdateDucking(true);break;
            case 3:
                Check(visibility.Hidden,"close crouched camera filters obstructing outfit");
                player.UpdateDucking(false);player.WorldPosition=new(-1000,1250,2);player.Body.Velocity=Vector3.Zero;break;
            case 4:
                Check(!visibility.Hidden&&visibility.HiddenPieces==0,"moving away restores all owned geometry");
                Check(marker.Aiming&&visibility.EyeDistance>52,"normal focused shoulder view remains visible");
                // Test only the filter boundaries; camera transform stays native.
                visibility.Apply(camera,player.EyePosition+Vector3.Backward*40,player.EyePosition);
                visibility.Apply(camera,player.EyePosition+Vector3.Backward*48,player.EyePosition);
                Check(visibility.Hidden,"hysteresis keeps visibility stable near close threshold");
                visibility.Apply(camera,player.EyePosition+Vector3.Backward*56,player.EyePosition);
                Check(!visibility.Hidden,"visibility returns outside hysteresis range");
                Done=true;break;
        }
    }
    protected override void OnDestroy()
    {
        visibility?.Restore();if(witness.IsValid())witness.Destroy();
        if(marker.IsValid())marker.ReviewAimOverride=null;
        if(player.IsValid()){player.WorldPosition=original;player.EyeAngles=angles;player.UseInputControls=input;player.UseLookControls=look;player.UpdateDucking(false);player.Body.Velocity=Vector3.Zero;}
    }
}
