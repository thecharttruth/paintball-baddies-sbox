using Sandbox;
using System;
namespace PaintballBaddies;

/// <summary>Opt-in native ballistic and real player/AI impact checks.</summary>
public sealed class LongRangePaintReview : Component
{
    public List<string> Results {get;}=new();
    public bool Done {get;private set;}
    public int Stage {get;private set;}
    PlayerController player;PaintballMarker marker;GameObject wall,ball;
    Vector3 oldPosition,target;Angles oldAngles;bool oldLook,oldInput;
    float elapsed;int index;bool ai;IDisposable hook;
    readonly float[] distances={2401,2800,3200};
    void Check(bool ok,string label)=>Results.Add((ok?"PASS ":"FAIL ")+label);
    protected override void OnStart()
    {
        Scene.GetAllComponents<PaintballControls>().First().StartPractice();
        player=MultiplayerSession.LocalPlayer(Scene);marker=player.Components.Get<PaintballMarker>();
        player.Components.Get<CoverController>().Leave();
        oldPosition=player.WorldPosition;oldAngles=player.EyeAngles;oldLook=player.UseLookControls;oldInput=player.UseInputControls;
        player.UseInputControls=player.UseLookControls=false;player.WorldPosition=new(0,0,6002);player.EyeAngles=Angles.Zero;
        player.Body.Velocity=Vector3.Zero;player.WishVelocity=Vector3.Zero;marker.ReviewAimOverride=true;
        TrainingRange.Box(GameObject,"Range review floor",new(1700,0,5998),new(4000,1000,4),Color.Gray,true);
        wall=TrainingRange.Box(GameObject,"Range review wall",new(3202,0,6100),new(4,500,200),Color.Gray,true);
        hook=Scene.AddHook(GameObjectSystem.Stage.StartUpdate,-35,()=>
        {Input.AnalogMove=Vector3.Zero;Input.AnalogLook=Angles.Zero;Input.SetAction("attack1",false);Input.SetAction("attack2",false);},nameof(LongRangePaintReview),"Temporary range checks");
        foreach(float d in new[]{100f,2400,2401,2800,3200})
            foreach(float height in new[]{-35f,0,45})
            {
                var aim=new Vector3(d,0,height);var velocity=PaintballFlight.LaunchVelocity(Vector3.Zero,aim);
                float time=d/velocity.x;
                var end=velocity*time+Vector3.Down*(PaintballFlight.Gravity*.5f*time*time);
                Check((end-aim).Length<.05f&&time<PaintballFlight.Lifetime,$"ballistic endpoint {d} height {height}: error {(end-aim).Length:F4}");
            }
        foreach(float edge in new[]{2400f,PaintballFlight.SightCompensationRange})
        {
            var a=PaintballFlight.LaunchVelocity(Vector3.Zero,new(edge-1,0,0));var b=PaintballFlight.LaunchVelocity(Vector3.Zero,new(edge+1,0,0));
            Check((a-b).Length<1,$"continuous velocity across {edge}");
        }
    }
    protected override void OnUpdate()
    {
        if(Done)return;elapsed+=Time.Delta;
        if(Stage==0&&elapsed>.7f)
        {
            var from=marker.Muzzle;target=from+Vector3.Forward*distances[index];
            wall.WorldPosition=target+Vector3.Forward*2;
            PaintImpactSystem.Find(Scene).Clear();
            if(ai)
            {
                ball=new GameObject(GameObject,true,"Range review AI paintball");ball.WorldPosition=from;
                var shot=ball.Components.Create<OpponentPaintball>();shot.Shooter=player.GameObject;shot.Velocity=PaintballFlight.LaunchVelocity(from,target);
            }
            else Check(marker.FireAt(target),$"player fires at {distances[index]}");
            Stage=1;elapsed=0;
        }
        else if(Stage==1&&elapsed>1.3f)
        {
            var mark=PaintImpactSystem.Find(Scene).OldestDecal;
            float error=mark.IsValid()?(mark.WorldPosition-target).Length:9999;
            Check(error<3,$"{(ai?"AI":"player")} hits wall at {distances[index]} (error {error:F2})");
            if(ball.IsValid())ball.Destroy();
            if(ai){index++;ai=false;}else ai=true;
            if(index>=distances.Length){Done=true;return;}Stage=0;elapsed=0;
        }
    }
    protected override void OnDestroy()
    {
        hook?.Dispose();Input.ReleaseActions();if(ball.IsValid())ball.Destroy();
        if(marker.IsValid())marker.ReviewAimOverride=null;
        PaintImpactSystem.Find(Scene)?.Clear();
        if(player.IsValid()){player.WorldPosition=oldPosition;player.EyeAngles=oldAngles;player.UseInputControls=oldInput;player.UseLookControls=oldLook;player.Body.Velocity=Vector3.Zero;player.WishVelocity=Vector3.Zero;}
    }
}
