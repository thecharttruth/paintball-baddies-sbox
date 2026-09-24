using System;

namespace PaintballBaddies;

/// <summary>Finite opponent ammunition with three-shot bursts and visible reload windows.</summary>
public sealed class OpponentMagazine
{
    public int Ammo { get; private set; } = 30;
    public int Reserve { get; private set; } = 120;
    public bool UnlimitedAmmo { get; set; }
    public int Reloads { get; private set; }
    public float ReloadRemaining { get; private set; }
    public float ReloadDuration { get; set; } = 2.2f;
    public float ActiveReloadDuration { get; private set; } = 2.2f;
    public float PauseRemaining { get; private set; }
    private int burstShots;
    public event Action ReloadStarted;
    public event Action ReloadCancelled;
    public void CancelReload()
    {
        if (ReloadRemaining<=0) return;
        ReloadRemaining=0;
        ReloadCancelled?.Invoke();
    }
    public bool BeginReload()
    {
        if(UnlimitedAmmo || ReloadRemaining>0 || Ammo>=30 || Reserve<=0)return false;
        ActiveReloadDuration=Math.Clamp(ReloadDuration,.1f,10f);
        ReloadRemaining=ActiveReloadDuration;
        ReloadStarted?.Invoke();return true;
    }

    public void Advance( float delta )
    {
        PauseRemaining = MathF.Max( 0, PauseRemaining - delta );
        if ( ReloadRemaining <= 0 ) return;
        ReloadRemaining = MathF.Max( 0, ReloadRemaining - delta );
        if ( ReloadRemaining > 0 ) return;
        int amount = Math.Min( 30 - Ammo, Reserve );
        Ammo += amount;
        Reserve -= amount;
        Reloads++;
        burstShots = 0;
    }
    public bool TryFire()
    {
        if ( ReloadRemaining > 0 || PauseRemaining > 0 || (!UnlimitedAmmo && Ammo <= 0) ) return false;
        if(!UnlimitedAmmo)Ammo--;
        burstShots++;
        PauseRemaining = PaintballFlight.ShotInterval;
        if ( burstShots >= 3 ) { burstShots = 0; PauseRemaining = BotCombatTuning.BurstRestSeconds; }
        if ( Ammo == 0 && Reserve > 0 ) BeginReload();
        return true;
    }
}
