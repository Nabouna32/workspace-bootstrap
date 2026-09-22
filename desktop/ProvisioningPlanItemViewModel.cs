namespace WorkspaceBootstrap.Desktop;

public sealed class ProvisioningPlanItemViewModel
{
    public ProvisioningPlanItemViewModel(ProvisioningPlanItem item)
    {
        Id = item.Id;
        Name = item.Name;
        StateLabel = item.StateCode switch
        {
            "CURRENT" => "À jour",
            "INSTALLED" => "Installé — version non vérifiée",
            "MISSING" => "Absent",
            "OUTDATED" => "Mise à jour disponible",
            "CONFIG-INCOMPLETE" => "Configuration incomplète",
            "REPAIRABLE" => "Réparation possible",
            "UNKNOWN" => "État non vérifié",
            _ => "État inconnu"
        };
        ActionLabel = item.ActionCode switch
        {
            "install" => "Installer",
            "update" => "Mettre à jour",
            "repair-config" => "Réparer la configuration",
            "repair" => "Réparer",
            "none" => "Aucune action",
            "version-unverified" => "Vérifier avant installation",
            _ => "À vérifier"
        };
    }

    public string Id { get; }
    public string Name { get; }
    public string StateLabel { get; }
    public string ActionLabel { get; }
}
