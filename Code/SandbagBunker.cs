using Sandbox;
namespace PaintballBaddies;

/// <summary>Reusable staggered library-bag cover; opt-in until placement review.</summary>
public sealed class SandbagBunker : Component
{
    [Property] public Vector3 Position { get; set; } = new(-850,-400,0);
    private GameObject stack;
    protected override async void OnStart()
    {
        var model=FoundryLibraryAssets.Sandbag;
        if(model.IsError)model=await Cloud.Load<Model>("facepunch.sandbag");
        if(!GameObject.IsValid() || !model.IsValid() || model.IsError)return;
        stack=new GameObject { NetworkMode=NetworkMode.Never, Name="Canvas sandbag cover" };
        stack.WorldPosition=Position;
        for(int course=0;course<13;course++)
        for(int depth=0;depth<2;depth++)
        for(int bag=0;bag<3;bag++)
        {
            var piece=new GameObject(stack) { Name="Sandbag" };
            var variation=System.MathF.Sin(course*2.3f+bag*4.1f+depth)*.65f;
            piece.LocalPosition=new Vector3((depth-.5f)*10.3f+variation,(bag-1)*20.6f+(course%2==0 ? -2.5f : 2.5f),course*3.05f);
            piece.LocalRotation=Rotation.FromYaw(((course+bag+depth)%2==0 ? 0 : 180)+variation*3);
            piece.Components.Create<ModelRenderer>().Model=model;
        }
        var collider=stack.Components.Create<BoxCollider>();
        collider.Scale=new Vector3(21,65,40.8f);collider.Center=new Vector3(0,0,20.4f);collider.Static=true;
        var cover=stack.Components.Create<CoverSurface>();cover.ModelHeight=40.8f;
        Log.Info($"SANDBAG_BUNKER ready=True bags=78 height=40.8 position={Position}");
    }
    protected override void OnDestroy() { if(stack.IsValid())stack.Destroy(); }
}
