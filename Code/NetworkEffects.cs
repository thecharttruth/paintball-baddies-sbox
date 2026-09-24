using Sandbox;
using System;
namespace PaintballBaddies;

public static class NetworkEffects
{
    [Rpc.Broadcast(NetFlags.HostOnly)]
    public static async void BankSecure(Vector3 position,int amount)
    {
        var scene=Game.ActiveScene;
        await GameTask.MainThread();
        if(!scene.IsValid() || scene!=Game.ActiveScene || MultiplayerSession.Find(scene)?.IsHeist!=true)return;
        HeistBankBurst.Spawn(scene,position,amount);
    }
    [Rpc.Broadcast(NetFlags.HostOnly)]
    public static async void ShieldBreak(Transform transform,Vector3 direction,int seed)
    {
        var scene=Game.ActiveScene;
        await GameTask.MainThread();
        if(!scene.IsValid() || scene!=Game.ActiveScene)return;
        ShieldBreakEffect.Spawn(scene,transform,direction,seed);
    }
    public static void SpawnBall(GameObject shooter,int team,Vector3 origin,Vector3 velocity)
    {
        var go=new GameObject(false,"Network paintball"){WorldPosition=origin};
        var ball=go.Components.Create<OpponentPaintball>();ball.Shooter=shooter;ball.Team=team;ball.Velocity=velocity;
        go.Enabled=true;
        if(MultiplayerSession.Online){go.NetworkMode=NetworkMode.Object;go.Network.SetOwnerTransfer(OwnerTransfer.Fixed);go.NetworkSpawn();}
        else go.NetworkMode=NetworkMode.Never;
    }
    [Rpc.Broadcast(NetFlags.HostOnly)]
    public static async void Sound(string path,Vector3 position,Guid alreadyPlayed)
    {
        var scene=Game.ActiveScene;
        await GameTask.MainThread();
        if(!scene.IsValid() || scene!=Game.ActiveScene)return;
        if(Connection.Local.Id==alreadyPlayed)return;
        Sandbox.Sound.Play(path,position==Vector3.Zero ? scene.Camera?.WorldPosition ?? position : position);
    }
    [Rpc.Broadcast(NetFlags.HostOnly)]
    public static async void WorldPaint(int id,Vector3 point,Vector3 normal,Color tint)
    {
        var scene=Game.ActiveScene;
        await GameTask.MainThread();
        if(!scene.IsValid() || scene!=Game.ActiveScene)return;
        DrawPaint(id,point,normal,tint,true);
    }
    internal static void DrawPaint(int id,Vector3 point,Vector3 normal,Color tint,bool fresh,float age=0)
    {
        var scene=Game.ActiveScene;
        if(MultiplayerSession.Find(scene)?.ClaimPaint(id)!=true)return;
        var hit=scene.Trace.Ray(point+normal*6,point-normal*16).WithSurfaceMeshes().WithoutTags("paintball_debris","paintball_actor").Run();
        if(hit.Hit)PaintImpactSystem.Find(scene)?.SpawnLocal(hit,tint,fresh,age);
    }
    [Rpc.Broadcast(NetFlags.HostOnly)]
    public static async void ClearPaint()
    {
        var scene=Game.ActiveScene;
        await GameTask.MainThread();
        if(!scene.IsValid() || scene!=Game.ActiveScene)return;
        MultiplayerSession.Find(scene)?.ResetWorldPaint();
        PaintImpactSystem.Find(scene)?.Clear();
        foreach(var paint in scene.GetAllComponents<CharacterPaint>())paint.Clear();
    }
}
