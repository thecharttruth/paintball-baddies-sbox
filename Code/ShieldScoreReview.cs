using Sandbox;
using System.Collections.Generic;

namespace PaintballBaddies;

/// <summary>Opt-in native projectile regression; isolated above the playable arena.</summary>
public sealed class ShieldScoreReview : Component
{
    private GameObject actorObject, shooterObject, shieldObject;
    private PaintballCombatant actor, attacker;
    private ArenaShield shield;
    private float elapsed;
    private int shots;
    public bool Done { get; private set; }
    public List<string> Results { get; } = new();
    protected override void OnStart()
    {
        actorObject=new GameObject(GameObject){Name="Shield test wearer",WorldPosition=new Vector3(0,0,1500)};
        actor=actorObject.Components.Create<PaintballCombatant>();actor.Team=91;actor.StayInMatch=true;
        var shape=actorObject.Components.Create<BoxCollider>();shape.Scale=new Vector3(28,28,66);shape.Center=new Vector3(0,0,33);
        shooterObject=new GameObject(GameObject){Name="Shield test shooter",WorldPosition=new Vector3(100,0,1565)};
        attacker=shooterObject.Components.Create<PaintballCombatant>();attacker.Team=92;attacker.StayInMatch=true;
        shieldObject=new GameObject(GameObject){Name="Shield scoring test panel"};shield=shieldObject.Components.Create<ArenaShield>();
    }
    protected override void OnUpdate()
    {
        if(Done)return;
        elapsed+=Time.Delta;
        if(elapsed<.2f)return;
        elapsed=0;
        if(shots==0 && shield.Owner!=actor){shield.Collect(actor);return;}
        if(shots>0)
        {
            bool pass=shots<=5 ? actor.PaintHits==0 && attacker.HitsLanded==0 && shield.Remaining==5-shots
                : actor.PaintHits==shots-5 && attacker.HitsLanded==shots-5;
            if(shots==5)pass=pass && shield.Shatters==1 && !shield.Owner.IsValid() && Scene.GetAllComponents<ShieldBreakEffect>().Sum(x=>x.Fragments)==16;
            var result=$"{(pass ? "PASS" : "FAIL")} shot={shots} shield={shield.Remaining} received={actor.PaintHits} landed={attacker.HitsLanded} paint={shield.PaintCount}";
            Results.Add(result);Log.Info("SHIELD_SCORE "+result);
        }
        if(shots>=6){Done=true;return;}
        var target=actorObject.WorldPosition+Vector3.Up*65; // Exposed helmet zone above the visible panel.
        var start=target+Vector3.Forward*100;
        var go=new GameObject(GameObject){Name="Shield score physical paintball",WorldPosition=start};
        var ball=go.Components.Create<OpponentPaintball>();ball.Shooter=shooterObject;ball.Team=92;ball.Velocity=PaintballFlight.LaunchVelocity(start,target);
        shots++;
    }
    protected override void OnDestroy()
    {
        // Broken shields deliberately detach from their wearer.
        if(shieldObject.IsValid())shieldObject.Destroy();
    }
}
