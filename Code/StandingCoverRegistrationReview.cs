using Sandbox;
namespace PaintballBaddies;
/// <summary>Temporary attachment/posture check for newly registered solid props.</summary>
public sealed class StandingCoverRegistrationReview : Component
{
    public bool Done {get;private set;}
    public List<string> Results {get;}=new();
    CoverSurface[] surfaces;PlayerController player;CoverController cover;Vector3 previous;Angles angles;bool look;
    float elapsed;int index,stage;
    protected override void OnStart()
    {
        Scene.GetAllComponents<PaintballControls>().First().StartPractice();
        player=MultiplayerSession.LocalPlayer(Scene);cover=player.Components.Get<CoverController>();previous=player.WorldPosition;angles=player.EyeAngles;look=player.UseLookControls;
        player.UseLookControls=false;cover.TestInput=true;
        surfaces=Scene.GetAllComponents<CoverSurface>().Where(x=>x.StandingOnly).ToArray();
        if(surfaces.Length!=4)Results.Add("FAIL expected four newly registered standing props");Place();
    }
    void Place()
    {
        cover.Leave();cover.TestAim=false;
        if(index>=surfaces.Length){Done=true;return;}
        var s=surfaces[index];var b=s.Components.Get<BoxCollider>();var centre=s.WorldTransform.PointToWorld(b.Center);
        foreach(var d in new[]{Vector3.Forward,Vector3.Backward,Vector3.Left,Vector3.Right})
        {
            var direction=s.WorldRotation*d;float extent=(System.MathF.Abs(d.x)*b.Scale.x*s.WorldScale.x+System.MathF.Abs(d.y)*b.Scale.y*s.WorldScale.y)*.5f;
            var p=(centre+direction*(extent+42)).WithZ(s.WorldPosition.z+2);
            if(Scene.Trace.Sphere(16,p+Vector3.Up*18,p+Vector3.Up*50).IgnoreGameObjectHierarchy(player.GameObject).Run().Hit)continue;
            player.WorldPosition=p;player.EyeAngles=Rotation.LookAt(-direction).Angles();player.Body.Velocity=Vector3.Zero;player.WishVelocity=Vector3.Zero;
            elapsed=0;stage=0;return;
        }
        Results.Add("FAIL "+s.GameObject.Name+" no open approach");index++;Place();
    }
    protected override void OnUpdate()
    {
        if(Done)return;elapsed+=Time.Delta;if(elapsed<.6f)return;elapsed=0;
        var s=surfaces[index];
        if(stage++==0){Results.Add((cover.TryEnter()&&cover.ReservedSurface==s?"PASS ":"FAIL ")+s.GameObject.Name+" attaches");return;}
        Results.Add((cover.Attached&&!cover.LowCover&&!player.IsDucking?"PASS ":"FAIL ")+s.GameObject.Name+" uses standing cover");
        cover.Leave();Results.Add((player.UseInputControls?"PASS ":"FAIL ")+s.GameObject.Name+" restores movement");index++;Place();
    }
    protected override void OnDestroy()
    {
        if(cover.IsValid()){cover.Leave();cover.TestInput=false;}
        if(player.IsValid()){player.WorldPosition=previous;player.EyeAngles=angles;player.UseLookControls=look;player.Body.Velocity=Vector3.Zero;}
    }
}
