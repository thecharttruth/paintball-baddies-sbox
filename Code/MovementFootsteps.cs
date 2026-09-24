using Sandbox;
using System;
namespace PaintballBaddies;

/// <summary>Measured run contact phases with distance fallback for unmeasured clips.</summary>
public sealed class MovementFootsteps : Component
{
    public bool ReviewContacts { get; set; }
    private Vector3 previous;
    private float distance,cooldown;
    private float previousPhase=-1;
    private string previousModel;
    public int StepsPlayed { get; private set; }
    public int NativeContactEvents { get; private set; }
    public int NativeStepsPlayed { get; private set; }
    private SkinnedModelRenderer contactSource;
    private bool pendingContact;
    private float contactAge=10;
    private void Contact(SceneModel.FootstepEvent e)
    {
        NativeContactEvents++;pendingContact=true;contactAge=0;
    }
    private void BindContacts(SkinnedModelRenderer renderer)
    {
        if(contactSource==renderer)return;
        if(contactSource.IsValid())contactSource.OnFootstepEvent-=Contact;
        contactSource=renderer;pendingContact=false;contactAge=10;
        if(contactSource.IsValid())contactSource.OnFootstepEvent+=Contact;
    }
    protected override void OnDestroy()=>BindContacts(null);
    protected override void OnStart()=>previous=WorldPosition;
    protected override void OnUpdate()
    {
        var citizenPlayer=Components.Get<CitizenPlayerPresentation>();
        var citizenOpponent=Components.Get<CitizenOpponentPresentation>();
        BindContacts(citizenPlayer?.IsReady==true ? citizenPlayer.FootstepRenderer :
            citizenOpponent?.IsReady==true ? citizenOpponent.FootstepRenderer : null);
        contactAge+=Time.Delta;
        var nativeContactPending=pendingContact;pendingContact=false;
        var travel=(WorldPosition-previous).WithZ(0).Length;
        previous=WorldPosition;
        cooldown=(cooldown-Time.Delta).Clamp(0,1);
        if(travel>32 || Components.Get<PaintballCombatant>()?.Eliminated==true ||
           Components.Get<VaultController>()?.IsVaulting==true)
        {distance=0;previousPhase=-1;return;}
        var speed=travel/MathF.Max(Time.Delta,.001f);
        if(speed<8){distance=0;previousPhase=-1;return;}
        var ground=Scene.Trace.Ray(WorldPosition+Vector3.Up*2,WorldPosition-Vector3.Up*5)
            .IgnoreGameObjectHierarchy(GameObject).Run();
        if(!ground.Hit || ground.Normal.z<.6f){distance=0;previousPhase=-1;return;}
        var player=Components.Get<PlayerController>();
        var crouched=player?.IsDucking==true || Components.Get<ArenaOpponent>()?.UsingCover==true;
        var stride=crouched ? 25f : speed>130 ? 48f : 32f;
        var animated=player?.Renderer ?? Components.Get<ArenaOpponent>()?.Body;
        var profile=animated?.Model?.Name switch
        {
            "models/characters/viper_protected_morph/viper_protected.vmdl" => (.0231f,.5529f),
            "models/characters/roxie_protected_morph/roxie_protected.vmdl" => (.0211f,.5570f),
            "models/characters/imani_protected_morph/imani_protected.vmdl" => (.0242f,.5221f),
            "models/characters/mei_protected_morph/mei_protected.vmdl" => (.0283f,.5590f),
            "models/characters/freya_protected_morph/freya_protected.vmdl" => (.0299f,.5635f),
            "models/characters/leilani_protected_morph/leilani_protected.vmdl" => (.0290f,.5548f),
            _ => (-1f,-1f)
        };
        if(previousModel!=animated?.Model?.Name){previousPhase=-1;previousModel=animated?.Model?.Name;}
        // Citizen uses an animation graph; the hidden legacy sequence is not a contact clock.
        bool citizen=Components.Get<CitizenPlayerPresentation>()?.IsReady==true ||
            Components.Get<CitizenOpponentPresentation>()?.IsReady==true;
        bool measuredRun=!citizen && animated.IsValid() && animated.Enabled && profile.Item1>=0 && animated.Sequence.Name=="run";
        bool nativeContact=contactSource.IsValid() && contactAge<.75f;
        if(nativeContact)
        {
            previousPhase=-1;distance=0;
            if(!nativeContactPending || cooldown>0)return;
        }
        else if(measuredRun)
        {
            var phase=animated.Sequence.TimeNormalized;
            bool crosses(float point)=>previousPhase>=0 && (phase>=previousPhase ? previousPhase<point && phase>=point : previousPhase<point || phase>=point);
            var advance=previousPhase<0 ? 0 : (phase-previousPhase+1)%1;
            bool contact=advance<=.1f && (crosses(profile.Item1)||crosses(profile.Item2));
            previousPhase=phase;distance=0;
            if(!contact || cooldown>0)return;
        }
        else
        {
            previousPhase=-1;distance+=travel;
            if(distance<stride || cooldown>0)return;
            distance%=stride;
        }
        cooldown=.16f;
        Sound.Play("sounds/paintball/footstep.sound",ground.HitPosition);
        StepsPlayed++;
        if(nativeContact)NativeStepsPlayed++;
        if(ReviewContacts && animated is { } body && body.TryGetBoneTransform("LeftFoot",out var left) && body.TryGetBoneTransform("RightFoot",out var right))
            Log.Info($"FOOTSTEP_CONTACT clip={body.Sequence.Name} phase={body.Sequence.TimeNormalized} left_z={left.Position.z-WorldPosition.z} right_z={right.Position.z-WorldPosition.z} speed={speed}");
    }
}
