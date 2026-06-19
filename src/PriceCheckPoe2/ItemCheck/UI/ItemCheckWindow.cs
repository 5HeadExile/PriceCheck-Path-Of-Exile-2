using PriceCheckPoe2.ItemCheck.Data;
using PriceCheckPoe2.ItemCheck.Parsing;
using PriceCheckPoe2.ItemCheck.Trade;
using Sw = System.Windows;
using Swc = System.Windows.Controls;
using Swm = System.Windows.Media;

namespace PriceCheckPoe2.ItemCheck.UI;

/// <summary>
/// WPF-окно price-check (строится в коде, без XAML, чтобы не конфликтовать с
/// WinForms-частью). Показывает предмет, чекбоксы модов с минимумами, оценку
/// poe.ninja и список живых листингов трейда. Запускается на собственном STA-
/// потоке с диспетчером (см. ItemCheckService).
/// </summary>
public sealed class ItemCheckWindow : Sw.Window
{
    private readonly List<(MatchedStat Stat, Swc.CheckBox Check, Swc.TextBox Min)> _rows = new();
    private readonly Swc.StackPanel _listings;
    private readonly Swc.TextBlock _status;
    private readonly Swc.TextBlock _ninja;

    /// <summary>Пользователь нажал «Поиск»: выбранные фильтры (trade id + min).</summary>
    public event Action<IReadOnlyList<TradeFilter>>? SearchRequested;

    public ItemCheckWindow(ParsedItem item, ItemStats stats)
    {
        Title = "PriceCheck — предмет";
        Width = 540;
        Height = 680;
        WindowStartupLocation = Sw.WindowStartupLocation.CenterScreen;
        Topmost = true;
        Background = Brush(24, 24, 28);

        var root = new Swc.DockPanel { Margin = new Sw.Thickness(12) };

        // Заголовок предмета.
        var header = new Swc.StackPanel();
        header.Children.Add(Text(item.Name ?? item.BaseType ?? "?", 16, true, Rgb(230, 230, 235)));
        if (!string.IsNullOrEmpty(item.BaseType) && item.BaseType != item.Name)
        {
            header.Children.Add(Text(item.BaseType!, 12, false, Rgb(170, 170, 178)));
        }

        var meta = new List<string> { item.RarityText };
        if (item.ItemClass is { } cls) meta.Add(cls);
        if (item.ItemLevel is { } il) meta.Add($"ilvl {il}");
        if (item.Quality is { } q) meta.Add($"Q{q}%");
        if (item.Corrupted) meta.Add("Corrupted");
        header.Children.Add(Text(string.Join("  ·  ", meta.Where(s => !string.IsNullOrEmpty(s))), 11, false, Rgb(140, 150, 160)));

        _ninja = Text("", 12, true, Rgb(150, 220, 255));
        header.Children.Add(_ninja);
        Swc.DockPanel.SetDock(header, Swc.Dock.Top);
        root.Children.Add(header);

        // Кнопки.
        var buttons = new Swc.StackPanel
        {
            Orientation = Swc.Orientation.Horizontal,
            Margin = new Sw.Thickness(0, 10, 0, 6),
        };
        var searchBtn = MakeButton("Поиск на трейде");
        searchBtn.Click += (_, _) => RaiseSearch();
        buttons.Children.Add(searchBtn);
        _status = Text("", 11, false, Rgb(160, 160, 168));
        _status.Margin = new Sw.Thickness(10, 6, 0, 0);
        buttons.Children.Add(_status);
        Swc.DockPanel.SetDock(buttons, Swc.Dock.Top);
        root.Children.Add(buttons);

        // Моды (чекбоксы + min) и листинги — в скролле, разделённый Grid сверху/снизу.
        var split = new Swc.Grid();
        split.RowDefinitions.Add(new Swc.RowDefinition { Height = new Sw.GridLength(1, Sw.GridUnitType.Star) });
        split.RowDefinitions.Add(new Swc.RowDefinition { Height = new Sw.GridLength(1, Sw.GridUnitType.Star) });

        var modsPanel = new Swc.StackPanel();
        modsPanel.Children.Add(Text("Моды (отметь для поиска, можно задать минимум):", 11, true, Rgb(200, 200, 206)));
        foreach (var m in stats.Mods)
        {
            modsPanel.Children.Add(MakeModRow(m, preselect: m.TradeId is not null));
        }

        if (stats.Pseudo.Count > 0)
        {
            modsPanel.Children.Add(Text("Псевдо-моды:", 11, true, Rgb(200, 200, 206)));
            foreach (var m in stats.Pseudo)
            {
                modsPanel.Children.Add(MakeModRow(m, preselect: false));
            }
        }

        var modsScroll = new Swc.ScrollViewer
        {
            Content = modsPanel,
            VerticalScrollBarVisibility = Swc.ScrollBarVisibility.Auto,
        };
        Swc.Grid.SetRow(modsScroll, 0);
        split.Children.Add(modsScroll);

        _listings = new Swc.StackPanel();
        var listingsScroll = new Swc.ScrollViewer
        {
            Content = _listings,
            VerticalScrollBarVisibility = Swc.ScrollBarVisibility.Auto,
            Margin = new Sw.Thickness(0, 8, 0, 0),
        };
        Swc.Grid.SetRow(listingsScroll, 1);
        split.Children.Add(listingsScroll);

        root.Children.Add(split);
        Content = root;
    }

