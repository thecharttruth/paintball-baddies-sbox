using Sandbox;
namespace PaintballBaddies;
/// <summary>Opt-in route-leg navigation using production agent dimensions and route points.</summary>
public sealed class PatrolRouteChecks : Component
{
    [Property] public bool ServiceYard { get; set; }
    private Vector3[] route;
    private NavMeshAgent[] agents;
    private Vector3[] previous;
    private bool[] reached;
    private int[] crossings;
    private float elapsed;
    protected override void OnStart()
    {
        Scene.NavMesh.IsEnabled=true;Scene.NavMesh.IncludeStaticBodies=true;Scene.NavMesh.IncludeKeyframedBodies=false;
        Scene.NavMesh.AgentHeight=67;Scene.NavMesh.AgentRadius=17;Scene.NavMesh.SetDirty();
        route=ServiceYard ? new[]{new Vector3(-650,650,0),new Vector3(-650,1100,0),new Vector3(-650,1400,0),new Vector3(650,1400,0),new Vector3(650,1100,0),new Vector3(650,650,0)} : ArenaOpponent.CreatePatrolRoute();
        int count=route.Length;
        agents=new NavMeshAgent[count];previous=new Vector3[count];reached=new bool[count];crossings=new int[count];
        for(int i=0;i<count;i++)
        {
            var go=new GameObject(true,$"Patrol route leg {i}");go.WorldPosition=route[i]+Vector3.Up;
            agents[i]=go.Components.Create<NavMeshAgent>();var a=agents[i];a.Height=67;a.Radius=17;a.MaxSpeed=140;a.Acceleration=600;a.UpdatePosition=true;
            previous[i]=go.WorldPosition;
        }
    }
    protected override void OnUpdate()
    {
        if(Scene.NavMesh.IsGenerating)return;
        elapsed+=Time.Delta;
        for(int i=0;i<agents.Length;i++)
        {
            var a=agents[i];var destination=route[(i+1)%agents.Length];var now=a.WorldPosition;
            if((now-previous[i]).Length>.1f && Scene.Trace.Sphere(12,previous[i]+Vector3.Up*30,now+Vector3.Up*30).IgnoreGameObjectHierarchy(a.GameObject).Run().Hit)crossings[i]++;
            previous[i]=now;
            if((now-destination).WithZ(0).Length<45){reached[i]=true;a.Stop();}
            if(!reached[i])a.MoveTo(destination);
        }
        if(elapsed<23)return;
        for(int i=0;i<agents.Length;i++)Log.Info($"PATROL_ROUTE {(reached[i] && crossings[i]==0?"PASS":"FAIL")} leg={i} reached={reached[i]} crossings={crossings[i]} position={agents[i].WorldPosition} destination={route[(i+1)%route.Length]}");
        Destroy();
    }
    protected override void OnDestroy(){if(agents is not null)foreach(var a in agents)if(a.IsValid())a.GameObject.Destroy();}
}
