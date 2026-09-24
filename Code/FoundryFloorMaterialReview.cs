using Sandbox;
namespace PaintballBaddies;
/// <summary>Opt-in comparison of an official industrial floor surface.</summary>
public sealed class FoundryFloorMaterialReview : Component
{
    protected override void OnStart()
    {
        var floor = Scene.GetAllComponents<ModelRenderer>().FirstOrDefault( x => x.GameObject.Name == "Arena floor" );
        var material = FoundryLibraryAssets.ResinFloor;
        if ( floor.IsValid() && material.IsValid() )
        {
            floor.MaterialOverride = material;
            floor.Tint = new Color( .55f, .58f, .60f );
        }
        Log.Info( $"FOUNDRY_RESIN_REVIEW floor={floor.IsValid()} material={material.IsValid()} path={material.Name}" );
    }
}
