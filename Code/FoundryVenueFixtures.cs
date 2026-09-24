using Sandbox;
namespace PaintballBaddies;

/// <summary>Low-cost official industrial fixtures outside playable cover lanes.</summary>
public sealed class FoundryVenueFixtures : Component
{
    private GameObject fixtures;
    protected override async void OnStart()
    {
        var light = FoundryLibraryAssets.WallLight;
        var panel = FoundryLibraryAssets.ElectricalPanel;
        var vent = FoundryLibraryAssets.VentGrille;
        if(light.IsError)light=await Cloud.Load<Model>("facepunch.industrial_wall_light");
        if(panel.IsError)panel=await Cloud.Load<Model>("facepunch.electricalindustrialpanel1");
        if(vent.IsError)vent=await Cloud.Load<Model>("facepunch.airduct_a_vent_64x32");
        if(!GameObject.IsValid())return;
        Log.Info( $"VENUE_VENT valid={vent.IsValid()} bounds={vent.Bounds}" );
        Log.Info( $"VENUE_FIXTURES light={light.IsValid()} {light.Name} bounds={light.Bounds} panel={panel.IsValid()} {panel.Name} bounds={panel.Bounds}" );
        if ( !light.IsValid() || light.IsError || !panel.IsValid() || panel.IsError ) return;
        fixtures = new GameObject { NetworkMode=NetworkMode.Never, Name = "Industrial venue fixtures" };
        foreach ( var y in new[] { -814f, 814f } )
        {
            var rotation = Rotation.FromYaw( y < 0 ? 90 : -90 );
            foreach ( var x in new[] { -950f, -475f, 0f, 475f, 950f } )
                Place( light, "Caged industrial wall light", new Vector3( x, y, 160 ), rotation * Rotation.FromYaw( 180 ) );
            Place( panel, "Venue electrical distribution", new Vector3( -1030, y, 65 ), rotation );
            if ( vent.IsValid() && !vent.IsError )
                foreach ( var x in new[] { -720f, 240f, 720f } )
                    Place( vent, "High wall ventilation grille", new Vector3( x, y, 184 ), rotation * Rotation.FromPitch( 90 ) );
        }
    }
    private void Place( Model model, string name, Vector3 position, Rotation rotation )
    {
        var fixture = new GameObject( fixtures ) { Name = name };
        fixture.WorldPosition = position;
        fixture.WorldRotation = rotation;
        fixture.Components.Create<ModelRenderer>().Model = model;
    }
    protected override void OnDestroy() => fixtures?.Destroy();
}
