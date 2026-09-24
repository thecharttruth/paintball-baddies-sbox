using Sandbox;
namespace PaintballBaddies;
public sealed class AiRangeReview : Component
{
    float time;int phase;Vector3 target=new(1168,-700,120);
    protected override void OnUpdate()
    {
        time+=Time.Delta;if(time<1.5f)return;time=0;
        var system=PaintImpactSystem.Find(Scene);
        if(phase==0||phase==2)
        {
            system.Clear();var from=new Vector3(phase==0 ? -650 : -1050,-700,60);
            var go=new GameObject(true,"AI range review paintball");go.WorldPosition=from;
            var ball=go.Components.Create<OpponentPaintball>();ball.Shooter=Scene.GetAllComponents<PlayerController>().First().GameObject;ball.Velocity=PaintballFlight.LaunchVelocity(from,target);
        }
        else if(phase==1||phase==3)
        {
            var mark=system.OldestDecal;var error=mark.IsValid() ? (mark.WorldPosition-target).Length : 9999;
            Log.Info($"AI_RANGE {(error<10 ? "PASS" : "FAIL")} {(phase==1 ? "46m" : "56m")} errorInches={error} receiver={mark?.GameObject.Parent?.Name}");
        }
        phase++;if(phase>3)Destroy();
    }
}
