using Sandbox;
namespace PaintballBaddies;

/// <summary>Plays the authored reload presentation on an isolated character candidate.</summary>
public sealed class ReloadPresentationTrack : Component
{
    public SkinnedModelRenderer Body { get; set; }
    public float Phase { get; set; }
    public bool Docked { get; set; }
    public Vector3 BodyOffset { get; set; }
    public string TrackPath { get; set; } = "animations/roxie_reload_presentation.json";
    public sealed class Pose { public float[] position { get; set; } public float[] rotation { get; set; } public bool visible { get; set; } }
    public sealed class Frame { public Pose[] objects { get; set; } public float leftGrip { get; set; } public float leftThumb { get; set; } }
    public sealed class Track { public float duration { get; set; } public Frame[] frames { get; set; } public Pose[] dockObjects { get; set; } public float[] dockBoneRestRotation { get; set; } }
    private Track track;
    private readonly System.Collections.Generic.List<GameObject> props = new();
    public System.Collections.Generic.IReadOnlyList<GameObject> PresentationProps => props;
    protected override void OnStart()
    {
        track = Json.Deserialize<Track>( FileSystem.Mounted.ReadAllText( TrackPath ) );
        for ( int i = 0; i < 11; i++ )
        {
            var prop = new GameObject { Name = $"Reload presentation {i}", NetworkMode=NetworkMode.Never };
            var renderer = prop.Components.Create<ModelRenderer>();
            renderer.Model = Model.Load( i < 3 ? $"models/gear/refill_pod/{new[] { "body", "lid", "pouch" }[i]}.vmdl" : "models/dev/sphere.vmdl" );
            if ( i >= 3 ) { prop.WorldScale = new Vector3( .0063f ); renderer.Tint = new Color( .95f, .28f, .025f ); }
            props.Add( prop );
        }
    }
    protected override void OnUpdate()
    {
        ApplyFrame();
    }
    public void ApplyFrame()
    {
        if ( track is null || !Body.IsValid() ) return;
        if ( Docked )
        {
            if ( track.dockObjects is null || !Body.TryGetBoneTransform( "Hips", out var hip ) ) return;
            var rotation = hip.Rotation;
            if ( track.dockBoneRestRotation is { Length: 4 } r )
                rotation *= Body.Model.GetBoneTransform( "Hips" ).Rotation.Inverse * new Rotation( r[0], r[1], r[2], r[3] );
            for ( int i = 0; i < props.Count; i++ )
            {
                props[i].Enabled = i < track.dockObjects.Length;
                if ( i >= track.dockObjects.Length ) continue;
                var pose = track.dockObjects[i];
                props[i].WorldPosition = hip.Position + rotation * new Vector3( pose.position[0], pose.position[1], pose.position[2] );
                props[i].WorldRotation = rotation * new Rotation( pose.rotation[0], pose.rotation[1], pose.rotation[2], pose.rotation[3] );
            }
            foreach ( var name in Body.Morphs.Names )
                if ( name == "LeftGrip" || name == "LeftThumb" || name == "RightGrip" || name == "RightThumb" ) Body.Morphs.Set( name, 1, 0 );
            return;
        }
        float sample = System.Math.Clamp( Phase, 0, 1 ) * (track.frames.Length - 1);
        int index = (int)sample; var a = track.frames[index]; var b = track.frames[System.Math.Min( index + 1, track.frames.Length - 1 )]; float t = sample - index;
        for ( int i = 0; i < props.Count; i++ )
        {
            var x = a.objects[i]; var y = b.objects[i];
            var p = Vector3.Lerp( new Vector3( x.position[0], x.position[1], x.position[2] ), new Vector3( y.position[0], y.position[1], y.position[2] ), t );
            var q = Rotation.Slerp( new Rotation( x.rotation[0], x.rotation[1], x.rotation[2], x.rotation[3] ), new Rotation( y.rotation[0], y.rotation[1], y.rotation[2], y.rotation[3] ), t );
            props[i].WorldPosition = Body.WorldPosition + Body.WorldRotation * (p + BodyOffset);
            props[i].WorldRotation = Body.WorldRotation * q;
            props[i].Enabled = x.visible;
        }
        // The authored track already interpolates grip; an additional animation-time
        // fade also prevents the shape reaching its target in a paused pose review.
        Body.Morphs.Set( "LeftGrip", a.leftGrip + (b.leftGrip - a.leftGrip) * t, 0 );
        Body.Morphs.Set( "LeftThumb", a.leftThumb + (b.leftThumb - a.leftThumb) * t, 0 );
        Body.Morphs.Set( "RightGrip", 1, 0 ); Body.Morphs.Set( "RightThumb", 1, 0 );
    }
    protected override void OnDestroy() { foreach ( var prop in props ) prop?.Destroy(); }
}

