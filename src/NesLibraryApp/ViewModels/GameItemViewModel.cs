using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NesLibraryApp.Models;
using NesLibraryApp.Services;

namespace NesLibraryApp.ViewModels;

public partial class GameItemViewModel : ViewModelBase
{
    private readonly GameEntry _game;
    private readonly CoverService _coverService;
    private readonly Action<GameItemViewModel>? _onFavoriteToggled;

    [ObservableProperty]
    private Bitmap? _coverImage;

    [ObservableProperty]
    private IBrush _cardBackground = new SolidColorBrush(Color.Parse("#2a2a2a"));

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private bool _isSelected;

    public GameItemViewModel(GameEntry game, CoverService coverService, Action<GameItemViewModel>? onFavoriteToggled = null)
    {
        _game = game;
        _coverService = coverService;
        _onFavoriteToggled = onFavoriteToggled;
        _isFavorite = game.IsFavorite;
        LoadCover();
    }

    public int Id => _game.Id;
    public string DisplayName => _game.DisplayName;
    public string FileName => _game.FileName;
    public string FilePath => _game.FilePath;
    public long FileSize => _game.FileSize;
    public DateTime DateAdded => _game.DateAdded;
    public DateTime? LastPlayed => _game.LastPlayed;

    public string FileSizeFormatted => FormatFileSize(FileSize);
    public string LastPlayedFormatted => LastPlayed?.ToString("yyyy-MM-dd") ?? "Never";

    [RelayCommand]
    private void ToggleFavorite()
    {
        IsFavorite = !IsFavorite;
        _game.IsFavorite = IsFavorite;
        _onFavoriteToggled?.Invoke(this);
    }

    private void LoadCover()
    {
        var coverPath = _coverService.GetCoverPath(_game.FileName);
        CoverImage = _coverService.LoadCover(coverPath);

        if (CoverImage != null && !string.IsNullOrEmpty(coverPath))
        {
            CardBackground = new SolidColorBrush(ExtractDominantColorFromFile(coverPath));
        }
    }

    private static Color ExtractDominantColorFromFile(string imagePath)
    {
        try
        {
            using var stream = File.OpenRead(imagePath);
            using var bitmap = new Bitmap(stream);

            var width = (int)bitmap.Size.Width;
            var height = (int)bitmap.Size.Height;

            // Use RenderTargetBitmap to access pixels
            using var rtb = new RenderTargetBitmap(new PixelSize(width, height));
            using (var ctx = rtb.CreateDrawingContext())
            {
                ctx.DrawImage(bitmap, new Rect(0, 0, width, height));
            }

            // Allocate buffer and copy pixels
            var stride = width * 4;
            var bufferSize = height * stride;
            var buffer = Marshal.AllocHGlobal(bufferSize);

            try
            {
                rtb.CopyPixels(new PixelRect(0, 0, width, height), buffer, bufferSize, stride);

                // Sample pixels from edges (top, bottom, left, right borders)
                // This is more likely to capture the background color
                long totalR = 0, totalG = 0, totalB = 0;
                int sampleCount = 0;
                int edgeSize = Math.Max(5, Math.Min(width, height) / 10);

                unsafe
                {
                    var ptr = (byte*)buffer;

                    // Sample from all four edges
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            // Only sample from edges
                            bool isEdge = y < edgeSize || y >= height - edgeSize ||
                                          x < edgeSize || x >= width - edgeSize;
                            if (!isEdge) continue;

                            var offset = y * stride + x * 4;
                            var b = ptr[offset];
                            var g = ptr[offset + 1];
                            var r = ptr[offset + 2];
                            var a = ptr[offset + 3];

                            // Skip transparent pixels
                            if (a < 128)
                                continue;

                            totalR += r;
                            totalG += g;
                            totalB += b;
                            sampleCount++;
                        }
                    }
                }

                if (sampleCount > 0)
                {
                    var avgR = (byte)(totalR / sampleCount);
                    var avgG = (byte)(totalG / sampleCount);
                    var avgB = (byte)(totalB / sampleCount);

                    return Color.FromRgb(avgR, avgG, avgB);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            // Fallback on error
        }

        return Color.Parse("#2a2a2a");
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
