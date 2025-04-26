# FluentMigrator Console Migration Tool

This is a .NET Core console application that manages database migrations using [FluentMigrator](https://fluentmigrator.github.io/). It supports generating new migration stubs, applying migrations (globally or per-database), and rolling back changes (per-database only) across multiple databases.

---

## 📁 Project Structure

```
/Migrations/
  /<DatabaseName>/
    YYYYMMDDHHMMSS_<databasename>_<MigrationName>.cs
Program.cs
appsettings.json
appsettings.Development.json
```

---

## ⚙️ Environment Configuration

The tool detects the current environment using:

- `DOTNET_ENVIRONMENT`
- or `ASPNETCORE_ENVIRONMENT`

Defaults to: `Production`.

Based on the environment, it loads:

- `appsettings.json` (required)
- `appsettings.{Environment}.json` (optional)
- Environment variables

---

## 🛠️ Configuration Format (`appsettings.json`)

```json
{
  "FluentMigrator": {
    "Databases": {
      "ExampleDb": {
        "Provider": "Postgres",
        "ConnectionString": "Host=localhost;Port=5432;Database=example;Username=user;Password=pass"
      }
    }
  }
}
```

Each key under `Databases` represents a logical database name and must match the folder name under `Migrations/`.

---

## 📜 Commands

Run with:

```bash
dotnet run -- [command] [args...]
```

### `create <DatabaseName> <MigrationName>`

Creates a new migration file in `Migrations/<DatabaseName>/`.

**Example:**

```bash
dotnet run -- create Billing AddInvoicesTable
```

Creates:

```
Migrations/Billing/20250421094530_billing_AddInvoicesTable.cs
```

### `migrate [<DatabaseName>]`

Applies all pending migrations for all configured databases.

```bash
dotnet run -- migrate
```
Applies all pending migrations for a specific database.

```bash
dotnet run -- migrate ExampleDb
```

### `rollback <DatabaseName> <TargetVersion>`

Rolls back only <DatabaseName> to exactly <TargetVersion>.

```bash
dotnet run -- rollback ExampleDb 20250420083000
```

### `rollback-prev <DatabaseName>`

Rolls back **only the most recent migration for the database**.

```bash
dotnet run -- rollback-prev ExampleDb
```

### `rollback-all <DatabaseName>`

Roll back only <DatabaseName> all the way to version 0 (reset schema).

```bash
dotnet run -- rollback-all ExampleDb
```

---

## 🔌 Supported Providers

Only **PostgreSQL** is currently supported.

---

## 📦 Dependencies

- [.NET 6+](https://dotnet.microsoft.com/)
- [FluentMigrator.Runner](https://www.nuget.org/packages/FluentMigrator.Runner)
- Microsoft.Extensions.Configuration
- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.Logging

---

## 🐞 Error Handling

- Missing connection string: shows a detailed exception.
- Missing database folder: alerts if `Migrations/<DatabaseName>` does not exist.
- Unsupported provider: throws a `NotSupportedException`.
- Incorrect usage of `create`: prints usage guide.

---

## 📄 License

MIT License – Feel free to use and modify.
