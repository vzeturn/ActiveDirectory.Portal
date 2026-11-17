namespace ADManager.Infrastructure.Storage;

public sealed class SqliteStorageSettings
{
    public string DatabasePath { get; set; } = @"C:\ProgramData\ad-manager\data\ad-manager.db";
}
