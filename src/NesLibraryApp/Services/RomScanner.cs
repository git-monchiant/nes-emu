using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NesLibraryApp.Models;

namespace NesLibraryApp.Services;

public class RomScanner
{
    private readonly string[] _supportedExtensions = [".nes", ".NES"];

    public IEnumerable<GameEntry> ScanDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            yield break;
        }

        var files = Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.AllDirectories)
            .Where(f => _supportedExtensions.Contains(Path.GetExtension(f)));

        foreach (var file in files)
        {
            var fileInfo = new FileInfo(file);
            var fileName = Path.GetFileNameWithoutExtension(file);

            yield return new GameEntry
            {
                FileName = Path.GetFileName(file),
                FilePath = file,
                DisplayName = CleanDisplayName(fileName),
                FileSize = fileInfo.Length,
                DateAdded = DateTime.Now
            };
        }
    }

    private static string CleanDisplayName(string fileName)
    {
        var name = fileName;

        // Remove common ROM naming conventions like (USA), [!], (E), etc.
        name = System.Text.RegularExpressions.Regex.Replace(name, @"\s*[\(\[].*?[\)\]]", "");

        // Trim and clean up
        name = name.Trim();

        return string.IsNullOrEmpty(name) ? fileName : name;
    }
}
