using WorkspaceBootstrap;

namespace WorkspaceControl.Application.Contracts;

public interface IOptimizationService
{
    IReadOnlyList<OptimizationPlanItem> GetSafePlan();
    IReadOnlyList<string> ApplySafe();
    IReadOnlyList<string> Rollback();
}
