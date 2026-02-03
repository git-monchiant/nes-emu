using System;
using System.IO;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace NesLibraryApp.Services;

public class CoverService
{
    private readonly string _coversDirectory;
    private readonly string _bundledCoversDirectory;
    private Bitmap? _placeholderBitmap;

    public CoverService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NesLibraryApp");
        _coversDirectory = Path.Combine(appDataPath, "covers");
        Directory.CreateDirectory(_coversDirectory);

        // Bundled covers in Assets/Covers (for development)
        _bundledCoversDirectory = FindBundledCoversDirectory();
    }

    private static string FindBundledCoversDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", "Covers"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", "Covers"),
            Path.Combine(Environment.CurrentDirectory, "Assets", "Covers"),
            Path.Combine(Environment.CurrentDirectory, "src", "NesLibraryApp", "Assets", "Covers"),
        };

        foreach (var path in candidates)
        {
            var fullPath = Path.GetFullPath(path);
            if (Directory.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return string.Empty;
    }

    public string? GetCoverPath(string gameFileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(gameFileName);
        // Clean the name for matching (remove region tags like (USA), [!], etc.)
        var cleanName = System.Text.RegularExpressions.Regex.Replace(baseName, @"\s*[\(\[].*?[\)\]]", "").Trim().ToLowerInvariant();

        var extensions = new[] { ".png", ".jpg", ".jpeg", ".webp" };

        // Check user covers directory first
        foreach (var ext in extensions)
        {
            var coverPath = Path.Combine(_coversDirectory, baseName + ext);
            if (File.Exists(coverPath))
            {
                return coverPath;
            }

            // Try clean name
            coverPath = Path.Combine(_coversDirectory, cleanName + ext);
            if (File.Exists(coverPath))
            {
                return coverPath;
            }
        }

        // Check bundled covers directory
        if (!string.IsNullOrEmpty(_bundledCoversDirectory))
        {
            foreach (var ext in extensions)
            {
                var coverPath = Path.Combine(_bundledCoversDirectory, baseName + ext);
                if (File.Exists(coverPath))
                {
                    return coverPath;
                }

                // Try clean name
                coverPath = Path.Combine(_bundledCoversDirectory, cleanName + ext);
                if (File.Exists(coverPath))
                {
                    return coverPath;
                }
            }
        }

        return null;
    }

    public Bitmap? LoadCover(string? coverPath)
    {
        if (string.IsNullOrEmpty(coverPath) || !File.Exists(coverPath))
        {
            return null;
        }

        try
        {
            return new Bitmap(coverPath);
        }
        catch
        {
            return null;
        }
    }

    public Bitmap GetPlaceholderBitmap()
    {
        if (_placeholderBitmap != null)
        {
            return _placeholderBitmap;
        }

        // Load from embedded resource
        var assembly = typeof(CoverService).Assembly;
        using var stream = assembly.GetManifestResourceStream("NesLibraryApp.Assets.placeholder-cover.png");

        if (stream != null)
        {
            _placeholderBitmap = new Bitmap(stream);
        }
        else
        {
            // Create a simple colored bitmap as fallback
            _placeholderBitmap = CreateFallbackPlaceholder();
        }

        return _placeholderBitmap;
    }

    private static Bitmap CreateFallbackPlaceholder()
    {
        // Simple 150x200 placeholder (will be replaced by actual asset)
        using var ms = new MemoryStream();
        // Return minimal valid bitmap - this is a fallback
        var assembly = typeof(CoverService).Assembly;
        using var stream = assembly.GetManifestResourceStream("NesLibraryApp.Assets.avalonia-logo.ico");
        if (stream != null)
        {
            return new Bitmap(stream);
        }
        throw new InvalidOperationException("No placeholder image available");
    }

    public string CoversDirectory => _coversDirectory;
}
