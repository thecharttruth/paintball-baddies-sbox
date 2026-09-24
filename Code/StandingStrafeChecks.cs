using Sandbox;
using System;
namespace PaintballBaddies;
/// <summary>Opt-in actual controller movement and directional animation review.</summary>
public sealed class StandingStrafeChecks : Component
{
    [Property] public int CharacterIndex { get; set; }
    private PlayerController player;
    private PaintballMarker weapon;
    private float elapsed;
    private int stage;
    private Vector3 start;
    private float maxTime;
    private bool originalInput,originalLook,originalAccept,selected;
    protected override void OnStart()
    {
        player=Components.Get<PlayerController>();weapon=Components.Get<PaintballMarker>();
        originalInput=player.UseInputControls;originalLook=player.UseLookControls;originalAccept=weapon.AcceptInput;
        player.UseInputControls=player.UseLookControls=false;weapon.AcceptInput=false;weapon.ReviewAimOverride=true;
        WorldPosition=new Vector3(-950,0,2);player.EyeAngles=new Angles(0,0,0);start=WorldPosition;
    }
    protected override void OnUpdate()
    {
        if(!selected){Components.Get<RosterSelection>().Select(CharacterIndex);selected=true;}
        elapsed+=Time.Delta;
        player.WishVelocity=stage==0 ? Vector3.Left*55 : stage==1 ? Vector3.Right*55 : Vector3.Zero;
        maxTime=MathF.Max(maxTime,player.Renderer.Sequence.Time);
        if(elapsed<2.5f)return;
        if(stage<2)
        {
            var expected=stage==0 ? "walk_left" : "walk_right";
            var direction=stage==0 ? Vector3.Left : Vector3.Right;
            var distance=Vector3.Dot(WorldPosition-start,direction);
            Check(expected+" moves",distance>70,$"distance={distance}");
            Check(expected+" plays",player.Renderer.Sequence.Name==expected && maxTime>.25f,$"clip={player.Renderer.Sequence.Name},time={maxTime}");
            Check(expected+" holds facing",Vector3.Dot(player.Renderer.WorldRotation.Forward,Vector3.Forward)>.98f,$"yaw={player.Renderer.WorldRotation.Yaw()}");
        }
        else
        {
            Check("selected character",player.Renderer.Model?.Name==RosterSelection.GetModelPath(CharacterIndex),player.Renderer.Model?.Name);
            Check("returns to idle",player.Renderer.Sequence.Name=="idle",player.Renderer.Sequence.Name);
            Log.Info("STANDING_STRAFE COMPLETE");Destroy();return;
        }
        stage++;elapsed=0;maxTime=0;start=WorldPosition;
    }
    private void Check(string name,bool pass,string detail)=>Log.Info($"STANDING_STRAFE {(pass ? "PASS" : "FAIL")} {name}: {detail}");
    protected override void OnDestroy()
    {
        if(player.IsValid()){player.WishVelocity=Vector3.Zero;player.UseInputControls=originalInput;player.UseLookControls=originalLook;}
        if(weapon.IsValid()){weapon.ReviewAimOverride=null;weapon.AcceptInput=originalAccept;}
    }
}
