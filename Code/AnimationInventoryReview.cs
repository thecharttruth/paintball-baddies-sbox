using Sandbox;
namespace PaintballBaddies;
public sealed class AnimationInventoryReview : Component
{
    protected override void OnStart()
    {
        foreach(var folder in new[]{"imani_out_review","imani_protected_morph"})
        {
            var model=Model.Load($"models/characters/{folder}/imani_protected.vmdl");
            Log.Info($"ANIMATION_INVENTORY {model.Name}: {string.Join(",",model.AnimationNames)}");
        }
        Destroy();
    }
}
