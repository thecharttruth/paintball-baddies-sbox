using Sandbox;
namespace PaintballBaddies;

/// <summary>Shared fitted equipment for player and opponent roster bodies.</summary>
public sealed class RosterHelmet : Component
{
    public static bool HasFit(int index) => index >= 0 && index < RosterSelection.Names.Length;
    private GameObject helmet, seal;
    private int equippedIndex=-1;
    private SkinnedModelRenderer sealRenderer;
    public bool Equipped => helmet.IsValid() && seal.IsValid();

    protected override void OnUpdate()
    {
        var roster=Components.Get<RosterSelection>();
        var player=Components.Get<PlayerController>();
        var opponent=Components.Get<ArenaOpponent>();
        var index=opponent.IsValid() ? opponent.CharacterIndex : roster?.Selected ?? -1;
        var body=opponent.IsValid() ? opponent.Body : player?.Renderer;
        if ( body.IsValid() && CoveredRosterAssets.CharacterIndex( body.Model?.Name ) >= 0 )
        {
            Clear();
            return;
        }
        if (!HasFit(index) || Components.Get<PaintballCombatant>()?.Eliminated == true ||
            (!opponent.IsValid() && Scene.GetAllComponents<HelmetFitPreview>().Any()))
        {
            Clear();
            return;
        }
        if (!body.IsValid() || !body.TryGetBoneTransform("Head",out var head)) return;
        if (!Equipped || equippedIndex!=index)
        {
            Clear();
            equippedIndex=index;
            var name=RosterSelection.Names[equippedIndex];
            var slug=name.ToLowerInvariant();
            helmet=new GameObject(GameObject,true,$"{name} fitted helmet");
            helmet.Components.Create<ModelRenderer>().Model=Model.Load($"models/gear/helmet_{slug}_hq/helmet_{slug}_hq.vmdl");
            seal=new GameObject(GameObject,true,$"{name} fitted neck seal");
            sealRenderer=seal.Components.Create<SkinnedModelRenderer>();
            var sealAsset=equippedIndex==4 ? "helmet_neck_seal" : equippedIndex==2 ? "helmet_neck_seal_imani_contained" : $"helmet_neck_seal_{slug}";
            sealRenderer.Model=Model.Load($"models/gear/{sealAsset}/{sealAsset}.vmdl");
        }
        var rotation=head.Rotation*body.Model.GetBoneTransform("Head").Rotation.Inverse;
        helmet.WorldRotation=rotation;
        helmet.WorldScale=Vector3.One*1.12f;
        helmet.WorldPosition=head.Position+rotation*new Vector3(1.3f,0,equippedIndex==2 ? -2.5f : -3.4f);
        seal.WorldTransform=body.WorldTransform;
        sealRenderer.BoneMergeTarget=body;
    }

    private void Clear()
    {
        if (helmet.IsValid()) helmet.Destroy();
        if (seal.IsValid()) seal.Destroy();
        helmet=null;seal=null;sealRenderer=null;equippedIndex=-1;
    }
    protected override void OnDisabled() => Clear();
    protected override void OnDestroy() => Clear();
}
