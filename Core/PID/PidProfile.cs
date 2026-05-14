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
}
