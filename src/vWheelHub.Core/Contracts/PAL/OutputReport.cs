namespace SimRacingHub.Core;

public readonly record struct OutputButtons(ulong Low = 0, ulong High = 0)
{
    public bool IsDown(int button) => button is >= 1 and <= 64 ? (Low & (1UL << (button - 1))) != 0 :
        button is >= 65 and <= 128 && (High & (1UL << (button - 65))) != 0;
    public OutputButtons With(int button) => button is >= 1 and <= 64 ? this with { Low = Low | (1UL << (button - 1)) } :
        button is >= 65 and <= 128 ? this with { High = High | (1UL << (button - 65)) } : this;
    public static OutputButtons Gears(bool down, bool up) => new((down ? 1UL : 0) | (up ? 2UL : 0));
}

public readonly record struct OutputReport(int Steer, int Gas, int Brake, OutputButtons Buttons);
