using PriceCheckPoe2.ItemCheck.Data;
using PriceCheckPoe2.ItemCheck.Parsing;
using PriceCheckPoe2.ItemCheck.Trade;
using Sw = System.Windows;
using Swc = System.Windows.Controls;
using Swm = System.Windows.Media;

namespace PriceCheckPoe2.ItemCheck.UI;

/// <summary>
/// WPF-окно price-check (строится в коде, без XAML, на собственном STA-потоке).
/// Показывает предмет, чекбоксы модов с минимумами, оценку poe.ninja и список
/// живых листингов трейда.
/// </summary>
public sealed class ItemCheckWindow : Sw.Window
{
    private readonly List<(MatchedStat Stat, Swc.CheckBox Check, Swc.TextBox Min)> _rows = new();
    private readonly Swc.StackPanel _listings;
    private readonly Swc.TextBlock _status;
    private readonly Swc.TextBlock _ninja;

    public event Action<IReadOnlyList<TradeFilter>>? SearchRequested;

    public ItemCheckWindow(ParsedItem item, ItemStats stats)
    {
        Title = "PriceCheck — предмет";
        Width = 560;
        Height = 720;
        WindowStartupLocation = Sw.WindowStartupLocation.CenterScreen;
        Topmost = true;
        Background = Brush(22, 22, 26);

        var grid = new Swc.Grid { Margin = new Sw.Thickness(14) };
        for (var i = 0; i < 4; i++)
        {
            grid.RowDefinitions.Add(new Swc.RowDefinition { Height = Sw.GridLength.Auto });
        }

        grid.RowDefinitions[3].Height = new Sw.GridLength(1, Sw.GridUnitType.Star); // листинги тянутся

        // --- Заголовок ---
        var header = new Swc.StackPanel { Margin = new Sw.Thickness(0, 0, 0, 8) };
        header.Children.Add(Line(item.Name ?? item.BaseType ?? "?", 17, true, RarityBrush(item.Rarity)));
        if (!string.IsNullOrEmpty(item.BaseType) && item.BaseType != item.Name)
        {
            header.Children.Add(Line(item.BaseType!, 12, false, Rgb(165, 165, 172)));
        }

        var meta = new List<string>();
        if (!string.IsNullOrEmpty(item.RarityText)) meta.Add(item.RarityText);
        if (item.ItemClass is { } cls) meta.Add(cls);
        if (item.ItemLevel is { } il) meta.Add($"ilvl {il}");
        if (item.Quality is { } q) meta.Add($"Q{q}%");
        if (item.Corrupted) meta.Add("Corrupted");
        header.Children.Add(Line(string.Join("   ·   ", meta), 11, false, Rgb(135, 145, 155)));

        _ninja = Line("", 12.5, true, Rgb(120, 200, 255));
        _ninja.Margin = new Sw.Thickness(0, 4, 0, 0);
        header.Children.Add(_ninja);
        Swc.Grid.SetRow(header, 0);
        grid.Children.Add(header);

        // --- Кнопка поиска + статус ---
        var bar = new Swc.StackPanel { Orientation = Swc.Orientation.Horizontal, Margin = new Sw.Thickness(0, 0, 0, 8) };
        var searchBtn = MakeButton("Поиск на трейде");
        searchBtn.Click += (_, _) => RaiseSearch();
        bar.Children.Add(searchBtn);
        _status = Line("", 11, false, Rgb(150, 150, 158));
        _status.Margin = new Sw.Thickness(12, 7, 0, 0);
        bar.Children.Add(_status);
        Swc.Grid.SetRow(bar, 1);
        grid.Children.Add(bar);

        // --- Моды ---
        var modsPanel = new Swc.StackPanel();
        if (stats.Mods.Count == 0 && stats.Pseudo.Count == 0)
        {
            modsPanel.Children.Add(Line("Без модов — поиск по типу/имени предмета.", 11, false, Rgb(150, 140, 120)));
        }
        else
        {
            modsPanel.Children.Add(SectionLabel("Моды (отметь для поиска, можно задать минимум):"));
            foreach (var m in stats.Mods)
            {
                modsPanel.Children.Add(MakeModRow(m, preselect: m.TradeId is not null));
            }

            if (stats.Pseudo.Count > 0)
            {
                modsPanel.Children.Add(SectionLabel("Псевдо-моды:"));
                foreach (var m in stats.Pseudo)
                {
                    modsPanel.Children.Add(MakeModRow(m, preselect: false));
                }
            }
        }

        var modsScroll = new Swc.ScrollViewer
        {
            Content = modsPanel,
            VerticalScrollBarVisibility = Swc.ScrollBarVisibility.Auto,
            MaxHeight = 240,
            Margin = new Sw.Thickness(0, 0, 0, 8),
        };
        Swc.Grid.SetRow(modsScroll, 2);
        grid.Children.Add(modsScroll);

        // --- Листинги ---
        _listings = new Swc.StackPanel();
        _listings.Children.Add(SectionLabel("Листинги появятся после поиска."));
        var listingsScroll = new Swc.ScrollViewer
        {
            Content = _listings,
            VerticalScrollBarVisibility = Swc.ScrollBarVisibility.Auto,
        };
        Swc.Grid.SetRow(listingsScroll, 3);
        grid.Children.Add(listingsScroll);

        Content = grid;
    }

