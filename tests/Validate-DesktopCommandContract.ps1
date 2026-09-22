$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$repoRoot = $root
$desktop = Get-Content (Join-Path $repoRoot "bootstrap/windows/engine/Desktop.ps1") -Raw
$core = Get-Content (Join-Path $repoRoot "bootstrap/windows/dev-env-core.ps1") -Raw
$window = Get-Content (Join-Path $repoRoot "desktop/MainWindow.xaml.cs") -Raw
$xaml = Get-Content (Join-Path $repoRoot "desktop/MainWindow.xaml") -Raw
$viewModel = Get-Content (Join-Path $repoRoot "desktop/MainViewModel.cs") -Raw
$commands = Get-Content (Join-Path $repoRoot "desktop/AsyncCommand.cs") -Raw
$client = Get-Content (Join-Path $repoRoot "desktop/DesktopEngineClient.cs") -Raw
$models = Get-Content (Join-Path $repoRoot "desktop/DesktopCommandModels.cs") -Raw
$configModel = Get-Content (Join-Path $repoRoot "desktop/ConfigurationProfile.cs") -Raw
$configSchema = Get-Content (Join-Path $repoRoot "contracts/configuration-profile.schema.json") -Raw
$jobModels = Get-Content (Join-Path $repoRoot "desktop/JobModels.cs") -Raw
$jobStore = Get-Content (Join-Path $repoRoot "desktop/JobStore.cs") -Raw
$jobScheduler = Get-Content (Join-Path $repoRoot "desktop/JobScheduler.cs") -Raw
$desktopProject = Get-Content (Join-Path $repoRoot "desktop/BounaDevEnvironment.Desktop.csproj") -Raw
$schemaPath = Join-Path $repoRoot "contracts/desktop-command-response.schema.json"
$operationSchemaPath = Join-Path $repoRoot "contracts/desktop-provisioning-operation.schema.json"
$schema = Get-Content $schemaPath -Raw | ConvertFrom-Json
$operationSchema = Get-Content $operationSchemaPath -Raw | ConvertFrom-Json

$requiredDesktopTokens = @(
    "schemaVersion = 1",
    'command = $Command',
    'success = $Success',
    'exitCode = $ExitCode',
    "timestampUtc",
    'data = $Data',
    'messages = @($Messages)',
    'error = $ErrorMessage',
    "provisioning-profiles",
    "provisioning-plan",
    "optimization-plan-safe",
    "optimization-apply-safe",
    "optimization-rollback",
    'statusCode = $statusCode',
    '"applied"',
    '"compliant"',
    '"drifted"',
    '"pending"'
)
foreach ($token in $requiredDesktopTokens) {
    if ($desktop -notlike "*$token*") { throw "Desktop contract token missing: $token" }
}

