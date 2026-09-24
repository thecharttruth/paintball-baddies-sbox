using Sandbox;
namespace PaintballBaddies;
public sealed class SnakeNavigationReview : Component
{
    private GameObject actor;
    private NavMeshAgent agent;
    private float elapsed,travelled,maxDetour;
    private Vector3 previous;
    private int crossings;
    private bool started,reported;
    protected override void OnStart()
    {
        Scene.NavMesh.IsEnabled=true;
        Scene.NavMesh.IncludeStaticBodies=true;
        Scene.NavMesh.IncludeKeyframedBodies=false;
        Scene.NavMesh.AgentHeight=67;Scene.NavMesh.AgentRadius=17;
        Scene.NavMesh.SetDirty();
    }
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;
        if(reported || Scene.NavMesh.IsGenerating || elapsed<1)return;
        if(!started)
        {
            actor=new GameObject {Name="Snake navigation probe"};
            actor.WorldPosition=new Vector3(-450,1400,0);previous=actor.WorldPosition;
            agent=actor.Components.Create<NavMeshAgent>();
            agent.Height=67;agent.Radius=17;agent.MaxSpeed=140;agent.Acceleration=600;
            agent.MoveTo(new Vector3(-250,1400,0));started=true;return;
        }
        var position=actor.WorldPosition;
        travelled+=(position-previous).Length;
        maxDetour=System.MathF.Max(maxDetour,System.MathF.Abs(position.y-1400));
        var hit=Scene.Trace.Sphere(12,previous+Vector3.Up*20,position+Vector3.Up*20).IgnoreGameObjectHierarchy(actor).Run();
        if(hit.Hit && hit.GameObject?.Name=="Inflatable snake bunker candidate")crossings++;
        previous=position;
        if(elapsed<8)return;
        var remaining=(position-new Vector3(-250,1400,0)).WithZ(0).Length;
        int shielded=0;
        foreach(float y in new[]{-30f,0f,30f})
        foreach(float z in new[]{35f,40f})
        {
            var ray=Scene.Trace.Ray(new Vector3(-150,1400+y,60),new Vector3(-385,1400+y,z)).IgnoreGameObjectHierarchy(actor).Run();
            if(ray.Hit && ray.GameObject?.Name=="Inflatable snake bunker candidate")shielded++;
        }
        Log.Info("SNAKE_NAV "+Json.Serialize(new {passed=remaining<15 && crossings==0 && maxDetour>65 && shielded==6,shielded,remaining,crossings,maxDetour,travelled}));
        reported=true;
    }
    protected override void OnDestroy(){if(actor.IsValid())actor.Destroy();}
}
