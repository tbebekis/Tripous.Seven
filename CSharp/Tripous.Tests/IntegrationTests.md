# Database integration tests

The full integration suite requires access to SQL Server, Oracle, PostgreSQL, MySQL and Firebird. Each database must be running and reachable from the test process, whether installed locally, running in a container or hosted remotely. SQLite runs in-process in the regular suite and needs no separate server.

The regular suite runs without external database credentials. The live tests currently execute a scalar query through `SqlStore` for each configured provider.

## Select the providers to test

Each connection-string variable also enables that provider's integration tests. A missing or blank variable skips the provider. If a connection string is supplied, connection and SQL failures fail the test.

| Variable | Example with placeholder credentials |
| --- | --- |
| `TRIPOUS_TEST_MSSQL_CONNECTION_STRING` | `Server=127.0.0.1,1433;Database=TripousTest;User Id=tripous_test;Password=<password>;TrustServerCertificate=True;` |
| `TRIPOUS_TEST_ORACLE_CONNECTION_STRING` | `Data Source=127.0.0.1:1521/FREEPDB1;User Id=tripous_test;Password=<password>;` |
| `TRIPOUS_TEST_POSTGRESQL_CONNECTION_STRING` | `Host=127.0.0.1;Port=5432;Database=tripous_test;Username=tripous_test;Password=<password>;` |
| `TRIPOUS_TEST_MYSQL_CONNECTION_STRING` | `Server=127.0.0.1;Port=3306;Database=tripous_test;User Id=tripous_test;Password=<password>;` |
| `TRIPOUS_TEST_FIREBIRD_CONNECTION_STRING` | `DataSource=127.0.0.1;Port=3050;Database=tripous_test;User=tripous_test;Password=<password>;` |

The SQL Server example assumes a local development certificate. Use a dedicated test database or schema.

## Run from Bash

Paste the complete connection string when prompted. Bash hides the input and keeps it out of command history. Repeat for each provider you want to enable, using the corresponding variable from the table.

```bash
read -r -s -p 'Oracle test connection string: ' TRIPOUS_TEST_ORACLE_CONNECTION_STRING
printf '\n'
export TRIPOUS_TEST_ORACLE_CONNECTION_STRING
```

Run from the same shell, in the `CSharp` directory.

```bash
# Regular suite and any configured integration tests
dotnet test Tripous.Tests/Tripous.Tests.csproj

# Regular suite only
dotnet test Tripous.Tests/Tripous.Tests.csproj --filter 'Category!=Integration'

# Integration tests only
dotnet test Tripous.Tests/Tripous.Tests.csproj --filter 'Category=Integration'
```

Clear variables you set when you finish, for example with `unset TRIPOUS_TEST_ORACLE_CONNECTION_STRING`. In CI, inject the variables from the secret store. For a job intended to cover all five providers, configure all five variables and verify that the integration test run reports zero skipped tests.