if ($core -notlike '*Join-Path $Engine "Desktop.ps1"*') { throw "Desktop command engine is not loaded by dev-env-core.ps1." }
if ($core -notlike '*Invoke-DesktopCommand -Command $Command -ProfileId $Profile*') { throw "Core command dispatch does not forward the selected profile to the desktop contract." }
if ($window -notlike "*new DesktopEngineClient*") { throw "Desktop window does not compose the engine client into the view model." }
if ($xaml -like "*Click=*") { throw "Desktop XAML still contains code-behind click handlers." }
if ($window -like "*ExecuteAsync*") { throw "Desktop window code-behind must not call the engine directly." }
if ($viewModel -notlike "*DesktopEngineClient*") { throw "Desktop view model does not own engine orchestration." }
if ($viewModel -notlike "*IsBusy*") { throw "Desktop view model does not expose operation busy state." }
if ($commands -notlike "*ICommand*") { throw "Reusable asynchronous desktop command is missing." }
if ($client -notlike "*SchemaVersion*") { throw "Desktop engine client does not validate the contract version." }
if ($client -notlike "*ExitCode != process.ExitCode*") { throw "Desktop engine client does not validate process/contract exit code consistency." }
if ($models -notlike "*DesktopCommandResponse<T>*") { throw "Typed desktop command response model is missing." }
if ($desktop -notlike "*capabilities*") { throw "Desktop capability command is missing." }
if ($client -notlike "*GetCapabilitiesAsync*") { throw "Typed capability query client method is missing." }
if ($jobModels -notlike "*CapabilitySnapshot*") { throw "Capability snapshot model is missing." }
if ($jobModels -notlike "*GetSnapshotAsync*") { throw "Capability snapshot provider contract is missing." }
$capabilityProvider = Get-Content (Join-Path $repoRoot "desktop/DesktopCapabilitySnapshotProvider.cs") -Raw
if ($capabilityProvider -notlike "*DesktopCapabilitySnapshotProvider*") { throw "Desktop capability snapshot provider implementation is missing." }
if ($viewModel -notlike "*RecoverAndReconcileAsync*") { throw "Desktop startup does not reconcile the persistent job queue." }
if ($window -notlike "*DesktopCapabilitySnapshotProvider*") { throw "Desktop shell does not compose the capability provider." }
if ($jobModels -notlike "*JobDefinition*") { throw "Persistent job definition model is missing." }
if ($jobModels -notlike "*Requires*") { throw "Job capability requirements are missing." }
if ($jobModels -notlike "*Provides*") { throw "Job capability provisions are missing." }
if ($jobModels -notlike "*JobResourcePolicy*") { throw "Scheduler resource policy model is missing." }
if ($jobModels -notlike "*JsonConverter(typeof(JsonStringEnumConverter))*") { throw "Scheduler persisted enums must use stable JSON strings." }
if ($jobModels -notlike "*RequiresNetwork*") { throw "Job network requirement is missing." }
if ($jobModels -notlike "*Idempotent*") { throw "Job idempotency declaration is missing." }
if ($jobModels -notlike "*JobVersionPolicy*") { throw "Job version selection policy is missing." }
if ($jobModels -notlike "*VersionSelectionMode*") { throw "Job version selection modes are missing." }
if ($configSchema -notlike "*versionPolicy*") { throw "Configuration version policy schema is missing." }
if ($configModel -notlike "*ConfigurationComponent*") { throw "Configuration component model is missing." }
if ($jobModels -notlike "*WaitingForResource*") { throw "Job resource-waiting state is missing." }
if ($jobModels -notlike "*Paused*") { throw "Job paused state is missing." }
if ($jobModels -notlike "*Recoverable*") { throw "Job recoverable state is missing." }
if ($jobStore -notlike "*SqliteConnection*") { throw "SQLite job store implementation is missing." }
if ($jobStore -notlike "*CREATE TABLE IF NOT EXISTS jobs*") { throw "Persistent jobs table is missing." }
if ($jobStore -notlike "*RecoverInterruptedAsync*") { throw "Job recovery implementation is missing." }
if ($jobStore -notlike "*TryClaimAsync*") { throw "Atomic job claim implementation is missing." }
if ($jobStore -notlike "*CREATE TABLE IF NOT EXISTS resource_policy*") { throw "Persistent resource policy table is missing." }
if ($jobStore -notlike "*SetResourcePolicyAsync*") { throw "Persistent resource policy write path is missing." }
if ($jobStore -notlike "*Version mode*requires a value*") { throw "Persisted job version policy validation is missing." }
if ($jobStore -notlike "*GetResourcePolicyAsync*") { throw "Persistent resource policy read path is missing." }
if ($jobScheduler -notlike "*RecoverAndReconcileAsync*") { throw "Capability-aware scheduler recovery is missing." }
$jobRunner = Get-Content (Join-Path $repoRoot "desktop/JobRunner.cs") -Raw
if ($jobRunner -notlike "*class JobRunner*") { throw "Persistent job runner is missing." }
if ($jobRunner -notlike "*TryClaimAsync*") { throw "Job runner does not atomically claim work." }
if ($jobRunner -notlike "*IJobExecutor*") { throw "Job runner executor contract is missing." }
if ($jobRunner -notlike "*Provides*") { throw "Job runner does not verify provided capabilities." }
if ($jobRunner -notlike "*postcondition-failed*") { throw "Job postcondition verification failure state is missing." }

