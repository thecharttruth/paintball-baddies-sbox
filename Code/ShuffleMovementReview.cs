using Sandbox;
using System;
namespace PaintballBaddies;

/// <summary>Temporary camera fixture for candidate crouch movement and transitions.</summary>
public sealed class ShuffleMovementReview : Component
{
    [Property] public bool MoveLeft { get; set; }
    [Property] public bool FinishForward { get; set; }
    [Property] public bool UseMei { get; set; }
    [Property] public bool StandAtEnd { get; set; }
    [Property] public bool UseImani { get; set; }
    private PlayerController player;
    private PaintballMarker weapon;
    private float elapsed;
    private bool selected;
    private Vector3 start;
    private float maximumRateError;
    private int samples;
    protected override void OnStart()
    {
        player=Scene.GetAllComponents<PlayerController>().First();
        weapon=player.Components.Get<PaintballMarker>();
        player.UseInputControls=player.UseLookControls=player.UseCameraControls=false;
        weapon.AcceptInput=false;weapon.ReviewAimOverride=true;
    }
    protected override void OnUpdate()
    {
        if(!selected)
        {
            player.Components.Get<RosterSelection>().Select(UseMei ? 3 : UseImani ? 2 : 4);
            player.Renderer.Model=Model.Load(UseMei ? "models/characters/mei_planted_review/mei_protected.vmdl" : UseImani ? "models/characters/imani_shuffle_pilot/imani_protected.vmdl" : "models/characters/freya_shuffle_pilot/freya_protected.vmdl");
            player.WorldPosition=new Vector3(-950,0,2);
            player.EyeAngles=new Angles(0,0,0);start=player.WorldPosition;selected=true;
        }
        elapsed+=Time.Delta;
        player.UpdateDucking(!(StandAtEnd && elapsed>=6));
        var direction=MoveLeft ? Vector3.Left : Vector3.Right;
        player.WishVelocity=elapsed>1 && elapsed<4 ? direction*55
            : FinishForward && elapsed>=4 && elapsed<6 ? Vector3.Forward*55 : Vector3.Zero;
        WorldPosition=player.WorldPosition+new Vector3(150,-120,75);
        WorldRotation=Rotation.LookAt(player.WorldPosition+Vector3.Up*32-WorldPosition);
        if(elapsed>2 && elapsed<3.8f)
        {
            maximumRateError=MathF.Max(maximumRateError,MathF.Abs(player.Renderer.PlaybackRate-player.Velocity.WithZ(0).Length/25.59055f));
            samples++;
        }
        if(elapsed<8f)return;
        Log.Info($"SHUFFLE_REVIEW left={MoveLeft} forward={FinishForward} model={player.Renderer.Model.Name} travel={Vector3.Dot(player.WorldPosition-start,direction)} rate_error={maximumRateError} samples={samples} final_clip={player.Renderer.Sequence.Name}");
        Destroy();
    }
    protected override void OnDestroy()
    {
        if(!player.IsValid())return;
        player.WishVelocity=Vector3.Zero;player.UpdateDucking(false);
        player.UseInputControls=player.UseLookControls=player.UseCameraControls=true;
        weapon.AcceptInput=true;weapon.ReviewAimOverride=null;
        player.Components.Get<RosterSelection>().Select(UseMei ? 3 : UseImani ? 2 : 4);
    }
}
