using Sandbox;
namespace PaintballBaddies;

/// <summary>Extend native per-camera render filtering to our separate body and equipment.</summary>
public sealed class CloseCameraVisibility : Component, ICameraModifier
{
    public int CameraOrder=>300;
    public bool Hidden {get;private set;}
    public float EyeDistance {get;private set;}
    public int HiddenPieces=>tagged.Count;
    // GameObject tags have a length limit; keep this per-owner token short.
    private readonly string ownerTag="pbcam_"+System.Guid.NewGuid().ToString("N")[..12];
    private readonly HashSet<GameObject> tagged=new();
    private CameraComponent lastCamera;
    public void ModifyCamera(CameraComponent camera,ref CameraView view) { }
    public void PostCameraSetup(CameraComponent camera,in CameraView view)
    {
        if(IsProxy || camera!=Scene.Camera)return;
        var player=Components.Get<PlayerController>();
        if(!player.IsValid()||!player.UseCameraControls){Restore();return;}
        Apply(camera,view.Position,player.EyePosition);
    }
    public void Apply(CameraComponent camera,Vector3 cameraPosition,Vector3 eye)
    {
        EyeDistance=(cameraPosition-eye).Length;
        // Hysteresis prevents flashing as the wall-clipped camera hovers near
        // the helmet. This changes rendering only, never physics or shot traces.
        Hidden=EyeDistance<(Hidden?52:44);
        if(!Hidden){Restore();return;}
        if(lastCamera.IsValid()&&lastCamera!=camera)lastCamera.RenderExcludeTags.Remove(ownerTag);
        lastCamera=camera;camera.RenderExcludeTags.Add(ownerTag);
        var visibleObjects=Components.GetAll<ModelRenderer>(FindMode.EverythingInSelfAndDescendants).Select(x=>x.GameObject)
            .Concat(Components.GetAll<Decal>(FindMode.EverythingInSelfAndDescendants).Select(x=>x.GameObject)).ToHashSet();
        foreach(var shield in Scene.GetAllComponents<ArenaShield>().Where(x=>x.Owner.IsValid()&&x.Owner.GameObject==GameObject))
            foreach(var r in shield.Components.GetAll<ModelRenderer>(FindMode.EverythingInSelfAndDescendants))visibleObjects.Add(r.GameObject);
        foreach(var old in tagged.Where(x=>!x.IsValid()||!visibleObjects.Contains(x)).ToArray())
        {if(old.IsValid())old.Tags.Remove(ownerTag);tagged.Remove(old);}
        foreach(var piece in visibleObjects)if(tagged.Add(piece))piece.Tags.Add(ownerTag);
    }
    public bool Filters(CameraComponent camera,GameObject piece)=>camera.RenderExcludeTags.Contains(ownerTag)&&piece.Tags.Contains(ownerTag);
    public void Restore()
    {
        Hidden=false;
        if(lastCamera.IsValid())lastCamera.RenderExcludeTags.Remove(ownerTag);
        foreach(var piece in tagged)if(piece.IsValid())piece.Tags.Remove(ownerTag);
        tagged.Clear();lastCamera=null;
    }
    protected override void OnDisabled()=>Restore();
    protected override void OnDestroy()=>Restore();
}
