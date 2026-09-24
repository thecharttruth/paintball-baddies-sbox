using Sandbox;
namespace PaintballBaddies;

/// <summary>The last twelve paint impacts, following native animated bone objects.</summary>
public sealed class CharacterPaint : Component
{
    public const int Capacity=12;
    private readonly Queue<GameObject> marks=new();
    public int Count=>marks.Count;
    public string LastBone { get; private set; }
    public Vector3 LastPosition { get; private set; }
    // Read only, used by the opt-in multiplayer release check.
    internal object AttachmentReview()
    {
        var body=RenderBody;
        var saved=string.IsNullOrEmpty(PaintHistory) ? new List<PaintRecord>() : Json.Deserialize<List<PaintRecord>>(PaintHistory);
        var visible=marks.ToArray();
        return new {Count=visible.Length,Records=saved.Count,History=PaintHistory,
            Marks=visible.Select((mark,i)=>new {Valid=mark.IsValid(),
                Bone=saved.Count>i?saved[i].Bone:null,
                Attached=mark.IsValid() && body.IsValid() && saved.Count>i && mark.Parent==body.GetBoneObject(saved[i].Bone),
                LocalError=mark.IsValid() && saved.Count>i ? (mark.LocalPosition-saved[i].Point).Length : 9999,
                World=mark.IsValid()?mark.WorldPosition.ToString():null}).ToArray()};
    }
    private int sequence;
    [Sync(SyncFlags.FromHost)] public string PaintHistory {get;set;}="";
    private readonly List<PaintRecord> records=new();
    private int receivedSequence;
    private string receivedHistory;
    private sealed class PaintRecord
    {
        public int Id {get;set;}
        public string Bone {get;set;}
        public Vector3 Point {get;set;}
        public Rotation Facing {get;set;}
        public Color Tint {get;set;}
    }
    private SkinnedModelRenderer RenderBody=>Components.Get<CitizenPlayerPresentation>()?.FootstepRenderer
        ?? Components.Get<CitizenOpponentPresentation>()?.FootstepRenderer
        ?? Components.Get<ArenaOpponent>()?.Body ?? Components.Get<PlayerController>()?.Renderer;
    protected override void OnUpdate()
    {
        if(MultiplayerSession.Authority || receivedHistory==PaintHistory || !RenderBody.IsValid())return;
        if(string.IsNullOrEmpty(PaintHistory)){Clear();receivedHistory=PaintHistory;return;}
        var entries=Json.Deserialize<List<PaintRecord>>(PaintHistory);
        foreach(var entry in entries)if(entry.Id>receivedSequence){if(!Apply(entry,RenderBody))return;receivedSequence=entry.Id;}
        receivedHistory=PaintHistory;
    }
    private bool Apply(PaintRecord record,SkinnedModelRenderer body)
    {
        var parent=body.GetBoneObject(record.Bone);if(!parent.IsValid())return false;
        while(marks.Count>=Capacity)marks.Dequeue()?.Destroy();
        var go=new GameObject(parent){Name="Clothing paint splat",NetworkMode=NetworkMode.Never};
        go.LocalPosition=record.Point;go.LocalRotation=record.Facing;
        var decal=go.Components.Create<Decal>();
        decal.Decals=new(){new(){ColorTexture=PaintImpactSystem.SplashTexture,Width=1,Height=1,ColorMix=1}};
        decal.Size=new Vector2(5.5f,5.5f)*PaintImpactSystem.SplashScale;decal.Depth=5;
        decal.ColorTint=record.Tint;decal.Rotation=(record.Id*137.508f)%360;
        decal.LifeTime=0;decal.Transient=false;decal.AttenuationAngle=.3f;
        marks.Enqueue(go);return true;
    }
    public void Add(SceneTraceResult hit,Color tint)
    {
        var body=Components.Get<CitizenPlayerPresentation>()?.FootstepRenderer
            ?? Components.Get<CitizenOpponentPresentation>()?.FootstepRenderer
            ?? Components.Get<ArenaOpponent>()?.Body ?? Components.Get<PlayerController>()?.Renderer;
        if(!body.IsValid())return;
        // A movement capsule/box is broader than the clothing. Project onto
        // the visible surface before attaching the paint, when available.
        var surface=Scene.Trace.Ray(hit.HitPosition+hit.Normal*12,hit.HitPosition-hit.Normal*24)
            .WithSurfaceMeshes().UsePhysicsWorld(!Game.IsEditor).WithoutTags("paintball_debris").Run();
        if(surface.Hit && PaintballCombatant.Find(surface.GameObject)?.GameObject==GameObject)hit=surface;
        // IK targets are control points, not cloth bones. Some are closest to
        // the seat or thigh during a stride and made paint swing with a hand.
        string boneName=null;float nearest=float.MaxValue;
        foreach(var bone in body.Model.Bones.AllBones)
        {
            var name=bone.Name;
            if(name!="pelvis" && name!="head" && name!="neck_0" && !name.StartsWith("spine_")
                && name!="leg_upper_L" && name!="leg_upper_R" && name!="leg_lower_L" && name!="leg_lower_R"
                && name!="arm_upper_L" && name!="arm_upper_R" && name!="arm_lower_L" && name!="arm_lower_R"
                && name!="hand_L" && name!="hand_R" && name!="foot_L" && name!="foot_R")continue;
            if(!body.TryGetBoneTransform(name,out var pose))continue;
            float distance=(pose.Position-hit.HitPosition).LengthSquared;
            if(distance<nearest){nearest=distance;boneName=name;}
        }
        if(body.TryGetBoneTransform("pelvis",out var pelvis))
        {
            var local=body.WorldTransform.PointToLocal(hit.HitPosition);
            var hip=body.WorldTransform.PointToLocal(pelvis.Position);
            if(System.MathF.Abs(local.z-hip.z)<7 && (local-hip).WithZ(0).Length<12
                && hit.GameObject?.Name!="Protective gloves")boneName="pelvis";
        }
        if(boneName is null)return;
        var parent=body.GetBoneObject(boneName);
        if(!parent.IsValid())return;
        if(!MultiplayerSession.Authority)return;
        var record=new PaintRecord{Id=++sequence,Bone=boneName,Point=parent.WorldTransform.PointToLocal(hit.HitPosition+hit.Normal*.2f),
            Facing=parent.WorldRotation.Inverse*Rotation.LookAt(-hit.Normal),Tint=tint};
        Apply(record,body);
        if(MultiplayerSession.Online)
        {
            records.Add(record);if(records.Count>Capacity)records.RemoveAt(0);
            PaintHistory=Json.Serialize(records);
        }
        LastBone=boneName;LastPosition=hit.HitPosition;
    }
    public void Clear(){while(marks.Count>0)marks.Dequeue()?.Destroy();records.Clear();receivedSequence=0;receivedHistory="";if(MultiplayerSession.Authority)PaintHistory="";}
    protected override void OnDestroy()=>Clear();
}
