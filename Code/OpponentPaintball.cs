using Sandbox;

namespace PaintballBaddies;

/// <summary>Physical AI paintball using the same speed, gravity and sweep radius as the player.</summary>
public sealed class OpponentPaintball : Component
{
    [Property] public GameObject Shooter { get; set; }
    [Property] public Vector3 Velocity { get; set; }
    [Property] public int Team { get; set; } = 1;
    private Vector3 position,previous;
    private GameObject bead;
    private float renderClock,simulationClock,physicsStep=.02f;
    private float life = PaintballFlight.Lifetime;
    private bool initialized;
    private Vector3 launchOrigin;
    private readonly HashSet<ArenaOpponent> alerted=new();
    public Color PaintColor { get; private set; }
    private void InitializePosition()
    {
        if(initialized)return;
        position=previous=WorldPosition;
        launchOrigin=WorldPosition;
        PaintColor=CharacterPaintColor.For(Shooter);
        initialized=true;
    }
    protected override void OnStart()
    {
        GameObject.Tags.Add("paintball_debris");
        InitializePosition();
        WorldScale=Vector3.One;
        // The joining shooter already renders a local prediction. Do not create
        // a second bead/trail or look up a replicated trail that may not exist.
        if(!MultiplayerSession.Authority && Shooter?.Components.Get<NetworkPawn>() is {IsProxy:false})
            return;
        bead=PaintballFlight.CreateBead(GameObject,PaintColor);
    }
    protected override void OnUpdate()=>renderClock+=Time.Delta;
    protected override void OnPreRender()
    {
        if(IsProxy){PaintballFlight.UpdateBead(bead);return;}
        InitializePosition();
        WorldPosition=Vector3.Lerp(previous,position,((renderClock-simulationClock)/physicsStep).Clamp(0,1));
        PaintballFlight.UpdateBead(bead);
    }
    protected override void OnFixedUpdate()
    {
        if(IsProxy)return;
        InitializePosition();
        simulationClock+=Time.Delta;physicsStep=System.MathF.Max(Time.Delta,.001f);previous=position;
        var end = position + Velocity * Time.Delta + Vector3.Down * (PaintballFlight.Gravity * .5f * Time.Delta * Time.Delta);
        var trace = PaintballFlight.Trace(Scene,position,end,Shooter);
        IncomingPaintAwareness.Report(Scene,Shooter,launchOrigin,position,trace.Hit ? trace.EndPosition : end,trace.GameObject,alerted);
        life -= Time.Delta;
        if ( trace.Hit )
        {
            var source=trace.EndPosition-Velocity.Normal*100;
            var tint=PaintColor;
            if(!ArenaShield.TryAbsorbImpact(trace,source,tint))
            {
                PaintballCombatant.Find(trace.GameObject)?.RegisterHit(Team,source,PaintballCombatant.Find(Shooter),launchOrigin);
                PaintImpactSystem.Find(Scene)?.Spawn(trace,tint);
            }
        }
        if ( trace.Hit || life <= 0 ) { GameObject.Destroy(); return; }
        position = end;
        Velocity += Vector3.Down * PaintballFlight.Gravity * Time.Delta;
    }
}
