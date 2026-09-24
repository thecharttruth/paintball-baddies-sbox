using Sandbox;
namespace PaintballBaddies;
/// <summary>Imported inflatable candidate. Not placed until collision and cover review.</summary>
public sealed class SnakeBunker : Component
{
    [Property] public Vector3 Position { get; set; } = new(-850,-700,0);
    private GameObject bunker;
    protected override void OnStart()
    {
        var model=Model.Load("models/foundry/snake_bunker_image/snake_bunker_image.vmdl");
        if(!model.IsValid()){Log.Warning("SNAKE_BUNKER model missing");return;}
        bunker=new GameObject { NetworkMode=NetworkMode.Never, Name="Inflatable snake bunker candidate" };
        bunker.WorldPosition=Position;
        var visual=new GameObject(bunker) { Name="Inflatable vinyl" };
        visual.LocalScale=new Vector3(46f/31.1154f,1,46f/29.8502f);
        visual.Components.Create<ModelRenderer>().Model=model;
        var collider=bunker.Components.Create<CapsuleCollider>();
        collider.Start=new Vector3(0,-36,23);collider.End=new Vector3(0,36,23);
        collider.Radius=23;collider.Static=true;
        bunker.Components.Create<CoverSurface>().ModelHeight=46;
        Log.Info($"SNAKE_BUNKER bounds={model.Bounds}");
    }
    protected override void OnDestroy(){if(bunker.IsValid())bunker.Destroy();}
}