    public void SetNinjaEstimate(string text) => Dispatcher.Invoke(() => _ninja.Text = text);

    public void SetStatus(string text) => Dispatcher.Invoke(() => _status.Text = text);

    public void ShowListings(IReadOnlyList<TradeListing> listings) => Dispatcher.Invoke(() =>
    {
        _listings.Children.Clear();
        _listings.Children.Add(SectionLabel($"Листинги ({listings.Count})"));

        foreach (var l in listings)
        {
            var row = new Swc.StackPanel { Orientation = Swc.Orientation.Horizontal, Margin = new Sw.Thickness(0, 2, 0, 2) };
            row.Children.Add(new Swc.TextBlock
            {
                Text = $"{l.Amount:0.##} {l.Currency}",
                Width = 120,
                FontWeight = Sw.FontWeights.Bold,
                Foreground = Rgb(230, 200, 110),
            });
            row.Children.Add(new Swc.TextBlock { Text = l.Account, Foreground = Rgb(200, 200, 208) });
            _listings.Children.Add(row);
        }

        if (listings.Count == 0)
        {
            _listings.Children.Add(Line("Ничего не найдено (или фильтр слишком строгий).", 11, false, Rgb(190, 150, 120)));
        }
    });

    private void RaiseSearch()
    {
        var filters = new List<TradeFilter>();
        foreach (var (stat, check, min) in _rows)
        {
            if (check.IsChecked != true || stat.TradeId is null)
            {
                continue;
            }

            double? minValue = double.TryParse(min.Text, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
            filters.Add(new TradeFilter(stat.TradeId, minValue));
        }

        SetStatus("Поиск…");
        SearchRequested?.Invoke(filters);
    }

    private Swc.Border MakeModRow(MatchedStat m, bool preselect)
    {
        var matched = m.TradeId is not null;
        var row = new Swc.StackPanel { Orientation = Swc.Orientation.Horizontal };
        var check = new Swc.CheckBox
        {
            IsChecked = preselect && matched,
            IsEnabled = matched, // несопоставленный мод нельзя искать
            VerticalAlignment = Sw.VerticalAlignment.Center,
            Foreground = !matched ? Rgb(120, 120, 128)
                : m.IsPseudo ? Rgb(150, 200, 235)
                : Rgb(220, 220, 226),
            Width = 380,
            Content = matched ? m.Text : m.Text + "  (нет в базе)",
        };
        var min = new Swc.TextBox
        {
            Width = 64,
            VerticalAlignment = Sw.VerticalAlignment.Center,
            Background = Brush(36, 36, 42),
            Foreground = Rgb(230, 230, 235),
            BorderBrush = Brush(70, 70, 80),
            ToolTip = "минимум",
        };
        row.Children.Add(check);
        row.Children.Add(min);
        _rows.Add((m, check, min));

        return new Swc.Border { Child = row, Padding = new Sw.Thickness(2, 3, 2, 3) };
    }

    private static Swc.Button MakeButton(string text) => new()
    {
        Content = text,
        Padding = new Sw.Thickness(14, 5, 14, 5),
        Background = Brush(46, 46, 56),
        Foreground = Rgb(232, 232, 238),
        BorderBrush = Brush(80, 80, 92),
        FontWeight = Sw.FontWeights.SemiBold,
        Cursor = Sw.Input.Cursors.Hand,
    };

    private static Swc.TextBlock SectionLabel(string text) =>
        Line(text, 11, true, Rgb(190, 190, 198));

    private static Swc.TextBlock Line(string text, double size, bool bold, Swm.Brush brush) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = bold ? Sw.FontWeights.Bold : Sw.FontWeights.Normal,
        Foreground = brush,
        TextWrapping = Sw.TextWrapping.Wrap,
        Margin = new Sw.Thickness(0, 1, 0, 1),
    };

    private static Swm.SolidColorBrush RarityBrush(ItemRarity r) => r switch
    {
        ItemRarity.Unique => Rgb(180, 100, 40),
        ItemRarity.Rare => Rgb(235, 220, 110),
        ItemRarity.Magic => Rgb(130, 140, 240),
        ItemRarity.Currency => Rgb(190, 175, 130),
        ItemRarity.Gem => Rgb(40, 175, 165),
        _ => Rgb(225, 225, 230),
    };

    private static Swm.SolidColorBrush Rgb(byte r, byte g, byte b) => new(Swm.Color.FromRgb(r, g, b));

    private static Swm.SolidColorBrush Brush(byte r, byte g, byte b) => new(Swm.Color.FromRgb(r, g, b));
}
