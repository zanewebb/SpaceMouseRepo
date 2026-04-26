namespace SpaceMouseRepo.Core.Behavior;

public sealed class ManipulationConfig
{
    public float RotationDegPerSec { get; set; } = 180f;
    public float TranslationMPerSec { get; set; } = 0.30f;
    public float MaxOffsetM { get; set; } = 0.15f;
    public float PrecisionScale { get; set; } = 0.2f;

    public bool InvertTx { get; set; }
    public bool InvertTy { get; set; }
    public bool InvertTz { get; set; }
    public bool InvertRx { get; set; }
    public bool InvertRy { get; set; }
    public bool InvertRz { get; set; }

    public ButtonAction Button1Action { get; set; } = ButtonAction.ResetRotation;
    public ButtonAction Button2Action { get; set; } = ButtonAction.TogglePrecisionMode;
}
