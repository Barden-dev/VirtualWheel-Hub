using SimRacingHub.Models;

namespace SimRacingHub.Services;

public sealed record ProfileComparisonBaseline(ResolvedProfile Resolved, ProfileContext? Context, bool IsAvailable);
