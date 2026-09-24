using Sandbox;

namespace PaintballBaddies;

/// <summary>Opt-in native bounds report used to validate imported source units and collision.</summary>
public sealed class ModelImportChecks : Component
{
    protected override void OnStart()
    {
        foreach ( var slug in new[] { "foundry_low_bunker", "foundry_equipment_crate", "foundry_ventilation_unit" } )
        {
            var model = Model.Load( $"models/foundry/{slug}/{slug}.vmdl" );
            Log.Info( $"MODEL_IMPORT {slug} bounds={model.Bounds} size={model.Bounds.Size} center={model.Bounds.Center}" );
        }
    }
}
