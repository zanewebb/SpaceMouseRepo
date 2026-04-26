namespace SpaceMouseRepo.Core.Input;

public readonly struct SpaceMouseState
{
    public static readonly SpaceMouseState Empty = default;

    public float Tx { get; }
    public float Ty { get; }
    public float Tz { get; }
    public float Rx { get; }
    public float Ry { get; }
    public float Rz { get; }
    public bool Button1 { get; }
    public bool Button2 { get; }

    public SpaceMouseState(float tx, float ty, float tz, float rx, float ry, float rz, bool button1, bool button2)
    {
        Tx = tx; Ty = ty; Tz = tz;
        Rx = rx; Ry = ry; Rz = rz;
        Button1 = button1; Button2 = button2;
    }
}
