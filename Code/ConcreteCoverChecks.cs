using Sandbox;
using System.Linq;
namespace PaintballBaddies;

/// <summary>Opt-in checks against the actual placed arena barriers.</summary>
public sealed class ConcreteCoverChecks : Component
{
    private PlayerController player;
    private CoverController cover;
    private CoverSurface[] barriers;
    private int index;
    private float elapsed;
    private void Place()
    {
        var surface=barriers[index];
        var collider=surface.Components.Get<ModelCollider>();
        var depth=collider.Model.Bounds.Size.x*surface.WorldScale.x;
        player.WorldPosition=surface.WorldPosition-surface.WorldRotation.Forward*(depth/2+30)+Vector3.Up*2;
        player.EyeAngles=surface.WorldRotation.Angles();
        player.Body.Velocity=Vector3.Zero;
        elapsed=0;
    }
    protected override void OnStart()
    {
        player=Components.Get<PlayerController>();cover=Components.Get<CoverController>();
        player.UseInputControls=player.UseLookControls=false;
        Components.Get<PaintballMarker>().AcceptInput=false;cover.TestInput=true;
        barriers=Scene.GetAllComponents<CoverSurface>().Where(c=>c.GameObject.Name.StartsWith("Concrete cover ")).ToArray();
        if(barriers.Length==0) { Log.Error("CONCRETE_COVER FAIL no placed barriers");Destroy();return; }
        Place();
    }
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;
        if(elapsed<.6f)return;
        var surface=barriers[index];
        var trace=Scene.Trace.Ray(player.WorldPosition+Vector3.Up*28,player.WorldPosition+Vector3.Up*28+surface.WorldRotation.Forward*70).IgnoreGameObjectHierarchy(GameObject).Run();
        bool attached=cover.TryEnter();
        Log.Info($"CONCRETE_COVER {(attached ? "PASS" : "FAIL")} {surface.GameObject.Name} attachment; normal={trace.Normal}, airborne={player.IsAirborne}");
        bool route=VaultPlanner.TryPlan(player,out var plan,out var reason);
        bool expected=surface.Top-player.WorldPosition.z<=58;
        Log.Info($"CONCRETE_COVER {(route==expected ? "PASS" : "FAIL")} {surface.GameObject.Name} vault route; expected={expected}, actual={route}, reason={reason}");
        cover.Leave();index++;
        if(index<barriers.Length) { Place();return; }
        cover.TestInput=false;player.UseInputControls=player.UseLookControls=true;
        Components.Get<PaintballMarker>().AcceptInput=true;
        Log.Info($"CONCRETE_COVER COMPLETE barriers={barriers.Length}");Destroy();
    }
}
