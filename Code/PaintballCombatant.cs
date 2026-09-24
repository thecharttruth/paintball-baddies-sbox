using Sandbox;

namespace PaintballBaddies;

public sealed partial class PaintballCombatant : Component
{
    protected override void OnStart()
    {
        observedHits=PaintHits;observedHitSerial=LastHitSerial;
        GameObject.Tags.Add("paintball_actor");
        if(Components.Get<CharacterVoice>() is null)Components.Create<CharacterVoice>();
        if(Components.Get<CloseCombat>() is null)Components.Create<CloseCombat>();
    }
    [Sync(SyncFlags.FromHost)] public int Team { get; set; }
    [Sync(SyncFlags.FromHost)] public int PaintHits { get; private set; }
    [Sync(SyncFlags.FromHost)] public int HitsLanded { get; private set; }
    [Sync(SyncFlags.FromHost)] public int KnockdownsLanded { get; private set; }
    [Sync(SyncFlags.FromHost)] public int KnockdownsReceived { get; private set; }
    public const int KnockdownPoints=2;
    public int Points => MultiplayerSession.Find(Scene)?.IsHeist==true ? BankedTokens : HitsLanded-PaintHits+KnockdownPoints*(KnockdownsLanded-KnockdownsReceived);
    [Sync(SyncFlags.FromHost)] public bool StayInMatch { get; set; }
    [Sync(SyncFlags.FromHost)] public bool AcceptHits { get; set; } = true;
    public Vector3 EyePosition => Components.Get<PlayerController>()?.EyePosition
        ?? WorldPosition+Vector3.Up*(Components.Get<ArenaOpponent>()?.CoverCrouched == true ? 35 : 60);
    public int HitLimit { get; set; } = 3;
    public bool Eliminated => !StayInMatch && PaintHits >= HitLimit;
    public float HitFeedbackRemaining { get; private set; }
    public const float DirectionIndicatorDuration=1.2f;
    public float DirectionIndicatorRemaining { get; private set; }
    [Sync(SyncFlags.FromHost)] public Vector3 LastHitDirection { get; private set; }
    [Sync(SyncFlags.FromHost)] public int LastHitSerial { get; private set; }
    public Vector3 LastHitOrigin { get; private set; }
    public PaintballCombatant LastAttacker { get; private set; }
    private int observedHits;
    private int observedHitSerial;
    protected override void OnUpdate()
    {
        UpdateHeistFeedback();
        if(PaintHits<observedHits)DirectionIndicatorRemaining=0;
        // A serial also detects the first new hit when scores reset between rounds.
        if(!MultiplayerSession.Authority && LastHitSerial!=observedHitSerial && PaintHits>0)
        {
            HitFeedbackRemaining=.45f;
            DirectionIndicatorRemaining=DirectionIndicatorDuration;
        }
        observedHits=PaintHits;
        observedHitSerial=LastHitSerial;
        HitFeedbackRemaining=System.MathF.Max(0,HitFeedbackRemaining-Time.Delta);
        DirectionIndicatorRemaining=System.MathF.Max(0,DirectionIndicatorRemaining-Time.Delta);
        if(Components.Get<CharacterVoice>() is null)Components.Create<CharacterVoice>();
        if(Components.Get<CloseCombat>() is null)Components.Create<CloseCombat>();
    }
    public bool RegisterHit( int attackingTeam, Vector3? sourcePosition = null, PaintballCombatant attacker = null, Vector3? observedOrigin = null )
    {
        if(!MultiplayerSession.Authority)return false;
        if ( !AcceptHits || attackingTeam == Team || Eliminated ) return false;
        PaintHits++;
        LastHitSerial=unchecked(LastHitSerial+1);
        LastAttacker=attacker;
        LastHitOrigin=observedOrigin ?? sourcePosition ?? (attacker.IsValid() ? attacker.WorldPosition : WorldPosition);
        if(attacker.IsValid() && attacker!=this)
        {
            attacker.HitsLanded++;
            MultiplayerSession.Find(Scene)?.HeistHit(this,attacker,false);
            if(MultiplayerSession.Online)attacker.Components.Get<NetworkPawn>()?.ConfirmHit();
        }
        LastHitDirection = sourcePosition.HasValue ? (sourcePosition.Value-WorldPosition).WithZ(0).Normal : Vector3.Zero;
        HitFeedbackRemaining = 0.45f;
        DirectionIndicatorRemaining=DirectionIndicatorDuration;
        return true;
    }
    /// <summary>Clockwise screen angle: front 0, right 90, behind 180, left -90.</summary>
    public float HitDirectionAngle(Rotation view)
    {
        var forward=Vector3.Dot(LastHitDirection,view.Forward.WithZ(0).Normal);
        var right=Vector3.Dot(LastHitDirection,view.Right.WithZ(0).Normal);
        return System.MathF.Atan2(right,forward)*180/System.MathF.PI;
    }
    public string HitDirectionLabel(Rotation view)
    {
        if(HitFeedbackRemaining<=0 || LastHitDirection.Length<.01f)return "";
        var forward=Vector3.Dot(LastHitDirection,view.Forward.WithZ(0).Normal);
        var right=Vector3.Dot(LastHitDirection,view.Right.WithZ(0).Normal);
        return System.MathF.Abs(right)>System.MathF.Abs(forward) ? (right>0 ? "RIGHT" : "LEFT") : (forward>0 ? "AHEAD" : "BEHIND");
    }
    internal bool RegisterKnockdown(PaintballCombatant attacker)
    {
        if(!MultiplayerSession.Authority || !AcceptHits || Eliminated || !attacker.IsValid()
            || !attacker.AcceptHits || attacker.Eliminated || attacker==this || attacker.Team==Team)return false;
        KnockdownsReceived++;attacker.KnockdownsLanded++;
        MultiplayerSession.Find(Scene)?.HeistHit(this,attacker,true);
        return true;
    }
    public void ResetPaintHits()
    {
        if(!MultiplayerSession.Authority)return;
        Components.Get<CloseCombat>()?.ResetState();
        Components.Get<CharacterPaint>()?.Clear();
        ResetHeistWallet();
        PaintHits = 0;
        observedHits=0;
        HitsLanded = 0;
        KnockdownsLanded=KnockdownsReceived=0;
        AcceptHits = true;
        HitFeedbackRemaining = 0;
        DirectionIndicatorRemaining=0;
        LastHitDirection = Vector3.Zero;
    }
    public static PaintballCombatant Find( GameObject go )
    {
        while ( go is not null )
        {
            var actor = go.Components.Get<PaintballCombatant>();
            if ( actor is not null ) return actor;
            go = go.Parent;
        }
        return null;
    }
}
