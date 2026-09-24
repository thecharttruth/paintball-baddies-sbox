using Sandbox;
using Sandbox.Navigation;
namespace PaintballBaddies;

/// <summary>Find a physically sheltered destination relative to an observed threat.</summary>
public static class OpponentCoverPlanner
{
    public static CoverSurface ShelteringSurface(GameObject actor,GameObject threat,Vector3 origin,Vector3 position)
    {
        var hit=actor.Scene.Trace.Ray(origin+Vector3.Up*55,position+Vector3.Up*30)
            .IgnoreGameObjectHierarchy(actor).IgnoreGameObjectHierarchy(threat).Run();
        return hit.GameObject?.Components.Get<CoverSurface>();
    }
    public static bool IsOccupied(GameObject actor,CoverSurface surface)
    {
        if(!surface.IsValid())return false;
        return actor.Scene.GetAllComponents<CoverController>().Any(x=>x.GameObject!=actor && x.ReservedSurface==surface)
            || actor.Scene.GetAllComponents<ArenaOpponent>().Any(x=>x.GameObject!=actor && x.GameObject.Active
                && x.Components.Get<PaintballCombatant>()?.Eliminated!=true && x.HasCoverPlan && x.ReservedCover==surface);
    }
    public static bool IsSheltered(GameObject actor, GameObject threatActor, Vector3 observedThreat, Vector3 position)
    {
        var shield=actor.Scene.Trace.Ray(observedThreat+Vector3.Up*55,position+Vector3.Up*30)
            .IgnoreGameObjectHierarchy(actor).IgnoreGameObjectHierarchy(threatActor).Run();
        return shield.Hit && shield.GameObject.Components.Get<CoverSurface>() is not null;
    }
    public static bool TryPeek(GameObject actor,GameObject threatActor,Vector3 threat,Vector3 shelter,out Vector3 peek,out bool low)
    {
        peek=shelter;low=false;
        var shield=actor.Scene.Trace.Ray(threat+Vector3.Up*55,shelter+Vector3.Up*30)
            .IgnoreGameObjectHierarchy(actor).IgnoreGameObjectHierarchy(threatActor).Run();
        var surface=shield.GameObject?.Components.Get<CoverSurface>();
        if(!surface.IsValid())return false;
        low=!surface.StandingOnly && surface.Top-shelter.z<64;
        var away=(shelter-threat).WithZ(0).Normal;
        var side=Vector3.Cross(Vector3.Up,away);
        var candidates=low ? new[]{shelter,shelter+side*44,shelter-side*44} : new[]{shelter+side*55,shelter-side*55,shelter+side*88,shelter-side*88};
        foreach(var candidate in candidates)
        {
            // Match the destination to the engine's walkable surface before
            // checking the firing lane; MoveTo otherwise silently stops short.
            var grounded=actor.Scene.NavMesh.GetClosestPoint(candidate,32);
            if(grounded is not Vector3 point || System.MathF.Abs(point.z-shelter.z)>8)continue;
            var route=actor.Scene.Trace.Sphere(16,shelter+Vector3.Up*50,point+Vector3.Up*50).IgnoreGameObjectHierarchy(actor).Run();
            if(route.Hit)continue;
            var floor=actor.Scene.Trace.Ray(point+Vector3.Up*12,point-Vector3.Up*24).IgnoreGameObjectHierarchy(actor).Run();
            if(!floor.Hit||floor.Normal.z<.8f)continue;
            var line=actor.Scene.Trace.Sphere(2,point+Vector3.Up*46,threat+Vector3.Up*40)
                .IgnoreGameObjectHierarchy(actor).IgnoreGameObjectHierarchy(threatActor).WithoutTags("paintball_debris").Run();
            if(line.Hit)continue;
            peek=point;return true;
        }
        return false;
    }
    public static bool TryFind(GameObject actor,GameObject threatActor,Vector3 observedThreat,out Vector3 destination,bool requirePeek=true,bool relocating=false)
    {
        destination=default;float best=float.MaxValue;
        var bot=actor.Components.Get<ArenaOpponent>();
        if(actor.Scene.NavMesh.IsGenerating)return false;
        foreach(var surface in actor.Scene.GetAllComponents<CoverSurface>())
        {
            if(!surface.Enabled || !surface.GameObject.Enabled || IsOccupied(actor,surface))continue;
            if(bot?.RecentlyUsed(surface)==true || (relocating && bot?.ReservedCover==surface))continue;
            var away=(surface.WorldPosition-observedThreat).WithZ(0).Normal;
            if(away.Length<.9f)continue;
            var centre=surface.WorldPosition.WithZ(actor.WorldPosition.z+28);
            var face=actor.Scene.Trace.Ray(centre+away*160,centre).IgnoreGameObjectHierarchy(actor).Run();
            if(!face.Hit || face.GameObject!=surface.GameObject)continue;
            var point=(face.EndPosition+away*25).WithZ(actor.WorldPosition.z);
            var floor=actor.Scene.Trace.Ray(point+Vector3.Up*20,point-Vector3.Up*40).IgnoreGameObjectHierarchy(actor).Run();
            if(!floor.Hit || floor.Normal.z<.8f)continue;
            point=floor.EndPosition+Vector3.Up;
            var grounded=actor.Scene.NavMesh.GetClosestPoint(point,32);
            if(grounded is not Vector3 reachable || System.MathF.Abs(reachable.z-point.z)>8)continue;
            point=reachable;
            if(surface.Top-point.z<37)continue;
            var body=actor.Scene.Trace.Sphere(15,point+Vector3.Up*20,point+Vector3.Up*21).IgnoreGameObjectHierarchy(actor).Run();
            if(body.Hit)continue;
            var shield=actor.Scene.Trace.Ray(observedThreat+Vector3.Up*55,point+Vector3.Up*30)
                .IgnoreGameObjectHierarchy(actor).IgnoreGameObjectHierarchy(threatActor).Run();
            if(!shield.Hit || shield.GameObject!=surface.GameObject)continue;
            // Choose cover that also offers a reachable firing position, avoiding
            // repeated trips into a blind pocket followed by immediate retreat.
            if(requirePeek && !TryPeek(actor,threatActor,observedThreat,point,out _,out _))continue;
            bool occupied=false;
            foreach(var other in actor.Scene.GetAllComponents<ArenaOpponent>())
            {
                if(other.Components.Get<PaintballCombatant>()?.Eliminated==true)continue;
                if(other.GameObject!=actor && ((other.WorldPosition-point).WithZ(0).Length<65
                    || (other.HasCoverPlan && (other.CoverDestination-point).WithZ(0).Length<65)))occupied=true;
            }
            float distance=(point-actor.WorldPosition).WithZ(0).Length;
            if(occupied || distance>(relocating ? 720 : 550) || (relocating && distance<140))continue;
            var path=actor.Scene.NavMesh.CalculatePath(new CalculatePathRequest{
                Start=actor.WorldPosition,Target=point,Agent=actor.Components.Get<NavMeshAgent>()});
            if(path.Status!=NavMeshPathStatus.Complete || path.Points.Count<1)continue;
            float routeLength=0,exposure=0;
            var previous=actor.WorldPosition;
            foreach(var step in path.Points)
            {
                routeLength+=(step.Position-previous).Length;
                if(relocating || bot?.EvadingAfterHit==true)
                {
                    var sample=(step.Position+previous)*.5f+Vector3.Up*38;
                    var sight=actor.Scene.Trace.Ray(observedThreat+Vector3.Up*55,sample)
                        .IgnoreGameObjectHierarchy(actor).IgnoreGameObjectHierarchy(threatActor).Run();
                    if(!sight.Hit)exposure+=(step.Position-previous).Length;
                }
                previous=step.Position;
            }
            if(routeLength>(relocating ? 1000 : 650))continue;
            // Prefer shorter protected routes, but vary comparable choices and
            // reward a new firing angle rather than another spot on one bunker.
            float angleChange=1-Vector3.Dot((actor.WorldPosition-observedThreat).WithZ(0).Normal,
                (point-observedThreat).WithZ(0).Normal);
            float cost=distance+routeLength*.35f+exposure*.55f-(relocating ? angleChange*90 : 0)+Game.Random.Float(0,65);
            // Avoid sheltering from one observed opponent while exposing our
            // back to the other visible contestants. No unseen positions used.
            if(bot is not null)
            foreach(var threat in bot.VisibleThreats)
            {
                if((threat-observedThreat).Length<60)continue;
                var danger=actor.Scene.Trace.Ray(threat+Vector3.Up*55,point+Vector3.Up*30)
                    .IgnoreGameObjectHierarchy(actor).WithoutTags("paintball_debris","paintball_actor").Run();
                if(!danger.Hit)cost+=180;
            }
            if(cost>=best)continue;
            best=cost;destination=point;
        }
        return best<float.MaxValue;
    }
}
