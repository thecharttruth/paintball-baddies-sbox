namespace PaintballBaddies;

/// <summary>Maps weapon-owned reload progress onto the native paintball presentation.</summary>
public sealed class CitizenReloadClock
{
    public const float PickupStart = .8f;
    public const float RecoveryStart = 3.2f;
    private float cancelElapsed;
    public float CancellationElapsed => cancelElapsed;

    public void Reset() => cancelElapsed = 0;

    public float Sample(float current, bool requested, bool cancelled, bool returning,
        bool podHeld, float reloadRemaining, float reloadProgress, float delta)
    {
        if (cancelled && !returning)
        {
            cancelElapsed += delta;
            return podHeld ? 2.6f+.36f*System.Math.Clamp(cancelElapsed/.5f,0,1) : RecoveryStart;
        }
        if (requested && reloadRemaining > 0)
            return PickupStart+System.Math.Clamp(reloadProgress,0,1)*2.4f;
        if (requested && !cancelled && current < RecoveryStart)
            return RecoveryStart;
        return current;
    }
}
