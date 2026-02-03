using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using NesLibraryApp.Models;

namespace NesLibraryApp.Services;

public class MetadataStore : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _dbPath;

    public MetadataStore()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NesLibraryApp");
        Directory.CreateDirectory(appDataPath);
        _dbPath = Path.Combine(appDataPath, "library.db");

        _connection = new SqliteConnection($"Data Source={_dbPath}");
        _connection.Open();
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        var command = _connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Games (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FileName TEXT NOT NULL,
                FilePath TEXT NOT NULL UNIQUE,
                DisplayName TEXT NOT NULL,
                FileSize INTEGER NOT NULL,
                DateAdded TEXT NOT NULL,
                LastPlayed TEXT,
                IsFavorite INTEGER NOT NULL DEFAULT 0,
                CoverPath TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_games_filepath ON Games(FilePath);
            """;
        command.ExecuteNonQuery();
    }

    public void UpsertGame(GameEntry game)
    {
        var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Games (FileName, FilePath, DisplayName, FileSize, DateAdded, LastPlayed, IsFavorite, CoverPath)
            VALUES ($fileName, $filePath, $displayName, $fileSize, $dateAdded, $lastPlayed, $isFavorite, $coverPath)
            ON CONFLICT(FilePath) DO UPDATE SET
                FileName = excluded.FileName,
                DisplayName = excluded.DisplayName,
                FileSize = excluded.FileSize
            """;
        command.Parameters.AddWithValue("$fileName", game.FileName);
        command.Parameters.AddWithValue("$filePath", game.FilePath);
        command.Parameters.AddWithValue("$displayName", game.DisplayName);
        command.Parameters.AddWithValue("$fileSize", game.FileSize);
        command.Parameters.AddWithValue("$dateAdded", game.DateAdded.ToString("O"));
        command.Parameters.AddWithValue("$lastPlayed", game.LastPlayed?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$isFavorite", game.IsFavorite ? 1 : 0);
        command.Parameters.AddWithValue("$coverPath", game.CoverPath ?? (object)DBNull.Value);
        command.ExecuteNonQuery();
    }

    public List<GameEntry> GetAllGames()
    {
        var games = new List<GameEntry>();
        var command = _connection.CreateCommand();
        command.CommandText = "SELECT * FROM Games ORDER BY DisplayName";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            games.Add(ReadGame(reader));
        }
        return games;
    }

    public GameEntry? GetGameByPath(string filePath)
    {
        var command = _connection.CreateCommand();
        command.CommandText = "SELECT * FROM Games WHERE FilePath = $filePath";
        command.Parameters.AddWithValue("$filePath", filePath);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadGame(reader) : null;
    }

    public void UpdateFavorite(int gameId, bool isFavorite)
    {
        var command = _connection.CreateCommand();
        command.CommandText = "UPDATE Games SET IsFavorite = $isFavorite WHERE Id = $id";
        command.Parameters.AddWithValue("$isFavorite", isFavorite ? 1 : 0);
        command.Parameters.AddWithValue("$id", gameId);
        command.ExecuteNonQuery();
    }

    public void UpdateLastPlayed(int gameId, DateTime lastPlayed)
    {
        var command = _connection.CreateCommand();
        command.CommandText = "UPDATE Games SET LastPlayed = $lastPlayed WHERE Id = $id";
        command.Parameters.AddWithValue("$lastPlayed", lastPlayed.ToString("O"));
        command.Parameters.AddWithValue("$id", gameId);
        command.ExecuteNonQuery();
    }

    public void RemoveGamesNotInPaths(IEnumerable<string> validPaths)
    {
        var pathSet = validPaths.ToHashSet();
        var command = _connection.CreateCommand();
        command.CommandText = "SELECT Id, FilePath FROM Games";

        var toRemove = new List<int>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var path = reader.GetString(1);
                if (!pathSet.Contains(path))
                {
                    toRemove.Add(reader.GetInt32(0));
                }
            }
        }

        foreach (var id in toRemove)
        {
            var deleteCmd = _connection.CreateCommand();
            deleteCmd.CommandText = "DELETE FROM Games WHERE Id = $id";
            deleteCmd.Parameters.AddWithValue("$id", id);
            deleteCmd.ExecuteNonQuery();
        }
    }

    private static GameEntry ReadGame(SqliteDataReader reader)
    {
        return new GameEntry
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            FileName = reader.GetString(reader.GetOrdinal("FileName")),
            FilePath = reader.GetString(reader.GetOrdinal("FilePath")),
            DisplayName = reader.GetString(reader.GetOrdinal("DisplayName")),
            FileSize = reader.GetInt64(reader.GetOrdinal("FileSize")),
            DateAdded = DateTime.Parse(reader.GetString(reader.GetOrdinal("DateAdded"))),
            LastPlayed = reader.IsDBNull(reader.GetOrdinal("LastPlayed"))
                ? null
                : DateTime.Parse(reader.GetString(reader.GetOrdinal("LastPlayed"))),
            IsFavorite = reader.GetInt32(reader.GetOrdinal("IsFavorite")) == 1,
            CoverPath = reader.IsDBNull(reader.GetOrdinal("CoverPath"))
                ? null
                : reader.GetString(reader.GetOrdinal("CoverPath"))
        };
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
