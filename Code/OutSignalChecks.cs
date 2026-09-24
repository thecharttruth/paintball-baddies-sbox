using Sandbox;
namespace PaintballBaddies;
public sealed class OutSignalChecks : Component
{
    [Property] public int PlayerHitLimit { get; set; } = 999;
    [Property] public int CharacterIndex { get; set; } = 2;
    [Property] public bool FinalElimination { get; set; }
    [Property] public bool FromCrouch { get; set; }
    private bool preparedCrouch;
    private Vector3? previousHead;
    private float largestHeadStep;
    private ArenaMatch match;
    private ArenaOpponent bot;
    private float elapsed;
    private int stage;
    private bool roundStarted;
    private BoxCollider collider;
    protected override void OnStart()
    {
        match=Scene.GetAllComponents<ArenaMatch>().First();
        Scene.GetAllComponents<PlayerController>().First().UseCameraControls=false;
    }
    protected override void OnUpdate()
    {
        MeasureEntry();
        elapsed+=Time.Delta;
        if(!roundStarted)
        {
            if(elapsed<.2f)return;
            match.StartRound();match.PlayerState.HitLimit=PlayerHitLimit;roundStarted=true;
        }
        if(FromCrouch && !preparedCrouch && elapsed>.3f)
        {
            bot=Scene.GetAllComponents<ArenaOpponent>().FirstOrDefault(x=>x.CharacterIndex==CharacterIndex);
            if(!bot.IsValid())return;
            bot.ReviewCrouch=true;
            bot.Components.Get<NavMeshAgent>().Stop();
            bot.Body.Sequence.Name="crouch";
            bot.Body.Sequence.Blending=false;
            bot.Body.PlaybackRate=0;
            bot.Body.Sequence.TimeNormalized=.5f;
            preparedCrouch=true;
        }
        if(stage==0 && elapsed>.5f)
        {
            if(!preparedCrouch)bot=Scene.GetAllComponents<ArenaOpponent>().FirstOrDefault(x=>x.CharacterIndex==CharacterIndex);
            if(!bot.IsValid())return;
            bot.ReviewCrouch=false;
            collider=bot.Components.Get<BoxCollider>();
            foreach(var actor in Scene.GetAllComponents<ArenaOpponent>())actor.CombatEnabled=false;
            for(int i=0;i<3;i++)bot.Components.Get<PaintballCombatant>().RegisterHit(0);
            if(FinalElimination) foreach(var actor in Scene.GetAllComponents<ArenaOpponent>().Where(x=>x!=bot))
                for(int i=0;i<3;i++)actor.Components.Get<PaintballCombatant>().RegisterHit(0);
            stage=1;
        }
        if(!bot.IsValid())return;
        WorldPosition=bot.WorldPosition+new Vector3(130,-130,75);
        WorldRotation=Rotation.LookAt(bot.WorldPosition+Vector3.Up*42-WorldPosition);
        if(stage==1 && elapsed>1.5f)
        {
            var raised=bot.Body.TryGetBoneTransform("LeftHand",out var hand) && bot.Body.TryGetBoneTransform("Head",out var head) && hand.Position.z>head.Position.z+5;
            Log.Info($"OUT_SIGNAL animation time={bot.Body.Sequence.Time} duration={bot.Body.Sequence.Duration} hand={hand.Position}");
            if(FromCrouch)Log.Info($"OUT_SIGNAL OBSERVATION crouch entry maximum head step={largestHeadStep:F4} inches");
            Log.Info($"OUT_SIGNAL {(raised ? "PASS" : "FAIL")} actual hand raised above helmet");
            Log.Info($"OUT_SIGNAL detail showing={bot.ShowingOutSignal} body={bot.Body.Enabled} model={bot.Body.Model?.Name} collider={bot.Components.Get<BoxCollider>()?.Enabled} remaining={match.OpponentsRemaining}");
            var matchState=FinalElimination ? !match.InRound && match.Status=="VICTORY" && match.OpponentsRemaining==0 : match.InRound && match.OpponentsRemaining==2;
            Log.Info($"OUT_SIGNAL {(bot.ShowingOutSignal && bot.Body.Enabled && bot.Body.Sequence.Name==(FromCrouch ? "out_signal_crouch" : "out_signal") && collider.IsValid() && !collider.Enabled && matchState ? "PASS" : "FAIL")} visible signal, disabled collider, expected match state final={FinalElimination}");stage=2;
        }
        if(stage==2 && elapsed>2.5f)
        {
            Log.Info($"OUT_SIGNAL {(!bot.ShowingOutSignal && !bot.Body.Enabled ? "PASS" : "FAIL")} signal finishes and body clears");stage=3;
        }
    }
    private void MeasureEntry()
    {
        if(!FromCrouch || !bot.IsValid() || elapsed<.45f || elapsed>1.05f)return;
        if(!bot.Body.TryGetBoneTransform("Head",out var head))return;
        var relative=head.Position-bot.WorldPosition;
        if(previousHead is { } prior)largestHeadStep=System.MathF.Max(largestHeadStep,(relative-prior).Length);
        previousHead=relative;
    }
}
