using System;
namespace PaintballBaddies;

/// <summary>Two distinct backward taps rotate the native view; a hold is ordinary movement.</summary>
public sealed class QuickTurnGesture
{
    public const float TapWindow=.32f;
    public const float Duration=.16f;
    private float sinceTap=TapWindow+1,elapsed=Duration;
    private bool armed,wasDown;
    public bool Turning => elapsed<Duration;
    public bool ConsumeBackward { get; private set; }
    public int Started { get; private set; }
    public int Completed { get; private set; }

    public void Reset(bool down=false)
    {
        armed=false;wasDown=down;sinceTap=TapWindow+1;
        elapsed=Duration;ConsumeBackward=false;
    }
    public float Advance(bool down,float delta,bool allowed)
    {
        if(!allowed){Reset(down);return 0;}
        delta=MathF.Max(0,delta);
        sinceTap+=delta;
        ConsumeBackward &= down;
        bool pressed=down&&!wasDown;
        wasDown=down;
        if(pressed&&!Turning)
        {
            if(armed&&sinceTap<=TapWindow)
            {
                armed=false;elapsed=0;ConsumeBackward=true;Started++;
            }
            else {armed=true;sinceTap=0;}
        }
        if(!Turning)return 0;
        float before=Ease(elapsed/Duration);
        elapsed=MathF.Min(Duration,elapsed+delta);
        if(!Turning)Completed++;
        return 180*(Ease(elapsed/Duration)-before);
    }
    private static float Ease(float t)=>t*t*(3-2*t);
}
