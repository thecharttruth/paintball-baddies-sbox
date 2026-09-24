using Sandbox;
namespace PaintballBaddies;

/// <summary>Official CC0 venue fixtures resolved by s&box's cloud asset pipeline.</summary>
public static class FoundryLibraryAssets
{
    // Publishing embeds the assets at these relative paths. A player does not
    // have the editor's cloud-package lookup cache, so prefer the shipped files.
    // Keep literal Cloud calls as fallbacks and publisher dependency declarations.
    public static Model WallLight => PackagedModel("models/props/industrial_wall_light/industrial_wall_light.vmdl",
        () => Cloud.Model("https://sbox.game/facepunch/industrial_wall_light"));
    public static Model ElectricalPanel => PackagedModel("models/props/electrical_panels_industrial/electrical_industrial_panel_1.vmdl",
        () => Cloud.Model("https://sbox.game/facepunch/electricalindustrialpanel1"));
    public static Material ResinFloor => Cloud.Material("https://sbox.game/facepunch/floorresinablend");
    public static Model VentGrille => PackagedModel("models/sbox_props/airducts/airduct_a_vent_64x32.vmdl",
        () => Cloud.Model("https://sbox.game/facepunch/airduct_a_vent_64x32"));
    public static Model Sandbag => PackagedModel("models/sbox_props/folding_construction_sign/sandbag.vmdl",
        () => Cloud.Model("https://sbox.game/facepunch/sandbag"));

    private static Model PackagedModel(string path, System.Func<Model> cloud)
    {
        var model=Model.Load(path);
        return model.IsValid() && !model.IsError ? model : cloud();
    }
}
