using Sandbox;
using System.Linq;
namespace PaintballBaddies;

/// <summary>Opt-in accessory fitting preview. Does not modify the character source mesh.</summary>
public sealed class HelmetFitPreview : Component
{
    [Property] public string ModelVariant { get; set; } = "";
    [Property] public bool CloseUp { get; set; }
    [Property] public int CharacterIndex { get; set; } = 4;
    [Property] public bool UseHairVariant { get; set; } = true;
    [Property] public float ViewYaw { get; set; }
    [Property] public string Motion { get; set; } = "";
    [Property] public string HelmetAsset { get; set; } = "helmet_freya_hq";
    [Property] public float HelmetScale { get; set; } = 1.12f;
    [Property] public Vector3 HeadOffset { get; set; } = new(1.3f,0,-3.4f);
    private PlayerController player;
    private GameObject helmet;
    private GameObject neckSeal;
    private bool fitted;
    [Property] public bool FullBody { get; set; }
    protected override void OnStart()
    {
        player=Scene.GetAllComponents<PlayerController>().First();
        helmet=new GameObject { Name="Freya enclosed helmet fitting preview" };
        helmet.Components.Create<ModelRenderer>().Model=Model.Load($"models/gear/{HelmetAsset}/{HelmetAsset}.vmdl");
        neckSeal=new GameObject { Name="Helmet fabric neck seal fitting preview" };
        var sealAsset=CharacterIndex==4 ? "helmet_neck_seal" : $"helmet_neck_seal_{RosterSelection.Names[CharacterIndex].ToLowerInvariant()}";
        if (!string.IsNullOrEmpty(ModelVariant)) sealAsset=$"helmet_neck_seal_{ModelVariant}";
        neckSeal.Components.Create<SkinnedModelRenderer>().Model=Model.Load($"models/gear/{sealAsset}/{sealAsset}.vmdl");
    }
    protected override void OnUpdate()
    {
        var roster=player.Components.Get<RosterSelection>();
        if (roster is null) return;
        if (roster.Selected != CharacterIndex && !roster.Select(CharacterIndex)) return;
        var renderer=player.Renderer;
        if (!fitted)
        {
            if (UseHairVariant)
            {
                var slug=RosterSelection.Names[CharacterIndex].ToLowerInvariant();
                renderer.Model=string.IsNullOrEmpty(ModelVariant) ? Model.Load($"models/characters/{slug}/{slug}_helmet_hair_preview.vmdl") : Model.Load($"models/characters/{ModelVariant}/{ModelVariant}_combat_preview.vmdl");
            }
            renderer.UseAnimGraph=false;
            renderer.Sequence.Name="idle";
            renderer.Sequence.Looping=true;
            fitted=true;
        }
        if (!string.IsNullOrEmpty(Motion))
        {
            player.Components.Get<ViperAvatar>().PreviewSequence=Motion;
        }
        if (!renderer.TryGetBoneTransform("Head",out var head)) return;
        var bind=renderer.Model.GetBoneTransform("Head");
        var rotation=head.Rotation*bind.Rotation.Inverse;
        helmet.WorldRotation=rotation;
        helmet.WorldScale=Vector3.One*HelmetScale;
        helmet.WorldPosition=head.Position+rotation*HeadOffset;
        neckSeal.WorldTransform=renderer.WorldTransform;
        neckSeal.Enabled=true;
        if (neckSeal.Enabled) neckSeal.Components.Get<SkinnedModelRenderer>().BoneMergeTarget=renderer;
        if (CloseUp)
        {
            player.UseCameraControls=false;
            player.UseInputControls=false;
            player.UseLookControls=false;
            var camera=Components.Get<CameraComponent>();
            var target=FullBody ? renderer.WorldPosition+Vector3.Up*40 : head.Position+Vector3.Up*2;
            camera.WorldPosition=target+(renderer.WorldRotation*Rotation.FromYaw(ViewYaw)).Forward*(FullBody ? 160 : 32)+Vector3.Up*3;
            camera.WorldRotation=Rotation.LookAt(target-camera.WorldPosition);
            camera.FieldOfView=45;
        }
    }
    protected override void OnDestroy() { helmet?.Destroy(); neckSeal?.Destroy(); }
}
