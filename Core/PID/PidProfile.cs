using System;

namespace NOAutopilot.Core.PID;

[Serializable]
public class PidProfile
{
    public string Alt;
    public string Vs;
    public string Pitch;
    public string Roll;
    public string RollRate;
    public string Crs;
    public string Spd;
    public string Gcas;

    public string SchedPitch;
    public string SchedRollRate;
    public string SchedVs;
    public string SchedSpd;

    public bool IsEmpty()
    {
        return string.IsNullOrEmpty(Alt) && string.IsNullOrEmpty(Vs) && string.IsNullOrEmpty(Pitch) &&
        string.IsNullOrEmpty(Roll) && string.IsNullOrEmpty(RollRate) && string.IsNullOrEmpty(Crs) &&
        string.IsNullOrEmpty(Spd) && string.IsNullOrEmpty(Gcas) &&
        string.IsNullOrEmpty(SchedPitch) && string.IsNullOrEmpty(SchedRollRate) &&
        string.IsNullOrEmpty(SchedVs) && string.IsNullOrEmpty(SchedSpd);
    }
}
