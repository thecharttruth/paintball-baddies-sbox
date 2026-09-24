using Sandbox;
namespace PaintballBaddies;

/// <summary>Opt-in native collision inspection of the authored bunker model.</summary>
public sealed class BunkerHullChecks : Component
{
    private GameObject fixture;
    private float elapsed;
    protected override void OnStart()
    {
        fixture = new GameObject { Name = "Bunker hull fixture", WorldPosition = new Vector3(0,0,500) };
        var model = Model.Load("models/foundry/foundry_cylinder_bunker/foundry_cylinder_bunker.vmdl");
        fixture.Components.Create<ModelRenderer>().Model = model;
        var collider = fixture.Components.Create<ModelCollider>(); collider.Model = model; collider.Static = true;
    }
    protected override void OnUpdate()
    {
        elapsed += Time.Delta; if (elapsed < 1) return;
        var center = Scene.Trace.Ray(new Vector3(-50,0,535),new Vector3(50,0,535)).Run();
        var corner = Scene.Trace.Ray(new Vector3(18,18,580),new Vector3(18,18,510)).Run();
        var top = Scene.Trace.Ray(new Vector3(0,0,600),new Vector3(0,0,490)).Run();
        Log.Info($"BUNKER_HULL {(center.GameObject == fixture && center.EndPosition.x > -22 && center.EndPosition.x < -15 ? "PASS" : "FAIL")} center collision {center.EndPosition}");
        Log.Info($"BUNKER_HULL {(corner.GameObject != fixture ? "PASS" : "FAIL")} square corner clear");
        Log.Info($"BUNKER_HULL {(top.GameObject == fixture && top.EndPosition.z > 568 && top.EndPosition.z < 574 ? "PASS" : "FAIL")} top height {top.EndPosition}");
        fixture.Destroy(); Destroy();
    }
}