if ($jobScheduler -notlike "*WaitingForReboot*") { throw "Scheduler reboot state is missing." }
if ($jobScheduler -notlike "*WaitingForResource*") { throw "Scheduler resource policy gating is missing." }
if ($jobScheduler -notlike "*MarkPaused*") { throw "Scheduler pause state is missing." }
if ($jobScheduler -notlike "*JobStatus.Recoverable*") { throw "Scheduler recovery-required state is missing." }
if ($configModel -notlike "*ConfigurationProfile*") { throw "Configuration profile model is missing." }
if ($configModel -notlike "*ConfigurationProfileValidator*") { throw "Configuration profile validation is missing." }
if ($configSchema -notlike "*configuration profile*") { throw "Configuration profile schema validation marker is missing." }
if ($desktopProject -notlike "*Microsoft.Data.Sqlite*") { throw "Desktop project does not reference SQLite." }
if (-not (Test-Path (Join-Path $repoRoot "desktop/InventoryModels.cs"))) { throw "Inventory canonical model is missing." }
if (-not (Test-Path (Join-Path $repoRoot "desktop/WindowsRegistryUninstallInventoryProvider.cs"))) { throw "Windows registry inventory provider is missing." }
if (-not (Test-Path (Join-Path $repoRoot "desktop/InventoryScanner.cs"))) { throw "Inventory scanner is missing." }
if (-not (Test-Path (Join-Path $repoRoot "desktop/WinGetInventoryProvider.cs"))) { throw "WinGet inventory provider is missing." }
$inventoryModels = Get-Content (Join-Path $repoRoot "desktop/InventoryModels.cs") -Raw
$inventoryProvider = Get-Content (Join-Path $repoRoot "desktop/WindowsRegistryUninstallInventoryProvider.cs") -Raw
$inventoryScanner = Get-Content (Join-Path $repoRoot "desktop/InventoryScanner.cs") -Raw
$wingetInventoryProvider = Get-Content (Join-Path $repoRoot "desktop/WinGetInventoryProvider.cs") -Raw
if ($inventoryModels -notlike "*InventoryObservation*") { throw "Inventory observation model is missing." }
if ($inventoryModels -notlike "*InventoryOwnership*") { throw "Inventory ownership model is missing." }
if ($inventoryModels -notlike "*InventoryEvidence*") { throw "Inventory evidence model is missing." }
if ($inventoryModels -notlike "*IInventoryProvider*") { throw "Inventory provider contract is missing." }
if ($inventoryModels -notlike "*InventoryProviderDiagnostic*") { throw "Inventory provider diagnostics are missing." }
if ($inventoryModels -notlike "*InventoryProviderResult*") { throw "Inventory provider result contract is missing." }
if ($inventoryProvider -notlike "*RegistryKey.OpenBaseKey*") { throw "Registry inventory provider does not use explicit registry views." }
if ($inventoryProvider -notlike "*Registry64*" -or $inventoryProvider -notlike "*Registry32*") { throw "Registry inventory provider does not cover both registry views." }
if ($inventoryProvider -notlike "*UninstallString*") { throw "Registry inventory provider does not retain the authoritative uninstall mechanism." }
if ($inventoryProvider -notlike "*CreateInstanceIdentity*") { throw "Registry inventory provider must preserve installed instances separately." }
if ($inventoryProvider -notlike "*WindowsInstaller*") { throw "Registry inventory provider must retain MSI ownership evidence." }
if ($inventoryScanner -notlike "*InventoryOwnership.Unknown*") { throw "Cleanup analyzer must require known ownership." }
if ($inventoryScanner -notlike "*MarkOlderVersionSignals*") { throw "Inventory version-family signals are missing." }
if ($inventoryScanner -notlike "*InventoryVersion.TryParse*") { throw "Inventory version normalization is missing." }
if ($wingetInventoryProvider -notlike "*winget.exe*") { throw "WinGet inventory provider is missing the executable integration." }
if ($wingetInventoryProvider -notlike "*ParseListOutput*") { throw "WinGet inventory parser is missing." }
if ($wingetInventoryProvider -notlike "*--include-unknown*") { throw "WinGet inventory must include entries with unknown versions." }
if ($wingetInventoryProvider -notlike "*InventoryProviderDiagnostic*") { throw "WinGet inventory provider health reporting is missing." }
if ($inventoryModels -notlike "*RemovalCommand*") { throw "Inventory model does not retain removal provenance." }
if ($inventoryModels -notlike "*Publisher*") { throw "Inventory model does not retain publisher provenance." }


if ($inventoryScanner -notlike "*Deduplicate*") { throw "Inventory deduplication is missing." }
if ($inventoryScanner -notlike "*InventoryCleanupAnalyzer*") { throw "Inventory cleanup analyzer is missing." }
if ($viewModel -notlike "*InventoryCommand*") { throw "Desktop inventory command is missing." }
if ($viewModel -notlike "*IsInventoryVisible*") { throw "Desktop inventory page visibility is missing." }
if ($xaml -notlike "*InventorySnapshot.Items*") { throw "Desktop inventory page does not render inventory items." }
if ($xaml -notlike "*InventoryRecommendationItems*") { throw "Desktop inventory page does not render cleanup recommendations." }
if ($viewModel -notlike "*InventoryScanCommand*") { throw "Desktop inventory scan command is missing." }
if ($xaml -notlike "*InventoryScanCommand*") { throw "Desktop inventory rescan button is not wired to the scan command." }
if ($viewModel -notlike "*InventoryProviderHealthSummary*") { throw "Inventory provider health is not surfaced in the desktop UI." }


