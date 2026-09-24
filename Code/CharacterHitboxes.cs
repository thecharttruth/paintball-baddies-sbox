using Sandbox;
namespace PaintballBaddies;

/// <summary>Native animated hitboxes for the adapted Citizen skeleton.</summary>
public sealed class CharacterHitboxes : Component
{
    private SkinnedModelRenderer body;
    public GameObject Actor { get; private set; }
    public int Count { get; private set; }
    private readonly List<GameObject> boxes=new();
    public void Configure(SkinnedModelRenderer renderer,GameObject actor){body=renderer;Actor=actor;}
    protected override void OnUpdate()
    {
        if(Count>0 || !body.IsValid() || !body.Model.IsValid())return;
        Add("pelvis","spine_0",7.5f);
        Add("spine_0","spine_2",6.5f);
        Add("head",null,5.4f);
        foreach(var side in new[]{"L","R"})
        {
            Add("arm_upper_"+side,"arm_lower_"+side,2.6f);
            Add("arm_lower_"+side,"hand_"+side,2.2f);
            Add("hand_"+side,null,2.1f);
            Add("leg_upper_"+side,"leg_lower_"+side,4.2f);
            Add("leg_lower_"+side,"foot_"+side,3.0f);
            Add("foot_"+side,null,3.0f);
        }
    }
    private void Add(string start,string end,float radius)
    {
        if(!body.TryGetBoneTransform(start,out var from))return;
        var to=from;
        if(end is not null && !body.TryGetBoneTransform(end,out to))return;
        var parent=body.GetBoneObject(start);
        if(!parent.IsValid())return;
        var go=new GameObject(parent){Name="Paint hitbox "+start};
        go.LocalPosition=Vector3.Zero;go.LocalRotation=Rotation.Identity;
        var hitbox=go.Components.Create<ManualHitbox>();
        hitbox.Shape=end is null ? ManualHitbox.HitboxShape.Sphere : ManualHitbox.HitboxShape.Capsule;
        hitbox.CenterA=Vector3.Zero;hitbox.CenterB=from.PointToLocal(to.Position);
        hitbox.Radius=radius;hitbox.Target=Actor;hitbox.Rebuild();
        boxes.Add(go);Count++;
    }
    protected override void OnDestroy(){foreach(var go in boxes)if(go.IsValid())go.Destroy();}
}
