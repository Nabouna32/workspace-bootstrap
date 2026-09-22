namespace BounaDevEnvironment;

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
            _ => "Échec du contrôle"
        };
        ActionLabel = item.ActionCode switch
        {
            "install" => "Installer",
            "update" => "Mettre à jour",
            "repair-config" => "Réparer la configuration",
            "repair" => "Réparer",
            "none" => "Aucune action",
            "version-unverified" => "Présence confirmée",
            _ => "Bloqué"
        };
    }

    public string Id { get; }
    public string Name { get; }
    public string StateLabel { get; }
    public string ActionLabel { get; }
}
