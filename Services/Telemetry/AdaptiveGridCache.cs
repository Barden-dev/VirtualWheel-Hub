namespace SimRacingHub.Services.Telemetry
{
    public enum HelperKind
    {
        Abs,
        Tc
    }

    public sealed class AdaptiveGridCache
    {
        public static void FlushIfActive() { }
        public static void ShutdownIfActive() { }
        public static string? CurrentCarIfActive => null;
        public static bool ResetCurrentCarIfActive(HelperKind kind) => false;
        public static void ResetAll(HelperKind kind) { }
        public static string KindName(HelperKind kind) => kind == HelperKind.Abs ? "ABS" : "TC";
        public static string RootDirectory(HelperKind kind) => string.Empty;
    }
}
