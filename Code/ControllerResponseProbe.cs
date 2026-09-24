using Sandbox;
namespace PaintballBaddies;
/// <summary>Opt-in native controller response measurement, without changing saved tuning.</summary>
public sealed class ControllerResponseProbe : Component
{
    [Property] public bool Quicker { get; set; }
    private PlayerController player;
    private Vector3 origin;
    private bool input;
    private float acceleration,deceleration,elapsed,phaseTime;
    private int phase;
    private float response=-1;
    protected override void OnStart()
    {
        player=Scene.GetAllComponents<PlayerController>().First();
        origin=player.WorldPosition;input=player.UseInputControls;
        acceleration=player.AccelerationTime;deceleration=player.DeaccelerationTime;
        if(Quicker){player.AccelerationTime=.10f;player.DeaccelerationTime=.08f;}
        player.UseInputControls=false;player.WorldPosition=new Vector3(-950,1200,2);player.Body.Velocity=Vector3.Zero;
    }
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;phaseTime+=Time.Delta;
        float speed=phase is 1 or 3 ? 177 : phase==4 ? -177 : 0;
        player.WishVelocity=Vector3.Forward*speed;
        var actual=player.Velocity.x;
        bool reached=speed==0 ? System.MathF.Abs(actual)<2 : actual*System.MathF.Sign(speed)>=159.3f;
        if(phase>0 && response<0 && reached)response=phaseTime;
        var duration=phase==0 ? .4f : speed==0 ? .6f : 1f;
        if(phaseTime<duration)return;
        if(phase>0)Log.Info($"CONTROLLER_RESPONSE quicker={Quicker} phase={phase} target={speed} response_s={response} final_speed={actual}");
        phase++;phaseTime=0;response=-1;
        if(phase>5){Log.Info("CONTROLLER_RESPONSE COMPLETE");Destroy();}
    }
    protected override void OnDestroy()
    {
        if(!player.IsValid())return;
        player.AccelerationTime=acceleration;player.DeaccelerationTime=deceleration;
        player.UseInputControls=input;player.WorldPosition=origin;player.Body.Velocity=Vector3.Zero;player.WishVelocity=Vector3.Zero;
    }
}
