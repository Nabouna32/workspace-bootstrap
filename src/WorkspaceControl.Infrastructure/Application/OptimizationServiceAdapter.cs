using WorkspaceControl.Application.Contracts;

namespace WorkspaceBootstrap.Application;

internal sealed class OptimizationServiceAdapter : IOptimizationService
{
    private readonly WindowsOptimizationService _service = new();

    public IReadOnlyList<OptimizationPlanItem> GetSafePlan() => _service.GetSafePlan();
    public IReadOnlyList<string> ApplySafe() => _service.ApplySafe();
    public IReadOnlyList<string> Rollback() => _service.Rollback();
}
