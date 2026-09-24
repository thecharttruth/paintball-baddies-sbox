using Sandbox;
namespace PaintballBaddies;

/// <summary>Native 2D events over compiled audio, without a second native sound-event wrapper.</summary>
public static class HeistAudio
{
    private static SoundEvent Cue(string path,float volume)=>new()
    {
        Sounds=[SoundFile.Load(path)],UI=true,Volume=volume,Pitch=1,
        DistanceAttenuation=false,OcclusionEnabled=false,AirAbsorption=false,ReverbEnabled=false
    };
    private static readonly SoundEvent collect=Cue("sounds/heist/collect.vsnd",.24f);
    private static readonly SoundEvent deposit=Cue("sounds/heist/deposit.vsnd",.32f);
    private static readonly SoundEvent spill=Cue("sounds/heist/spill.vsnd",.22f);
    private static readonly SoundEvent bankMove=Cue("sounds/heist/bank_move.vsnd",.20f);
    public static SoundEvent Get(string kind)=>kind switch
    {
        "collect"=>collect,"bank" or "deposit"=>deposit,"spill"=>spill,"bank_move"=>bankMove,_=>null
    };
    public static SoundHandle Play(string kind,Vector3 position)=>Get(kind) is {} cue ? Sound.Play(cue,position) : null;
}
