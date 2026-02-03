using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NesLibraryApp.Models;
using NesLibraryApp.Services;

namespace NesLibraryApp.ViewModels;

public partial class LibraryViewModel : ViewModelBase
{
    private readonly MetadataStore _metadataStore;
    private readonly RomScanner _romScanner;
    private readonly CoverService _coverService;
    private readonly List<GameItemViewModel> _allGames = [];

    public Action<GameItemViewModel>? NavigateToGameDetails { get; set; }

    [ObservableProperty]
    private ObservableCollection<GameItemViewModel> _games = [];

    [ObservableProperty]
    private GameItemViewModel? _selectedGame;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _romDirectory = string.Empty;

    [ObservableProperty]
    private bool _showFavoritesOnly;

    [ObservableProperty]
    private int _totalGamesCount;

    [ObservableProperty]
    private bool _isLoading;

    public LibraryViewModel()
    {
        _metadataStore = new MetadataStore();
        _romScanner = new RomScanner();
        _coverService = new CoverService();

        // Default ROM directory - try to find roms folder
        RomDirectory = FindRomsDirectory();

        // Load mock data for UI testing
        LoadMockData();
    }

    private static string FindRomsDirectory()
    {
        // Try common locations
        var candidates = new[]
        {
            // NesLibraryApp/roms folder (for development - from bin/Debug/net9.0)
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "roms"),
            // Running from src/NesLibraryApp directory
            Path.Combine(Environment.CurrentDirectory, "roms"),
            // Running from project root (nes-hh)
            Path.Combine(Environment.CurrentDirectory, "src", "NesLibraryApp", "roms"),
            // User's home directory
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "NES", "roms"),
        };

        foreach (var path in candidates)
        {
            var fullPath = Path.GetFullPath(path);
            if (Directory.Exists(fullPath))
            {
                return fullPath;
            }
        }

        // Fallback to user's home directory (will be created if needed)
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "NES", "roms");
    }

    private void LoadMockData()
    {
        var mockGames = new[]
        {
            new GameEntry { Id = -1, DisplayName = "Super Mario Bros", FileName = "super_mario_bros.nes", FileSize = 40976, DateAdded = DateTime.Now.AddDays(-30), IsFavorite = true },
            new GameEntry { Id = -2, DisplayName = "The Legend of Zelda", FileName = "legend_of_zelda.nes", FileSize = 131088, DateAdded = DateTime.Now.AddDays(-25), IsFavorite = true },
            new GameEntry { Id = -3, DisplayName = "Mega Man 2", FileName = "mega_man_2.nes", FileSize = 262160, DateAdded = DateTime.Now.AddDays(-20), IsFavorite = false },
            new GameEntry { Id = -4, DisplayName = "Castlevania", FileName = "castlevania.nes", FileSize = 131088, DateAdded = DateTime.Now.AddDays(-15), IsFavorite = false },
            new GameEntry { Id = -5, DisplayName = "Metroid", FileName = "metroid.nes", FileSize = 131088, DateAdded = DateTime.Now.AddDays(-12), IsFavorite = false },
            new GameEntry { Id = -6, DisplayName = "Duck Hunt", FileName = "duck_hunt.nes", FileSize = 24592, DateAdded = DateTime.Now.AddDays(-10), IsFavorite = false },
            new GameEntry { Id = -7, DisplayName = "Tetris", FileName = "tetris.nes", FileSize = 49168, DateAdded = DateTime.Now.AddDays(-8), IsFavorite = true },
            new GameEntry { Id = -8, DisplayName = "Punch-Out!!", FileName = "punch_out.nes", FileSize = 262160, DateAdded = DateTime.Now.AddDays(-5), IsFavorite = false },
            new GameEntry { Id = -9, DisplayName = "Excitebike", FileName = "excitebike.nes", FileSize = 24592, DateAdded = DateTime.Now.AddDays(-2), IsFavorite = false },
        };

        foreach (var game in mockGames)
        {
            var vm = new GameItemViewModel(game, _coverService, OnFavoriteToggled);
            _allGames.Add(vm);
        }

        TotalGamesCount = _allGames.Count;
        FilterGames();
    }

    partial void OnSearchTextChanged(string value)
    {
        FilterGames();
    }

    partial void OnShowFavoritesOnlyChanged(bool value)
    {
        FilterGames();
    }

    [RelayCommand]
    private async Task ScanRomsAsync()
    {
        if (string.IsNullOrWhiteSpace(RomDirectory) || !Directory.Exists(RomDirectory))
        {
            return;
        }

        IsLoading = true;

        await Task.Run(() =>
        {
            var scannedGames = _romScanner.ScanDirectory(RomDirectory).ToList();

            // Upsert scanned games
            foreach (var game in scannedGames)
            {
                var existing = _metadataStore.GetGameByPath(game.FilePath);
                if (existing != null)
                {
                    game.Id = existing.Id;
                    game.IsFavorite = existing.IsFavorite;
                    game.LastPlayed = existing.LastPlayed;
                    game.DateAdded = existing.DateAdded;
                }
                _metadataStore.UpsertGame(game);
            }

            // Remove games that no longer exist
            var validPaths = scannedGames.Select(g => g.FilePath);
            _metadataStore.RemoveGamesNotInPaths(validPaths);
        });

        await LoadGamesAsync();
        IsLoading = false;
    }

    [RelayCommand]
    private async Task LoadGamesAsync()
    {
        IsLoading = true;

        var games = await Task.Run(() => _metadataStore.GetAllGames());

        // Remove only real games (positive IDs), keep mock data (negative IDs)
        _allGames.RemoveAll(g => g.Id > 0);

        // Insert real games at the beginning (before mock data)
        var insertIndex = 0;
        foreach (var game in games)
        {
            var vm = new GameItemViewModel(game, _coverService, OnFavoriteToggled);
            _allGames.Insert(insertIndex++, vm);
        }

        TotalGamesCount = _allGames.Count;
        FilterGames();

        IsLoading = false;
    }

    [RelayCommand]
    private void SelectGame(GameItemViewModel? game)
    {
        if (SelectedGame != null)
        {
            SelectedGame.IsSelected = false;
        }
        SelectedGame = game;
        if (SelectedGame != null)
        {
            SelectedGame.IsSelected = true;
        }
    }

    [RelayCommand]
    private void OpenGameDetails(GameItemViewModel? game = null)
    {
        var targetGame = game ?? SelectedGame;
        if (targetGame != null)
        {
            NavigateToGameDetails?.Invoke(targetGame);
        }
    }

    private void FilterGames()
    {
        var filtered = _allGames.AsEnumerable();

        if (ShowFavoritesOnly)
        {
            filtered = filtered.Where(g => g.IsFavorite);
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.ToLowerInvariant();
            filtered = filtered.Where(g =>
                g.DisplayName.Contains(searchLower, StringComparison.InvariantCultureIgnoreCase) ||
                g.FileName.Contains(searchLower, StringComparison.InvariantCultureIgnoreCase));
        }

        Games = new ObservableCollection<GameItemViewModel>(filtered);
    }

    private void OnFavoriteToggled(GameItemViewModel game)
    {
        _metadataStore.UpdateFavorite(game.Id, game.IsFavorite);
        if (ShowFavoritesOnly)
        {
            FilterGames();
        }
    }
}
