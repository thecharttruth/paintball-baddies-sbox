using Sandbox;
namespace PaintballBaddies;

/// <summary>Temporary native material/scale review; never saved in playable scenes.</summary>
public sealed class StaticPropReview : Component
{
    [Property] public string ModelPath { get; set; } = "models/foundry/upright_spool/upright_spool.vmdl";
    [Property] public bool SmallProp {get;set;}
    [Property] public bool VisualOnly { get; set; }
    [Property] public bool TallProp { get; set; }
    [Property] public bool CheckNet { get; set; }
    [Property] public bool CheckTankCollision { get; set; }
    [Property] public bool CheckTankVault { get; set; }
    [Property] public string CandidateCharacterModel { get; set; }
    [Property] public bool CheckPlanner { get; set; }
    [Property] public int CharacterIndex { get; set; }
    private int headSamples, shelteredHeadSamples;
    private float maxHeadHeight;
    [Property] public bool UsePlaced { get; set; }
    [Property] public string ReviewObjectName { get; set; } = "Cable spool - west approach";
    [Property] public bool CheckCover { get; set; }
    private PlayerController player;
    private CoverController cover;
    private int coverStage;
    private Vector3 slideStart;
    private float maxLift;
    private GameObject subject;
    private float elapsed;
    private bool checkedCollision;
    protected override void OnStart()
    {
        player=Scene.GetAllComponents<PlayerController>().First();
        player.UseCameraControls=false;player.UseInputControls=false;
        ModelRenderer model;
        if(UsePlaced)
        {
            model=Scene.GetAllComponents<ModelRenderer>().First(x=>x.GameObject.Name==ReviewObjectName);subject=model.GameObject;
        }
        else
        {
            subject=new GameObject(true,"Static prop review");subject.WorldPosition=new Vector3(-850,-400,0);
            model=subject.Components.Create<ModelRenderer>();model.Model=Model.Load(ModelPath);
            var collider=subject.Components.Create<ModelCollider>();collider.Model=model.Model;
            var surface=subject.Components.Create<CoverSurface>();surface.Curved=!CheckTankCollision;surface.ModelHeight=CheckTankCollision ? 52.36f : 48.47f;
        }
        WorldPosition=subject.WorldPosition+(CheckTankVault ? new Vector3(170,-220,160) : new Vector3(95,-125,90));
        WorldRotation=Rotation.LookAt(subject.WorldPosition+Vector3.Up*(CheckTankVault ? 60 : 21)-WorldPosition);
        if(SmallProp){WorldPosition=subject.WorldPosition+new Vector3(35,-40,30);WorldRotation=Rotation.LookAt(subject.WorldPosition+Vector3.Up*8-WorldPosition);}
        if(TallProp)
        {
            WorldPosition=subject.WorldPosition+new Vector3(150,-135,85);
            WorldRotation=Rotation.LookAt(subject.WorldPosition+Vector3.Up*50-WorldPosition);
        }
        if(CheckCover)
        {
            cover=player.Components.Get<CoverController>();cover.TestInput=true;
            player.UseLookControls=false;
            player.WorldPosition=subject.WorldPosition+new Vector3(-55,CheckTankCollision ? 25 : 0,2);
            player.EyeAngles=new Angles(0,0,0);player.Body.Velocity=Vector3.Zero;
        }
        Log.Info($"PROP_REVIEW loaded={model.Model.IsValid()} model={model.Model?.Name} bounds={model.Model?.Bounds}");
    }
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;
        if(CheckNet && !checkedCollision && elapsed>.3f)
        {
            checkedCollision=true;
            foreach(var height in new[]{5f,50f,100f})
            {
                var from=subject.WorldPosition+new Vector3(40,0,height);
                var hit=Scene.Trace.Ray(from,from-Vector3.Forward*80).Run();
                Log.Info($"NET_CHECK {(hit.Hit && hit.GameObject==subject ? "PASS" : "FAIL")} barrier at height={height}");
            }
            foreach(var netSide in new[]{-85f,85f})
            {
                var from=subject.WorldPosition+new Vector3(40,netSide,35);
                var hit=Scene.Trace.Ray(from,from-Vector3.Forward*80).Run();
                Log.Info($"NET_CHECK {(!hit.Hit ? "PASS" : "FAIL")} side access y={netSide}");
            }
            var noAttach=subject.Components.Get<CoverSurface>() is null;
            Log.Info($"NET_CHECK {(noAttach ? "PASS" : "FAIL")} no cover attachment on net");
        }
        if(CheckCover)
        {
            if(coverStage==0 && elapsed>.7f)
            {
                player.Components.Get<RosterSelection>().Select(CharacterIndex);
                if(!string.IsNullOrEmpty(CandidateCharacterModel))player.Renderer.Model=Model.Load(CandidateCharacterModel);
                var entered=cover.TryEnter();slideStart=player.WorldPosition;coverStage=1;
                Log.Info($"SPOOL_COVER {(entered && cover.LowCover ? "PASS" : "FAIL")} low cover attachment");
            }
            if(coverStage==1)
            {
                if(elapsed>1.2f && player.Renderer.TryGetBoneTransform("Head",out var head))
                {
                    headSamples++;maxHeadHeight=System.MathF.Max(maxHeadHeight,head.Position.z-subject.WorldPosition.z);
                    var outward=(player.WorldPosition-subject.WorldPosition).WithZ(0).Normal;
                    var from=(subject.WorldPosition-outward*100).WithZ(head.Position.z);
                    var hit=Scene.Trace.Ray(from,head.Position).IgnoreGameObjectHierarchy(player.GameObject).Run();
                    if(hit.Hit && hit.GameObject==subject)shelteredHeadSamples++;
                }
                maxLift=System.MathF.Max(maxLift,player.WorldPosition.z-subject.WorldPosition.z);
                cover.TestMovement=Vector3.Cross(Vector3.Up,cover.Normal).Normal;
                if(elapsed>3.2f)
                {
                    var moved=(player.WorldPosition-slideStart).WithZ(0).Length;
                    Log.Info($"SPOOL_COVER {(cover.Attached && moved>45 && maxLift<8 ? "PASS" : "FAIL")} cover slide distance={moved} max_lift={maxLift}");
                    Log.Info($"SPOOL_COVER {(headSamples>30 && shelteredHeadSamples>=headSamples*.95f ? "PASS" : "FAIL")} head shelter samples={shelteredHeadSamples}/{headSamples} max_head={maxHeadHeight} character={CharacterIndex}");
                    cover.TestMovement=Vector3.Zero;cover.TestAim=true;coverStage=2;
                }
            }
            if(coverStage==2 && elapsed>4.2f)
            {
                Log.Info($"SPOOL_COVER {(cover.Attached && cover.Peeking ? "PASS" : "FAIL")} low cover peek");
                if(CheckTankVault)
                {
                    cover.TestAim=false;
                    var vault=player.Components.Get<VaultController>();
                    Log.Info($"TANK_VAULT {(vault.TryBegin() ? "PASS" : "FAIL")} begins from tank side cover");
                }
                else cover.Leave();
                cover.TestInput=false;coverStage=3;
            }
            if(CheckTankVault && coverStage==3 && elapsed>5.4f)
            {
                var vault=player.Components.Get<VaultController>();
                bool landed=vault.Outcome=="Landed" && !vault.IsVaulting && player.WorldPosition.x>subject.WorldPosition.x+20 && player.Body.MotionEnabled;
                Log.Info($"TANK_VAULT {(landed ? "PASS" : "FAIL")} far-side landing outcome={vault.Outcome} relative={player.WorldPosition-subject.WorldPosition} model={player.Renderer.Model?.Name}");
                coverStage=4;
            }
        }
        if(CheckTankCollision && CheckPlanner && !checkedCollision && elapsed>.3f)
        {
            checkedCollision=true;
            var actor=new GameObject(true,"Tank planner probe");
            for(int i=0;i<8;i++)
            {
                var away=Rotation.FromYaw(i*45).Forward;
                var threat=subject.WorldPosition-away*200;
                actor.WorldPosition=subject.WorldPosition+away*120+Vector3.Up;
                bool found=OpponentCoverPlanner.TryFind(actor,player.GameObject,threat,out var destination);
                var ray=Scene.Trace.Ray(threat+Vector3.Up*55,destination+Vector3.Up*30).IgnoreGameObjectHierarchy(actor).IgnoreGameObjectHierarchy(player.GameObject).Run();
                bool tankShelters=found && ray.Hit && ray.GameObject==subject;
                Log.Info($"TANK_PLANNER {(tankShelters ? "PASS" : "FAIL")} approach={i*45} found={found} tankShelter={tankShelters} destination={destination}");
            }
            actor.Destroy();
        }
        if(CheckTankCollision && !checkedCollision && elapsed>.3f)
        {
            checkedCollision=true;
            foreach(var test in new[]{(0f,30f,true),(0f,5f,false),(-29.53f,5f,true),(29.53f,5f,true)})
            {
                var from=subject.WorldPosition+new Vector3(-60,test.Item1,test.Item2);
                var to=subject.WorldPosition+new Vector3(60,test.Item1,test.Item2);
                var trace=Scene.Trace.Ray(from,to).IgnoreGameObjectHierarchy(player.GameObject).Run();
                bool hitSubject=trace.Hit && trace.GameObject==subject;
                Log.Info($"TANK_COLLISION {(hitSubject==test.Item3 ? "PASS" : "FAIL")} y={test.Item1} z={test.Item2} expected={test.Item3} actual={hitSubject}");
            }
        }
        if(VisualOnly || checkedCollision || elapsed<.3f)return;
        checkedCollision=true;
        if(CheckPlanner)
        {
            var actor=new GameObject(true,"Spool planner probe");
            for(int i=0;i<8;i++)
            {
                var away=Rotation.FromYaw(i*45).Forward;
                var threat=subject.WorldPosition-away*200;
                actor.WorldPosition=subject.WorldPosition+away*110+Vector3.Up;
                bool found=OpponentCoverPlanner.TryFind(actor,player.GameObject,threat,out var destination);
                bool near=found && (destination-subject.WorldPosition).WithZ(0).Length<70;
                bool sheltered=found && OpponentCoverPlanner.IsSheltered(actor,player.GameObject,threat,destination);
                Log.Info($"SPOOL_PLANNER {(found && near && sheltered ? "PASS" : "FAIL")} approach={i*45} found={found} near={near} shelter={sheltered} destination={destination}");
            }
            actor.Destroy();
        }
        foreach(var height in new[]{2f,28f,47f})
        {
            var origin=subject.WorldPosition+new Vector3(-60,0,height);
            var hit=Scene.Trace.Ray(origin,origin+Vector3.Forward*120).Run();
            Log.Info($"PROP_COLLISION {(hit.Hit && hit.GameObject==subject ? "PASS" : "FAIL")} solid at height={height} point={hit.EndPosition}");
        }
        var side=subject.WorldPosition+new Vector3(-60,24,28);
        var gap=Scene.Trace.Ray(side,side+Vector3.Forward*120).Run();
        Log.Info($"PROP_COLLISION {(!gap.Hit ? "PASS" : "FAIL")} open waist below flange");
    }
    protected override void OnDestroy()
    {
        if(!UsePlaced && subject.IsValid())subject.Destroy();
    }
}
