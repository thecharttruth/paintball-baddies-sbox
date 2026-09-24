using Sandbox;
namespace PaintballBaddies;

/// <summary>Opt-in unobstructed flight check for both production projectile paths.</summary>
public sealed class PaintballFlightReview : Component
{
    private PlayerController player;
    private PaintballMarker weapon;
    private GameObject opponentBall;
    private Vector3 origin;
    private float elapsed, shotTime;
    private int stage;
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;
        if(stage==0 && elapsed>1)
        {
            player=Scene.GetAllComponents<PlayerController>().First();
            weapon=player.Components.Get<PaintballMarker>();
            player.UseInputControls=false;weapon.AcceptInput=false;
            player.WorldPosition=new Vector3(0,0,5000);stage=1;
        }
        if(stage>0)player.WorldPosition=new Vector3(0,0,5000);
        if(stage==1 && elapsed>2)
        {
            origin=weapon.Muzzle;
            var fired=weapon.FireAt(origin+Vector3.Up*10000);
            opponentBall=new GameObject(true,"Flight review AI ball");
            opponentBall.WorldPosition=origin+Vector3.Right*100;
            var ball=opponentBall.Components.Create<OpponentPaintball>();
            ball.Shooter=player.GameObject;ball.Velocity=Vector3.Up*PaintballFlight.Speed;
            shotTime=elapsed;stage=2;
            Log.Info($"FLIGHT_REVIEW fired={fired}");
        }
        if(stage==2 && elapsed-shotTime>2)
        {
            var ball=Scene.GetAllComponents<TrailRenderer>().FirstOrDefault(x=>x.GameObject.Parent?.Name=="Paintball");
            Log.Info("FLIGHT_REVIEW "+Json.Serialize(new{age=elapsed-shotTime,
                player_alive=weapon.ActivePaintballs==1,player_distance=ball.IsValid() ? (ball.WorldPosition-origin).Length : 0,
                ai_alive=opponentBall.IsValid(),ai_distance=opponentBall.IsValid() ? (opponentBall.WorldPosition-origin).Length : 0,
                player_trail=ball.IsValid(),ai_trail=opponentBall.IsValid() && opponentBall.Components.Get<TrailRenderer>(FindMode.EverythingInSelfAndDescendants).IsValid()}));
            stage=3;
        }
        if(stage==3 && elapsed-shotTime>3.2f)
        {
            Log.Info("FLIGHT_REVIEW "+Json.Serialize(new{expired_player=weapon.ActivePaintballs==0,expired_ai=!opponentBall.IsValid()}));stage=4;
        }
    }
}
