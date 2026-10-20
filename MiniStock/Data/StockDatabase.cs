using Microsoft.Data.Sqlite;

namespace MiniStock.Data;

public sealed class StockDatabase
{
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly string _databasePath = Path.Combine(FileSystem.AppDataDirectory, "ministock.db3");
    private bool _initialized;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync();
        try
        {
            if (_initialized)
            {
                return;
            }

            SQLitePCL.Batteries_V2.Init();

            using var connection = CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                PRAGMA foreign_keys = ON;

                CREATE TABLE IF NOT EXISTS Boutiques (
                    Id TEXT NOT NULL PRIMARY KEY,
                    Nom TEXT NOT NULL,
                    Telephone TEXT NOT NULL,
                    Adresse TEXT NULL,
                    CreeLe TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Utilisateurs (
                    Id TEXT NOT NULL PRIMARY KEY,
                    BoutiqueId TEXT NOT NULL,
                    Nom TEXT NOT NULL,
                    Identifiant TEXT NOT NULL COLLATE NOCASE UNIQUE,
                    MotDePasseHash TEXT NOT NULL,
                    CreeLe TEXT NOT NULL,
                    FOREIGN KEY (BoutiqueId) REFERENCES Boutiques (Id)
                );
                """;
            await command.ExecuteNonQueryAsync();
            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath
        }.ToString());
        connection.Open();
        return connection;
    }
}