    public void SetNinjaEstimate(string text) =>
        Dispatcher.Invoke(() => _ninja.Text = text);

    public void SetStatus(string text) =>
        Dispatcher.Invoke(() => _status.Text = text);

    public void ShowListings(IReadOnlyList<TradeListing> listings) => Dispatcher.Invoke(() =>
    {
        _listings.Children.Clear();
        _listings.Children.Add(Text($"Листинги ({listings.Count}):", 11, true, Rgb(200, 200, 206)));
        foreach (var l in listings)
        {
            var line = $"{l.Amount:0.##} {l.Currency}   —   {l.Account}";
            _listings.Children.Add(Text(line, 12, false, Rgb(210, 210, 216)));
        }

        if (listings.Count == 0)
        {
            _listings.Children.Add(Text("Ничего не найдено (или слишком строгий фильтр).", 11, false, Rgb(180, 140, 120)));
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
        var row = new Swc.StackPanel { Orientation = Swc.Orientation.Horizontal, Margin = new Sw.Thickness(0, 2, 0, 2) };
        var check = new Swc.CheckBox
        {
            IsChecked = preselect,
            VerticalAlignment = Sw.VerticalAlignment.Center,
            Foreground = Rgb(220, 220, 226),
            Width = 360,
            Content = m.Text,
        };
        var min = new Swc.TextBox
        {
            Width = 60,
            VerticalAlignment = Sw.VerticalAlignment.Center,
            Background = Brush(36, 36, 42),
            Foreground = Rgb(230, 230, 235),
            BorderBrush = Brush(70, 70, 80),
            ToolTip = "минимум",
        };
        row.Children.Add(check);
        row.Children.Add(min);
        _rows.Add((m, check, min));

        return new Swc.Border
        {
            Child = row,
            Padding = new Sw.Thickness(2),
        };
    }

    private static Swc.Button MakeButton(string text) => new()
    {
        Content = text,
        Padding = new Sw.Thickness(12, 4, 12, 4),
        Background = Brush(40, 40, 48),
        Foreground = Rgb(230, 230, 235),
        BorderBrush = Brush(70, 70, 80),
    };

    private static Swc.TextBlock Text(string text, double size, bool bold, Swm.Brush brush) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = bold ? Sw.FontWeights.Bold : Sw.FontWeights.Normal,
        Foreground = brush,
        TextWrapping = Sw.TextWrapping.Wrap,
        Margin = new Sw.Thickness(0, 1, 0, 1),
    };

    private static Swm.SolidColorBrush Rgb(byte r, byte g, byte b) =>
        new(Swm.Color.FromRgb(r, g, b));

    private static Swm.SolidColorBrush Brush(byte r, byte g, byte b) =>
        new(Swm.Color.FromRgb(r, g, b));
}
