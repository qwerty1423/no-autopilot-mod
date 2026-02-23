namespace NOAutopilot.ACLS;

public class PIDConfig
{
    public float Kp { get; set; }

    public float Ki { get; set; }

    public float Kd { get; set; }

    public float BufferDuration { get; set; }

    public bool Invert { get; set; }
}
