using Sandbox;
using System.Linq;
namespace PaintballBaddies;
/// <summary>Opt-in staging-area collision and camera review.</summary>
public sealed class StagingAreaChecks : Component
{
    private bool ran;
    protected override void OnUpdate()
    {
        var player=Scene.GetAllComponents<PlayerController>().First();
        player.UseCameraControls=player.UseInputControls=player.UseLookControls=false;
        var camera=Components.Get<CameraComponent>();
        camera.WorldPosition=new Vector3(-880,-290,115);
        camera.WorldRotation=Rotation.LookAt(new Vector3(-1146,-410,40)-camera.WorldPosition);
        camera.FieldOfView=55;
        if(ran)return;ran=true;
        foreach(float y in new float[]{-280,-325,-370})
        {
            var hit=Scene.Trace.Sphere(12,new Vector3(-1070,y,35),new Vector3(-1160,y,35)).Run();
            Log.Info($"STAGING {(hit.Hit && hit.GameObject.Name.StartsWith("Staging locker") ? "PASS" : "FAIL")} solid locker at {y}");
        }
        var station=Scene.Trace.Sphere(12,new Vector3(-1070,-425,25),new Vector3(-1160,-425,25)).Run();
        Log.Info($"STAGING {(station.Hit && station.GameObject.Name == "Staging air fill station" ? "PASS" : "FAIL")} solid refill cabinet");
        var bench=Scene.Trace.Sphere(12,new Vector3(-1070,-510,20),new Vector3(-1160,-510,20)).Run();
        Log.Info($"STAGING {(bench.Hit && bench.GameObject.Name == "Staging team bench" ? "PASS" : "FAIL")} solid team bench");
        var lane=Scene.Trace.Sphere(17,new Vector3(-1080,-230,35),new Vector3(-1080,-560,35)).Run();
        Log.Info($"STAGING {(!lane.Hit ? "PASS" : "FAIL")} clear approach lane");
        Log.Info("STAGING COMPLETE");
    }
}
