using Sandbox;
namespace PaintballBaddies;

/// <summary>Shared physical paintball awareness for player and AI projectiles.</summary>
public static class IncomingPaintAwareness
{
    public static void Report(Scene scene,GameObject shooter,Vector3 origin,Vector3 from,Vector3 to,
        GameObject impact,HashSet<ArenaOpponent> alerted)
    {
        var segment=to-from;
        var lengthSquared=segment.LengthSquared;
        foreach(var bot in scene.GetAllComponents<ArenaOpponent>())
        {
            if(!bot.CombatEnabled || !bot.Enabled || bot.GameObject==shooter || alerted.Contains(bot))continue;
            var centre=bot.WorldPosition+Vector3.Up*38;
            float t=lengthSquared>.001f ? (Vector3.Dot(centre-from,segment)/lengthSquared).Clamp(0,1) : 0;
            var closest=from+segment*t;
            bool ownCover=impact.IsValid() && bot.ReservedCover.IsValid() && impact==bot.ReservedCover.GameObject
                && (to-centre).Length<150;
            if(!ownCover)
            {
                if((closest-centre).Length>85)continue;
                var nearby=scene.Trace.Ray(closest,centre).IgnoreGameObjectHierarchy(shooter)
                    .IgnoreGameObjectHierarchy(bot.GameObject).WithoutTags("paintball_debris").Run();
                if(nearby.Hit && nearby.GameObject!=bot.ReservedCover?.GameObject)continue;
            }
            alerted.Add(bot);
            bot.ObserveIncomingPaint(shooter,origin);
        }
    }
}
