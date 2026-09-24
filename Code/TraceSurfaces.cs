using Sandbox;
namespace PaintballBaddies;

internal static class TraceSurfaces
{
    // Mesh tracing is an editor facility. Runtime clients use collision meshes
    // and animated hitboxes, without calling the unsupported editor API.
    public static SceneTrace WithSurfaceMeshes(this SceneTrace trace)=>Game.IsEditor ? trace.UseRenderMeshes(true) : trace;
}
