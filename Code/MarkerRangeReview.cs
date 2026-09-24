using Sandbox;
namespace PaintballBaddies;

/// <summary>Opt-in production wall-impact and sustained-fire review.</summary>
public sealed class MarkerRangeReview : Component
{
    private PlayerController player;
    private PaintballMarker weapon;
    private float elapsed,next,lastShot=-1,minInterval=10;
    private int stage,shots;
    private Vector3 point=new(1168,-700,120);
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;
        if(stage==0 && elapsed>1)
        {
            player=Scene.GetAllComponents<PlayerController>().First();
            weapon=player.Components.Get<PaintballMarker>();
            player.UseInputControls=false;player.UseLookControls=false;weapon.AcceptInput=false;
            player.WorldPosition=new Vector3(-650,-700,2);player.EyeAngles=new Angles(0,0,0);
            stage=1;next=elapsed+.5f;
        }
        if(stage==0)return;
        player.WishVelocity=Vector3.Zero;
        if(elapsed<next)return;
        if(stage==1 || stage==3)
        {
            PaintImpactSystem.Find(Scene)?.Clear();
            Log.Info($"MARKER_RANGE shot={weapon.FireAt(point)} distance_m={(point-weapon.Muzzle).Length*.0254f}");
            stage++;next=elapsed+1;
        }
        else if(stage==2 || stage==4)
        {
            var marks=Scene.GetAllComponents<Decal>().Where(x=>x.GameObject.Name=="Paint splash decal").ToArray();
            float error=marks.Length>0 ? marks.Min(x=>(x.WorldPosition-point).Length) : 9999;
            Log.Info($"MARKER_RANGE wall_stage={stage} pass={error<3} error_inches={error}");
            if(stage==2){player.WorldPosition=new Vector3(-1050,-700,2);stage=3;next=elapsed+.5f;}
            else{stage=5;next=elapsed;lastShot=-1;}
        }
        else if(stage==5)
        {
            if(weapon.FireAt(point))
            {
                if(lastShot>=0)minInterval=System.MathF.Min(minInterval,elapsed-lastShot);
                lastShot=elapsed;shots++;
            }
            if(shots>=11)
            {
                Log.Info($"MARKER_RANGE cadence_pass={minInterval>=PaintballFlight.ShotInterval-.002f} shots={shots} min_interval={minInterval}");
                stage=6;
            }
        }
    }
    protected override void OnDestroy()
    {
        if(player.IsValid()){player.UseInputControls=true;player.UseLookControls=true;}
        if(weapon.IsValid())weapon.AcceptInput=true;
    }
}
