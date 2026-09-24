using Sandbox;
namespace PaintballBaddies;
public sealed class ShieldVaultReview : Component
{
    float elapsed;int stage,shots;PlayerController player;ArenaShield shield;GameObject shooter,obstacle;Vector3 spawn;
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;if(elapsed<1)return;elapsed=0;
        if(stage==0)
        {
            player=Scene.GetAllComponents<PlayerController>().First();player.UseInputControls=player.UseLookControls=false;
            player.WorldPosition=new(-650,-700,1);player.EyeAngles=new(0,0,0);
            shield=Scene.GetAllComponents<ArenaShield>().First();shield.WorldPosition=player.WorldPosition+new Vector3(25,0,24);spawn=shield.WorldPosition;
            shooter=new GameObject(true,"Shield review shooter");stage=1;return;
        }
        if(stage==1)
        {
            if(shots==0)Log.Info("SHIELD_PICKUP "+(shield.Owner==player.Components.Get<PaintballCombatant>()));
            if(shots<ArenaShield.Capacity)
            {
                var target=shield.WorldPosition;var from=target+Vector3.Forward*150;
                var go=new GameObject(true,"Shield review projectile");go.WorldPosition=from;
                var ball=go.Components.Create<OpponentPaintball>();ball.Shooter=shooter;ball.Team=9;ball.Velocity=PaintballFlight.LaunchVelocity(from,target);shots++;return;
            }
            Log.Info("SHIELD_BREAK "+Json.Serialize(new{shield.Remaining,Detached=!shield.Owner.IsValid(),Hits=player.Components.Get<PaintballCombatant>().PaintHits,Count=Scene.GetAllComponents<ArenaShield>().Count()}));stage=2;elapsed=-4;return;
        }
        if(stage==2)
        {
            Log.Info("SHIELD_RESPAWN "+Json.Serialize(new{shield.Remaining,Unowned=!shield.Owner.IsValid(),Moved=(shield.WorldPosition-spawn).Length>180,Count=Scene.GetAllComponents<ArenaShield>().Count()}));
            obstacle=TrainingRange.Box(null,"Vault direct review",new(-250,-700,22),new(35,140,44),Color.Gray,true);obstacle.Components.Create<CoverSurface>();
            player.WorldPosition=new(-310,-700,1);player.EyeAngles=new(0,0,0);player.Body.Velocity=Vector3.Zero;stage=3;return;
        }
        if(stage==3)
        {
            Log.Info("VAULT_DIRECT begin="+player.Components.Get<VaultController>().TryBegin()+" attached="+player.Components.Get<CoverController>().Attached);stage=4;elapsed=-1;return;
        }
        if(stage==4)
        {
            var vault=player.Components.Get<VaultController>();Log.Info("VAULT_DIRECT "+Json.Serialize(new{vault.Completed,vault.Outcome,player.Body.MotionEnabled,Position=player.WorldPosition.ToString()}));stage=5;
        }
        if(stage==5)
        {
            player.WorldPosition=new(-310,-700,1);player.EyeAngles=new(0,0,0);player.Body.Velocity=Vector3.Zero;
            player.UseInputControls=true;shield.Collect(player.Components.Get<PaintballCombatant>());stage=6;
        }
    }
    protected override void OnDestroy(){shooter?.Destroy();obstacle?.Destroy();if(player.IsValid()){player.Components.Get<VaultController>()?.Cancel();player.UseInputControls=player.UseLookControls=true;}}
}
