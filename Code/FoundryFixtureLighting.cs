using Sandbox;
using System.Collections.Generic;
using System.Linq;
namespace PaintballBaddies;
/// <summary>Downward fixture light with a restrained unshadowed ceiling fill.</summary>
public sealed class FoundryFixtureLighting : Component
{
    private readonly List<(PointLight light, Color color, GameObject down)> fixtures = new();
    protected override void OnStart()
    {
        foreach (var light in Scene.GetAllComponents<PointLight>().Where(x=>x.Shadows && (x.GameObject.Name.StartsWith("Foundry overhead key") || x.GameObject.Name.StartsWith("Service annex luminaire"))).ToArray())
        {
            var down = new GameObject(light.GameObject) { NetworkMode=NetworkMode.Never, Name = "Downward fixture light" };
            down.WorldRotation = Rotation.LookAt(Vector3.Down, Vector3.Forward);
            var spot = down.Components.Create<SpotLight>();
            spot.LightColor = light.LightColor * .45f;
            spot.Radius = light.Radius;
            spot.ConeInner = 65;
            spot.ConeOuter = 85;
            spot.Shadows = true;
            spot.Attenuation = light.Attenuation;
            fixtures.Add((light,light.LightColor,down));
            light.LightColor *= .3f;
            light.Shadows = false;
        }
    }
    protected override void OnDestroy()
    {
        foreach (var fixture in fixtures)
        {
            if(fixture.light.IsValid()){fixture.light.LightColor=fixture.color;fixture.light.Shadows=true;}
            if(fixture.down.IsValid())fixture.down.Destroy();
        }
        fixtures.Clear();
    }
}
