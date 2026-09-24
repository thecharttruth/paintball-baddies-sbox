using Sandbox;
using Sandbox.Citizen;

namespace PaintballBaddies;

/// <summary>Reusable pod pickup, pour and return choreography for the Citizen human rig.</summary>
public sealed class CitizenPodReload : Component
{
    public GameObject refillPod;
    private GameObject refillLid, refillPouch, reloadRightTarget;
    private readonly List<GameObject> refillBalls = new();
    public bool podHeld, podReturned;
    private bool pickupStarted;
    private Transform pickupStart;
    private Rotation carryHandRotation;
    private bool carryHandCaptured;
    public float pickupError=-1, returnError=-1, minimumReturnGap=float.MaxValue;
    public float MinimumPickupGap { get; private set; } = float.MaxValue;
    public float returnReachDistance, returnArmLength;
    public int pourSamples, pourAlignedSamples;
    public float maximumPourError, totalPourError;
    private float podGripWeight;
    private GameObject subject => GameObject;
    public void CreateGear()
    {
        if (refillPod.IsValid()) return;
                reloadRightTarget = new GameObject(subject) { Name="Native reload marker tuck target" };
                refillPod = new GameObject(subject) { Name="Existing refill pod reach review" };
                refillPod.Components.Create<ModelRenderer>().Model=Model.Load("models/gear/refill_pod/body.vmdl");
                refillPouch=new GameObject(subject) { Name="Existing belt pod pouch" };
                refillPouch.Components.Create<ModelRenderer>().Model=Model.Load("models/gear/refill_pod/pouch.vmdl");
                refillLid = new GameObject(refillPod) { Name="Existing hinged refill lid" };
                refillLid.Components.Create<ModelRenderer>().Model=Model.Load("models/gear/refill_pod/lid.vmdl");
                for(int i=0;i<2;i++)
                {
                    var ball=new GameObject(subject) { Name="Existing reload paintball stream" };
                    var renderer=ball.Components.Create<ModelRenderer>();
                    renderer.Model=Model.Load("models/dev/sphere.vmdl");
                    renderer.Tint=new Color(.95f,.28f,.025f);
                    ball.WorldScale=new Vector3(.0063f);
                    ball.Enabled=false;
                    refillBalls.Add(ball);
                }
    }
    public void ResetCycle()
    {
        pickupStarted=false;podHeld=false;podReturned=false;podGripWeight=0;
        carryHandCaptured=false;
        pickupError=-1;returnError=-1;minimumReturnGap=float.MaxValue;
        MinimumPickupGap=float.MaxValue;
        returnReachDistance=0;returnArmLength=0;
        pourSamples=0;pourAlignedSamples=0;maximumPourError=0;totalPourError=0;
    }
    public void StopPresentation()
    {
        podHeld=false;
        if(refillPod.IsValid()) refillPod.Enabled=false;
        foreach(var ball in refillBalls) if(ball.IsValid()) ball.Enabled=false;
    }
    public void DropHeldPod(Vector3 velocity)
    {
        if(!podHeld || !refillPod.IsValid())return;
        var dropped=new GameObject {Name="Released paintball pod"};
        dropped.Tags.Add( "paintball_debris" );
        dropped.WorldTransform=refillPod.WorldTransform;
        dropped.Components.Create<ModelRenderer>().Model=Model.Load("models/gear/refill_pod/body.vmdl");
        if(refillLid.IsValid())
        {
            var lid=new GameObject(dropped){Name="Pod lid"};lid.WorldTransform=refillLid.WorldTransform;
            lid.Components.Create<ModelRenderer>().Model=Model.Load("models/gear/refill_pod/lid.vmdl");
        }
        var collider=dropped.Components.Create<BoxCollider>();
        collider.Scale=new Vector3(3,3,9);collider.Center=new Vector3(0,0,4.5f);
        var physics=dropped.Components.Create<Rigidbody>();
        physics.MassOverride=.08f;physics.EnableImpactDamage=false;physics.Gravity=true;
        physics.Velocity=velocity;physics.AngularVelocity=new Vector3(1,2,.5f);
        dropped.Components.Create<DroppedReloadPod>();
        StopPresentation();
    }
    public bool HasActiveReloadProps => (refillPod.IsValid() && refillPod.Enabled)
        || refillBalls.Any(ball=>ball.IsValid() && ball.Enabled);
    public void SetCarryVisible(bool visible)
    {
        if(refillPod.IsValid())refillPod.Enabled=visible;
        if(refillPouch.IsValid())refillPouch.Enabled=visible;
        if(!visible)foreach(var ball in refillBalls)if(ball.IsValid())ball.Enabled=false;
    }
    protected override void OnDestroy()
    {
        foreach (var ball in refillBalls) if (ball.IsValid()) ball.Destroy();
        refillBalls.Clear();
        if (refillPod.IsValid()) refillPod.Destroy();
        if (refillPouch.IsValid()) refillPouch.Destroy();
        if (reloadRightTarget.IsValid()) reloadRightTarget.Destroy();
    }
    public void Apply(SkinnedModelRenderer body, CitizenAnimationHelper animation, GameObject marker,
        GameObject supportTarget, Rotation supportRotation, float currentGrip, float reviewTime,
        float DuckLevel, bool releaseGrip, bool reloadCancelled, float cancelElapsed,
        Transform cancelHand, Rotation cancelPodRotation, float delta)
    {
        if (!refillPod.IsValid()) return;
            var dockRotation=subject.WorldRotation;
            var dockOrigin=subject.WorldPosition+subject.WorldRotation*new Vector3(3,7,26-18*DuckLevel);
            if (body.TryGetBoneTransform("pelvis",out var beltPelvis))
                dockOrigin=beltPelvis.Position+subject.WorldRotation*new Vector3(3,7,-4);
            var podGrip=new Vector3(1.5f,1.4f,3.88583f);
            var dockHand=dockOrigin+dockRotation*podGrip;
            if(refillPouch is not null) { refillPouch.WorldPosition=dockOrigin;refillPouch.WorldRotation=dockRotation; }
            if (releaseGrip && supportTarget is not null && body.TryGetBoneTransform("hand_L",out var reloadHand))
            {
                var reachIn=((reviewTime-1.35f)/.4f).Clamp(0,1);
                var reachOut=((2.95f-reviewTime)/.35f).Clamp(0,1);
                var blend=System.MathF.Min(reachIn,reachOut);
                blend=blend*blend*(3-2*blend);
                // Existing speed-feed source opening: (-Y 0.045m, Z 0.231m), exported forward X.
                if (body.TryGetBoneTransform("hand_R",out var rightHand))
                {
                    // Preserve the carry orientation instead of copying the rifle
                    // magazine gesture's wrist twist into a hopper-fed marker.
                    if(!carryHandCaptured)
                    {
                        carryHandRotation=subject.WorldRotation.Inverse*rightHand.Rotation;
                        carryHandCaptured=true;
                    }
                    reloadRightTarget.WorldPosition=Vector3.Lerp(rightHand.Position,subject.WorldTransform.PointToWorld(new Vector3(12,-6,44-22*DuckLevel)),blend);
                    reloadRightTarget.WorldRotation=Rotation.Slerp(rightHand.Rotation,subject.WorldRotation*carryHandRotation,blend);
                    animation.IkRightHand=reloadRightTarget;
                }
                var opening=marker.WorldPosition+marker.WorldRotation*new Vector3(1.77165f,0,9.09449f);
                var handTarget=opening+marker.WorldRotation*new Vector3(-1.5f,1.4f,6.1f);
                if(reviewTime>=1.85f && reviewTime<2.55f)
                {
                    var error=(reloadHand.Position-handTarget).Length;
                    pourSamples++;
                    if(error<1) pourAlignedSamples++;
                    totalPourError+=error;
                    maximumPourError=System.MathF.Max(maximumPourError,error);
                }
                if(!pickupStarted) { pickupStart=subject.WorldTransform.ToLocal(reloadHand);pickupStarted=true; }
                var pickup=((reviewTime-.8f)/.4f).Clamp(0,1);
                pickup=pickup*pickup*(3-2*pickup);
                supportTarget.WorldPosition=Vector3.Lerp(subject.WorldTransform.PointToWorld(pickupStart.Position),Vector3.Lerp(dockHand,handTarget,blend),pickup);
                supportTarget.WorldRotation=Rotation.Slerp(reloadHand.Rotation,marker.WorldRotation*supportRotation,blend);

                if(reloadCancelled)
                {
                    var recovery=(cancelElapsed/.5f).Clamp(0,1);
                    recovery=recovery*recovery*(3-2*recovery);
                    supportTarget.WorldPosition=Vector3.Lerp(subject.WorldTransform.PointToWorld(cancelHand.Position),dockHand,recovery);
                    supportTarget.WorldRotation=subject.WorldRotation*cancelHand.Rotation;
                }
                animation.IkLeftHand=supportTarget;
                if(refillPod is not null)
                {
                    refillPod.Enabled=true;
                    refillPod.WorldRotation=Rotation.Slerp(dockRotation,marker.WorldRotation*Rotation.FromPitch(180),blend);
                    if(reloadCancelled)
                    {
                        var recovery=(cancelElapsed/.5f).Clamp(0,1);
                        recovery=recovery*recovery*(3-2*recovery);
                        refillPod.WorldRotation=Rotation.Slerp(subject.WorldRotation*cancelPodRotation,dockRotation,recovery);
                    }
                    // Mouth at 8.386 inches from the pod base; follow the actual hand to expose IK fit errors.
                    var dockError=(reloadHand.Position-dockHand).Length;
                    if(reviewTime>=1.2f && reviewTime<1.35f) MinimumPickupGap=System.MathF.Min(MinimumPickupGap,dockError);
                    // Allow the gloved palm to reach the pod, without requiring the
                    // wrist joint to converge within half an inch in a brief window.
                    if(!podHeld && !podReturned && reviewTime>=1.2f && reviewTime<1.35f && dockError<.75f)
                    {
                        podHeld=true;
                        pickupError=dockError;
                    }
                    if(reviewTime>=2.95f && dockError<minimumReturnGap)
                    {
                        minimumReturnGap=dockError;
                        if(body.TryGetBoneTransform("arm_upper_L",out var returnShoulder) && body.TryGetBoneTransform("arm_lower_L",out var returnElbow))
                        {
                            returnReachDistance=(returnShoulder.Position-dockHand).Length;
                            returnArmLength=(returnShoulder.Position-returnElbow.Position).Length+(returnElbow.Position-reloadHand.Position).Length;
                        }
                    }
                    if(podHeld && reviewTime>=2.95f && dockError<.75f)
                    {
                        podHeld=false;
                        podReturned=true;
                        returnError=dockError;
                    }
                    podGripWeight += ((podHeld ? 1 : 0)-podGripWeight)*(1-System.MathF.Exp(-25*delta));
                    body.Set("pb_left_grip",System.MathF.Max(currentGrip,podGripWeight));
                    if(!podHeld) refillPod.WorldRotation=dockRotation;
                    refillPod.WorldPosition=podHeld ? reloadHand.Position-refillPod.WorldRotation*podGrip : dockOrigin;
                    var open=((reviewTime-1.4f)/.3f).Clamp(0,1);
                    open=open*open*(3-2*open);
                    var close=((reviewTime-2.65f)/.25f).Clamp(0,1);
                    close=close*close*(3-2*close);
                    refillLid.WorldPosition=refillPod.WorldPosition+refillPod.WorldRotation*new Vector3(-1.33858f,0,8.46457f);
                    refillLid.WorldRotation=refillPod.WorldRotation*Rotation.FromPitch(115*open*(1-close));
                    var mouth=refillPod.WorldPosition+refillPod.WorldRotation.Up*8.38583f;
                    for(int i=0;i<refillBalls.Count;i++)
                    {
                        var ball=refillBalls[i];
                        ball.Enabled=podHeld && reviewTime>=1.85f && reviewTime<2.55f && (reloadHand.Position-handTarget).Length<1;
                        var t=(reviewTime*5+i/(float)refillBalls.Count)%1;
                        var jitter=marker.WorldRotation*new Vector3(System.MathF.Sin(i*2.4f)*.13f,System.MathF.Cos(i*2.4f)*.13f,0);
                        ball.WorldPosition=Vector3.Lerp(mouth,opening,t*t)+jitter;
                    }
                }
            }
            else if(refillPod is not null)
            {
                refillPod.Enabled=true;
                if(!podHeld) { refillPod.WorldPosition=dockOrigin; refillPod.WorldRotation=dockRotation; }
                else if(body.TryGetBoneTransform("hand_L",out var retainedHand))
                    refillPod.WorldPosition=retainedHand.Position-refillPod.WorldRotation*podGrip;
                refillLid.WorldPosition=refillPod.WorldPosition+refillPod.WorldRotation*new Vector3(-1.33858f,0,8.46457f);
                refillLid.WorldRotation=refillPod.WorldRotation;
                animation.IkRightHand=null;
                foreach(var ball in refillBalls) ball.Enabled=false;
            }
    }
}
