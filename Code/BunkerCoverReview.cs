using Sandbox;
namespace PaintballBaddies;
public sealed class BunkerCoverReview : Component
{
 PlayerController player;CoverController cover;float t;int phase;
 protected override void OnUpdate(){t+=Time.Delta;if(t<1)return;
 if(phase==0){player=Scene.GetAllComponents<PlayerController>().First();cover=player.Components.Get<CoverController>();cover.Leave();player.WorldPosition=new(-565,-260,1);player.EyeAngles=new(0,0,0);player.UseLookControls=false;cover.TestInput=true;phase=1;t=0;}
 else if(phase==1){Log.Info("BUNKER_ATTACH "+cover.TryEnter());phase=2;t=0;}
 else if(phase==2&&t>2){cover.TestAim=true;phase=3;t=0;}
 else if(phase==3&&t>1.5f){var w=player.Components.Get<PaintballMarker>();var tr=Scene.Trace.Sphere(.35f,w.Muzzle,w.Muzzle+Vector3.Forward*100).IgnoreGameObjectHierarchy(player.GameObject).WithoutTags("paintball_debris").WithSurfaceMeshes().Run();Log.Info("BUNKER_AIM "+Json.Serialize(new{cover.Peeking,player.IsDucking,Muzzle=w.Muzzle.ToString(),Clear=!tr.Hit,Obstacle=tr.GameObject?.Name}));w.FireAt(new Vector3(1168,-260,60));phase=4;t=0;}
 else if(phase==4&&t>2){Log.Info("BUNKER_SHOT "+player.Components.Get<PaintballMarker>().LastImpactObject);cover.TestAim=false;phase=5;t=0;}
 else if(phase==5){player.EyeAngles=new(0,90,0);var vault=player.Components.Get<VaultController>();Log.Info("BUNKER_VAULT begin="+vault.TryBegin()+" reason="+vault.BlockReason);phase=6;t=0;}
 else if(phase==6&&t>2){var v=player.Components.Get<VaultController>();Log.Info("BUNKER_VAULT "+Json.Serialize(new{v.Completed,v.Outcome,player.UseInputControls,player.Body.MotionEnabled}));phase=7;}
 }
 protected override void OnDestroy(){if(cover.IsValid()){cover.Leave();cover.TestInput=false;}if(player.IsValid()){player.Components.Get<VaultController>()?.Cancel();player.UseInputControls=player.UseLookControls=true;}}
}
