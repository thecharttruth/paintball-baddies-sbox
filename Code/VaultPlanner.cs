using Sandbox;
using System;

namespace PaintballBaddies;

/// <summary>Validates a conservative body-sized route before traversal is allowed.</summary>
public static class VaultPlanner
{
    public readonly record struct Plan( Vector3 Start, Vector3 AboveStart, Vector3 AboveLanding, Vector3 Landing, float Height, float Depth );
    public static bool TryPlan( PlayerController player, out Plan plan, out string reason )
    {
        plan = default;
        var start = player.WorldPosition;
        var attached=player.Components.Get<CoverController>();
        var forward = attached is {Attached:true} ? -attached.Normal : Rotation.FromYaw( player.EyeAngles.yaw ).Forward;
        var scene = player.Scene;
        var front = scene.Trace.Ray( start + Vector3.Up * 28, start + Vector3.Up * 28 + forward * 70 ).IgnoreGameObjectHierarchy( player.GameObject ).Run();
        if ( !front.Hit || front.GameObject.Components.Get<CoverSurface>() is null || Vector3.Dot( front.Normal.WithZ(0).Normal, -forward ) < .9f )
        { reason = "Face a cover surface"; return false; }
        var surface = front.GameObject.Components.Get<CoverSurface>();
        var height = surface.Top - start.z;
        if ( height < 30 || height > 58 ) { reason = height > 58 ? "Cover is too tall to vault" : "Cover is too low to vault"; return false; }
        var far = scene.Trace.Ray( front.EndPosition + forward * 110, front.EndPosition - forward * 2 ).IgnoreGameObjectHierarchy( player.GameObject ).Run();
        if ( !far.Hit || far.GameObject != front.GameObject ) { reason = "No clear far edge"; return false; }
        var depth = Vector3.Dot( far.EndPosition - front.EndPosition, forward );
        if ( depth < 3 || depth > 64 ) { reason = "Cover is too deep"; return false; }
        var landingProbe = (far.EndPosition + forward * 24).WithZ( surface.Top + 4 );
        var ground = scene.Trace.Ray( landingProbe, landingProbe.WithZ( start.z - 16 ) ).IgnoreGameObjectHierarchy( player.GameObject ).Run();
        if ( !ground.Hit || ground.Normal.z < .9f || MathF.Abs( ground.EndPosition.z - start.z ) > 8 )
        { reason = "No level landing"; return false; }
        var landing = ground.EndPosition + Vector3.Up * .1f;
        var highStart = start.WithZ( surface.Top + 3 );
        var highLanding = landing.WithZ( surface.Top + 3 );
        // Match the native rounded controller footprint at sloping barrier bases.
        var body = new Capsule(Vector3.Up*17,Vector3.Up*(player.BodyHeight-16),16);
        foreach ( var route in new[] { (start, highStart), (highStart, highLanding), (highLanding, landing) } )
        {
            var sweep = scene.Trace.Capsule( body, route.Item1, route.Item2 ).IgnoreGameObjectHierarchy( player.GameObject ).Run();
            if ( sweep.Hit ) { reason = "Body route is obstructed"; return false; }
        }
        plan = new Plan( start, highStart, highLanding, landing, height, depth );
        reason = "Clear vault route"; return true;
    }
}
