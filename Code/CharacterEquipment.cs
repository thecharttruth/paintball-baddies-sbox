using Sandbox;

namespace PaintballBaddies;

/// <summary>Reusable skinned protective gear for Citizen-compatible player or AI bodies.</summary>
public sealed class CharacterEquipment : Component
{
    private readonly List<GameObject> pieces = new();
    public SkinnedModelRenderer Outfit { get; private set; }
    public SkinnedModelRenderer Gloves { get; private set; }
    public SkinnedModelRenderer Boots { get; private set; }
    public SkinnedModelRenderer Helmet { get; private set; }
    protected override void OnUpdate()
    {
        var body=Components.Get<SkinnedModelRenderer>();
        if(body.IsValid() && Components.Get<CharacterHitboxes>() is null)
            Components.Create<CharacterHitboxes>().Configure(body,body.GameObject.Parent);
    }

    public void Configure(SkinnedModelRenderer body, string outfit, string gloves, string boots, string helmet = "")
    {
        Clear();
        if (!body.IsValid()) return;
        Outfit = Attach(body, outfit, "Protective suit");
        Gloves = Attach(body, gloves, "Protective gloves");
        Boots = Attach(body, boots, "Protective boots");
        Helmet = Attach(body, helmet, "Protective helmet");
        if(Components.Get<CharacterHitboxes>() is null)
            Components.Create<CharacterHitboxes>().Configure(body,body.GameObject.Parent);
    }

    private SkinnedModelRenderer Attach(SkinnedModelRenderer body, string path, string name)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        // Configure the final model and bone merge before allocating a render
        // object; don't briefly render the default box or an unmerged suit.
        var piece = new GameObject(GameObject) { Name = name, Enabled = false };
        pieces.Add(piece);
        var renderer = piece.Components.Create<SkinnedModelRenderer>();
        renderer.Model = Model.Load(path);
        renderer.BoneMergeTarget = body;
        renderer.UseAnimGraph = false;
        piece.Enabled = true;
        return renderer;
    }

    public void Clear()
    {
        foreach (var piece in pieces)
            if (piece.IsValid()) piece.Destroy();
        pieces.Clear();
        Outfit = null;
        Gloves = null;
        Boots = null;
        Helmet = null;
    }

    protected override void OnDestroy() => Clear();
}
