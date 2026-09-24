using Sandbox;
namespace PaintballBaddies;

/// <summary>Occasional user-selected D/E efforts, shared by player and AI.</summary>
public sealed class CharacterVoice : Component
{
    public const string EffortSound="sounds/voices/occasional_effort.sound";
    public const string KnockdownSound="sounds/voices/knockdown_effort.sound";
    private float cooldown=2;
    private int previousHits;
    private bool previousTraversal;
    public float SinceLastVoice { get; private set; }=100;
    public int Plays { get; private set; }
    public int KnockdownPlays { get; private set; }
    protected override void OnUpdate()
    {
        if(!MultiplayerSession.Authority)return;
        cooldown=System.MathF.Max(0,cooldown-Time.Delta);
        SinceLastVoice+=Time.Delta;
        var actor=Components.Get<PaintballCombatant>();
        if(!actor.IsValid())return;
        bool traversal=Components.Get<VaultController>()?.IsVaulting==true || Components.Get<CoverController>()?.Sliding==true;
        bool hit=actor.PaintHits>previousHits;
        bool effort=traversal&&!previousTraversal;
        previousHits=actor.PaintHits;previousTraversal=traversal;
        if((hit || effort) && Game.Random.Float(0,1)<(effort ? .45f : .28f))TryPlay();
    }
    public bool TryPlay()
    {
        if(!MultiplayerSession.Authority)return false;
        if(cooldown>0 || Scene.Camera is not {} camera || (camera.WorldPosition-WorldPosition).Length>650)return false;
        if(Scene.GetAllComponents<CharacterVoice>().Any(x=>x.SinceLastVoice<4))return false;
        PlayEvent(EffortSound);return true;
    }
    /// <summary>Confirmed knockdowns always speak, regardless of chatter cooldown or host camera distance.</summary>
    public bool PlayKnockdown()
    {
        if(!MultiplayerSession.Authority)return false;
        PlayEvent(KnockdownSound);KnockdownPlays++;return true;
    }
    private void PlayEvent(string sound)
    {
        // Let each listener's native spatial mixer attenuate the event. In a
        // network game the host camera must not decide what a client can hear.
        if(MultiplayerSession.Online)NetworkEffects.Sound(sound,WorldPosition+Vector3.Up*48,System.Guid.Empty);
        else Sound.Play(sound,WorldPosition+Vector3.Up*48);
        SinceLastVoice=0;cooldown=Game.Random.Float(12,20);Plays++;
    }
}
