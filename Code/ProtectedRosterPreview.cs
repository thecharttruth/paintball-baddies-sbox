using Sandbox;

namespace PaintballBaddies;

/// <summary>Isolated native preview of the protected roster candidate.</summary>
public sealed class ProtectedRosterPreview : Component
{
    [Property] public string ModelPath { get; set; } = "models/characters/roxie_protected/roxie_protected.vmdl";
    [Property] public string Motion { get; set; } = "run";
    [Property] public Vector3 MarkerOffset { get; set; } = new( 5, 0, 0 );
    [Property] public bool HandCloseUp { get; set; }
    [Property] public bool ShowMarker { get; set; } = true;
    [Property] public bool ShowBeltGear { get; set; }
    [Property] public bool ShowContactFloor { get; set; }
    [Property] public float ViewYaw { get; set; }
    [Property] public float MotionPhase { get; set; } = -1;
    [Property] public bool LogPoseReview { get; set; }
    private bool poseLogged;
    [Property] public bool HeadCloseUp { get; set; }
    [Property] public float GripAmount { get; set; } = 1;
    [Property] public float ThumbAmount { get; set; } = 1;
    [Property] public float AimPitch { get; set; }
    [Property] public bool ReviewAimTransition { get; set; }
    private float appliedPitch;
    private float aimReviewTime;
    [Property] public string MarkerModelPath { get; set; } = "models/weapons/vx9_full_frame/vx9_full_frame.vmdl";
    [Property] public Vector3 CloseUpFocusOffset { get; set; } = new( 5, 0, 0 );
    [Property] public Vector3 CloseUpCameraOffset { get; set; } = new( 30, -35, 15 );
    [Property] public float ReloadPhase { get; set; } = .55f;
    [Property] public string ReloadTrackPath { get; set; } = "";
    private ReloadPresentationTrack reloadTrack;
    private GameObject subject;
    private GameObject marker;
    private SkinnedModelRenderer body;
    protected override void OnStart()
    {
        var player = Scene.GetAllComponents<PlayerController>().First();
        player.UseCameraControls = false;
        player.UseInputControls = false;
        player.UseLookControls = false;
        subject = new GameObject { Name = "Protected Roxie review" };
        subject.WorldPosition = new Vector3( 0, 0, 410 );
        if ( ShowContactFloor )
            TrainingRange.Box( subject, "Sole contact reference", new Vector3( 0, 0, -1 ),
                new Vector3( 160, 160, 2 ), new Color( .55f, .58f, .60f ) );
        body = subject.Components.Create<SkinnedModelRenderer>();
        body.Model = Model.Load( ModelPath );
        marker = new GameObject { Name = "Protected marker fit review" };
        marker.Components.Create<ModelRenderer>().Model = Model.Load( MarkerModelPath );
        marker.Enabled = ShowMarker;
        body.UseAnimGraph = false;
        body.Sequence.Name = Motion;
        body.Sequence.Looping = true;
        if ( Motion == "reload_standing" )
        {
            body.Sequence.Looping = false;
            body.PlaybackRate = 0;
            reloadTrack = subject.Components.Create<ReloadPresentationTrack>();
            reloadTrack.Body = body;
            if ( ModelPath.Contains( "/mei_protected" ) ) reloadTrack.TrackPath = "animations/mei_reload_presentation.json";
            if ( !string.IsNullOrWhiteSpace( ReloadTrackPath ) ) reloadTrack.TrackPath = ReloadTrackPath;
            reloadTrack.Phase = ReloadPhase;
            reloadTrack.ApplyFrame();
        }
        else if ( ShowBeltGear && CoveredRosterAssets.ReloadTrack( ModelPath ) is { } trackPath )
        {
            reloadTrack = subject.Components.Create<ReloadPresentationTrack>();
            reloadTrack.Body = body; reloadTrack.TrackPath = trackPath; reloadTrack.Docked = true;
        }
        if ( Motion != "reload_standing" && MotionPhase >= 0 ) body.PlaybackRate = 0;
        Log.Info( $"PB_PROTECTED model={body.Model.IsValid()} clip={body.Sequence.Name}" );
        Log.Info( $"PB_PROTECTED_MORPHS {string.Join(",", body.Morphs.Names)}" );
    }
    protected override void OnUpdate()
    {
        aimReviewTime += Time.Delta;
        if ( Motion != "reload_standing" && MotionPhase >= 0 ) body.Sequence.TimeNormalized = MotionPhase.Clamp( 0, 1 );
        var targetPitch = ReviewAimTransition ? (aimReviewTime < 1 ? -30f : aimReviewTime < 2 ? 30f : 0f) : AimPitch;
        appliedPitch += (targetPitch.Clamp( -45, 45 ) - appliedPitch) * (1 - System.MathF.Exp( -12 * Time.Delta ));
        if ( reloadTrack is not null && !reloadTrack.Docked )
        {
            body.Sequence.TimeNormalized = ReloadPhase;
            reloadTrack.Phase = ReloadPhase;
            reloadTrack.ApplyFrame();
        }
        else foreach ( var name in body.Morphs.Names )
            if ( name == "LeftGrip" || name == "RightGrip" || name == "LeftThumb" || name == "RightThumb" )
                body.Morphs.Set( name, name.Contains("Thumb") ? ThumbAmount : GripAmount, 0 );
        if(body.Model.Morphs.GetIndex("WristVolume")>=0)
            body.Morphs.Set("WristVolume",Motion=="run" ? 1 : 0,0);
        if ( body.TryGetBoneTransform( "RightHand", out var hand ) )
        {
            marker.WorldPosition = hand.Position + subject.WorldRotation * MarkerOffset;
            marker.WorldRotation = subject.WorldRotation;
        }
        var camera = Components.Get<CameraComponent>();
        camera.WorldPosition = new Vector3( 0, 0, 445 ) + Rotation.FromYaw( ViewYaw ) * new Vector3( 145, -95, 13 );
        camera.WorldRotation = Rotation.LookAt( new Vector3(0,0,445) - camera.WorldPosition );
        camera.FieldOfView = 40;
        if ( HeadCloseUp )
        {
            var target = body.TryGetBoneTransform( "Head", out var head )
                ? head.Position + Vector3.Up * 5
                : subject.WorldPosition + Vector3.Up * 62;
            camera.WorldPosition = target + new Vector3( 25, -15, 3 );
            camera.WorldRotation = Rotation.LookAt( target - camera.WorldPosition );
            camera.FieldOfView = 32;
        }
        if ( HandCloseUp && body.TryGetBoneTransform( "RightHand", out var focus ) )
        {
            var target = focus.Position + CloseUpFocusOffset;
            camera.WorldPosition = target + Rotation.FromYaw( ViewYaw ) * CloseUpCameraOffset;
            camera.WorldRotation = Rotation.LookAt( target - camera.WorldPosition );
            camera.FieldOfView = 32;
        }
    }
    protected override void OnDestroy() { subject?.Destroy(); marker?.Destroy(); }
    protected override void OnPreRender()
    {
        if ( !body.IsValid() ) return;
        if(LogPoseReview && !poseLogged && aimReviewTime>.3f)
        {
            foreach(var name in new[]{"Hips","Spine","RightArm","RightForeArm","LeftArm","LeftForeArm","RightHand","LeftHand","LeftFoot","RightFoot"})
                if(body.TryGetBoneTransform(name,out var bone))
                    Log.Info($"STRAFE_POSE {name} local={body.WorldTransform.PointToLocal(bone.Position)} phase={body.Sequence.TimeNormalized} time={body.Sequence.Time}");
            poseLogged=true;
        }
        CoveredSuitMotion.Apply( body );
        if ( CoveredAimPose.Apply( body, appliedPitch, MarkerOffset, out var pose ) )
        {
            marker.WorldPosition = pose.Position;
            marker.WorldRotation = pose.Rotation;
        }
    }
}
