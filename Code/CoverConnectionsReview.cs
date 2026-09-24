using Sandbox;
using Sandbox.Navigation;
namespace PaintballBaddies;

/// <summary>Unsaved native attachment and navigation checks for the added cover links.</summary>
public sealed class CoverConnectionsReview : Component
{
    public List<string> Results {get;}=new();
    public bool Done {get;private set;}
    CoverSurface[] links;PlayerController player;CoverController cover;NavMeshAgent agent;
    Vector3 oldPosition;Angles oldAngles;bool oldLook;int index;float elapsed;
    protected override void OnStart()
    {
        Scene.GetAllComponents<PaintballControls>().First().StartPractice();
        Scene.NavMesh.IsEnabled=true;Scene.NavMesh.IncludeStaticBodies=true;Scene.NavMesh.IncludeKeyframedBodies=false;
        Scene.NavMesh.AgentHeight=67;Scene.NavMesh.AgentRadius=17;Scene.NavMesh.SetDirty();
        player=MultiplayerSession.LocalPlayer(Scene);cover=player.Components.Get<CoverController>();
        oldPosition=player.WorldPosition;oldAngles=player.EyeAngles;oldLook=player.UseLookControls;
        player.UseLookControls=false;cover.TestInput=true;
        links=Scene.GetAllComponents<CoverSurface>().Where(x=>x.GameObject.Name.StartsWith("Cover link ")).OrderBy(x=>x.GameObject.Name).ToArray();
        var probe=new GameObject(GameObject){Name="Temporary cover navigation probe",WorldPosition=new(-620,0,2)};
        agent=probe.Components.Create<NavMeshAgent>();agent.Height=67;agent.Radius=17;agent.UpdatePosition=false;agent.UpdateRotation=false;
        Check(links.Length==9,"nine cover links present");
        if(links.Length==0){Done=true;return;}Place();
    }
    void Check(bool ok,string label){Results.Add((ok ? "PASS " : "FAIL ")+label);}
    void Place()
    {
        cover.Leave();var link=links[index];var box=link.Components.Get<BoxCollider>();
        player.WorldPosition=link.WorldPosition-Vector3.Forward*(box.Scale.x*link.WorldScale.x*.5f+45);
        player.WorldPosition=player.WorldPosition.WithZ(2);player.EyeAngles=Angles.Zero;player.Body.Velocity=Vector3.Zero;
        elapsed=0;
    }
    protected override void OnUpdate()
    {
        if(Done)return;elapsed+=Time.Delta;if(elapsed<.8f || Scene.NavMesh.IsGenerating)return;
        var link=links[index];
        Check(cover.TryEnter() && cover.ReservedSurface==link,link.GameObject.Name+" attaches from open approach");
        var start=Scene.NavMesh.GetClosestPoint(new Vector3(-620,0,2),40);
        var finish=Scene.NavMesh.GetClosestPoint(player.WorldPosition,40);
        bool reachable=start is Vector3 from && finish is Vector3 to
            && Scene.NavMesh.CalculatePath(new CalculatePathRequest{Start=from,Target=to,Agent=agent}).Status==NavMeshPathStatus.Complete;
        Check(reachable,link.GameObject.Name+" reachable from main hall on native navmesh");
        cover.Leave();index++;
        if(index==links.Length){Done=true;return;}Place();
    }
    protected override void OnDestroy()
    {
        if(cover.IsValid()){cover.Leave();cover.TestInput=false;}
        if(player.IsValid()){player.WorldPosition=oldPosition;player.EyeAngles=oldAngles;player.UseLookControls=oldLook;player.Body.Velocity=Vector3.Zero;player.WishVelocity=Vector3.Zero;}
    }
}
