namespace WorkspaceControl.Domain;

public sealed record OptimizationPlanItem(
    string Id,
    string Name,
    string StatusCode,
    string Impact,
    string Risk,
    bool Reversible,
    string Description);
