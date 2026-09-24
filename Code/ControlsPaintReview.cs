using Sandbox;
namespace PaintballBaddies;
public sealed class ControlsPaintReview : Component
{
    [Property] public string CaptureId { get; set; }="";
    [Property] public bool ShowMenu { get; set; }
    private int frame;
    private PaintballControls controls;
    protected override void OnUpdate()
    {
        frame++;
        if(frame==30)
        {
            var model=Scene.GetAllComponents<ArenaOpponent>().First(x=>x.WorldPosition.z>900);
            model.Components.Create<PaintballCombatant>().Team=1;
            var collider=model.Components.Create<BoxCollider>();collider.Scale=new Vector3(28,28,66);collider.Center=new Vector3(0,0,33);
        }
        if(frame!=90)return;
        controls=Scene.GetAllComponents<PaintballControls>().First();
        var old=controls.Bindings["cover"];
        var other=controls.Bindings["vault"];
        var rebound=controls.Rebind("cover",other) && controls.Bindings["vault"]==old;
        controls.Rebind("cover",old);
        var restored=controls.Bindings["cover"]==old && controls.Bindings["vault"]==other;
        var actor=Scene.GetAllComponents<ArenaOpponent>().First(x=>x.WorldPosition.z>900);
        var impacts=PaintImpactSystem.Find(Scene);
        int hits=0;
        for(int i=0;i<16;i++)
        {
            var spot=actor.WorldPosition+Vector3.Up*(35+i%4*5)+Vector3.Right*(i%3-1)*3;
            var ray=Scene.Trace.Ray(spot-Vector3.Forward*100,spot+Vector3.Forward*10).Run();
            if(ray.Hit && PaintballCombatant.Find(ray.GameObject)==actor.Components.Get<PaintballCombatant>())
            {impacts.Spawn(ray,new Color(1,.12f,.6f));hits++;}
        }
        var audio=Sound.Play("sounds/paintball/marker.sound",Scene.Camera.WorldPosition+Scene.Camera.WorldRotation.Forward*20);
        var definition=ResourceLibrary.Get<SoundEvent>("sounds/paintball/marker.sound");
        Log.Info("CONTROLS_PAINT_REVIEW "+Json.Serialize(new {capture_id=CaptureId,rebound,restored,hits,
            marks=actor.Components.Get<CharacterPaint>()?.Count,master_volume=Sound.MasterVolume,
            sound_valid=audio.IsValid,sound_volume=audio.Volume,event_volume=definition.Volume.FixedValue,event_sounds=definition.Sounds,listener=Sound.Listener.Position,
            camera=Scene.Camera.WorldPosition,flight_speed=PaintballFlight.Speed,flight_lifetime=PaintballFlight.Lifetime}));
        if(ShowMenu)controls.Toggle();
    }
    protected override void OnDestroy(){if(controls.IsValid() && controls.Open)controls.Toggle();}
}