if ($models -notlike "*ProvisioningPlanData*") { throw "Typed provisioning plan model is missing." }
if ($models -notlike "*ProvisioningOperationState*") { throw "Typed provisioning operation model is missing." }
if ($models -notlike "*ProvisioningOperationHistoryItem*") { throw "Typed provisioning history model is missing." }
if ($models -notlike "*ProvisioningOperationDetail*") { throw "Typed provisioning detail model is missing." }
if ($client -notlike "*StartProvisioningAsync*") { throw "Typed provisioning start client method is missing." }
if ($client -notlike "*GetProvisioningStatusAsync*") { throw "Typed provisioning status client method is missing." }
if ($client -notlike "*ResumeProvisioningAsync*") { throw "Typed provisioning resume client method is missing." }
if ($client -notlike "*GetProvisioningRecoveryAsync*") { throw "Typed provisioning recovery client method is missing." }
if ($client -notlike "*GetProvisioningHistoryAsync*") { throw "Typed provisioning history client method is missing." }
if ($client -notlike "*GetProvisioningDetailAsync*") { throw "Typed provisioning detail client method is missing." }
if ($viewModel -notlike "*PollProvisioningOperationAsync*") { throw "Desktop view model does not poll operation progress." }
if ($viewModel -notlike "*ResumeProvisioningAsync*") { throw "Desktop view model does not expose provisioning resume orchestration." }
if ($viewModel -notlike "*RecoverProvisioningOperationAsync*") { throw "Desktop view model does not recover provisioning operations on startup." }
if ($core -notlike "*OperationWorker*") { throw "Provisioning worker entry point is missing." }
if ($core -notlike "*nextIndex*") { throw "Provisioning worker does not persist its resume index." }
if ($core -notlike "*workerPid*") { throw "Provisioning worker does not expose worker liveness state." }
if ($core -notlike '*runId = $run.Id*') { throw "Provisioning worker does not persist its run identity." }
if ($desktop -notlike "*provisioning-start*") { throw "Desktop provisioning start command is missing." }
if ($desktop -notlike "*provisioning-status*") { throw "Desktop provisioning status command is missing." }
if ($desktop -notlike "*provisioning-resume*") { throw "Desktop provisioning resume command is missing." }
if ($desktop -notlike "*provisioning-recovery*") { throw "Desktop provisioning recovery command is missing." }
if ($desktop -notlike "*provisioning-history*") { throw "Desktop provisioning history command is missing." }
if ($desktop -notlike "*provisioning-detail*") { throw "Desktop provisioning detail command is missing." }
if ($viewModel -notlike "*provisioning-plan*") { throw "Desktop view model does not consume the provisioning plan contract." }
if ($xaml -notlike "*ProvisioningPlanItems*") { throw "Desktop provisioning view does not render structured plan items." }
if ($xaml -notlike "*ProvisioningDetailCommand*") { throw "Desktop provisioning view does not expose operation details." }
if ($client -notlike "*profileId*") { throw "Desktop engine client does not support profile-scoped commands." }
if (-not (Test-Path -LiteralPath $schemaPath -PathType Leaf)) { throw "Desktop command JSON schema is missing." }
if (-not (Test-Path -LiteralPath $operationSchemaPath -PathType Leaf)) { throw "Desktop provisioning operation JSON schema is missing." }
foreach ($property in @("schemaVersion","operationId","status","phase","profileId","completed","total","percent","messageKey","canResume","updatedAt")) {
    if (-not $operationSchema.required.Contains($property)) { throw "Provisioning operation schema required property missing: $property" }
}
if ([int]$operationSchema.properties.schemaVersion.const -ne 1) { throw "Provisioning operation schema version must remain 1." }
if (-not ($operationSchema.properties.status.enum -contains "reboot-required")) { throw "Provisioning operation schema must model reboot-required state." }
foreach ($property in @("schemaVersion","command","success","exitCode","timestampUtc","data","messages","error")) {
    if (-not $schema.required.Contains($property)) { throw "Schema required property missing: $property" }
}
if ([int]$schema.properties.schemaVersion.const -ne 1) { throw "Desktop schema version must remain 1." }
if ($window -like '*GetProperty("status")*') { throw "Desktop UI still depends on localized optimization status text." }

if ($viewModel -notlike "*_jobStore.InitializeAsync*") { throw "Desktop startup does not initialize the persistent job store." }
Write-Host "Desktop command contract and job queue validation: OK."