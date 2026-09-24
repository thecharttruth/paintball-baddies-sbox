using Sandbox;
namespace PaintballBaddies;

/// <summary>Temporary placed-tank route check using the production agent dimensions.</summary>
public sealed class TankNavigationChecks : Component
{
    private GameObject actor;
    private NavMeshAgent agent;
    private Vector3 previous, destination;
    private float elapsed, travelled, lateral;
    private int crossings;
    private bool moving, finished;
    protected override void OnStart()
    {
        Scene.NavMesh.IsEnabled=true;
        Scene.NavMesh.IncludeStaticBodies=true;
        Scene.NavMesh.IncludeKeyframedBodies=false;
        Scene.NavMesh.AgentHeight=67;
        Scene.NavMesh.AgentRadius=17;
        Scene.NavMesh.SetDirty();
        actor=new GameObject(true,"Tank route inspection agent");
        actor.WorldPosition=new Vector3(880,-400,1);
        previous=actor.WorldPosition;
        destination=new Vector3(560,-400,1);
        agent=actor.Components.Create<NavMeshAgent>();
        agent.Height=67;agent.Radius=17;agent.MaxSpeed=140;agent.Acceleration=600;
        agent.UpdatePosition=true;agent.UpdateRotation=true;
    }
    protected override void OnUpdate()
    {
        if(finished)return;
        elapsed+=Time.Delta;
        if(!moving && elapsed>1 && !Scene.NavMesh.IsGenerating)
        {
            agent.MoveTo(destination);moving=true;
        }
        var pos=actor.WorldPosition;
        if((pos-previous).Length>.01f)
        {
            var hit=Scene.Trace.Sphere(12,previous+Vector3.Up*30,pos+Vector3.Up*30)
                .IgnoreGameObjectHierarchy(actor).Run();
            if(hit.Hit)crossings++;
        }
        travelled+=(pos-previous).Length;
        lateral=System.MathF.Max(lateral,System.MathF.Abs(pos.y+400));
        previous=pos;
        if((moving && (pos-destination).WithZ(0).Length<8) || elapsed>18)
        {
            finished=true;
            var reached=(pos-destination).WithZ(0).Length<8;
            Log.Info($"TANK_ROUTE {(reached && crossings==0 && lateral>75 ? "PASS":"FAIL")} reached={reached} crossings={crossings} lateral={lateral} travelled={travelled} position={pos}");
        }
    }
    protected override void OnDestroy(){if(actor.IsValid())actor.Destroy();}
}
