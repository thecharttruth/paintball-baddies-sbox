using Sandbox;
using Sandbox.Audio;
namespace PaintballBaddies;

/// <summary>Opt-in native audio observation; no microphones or system audio recording.</summary>
public sealed class AudioPlaybackReview : Component
{
    public bool Audition { get; set; }
    public bool Done { get; private set; }
    public sealed class Observation
    {
        public string File { get; set; }
        public string Name { get; set; }
        public float Volume { get; set; }
        public bool Played { get; set; }
        public float Amplitude { get; set; }
        public float MixerPeak { get; set; }
    }
    public List<Observation> Sounds { get; }=new();
    private readonly Dictionary<SoundHandle,Observation> observed=new();
    private readonly List<SoundHandle> handles=new();
    private readonly List<SoundHandle> auditionHandles=new();
    private float elapsed,next=.5f;
    private int index;
    private GameObject[] knockTargets;
    private float knockAt=-1;
    private int knockStage;
    public List<string> KnockdownChecks { get; }=new();
    public void CheckKnockdowns()
    {
        var camera=Scene.Camera;
        knockTargets=new[]{24f,900f}.Select(distance=>
        {
            var go=new GameObject(GameObject){Name="Temporary knockdown voice receiver",WorldPosition=camera.WorldPosition+camera.WorldRotation.Forward*distance};
            go.Components.Create<PaintballCombatant>();go.Components.Create<CharacterVoice>();go.Components.Create<CloseCombat>();
            return go;
        }).ToArray();
        knockAt=elapsed+.2f;knockStage=0;
    }
    private void Check(bool ok,string label)=>KnockdownChecks.Add((ok ? "PASS " : "FAIL ")+label);
    private void AdvanceKnockdownChecks()
    {
        if(knockAt<0 || elapsed<knockAt)return;
        var near=knockTargets[0].Components.Get<CharacterVoice>();var far=knockTargets[1].Components.Get<CharacterVoice>();
        var contact=knockTargets[0].Components.Get<CloseCombat>();
        if(knockStage==0)
        {
            Check(!near.TryPlay(),"ordinary voice respects initial cooldown");
            contact.ReceiveContact(knockTargets[0].WorldPosition-Vector3.Forward*35,true);
            Check(near.KnockdownPlays==1,"confirmed knockdown bypasses ordinary cooldown");
            knockTargets[1].Components.Get<CloseCombat>().ReceiveContact(knockTargets[1].WorldPosition-Vector3.Forward*35,true);
            Check(far.KnockdownPlays==1,"another knockdown bypasses shared gap and old 650-unit camera cutoff");
            contact.ReceiveContact(knockTargets[0].WorldPosition-Vector3.Forward*35,true);
            Check(near.KnockdownPlays==1,"already downed actor does not repeat voice");
            Check(!near.TryPlay(),"knockdown does not enable extra ordinary chatter");
            knockStage=1;knockAt=elapsed+.3f;
        }
        else
        {
            contact.ResetState();contact.ReceiveContact(knockTargets[0].WorldPosition-Vector3.Forward*35,true);
            Check(near.KnockdownPlays==2,"next confirmed knockdown speaks during long character cooldown");
            knockAt=-1;
        }
    }
    private PaintballCombatant scoreReceiver;
    private float scoringShotAt=-1;
    public bool ShotFired { get; private set; }
    public int ScoringHits=>scoreReceiver?.PaintHits ?? 0;
    public void CheckScoring()
    {
        var player=Scene.GetAllComponents<PlayerController>().First();
        var weapon=player.Components.Get<PaintballMarker>();
        if(scoreReceiver.IsValid())scoreReceiver.GameObject.Destroy();
        var target=TrainingRange.Box(GameObject,"Audio scoring receiver",weapon.Muzzle+player.EyeAngles.Forward*80,new Vector3(16,16,24),Color.Gray,true);
        scoreReceiver=target.Components.Create<PaintballCombatant>();scoreReceiver.Team=91;scoreReceiver.StayInMatch=true;
        scoringShotAt=elapsed+.2f;ShotFired=false;
    }
    private string[] Events=>new[]{
        CharacterMarkerSound.For(0),CharacterMarkerSound.For(1),CharacterMarkerSound.For(2),
        CharacterMarkerSound.For(3),CharacterMarkerSound.For(4),
        "sounds/voices/selected/knockdown_1.sound","sounds/voices/selected/knockdown_2.sound",
        "sounds/voices/selected/knockdown_3.sound","sounds/voices/selected/knockdown_4.sound",
        "sounds/paintball/hit_confirm.sound",CloseCombat.ImpactSound,CloseCombat.ImpactSound,
        CharacterVoice.EffortSound,CharacterVoice.EffortSound,CharacterVoice.EffortSound,CharacterVoice.EffortSound,
        "sounds/paintball/shield_collect.sound","sounds/paintball/shield_break.sound"};
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;
        AdvanceKnockdownChecks();
        if(scoringShotAt>=0 && elapsed>=scoringShotAt)
        {
            scoringShotAt=-1;
            ShotFired=Scene.GetAllComponents<PlayerController>().First().Components.Get<PaintballMarker>().FireAt(scoreReceiver.WorldPosition);
        }
        if(Audition && elapsed>=next && !Done)
        {
            if(index>=Events.Length)Done=true;
            else
            {
                // Nearby unoccluded playback through the normal event and mixer.
                var camera=Scene.Camera;
                auditionHandles.Add(Sound.Play(Events[index++],camera.WorldPosition+camera.WorldRotation.Forward*24));
                next=elapsed+1.1f;
            }
        }
        handles.Clear();SoundHandle.GetActive(handles);
        foreach(var handle in handles)
        {
            var file=handle.SoundFile?.ResourcePath;
            if(file is null || (!file.StartsWith("sounds/paintball/") && !file.StartsWith("sounds/voices/") && !file.StartsWith("sounds/contact/")))continue;
            if(!observed.TryGetValue(handle,out var entry))
            {
                entry=new(){File=file,Name=handle.Name,Volume=handle.Volume};
                observed.Add(handle,entry);Sounds.Add(entry);
            }
            entry.Played|=handle.IsPlaying;
            entry.Amplitude=System.MathF.Max(entry.Amplitude,handle.Amplitude);
            var meter=(handle.TargetMixer ?? Mixer.Default).Meter;
            if(meter is not null)
                entry.MixerPeak=System.MathF.Max(entry.MixerPeak,System.MathF.Max(meter.Current.MaxLevelLeft,meter.Current.MaxLevelRight));
        }
    }
    protected override void OnDestroy(){foreach(var handle in auditionHandles)handle?.Stop();}
}
