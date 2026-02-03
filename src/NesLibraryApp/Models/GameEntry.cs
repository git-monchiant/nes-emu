using System;

namespace NesLibraryApp.Models;

public class GameEntry
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime DateAdded { get; set; }
    public DateTime? LastPlayed { get; set; }
    public bool IsFavorite { get; set; }
    public string? CoverPath { get; set; }
}
