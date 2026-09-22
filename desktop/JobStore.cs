using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;

namespace BounaDevEnvironment;

public sealed class JobStore : IJobExecutionStateWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _connectionString;

    public JobStore(string? databasePath = null)
    {
        var path = databasePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BounaDevEnvironment",
            "jobs",
            "jobs.db");

        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Unable to determine the job database directory.");

        Directory.CreateDirectory(directory);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL;";
        await pragma.ExecuteNonQueryAsync(cancellationToken);

        await using var busyTimeout = connection.CreateCommand();
        busyTimeout.CommandText = "PRAGMA busy_timeout=5000;";
        await busyTimeout.ExecuteNonQueryAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS jobs (
                id TEXT NOT NULL PRIMARY KEY,
                type TEXT NOT NULL,
                definition_json TEXT NOT NULL,
                status TEXT NOT NULL,
                attempts INTEGER NOT NULL,
                progress REAL NOT NULL,
                message_key TEXT NULL,
                error TEXT NULL,
                worker_id TEXT NULL,
                execution_id TEXT NULL,
                external_operation_id TEXT NULL,
                executor_state_json TEXT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_jobs_status_priority
                ON jobs(status, updated_at);

            CREATE TABLE IF NOT EXISTS resource_policy (
                id INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
                policy_json TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        // Keep the queue forward-compatible with databases created by older builds.
        await EnsureColumnAsync(connection, "jobs", "execution_id", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "jobs", "external_operation_id", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "jobs", "executor_state_json", "TEXT NULL", cancellationToken);
    }

    public async Task<JobResourcePolicy> GetResourcePolicyAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT policy_json FROM resource_policy WHERE id = 1;";

        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is null or DBNull)
            return new JobResourcePolicy();

        return JsonSerializer.Deserialize<JobResourcePolicy>((string)value, JsonOptions)
            ?? throw new InvalidDataException("Stored resource policy is invalid.");
    }

    public async Task SetResourcePolicyAsync(
        JobResourcePolicy policy,
        CancellationToken cancellationToken = default)
    {
        ValidateResourcePolicy(policy);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO resource_policy (id, policy_json, updated_at)
            VALUES (1, $policy, $updated)
            ON CONFLICT(id) DO UPDATE SET
                policy_json = excluded.policy_json,
                updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$policy", JsonSerializer.Serialize(policy, JsonOptions));
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task EnqueueAsync(JobEnqueueRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateDefinition(request.Definition);

        var now = DateTimeOffset.UtcNow;
        var definitionJson = JsonSerializer.Serialize(request.Definition, JsonOptions);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO jobs (
                id, type, definition_json, status, attempts, progress,
                message_key, error, worker_id, execution_id, external_operation_id,
                executor_state_json, created_at, updated_at
            )
            VALUES ($id, $type, $definition, $status, 0, 0, NULL, NULL, NULL, NULL, NULL, NULL, $created, $updated);
            """;
        command.Parameters.AddWithValue("$id", request.Definition.Id);
        command.Parameters.AddWithValue("$type", request.Definition.Type);
        command.Parameters.AddWithValue("$definition", definitionJson);
        command.Parameters.AddWithValue("$status", JobStatus.Queued.ToString());
        command.Parameters.AddWithValue("$created", now.ToString("O"));
        command.Parameters.AddWithValue("$updated", now.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, definition_json, status, attempts, progress,
                   message_key, error, worker_id, execution_id, external_operation_id,
                   executor_state_json, created_at, updated_at
            FROM jobs
            ORDER BY updated_at DESC;
            """;

        var jobs = new List<JobRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            jobs.Add(ReadJob(reader));

        return jobs;
    }

    public async Task<bool> TryClaimAsync(
        string jobId,
        string workerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new ArgumentException("Job ID is required.", nameof(jobId));

        if (string.IsNullOrWhiteSpace(workerId))
            throw new ArgumentException("Worker ID is required.", nameof(workerId));

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE jobs
            SET status = $running,
                worker_id = $worker,
                attempts = attempts + 1,
                message_key = $message,
                error = NULL,
                updated_at = $updated
            WHERE id = $id
              AND status = $ready;
            """;
        command.Parameters.AddWithValue("$running", JobStatus.Running.ToString());
        command.Parameters.AddWithValue("$worker", workerId);
        command.Parameters.AddWithValue("$message", "job.running");
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", jobId);
        command.Parameters.AddWithValue("$ready", JobStatus.Ready.ToString());

        // SQLite executes this single UPDATE atomically, so two runners cannot
        // both transition the same Ready row to Running.
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public Task PersistAsync(JobRecord job, CancellationToken cancellationToken = default)
        => UpdateAsync(job, cancellationToken);

    public async Task UpdateAsync(JobRecord job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        ValidateDefinition(job.Definition);

        job.UpdatedAt = DateTimeOffset.UtcNow;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE jobs
            SET definition_json = $definition,
                type = $type,
                status = $status,
                attempts = $attempts,
                progress = $progress,
                message_key = $message_key,
                error = $error,
                worker_id = $worker_id,
                execution_id = $execution_id,
                external_operation_id = $external_operation_id,
                executor_state_json = $executor_state_json,
                updated_at = $updated
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", job.Id);
        command.Parameters.AddWithValue("$type", job.Definition.Type);
        command.Parameters.AddWithValue("$definition", JsonSerializer.Serialize(job.Definition, JsonOptions));
        command.Parameters.AddWithValue("$status", job.Status.ToString());
        command.Parameters.AddWithValue("$attempts", job.Attempts);
        command.Parameters.AddWithValue("$progress", job.Progress);
        command.Parameters.AddWithValue("$message_key", (object?)job.MessageKey ?? DBNull.Value);
        command.Parameters.AddWithValue("$error", (object?)job.Error ?? DBNull.Value);
        command.Parameters.AddWithValue("$worker_id", (object?)job.WorkerId ?? DBNull.Value);
        command.Parameters.AddWithValue("$execution_id", (object?)job.ExecutionContext?.ExecutionId ?? DBNull.Value);
        command.Parameters.AddWithValue("$external_operation_id", (object?)job.ExecutionContext?.ExternalOperationId ?? DBNull.Value);
        command.Parameters.AddWithValue("$executor_state_json", (object?)job.ExecutionContext?.StateJson ?? DBNull.Value);
        command.Parameters.AddWithValue("$updated", job.UpdatedAt.ToString("O"));

        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException($"Job '{job.Id}' does not exist.");
    }

    public async Task RecoverInterruptedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE jobs
            SET status = $interrupted,
                worker_id = NULL,
                error = $error,
                updated_at = $updated
            WHERE status = $running;
            """;
        command.Parameters.AddWithValue("$interrupted", JobStatus.Interrupted.ToString());
        command.Parameters.AddWithValue("$running", JobStatus.Running.ToString());
        command.Parameters.AddWithValue("$error", "The previous application session ended while this job was running.");
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureColumnAsync(
        SqliteConnection connection,
        string table,
        string column,
        string definition,
        CancellationToken cancellationToken)
    {
        await using var check = connection.CreateCommand();
        check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info($table) WHERE name = $column;";
        check.Parameters.AddWithValue("$table", table);
        check.Parameters.AddWithValue("$column", column);
        var exists = Convert.ToInt32(await check.ExecuteScalarAsync(cancellationToken)) > 0;
        if (exists)
            return;

        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        await alter.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static JobRecord ReadJob(SqliteDataReader reader)
    {
        var definition = JsonSerializer.Deserialize<JobDefinition>(
            reader.GetString(1), JsonOptions)
            ?? throw new InvalidDataException("Stored job definition is invalid.");

        var status = Enum.Parse<JobStatus>(reader.GetString(2), ignoreCase: true);

        return new JobRecord
        {
            Id = reader.GetString(0),
            Definition = definition,
            Status = status,
            Attempts = reader.GetInt32(3),
            Progress = reader.GetDouble(4),
            MessageKey = reader.IsDBNull(5) ? null : reader.GetString(5),
            Error = reader.IsDBNull(6) ? null : reader.GetString(6),
            WorkerId = reader.IsDBNull(7) ? null : reader.GetString(7),
            ExecutionContext = reader.IsDBNull(8)
                ? null
                : new JobExecutionContext(
                    reader.GetString(8),
                    reader.IsDBNull(9) ? null : reader.GetString(9),
                    reader.IsDBNull(10) ? null : reader.GetString(10)),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(11)),
            UpdatedAt = DateTimeOffset.Parse(reader.GetString(12))
        };
    }

    private static void ValidateDefinition(JobDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Id))
            throw new ArgumentException("Job ID is required.", nameof(definition));

        if (string.IsNullOrWhiteSpace(definition.Type))
            throw new ArgumentException("Job type is required.", nameof(definition));

        if (definition.Requires.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Job requirements must contain stable capability IDs.", nameof(definition));

        if (definition.Provides.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Job provisions must contain stable capability IDs.", nameof(definition));

        if (definition.VersionPolicy is { } version)
        {
            if ((version.Mode is VersionSelectionMode.Exact or VersionSelectionMode.Minimum or VersionSelectionMode.Range)
                && string.IsNullOrWhiteSpace(version.Value))
                throw new ArgumentException($"Version mode '{version.Mode}' requires a value.", nameof(definition));

            if (version.Mode == VersionSelectionMode.Channel && string.IsNullOrWhiteSpace(version.Channel))
                throw new ArgumentException("Version channel mode requires a channel.", nameof(definition));
        }
    }

    private static void ValidateResourcePolicy(JobResourcePolicy policy)
    {
        if (policy.MaxDownloadBytesPerSecond is <= 0)
            throw new ArgumentOutOfRangeException(nameof(policy), "Maximum download rate must be positive when specified.");

        if (policy.MaxConcurrentDownloads < 1)
            throw new ArgumentOutOfRangeException(nameof(policy), "Maximum concurrent downloads must be at least one.");
    }
}
