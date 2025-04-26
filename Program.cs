using System.Reflection;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Initialization;
using FluentRun.Console;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


// 1. Determine environment
var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                  ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                  ?? "Production";

// 2. Load configuration
var config = new ConfigurationBuilder()
    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
    .AddCustomUserSecrets()
    .AddJsonFile("appsettings.json", false, true)
    .AddJsonFile($"appsettings.{environment}.json", true, true)
    .AddEnvironmentVariables()
    .Build();

// 3. Determine command
var command = args.Length > 0 ? args[0].ToLower() : "migrate";

// 4. Handle “create” separately
if (command == "create")
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: create <DatabaseName> <MigrationName>");
        return;
    }

    var dbName = args[1];
    var migrationName = args[2];
    var dbFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Migrations", dbName.ToLower());
    if (!Directory.Exists(dbFolder))
    {
        Console.Error.WriteLine($"Error: migrations folder '{dbFolder}' not found. Create it first.");
        return;
    }

    var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
    var fileName = $"{timestamp}_{dbName.ToLower()}_{migrationName}.cs";
    var filePath = Path.Combine(dbFolder, fileName);

    File.WriteAllText(filePath,
        $"using FluentMigrator;{Environment.NewLine}{Environment.NewLine}" +
        $"namespace FluentRun.Migrations.{dbName.ToLower()} {{" + Environment.NewLine +
        $"    [Migration({timestamp})]" + Environment.NewLine +
        $"    public class {dbName.ToLower()}_{timestamp}_{migrationName} : Migration" + Environment.NewLine +
        "    {" + Environment.NewLine +
        "        public override void Up() { }" + Environment.NewLine +
        "        public override void Down() { }" + Environment.NewLine +
        "    }" + Environment.NewLine +
        "}"
    );

    Console.WriteLine($"Created migration stub: {filePath}");
    return;
}

// 5. Get valid database keys
var dbKeys = config.GetSection("FluentMigrator:Databases")
    .GetChildren()
    .Select(d => d.Key)
    .ToList();

// 6. Parse optional target database for migrate, required for rollback
string targetDb = null;
if (args.Length > 1 && dbKeys.Contains(args[1], StringComparer.OrdinalIgnoreCase))
{
    targetDb = args[1];
    args = args.Skip(1).ToArray();
}

// 7. Enforce targetDb for rollback commands
if ((command == "rollback" || command == "rollback-prev" || command == "rollback-all")
    && string.IsNullOrEmpty(targetDb))
{
    Console.Error.WriteLine($"Usage: {command} <DatabaseName>{(command == "rollback" ? " <Version>" : "")}");
    Console.Error.WriteLine($"Valid DatabaseName values: {string.Join(", ", dbKeys)}");
    return;
}

// 8. Parse optional rollback version
long? targetVersion = null;
if (command == "rollback" && args.Length > 1 && long.TryParse(args[1], out var tv))
{
    targetVersion = tv;
}

// 9. Iterate each DB defined in config
foreach (var db in config.GetSection("FluentMigrator:Databases").GetChildren())
{
    var dbName = db.Key;

    // If a targetDb is specified, skip others
    if (!string.IsNullOrEmpty(targetDb)
        && !dbName.Equals(targetDb, StringComparison.OrdinalIgnoreCase))
    {
        continue;
    }

    var provider = db["Provider"] ?? "Postgres";
    var connectionString = db["ConnectionString"]
                           ?? throw new InvalidOperationException($"ConnectionString missing for {dbName}");

    // 10. Build the FluentMigrator runner for this DB
    var services = new ServiceCollection()
        .AddLogging(lb => lb.AddFluentMigratorConsole())
        .AddFluentMigratorCore()
        .ConfigureRunner(rb =>
        {
            if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
            {
                rb.AddPostgres();
            }
            else
            {
                throw new NotSupportedException($"Provider '{provider}' not supported");
            }

            rb.WithGlobalConnectionString(connectionString)
                .ScanIn(Assembly.GetExecutingAssembly()).For.Migrations();
        })
        .Configure<TypeFilterOptions>(opts =>
        {
            opts.Namespace = $"FluentRun.Migrations.{dbName.ToLower()}";
            opts.NestedNamespaces = false;
        });

    using var serviceProvider = services.BuildServiceProvider();
    using var scope = serviceProvider.CreateScope();
    var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();

    // 11. Execute the command
    switch (command)
    {
        case "migrate":
            Console.WriteLine($"\n→ Applying migrations for '{dbName}'...");
            runner.MigrateUp();
            break;

        case "rollback" when targetVersion.HasValue:
            Console.WriteLine($"\n→ Rolling back '{dbName}' to {targetVersion.Value}...");
            runner.MigrateDown(targetVersion.Value);
            break;

        case "rollback-prev":
        {
            var loader = scope.ServiceProvider.GetRequiredService<IVersionLoader>();
            var applied = loader.VersionInfo
                .AppliedMigrations()
                .OrderByDescending(v => v)
                .ToList();

            if (applied.Count < 2)
            {
                Console.WriteLine($"No previous migration to roll back for '{dbName}'.");
            }
            else
            {
                var prev = applied[1];
                Console.WriteLine($"\n→ Rolling back '{dbName}' to previous version {prev}...");
                runner.MigrateDown(prev);
            }

            break;
        }

        case "rollback-all":
            Console.WriteLine($"\n→ Rolling back all migrations for '{dbName}'...");
            runner.MigrateDown(0);
            break;

        default:
            Console.Error.WriteLine(
                $"Unknown command '{command}'. Valid: create, migrate, rollback, rollback-prev, rollback-all.");
            break;
    }
}