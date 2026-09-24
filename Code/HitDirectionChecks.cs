using Sandbox;
namespace PaintballBaddies;
public sealed class HitDirectionChecks : Component
{
    private float elapsed;
    private int stage;
    private PlayerController player;
    private PaintballCombatant state;
    private void Check(string name,bool pass)=>Log.Info($"HIT_DIRECTION {(pass?"PASS":"FAIL")} {name}");
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;
        if(stage==0 && elapsed>.5f)
        {
            player=Scene.GetAllComponents<PlayerController>().First();
            var match=player.Components.Get<ArenaMatch>();match.StartRound();state=match.PlayerState;
            player.UseLookControls=false;player.EyeAngles=new Angles(0,0,0);
            foreach(var entry in new[]{(Vector3.Forward,"AHEAD"),(Vector3.Backward,"BEHIND"),(Rotation.Identity.Right,"RIGHT"),(-Rotation.Identity.Right,"LEFT")})
            {
                state.ResetPaintHits();state.RegisterHit(1,player.WorldPosition+entry.Item1*100);
                Check(entry.Item2,state.HitDirectionLabel(Rotation.Identity)==entry.Item2);
            }
            var before=state.LastHitDirection;
            Check("friendly hit preserves feedback",!state.RegisterHit(0,player.WorldPosition+Vector3.Forward*100) && state.LastHitDirection==before);
            state.ResetPaintHits();Check("reset clears direction",state.LastHitDirection==Vector3.Zero && state.HitDirectionLabel(Rotation.Identity)=="");
            stage++;
        }
        if(stage==1 && elapsed>1)
        {
            var shot=new GameObject(true,"Directional projectile test");shot.WorldPosition=player.WorldPosition+Vector3.Backward*70+Vector3.Up*40;
            var ball=shot.Components.Create<OpponentPaintball>();ball.Shooter=shot;ball.Velocity=Vector3.Forward*2283;
            stage++;
        }
        if(stage==2 && elapsed>1.15f)
        {
            Check("physical shot reports behind",state.PaintHits==1 && state.HitDirectionLabel(Rotation.Identity)=="BEHIND");
            Check("view rotation updates direction",state.HitDirectionLabel(Rotation.FromYaw(180))=="AHEAD");stage++;
        }
        if(stage==3 && elapsed>1.8f){Check("feedback expires",state.HitDirectionLabel(Rotation.Identity)=="");stage++;}
    }
}
