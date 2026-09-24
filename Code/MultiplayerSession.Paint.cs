using Sandbox;
using System;
namespace PaintballBaddies;

public sealed partial class MultiplayerSession
{
    public sealed class WorldMark
    {
        public int Id {get;set;}
        public Vector3 Point {get;set;}
        public Vector3 Normal {get;set;}
        public Color Tint {get;set;}
        public float Time {get;set;}
        public float Age {get;set;}
    }
    private readonly List<WorldMark> worldMarks=new();
    private readonly HashSet<int> drawnMarks=new();
    private int markSequence;
    public int RememberedPaint=>worldMarks.Count;
    internal void AddWorldPaint(Vector3 point,Vector3 normal,Color tint)
    {
        var mark=new WorldMark{Id=++markSequence,Point=point,Normal=normal,Tint=tint,Time=Time.Now};
        worldMarks.Add(mark);
        worldMarks.RemoveAll(x=>Time.Now-x.Time>PaintImpactSystem.Duration);
        while(worldMarks.Count>PaintImpactSystem.Capacity)worldMarks.RemoveAt(0);
        NetworkEffects.WorldPaint(mark.Id,point,normal,tint);
    }
    internal string PaintSnapshot()=>Json.Serialize(worldMarks.Where(x=>Time.Now-x.Time<PaintImpactSystem.Duration).Select(x=>new WorldMark{Id=x.Id,Point=x.Point,Normal=x.Normal,Tint=x.Tint,Age=Time.Now-x.Time}).ToArray());
    internal bool ClaimPaint(int id){drawnMarks.RemoveWhere(x=>x<id-PaintImpactSystem.Capacity*2);return drawnMarks.Add(id);}
    internal void ResetWorldPaint(){worldMarks.Clear();drawnMarks.Clear();}
    internal void ReplayPaint(string json)
    {
        foreach(var mark in Json.Deserialize<WorldMark[]>(json))
            NetworkEffects.DrawPaint(mark.Id,mark.Point,mark.Normal,mark.Tint,false,mark.Age);
    }
}
