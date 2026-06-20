using System.Globalization;
using PriceCheckPoe2.ItemCheck.Data;
using PriceCheckPoe2.ItemCheck.Parsing;
using PriceCheckPoe2.ItemCheck.Trade;
using Sw = System.Windows;
using Swc = System.Windows.Controls;
using Swm = System.Windows.Media;

namespace PriceCheckPoe2.ItemCheck.UI;

/// <summary>
/// WPF-окно price-check (в коде, без XAML, на своём STA-потоке). Тёмный кастомный
/// заголовок, моды с чекбоксами/минимумами и пресетами, оценка poe.ninja, листинги
/// и ссылка «Открыть на trade».
/// </summary>
public sealed class ItemCheckWindow : Sw.Window
{
    private readonly List<(MatchedStat Stat, Swc.CheckBox Check, Swc.TextBox Min, Swc.TextBox Max)> _rows = new();
    private readonly Swc.StackPanel _listings;
    private readonly Swc.TextBlock _status;
    private readonly Swc.TextBlock _ninja;
    private readonly Swc.Button _openSite;
    private string? _tradeUrl;

    public event Action<IReadOnlyList<TradeFilter>>? SearchRequested;

    public ItemCheckWindow(ParsedItem item, ItemStats stats)
    {
        Width = 560;
        Height = 720;
        WindowStartupLocation = Sw.WindowStartupLocation.CenterScreen;
        Topmost = true;
        WindowStyle = Sw.WindowStyle.None;
        ResizeMode = Sw.ResizeMode.CanResize;
        Background = Brush(20, 20, 24);

        var outer = new Swc.Grid();
        outer.RowDefinitions.Add(new Swc.RowDefinition { Height = Sw.GridLength.Auto });
        outer.RowDefinitions.Add(new Swc.RowDefinition { Height = new Sw.GridLength(1, Sw.GridUnitType.Star) });
        outer.Children.Add(BuildTitleBar());

        var grid = new Swc.Grid { Margin = new Sw.Thickness(14, 10, 14, 14) };
        Swc.Grid.SetRow(grid, 1);
        for (var i = 0; i < 4; i++)
        {
            grid.RowDefinitions.Add(new Swc.RowDefinition { Height = Sw.GridLength.Auto });
        }

        grid.RowDefinitions[3].Height = new Sw.GridLength(1, Sw.GridUnitType.Star);

        // --- Заголовок предмета ---
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

        // --- Кнопки поиска/пресетов + статус ---
        var bar = new Swc.StackPanel { Orientation = Swc.Orientation.Horizontal, Margin = new Sw.Thickness(0, 0, 0, 6) };
        var searchBtn = MakeButton("Поиск на трейде", accent: true);
        searchBtn.Click += (_, _) => RaiseSearch();
        bar.Children.Add(searchBtn);

        _openSite = MakeButton("Открыть на trade");
        _openSite.IsEnabled = false;
        _openSite.Margin = new Sw.Thickness(8, 0, 0, 0);
        _openSite.Click += (_, _) => OpenTradeSite();
        bar.Children.Add(_openSite);

        _status = Line("", 11, false, Rgb(150, 150, 158));
        _status.Margin = new Sw.Thickness(12, 8, 0, 0);
        bar.Children.Add(_status);
        Swc.Grid.SetRow(bar, 1);
        grid.Children.Add(bar);

        // --- Моды ---
        var modsHost = new Swc.StackPanel();
        if (stats.Mods.Count == 0 && stats.Pseudo.Count == 0)
        {
            modsHost.Children.Add(Line("Без модов — поиск по типу/имени предмета.", 11, false, Rgb(150, 140, 120)));
        }
        else
        {
            modsHost.Children.Add(BuildPresetBar());
            foreach (var m in stats.Mods)
            {
                modsHost.Children.Add(MakeModRow(m));
            }

            if (stats.Pseudo.Count > 0)
            {
                modsHost.Children.Add(SectionLabel("Псевдо-моды:"));
                foreach (var m in stats.Pseudo)
                {
                    modsHost.Children.Add(MakeModRow(m));
                }
            }
        }

        var modsScroll = new Swc.ScrollViewer
        {
            Content = modsHost,
            VerticalScrollBarVisibility = Swc.ScrollBarVisibility.Auto,
            MaxHeight = 260,
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

        outer.Children.Add(grid);
        Content = outer;
    }

    public void SetNinjaEstimate(string text) => Dispatcher.Invoke(() => _ninja.Text = text);

    public void SetStatus(string text) => Dispatcher.Invoke(() => _status.Text = text);

    public void SetTradeUrl(string url) => Dispatcher.Invoke(() =>
    {
        _tradeUrl = url;
        _openSite.IsEnabled = true;
    });

    public void ShowListings(IReadOnlyList<TradeListing> listings) => Dispatcher.Invoke(() =>
    {
        _listings.Children.Clear();
        _listings.Children.Add(SectionLabel($"Листинги ({listings.Count})"));

        var summary = TradeClient.Summarize(listings);
        if (summary is not null)
        {
            _listings.Children.Add(Line(
                $"Цена: от {summary.Min:0.##} · медиана {summary.Median:0.##} {summary.Currency} (по {summary.Count})",
                12, true, Rgb(235, 205, 120)));
        }

        foreach (var l in listings)
        {
            var row = new Swc.StackPanel { Orientation = Swc.Orientation.Horizontal, Margin = new Sw.Thickness(0, 2, 0, 2) };
            row.Children.Add(new Swc.TextBlock
            {
                Text = $"{l.Amount:0.##} {l.Currency}",
                Width = 110,
                FontWeight = Sw.FontWeights.Bold,
                Foreground = Rgb(230, 200, 110),
            });
            row.Children.Add(StatusDot(l.Status));
            row.Children.Add(new Swc.TextBlock
            {
                Text = l.Ign is { Length: > 0 } ? l.Ign : l.Account,
                Width = 150,
                Foreground = Rgb(200, 200, 208),
                ToolTip = l.Account,
            });
            row.Children.Add(new Swc.TextBlock
            {
                Text = RelativeTime(l.Indexed),
                Foreground = Rgb(140, 140, 150),
                VerticalAlignment = Sw.VerticalAlignment.Center,
            });
            _listings.Children.Add(row);
        }

        if (listings.Count == 0)
        {
            _listings.Children.Add(Line("Ничего не найдено (или фильтр слишком строгий).", 11, false, Rgb(190, 150, 120)));
        }
    });

    private Swc.Border BuildTitleBar()
    {
        var dock = new Swc.DockPanel { LastChildFill = true };

        var close = TitleButton("✕");
        close.Click += (_, _) => Close();
        Swc.DockPanel.SetDock(close, Swc.Dock.Right);
        dock.Children.Add(close);

        var min = TitleButton("—");
        min.Click += (_, _) => WindowState = Sw.WindowState.Minimized;
        Swc.DockPanel.SetDock(min, Swc.Dock.Right);
        dock.Children.Add(min);

        dock.Children.Add(new Swc.TextBlock
        {
            Text = "PriceCheck — предмет",
            Foreground = Rgb(200, 200, 208),
            FontWeight = Sw.FontWeights.SemiBold,
            VerticalAlignment = Sw.VerticalAlignment.Center,
            Margin = new Sw.Thickness(12, 0, 0, 0),
        });

        var bar = new Swc.Border { Background = Brush(30, 30, 38), Height = 34, Child = dock };
        Swc.Grid.SetRow(bar, 0);
        bar.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == Sw.Input.MouseButtonState.Pressed)
            {
                DragMove();
            }
        };
        return bar;
    }

    private Swc.StackPanel BuildPresetBar()
    {
        var panel = new Swc.StackPanel { Orientation = Swc.Orientation.Horizontal, Margin = new Sw.Thickness(0, 0, 0, 4) };
        panel.Children.Add(new Swc.TextBlock
        {
            Text = "Пресеты:",
            Foreground = Rgb(190, 190, 198),
            FontWeight = Sw.FontWeights.Bold,
            VerticalAlignment = Sw.VerticalAlignment.Center,
            Margin = new Sw.Thickness(0, 0, 8, 0),
        });
        panel.Children.Add(PresetButton("только база", s => false));
        panel.Children.Add(PresetButton("ключевые", s => !s.IsPseudo && IsImportant(s.Text)));
        panel.Children.Add(PresetButton("псевдо", s => s.IsPseudo));
        return panel;
    }

    private Swc.Button PresetButton(string text, Func<MatchedStat, bool> select)
    {
        var b = MakeButton(text);
        b.Margin = new Sw.Thickness(0, 0, 6, 0);
        b.Padding = new Sw.Thickness(8, 2, 8, 2);
        b.Click += (_, _) =>
        {
            foreach (var (stat, check, _, _) in _rows)
            {
                if (check.IsEnabled)
                {
                    check.IsChecked = select(stat);
                }
            }
        };
        return b;
    }

    private void OpenTradeSite()
    {
        if (string.IsNullOrEmpty(_tradeUrl))
        {
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_tradeUrl) { UseShellExecute = true });
        }
        catch
        {
            // не критично
        }
    }

    private void RaiseSearch()
    {
        var filters = new List<TradeFilter>();
        foreach (var (stat, check, min, max) in _rows)
        {
            if (check.IsChecked != true || stat.TradeId is null)
            {
                continue;
            }

            double? minValue = double.TryParse(min.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var lo) ? lo : null;
            double? maxValue = double.TryParse(max.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var hi) ? hi : null;
            filters.Add(new TradeFilter(stat.TradeId, minValue, maxValue));
        }

        SetStatus("Поиск…");
        SearchRequested?.Invoke(filters);
    }

    private Swc.Border MakeModRow(MatchedStat m)
    {
        var matched = m.TradeId is not null;
        var row = new Swc.StackPanel { Orientation = Swc.Orientation.Horizontal };
        var check = new Swc.CheckBox
        {
            IsChecked = false, // по умолчанию ничего не отмечаем — выбор за игроком/пресетом
            IsEnabled = matched,
            VerticalAlignment = Sw.VerticalAlignment.Center,
            Foreground = !matched ? Rgb(120, 120, 128)
                : m.IsPseudo ? Rgb(150, 200, 235)
                : Rgb(222, 222, 228),
            Width = 380,
            Content = matched ? m.Text : m.Text + "  (нет в базе)",
        };
        var min = MakeBoundBox("минимум");
        var max = MakeBoundBox("максимум");

        // Границы поиска как в EE2: окно ±10% вокруг ролла с учётом направления
        // «лучше» (для negative-роллов заполняем max) и инверсии стата.
        if (matched && m.Value is { } v)
        {
            var (lo, hi) = StatFilterMath.FilterBounds(v, m.Better, m.Dp, m.Inverted);
            if (lo.HasValue) min.Text = FormatBound(lo.Value, m.Dp);
            if (hi.HasValue) max.Text = FormatBound(hi.Value, m.Dp);
        }

        row.Children.Add(check);
        row.Children.Add(min);
        row.Children.Add(max);
        _rows.Add((m, check, min, max));
        return new Swc.Border { Child = row, Padding = new Sw.Thickness(2, 3, 2, 3) };
    }

    private Swc.TextBox MakeBoundBox(string tip) => new()
    {
        Width = 56,
        Margin = new Sw.Thickness(2, 0, 0, 0),
        VerticalAlignment = Sw.VerticalAlignment.Center,
        Background = Brush(36, 36, 42),
        Foreground = Rgb(230, 230, 235),
        BorderBrush = Brush(70, 70, 80),
        ToolTip = tip,
    };

    private static string FormatBound(double value, bool dp) =>
        dp ? value.ToString("0.##", CultureInfo.InvariantCulture)
           : ((long)Math.Round(value)).ToString(CultureInfo.InvariantCulture);

    // Цветной индикатор статуса продавца: зелёный — онлайн, жёлтый — afk, серый — офлайн.
    private Swc.TextBlock StatusDot(Trade.SellerStatus status) => new()
    {
        Text = "●",
        Width = 16,
        TextAlignment = Sw.TextAlignment.Center,
        VerticalAlignment = Sw.VerticalAlignment.Center,
        ToolTip = status switch
        {
            Trade.SellerStatus.Online => "онлайн",
            Trade.SellerStatus.Afk => "afk",
            _ => "офлайн",
        },
        Foreground = status switch
        {
            Trade.SellerStatus.Online => Rgb(110, 200, 110),
            Trade.SellerStatus.Afk => Rgb(220, 200, 110),
            _ => Rgb(120, 120, 128),
        },
    };

    // Относительное время листинга («5 мин назад», «3 ч назад», «2 дн назад»).
    private static string RelativeTime(DateTime? indexed)
    {
        if (indexed is not { } dt)
        {
            return string.Empty;
        }

        var span = DateTime.UtcNow - dt.ToUniversalTime();
        if (span < TimeSpan.Zero)
        {
            span = TimeSpan.Zero;
        }

        if (span.TotalMinutes < 1)
        {
            return "только что";
        }

        if (span.TotalHours < 1)
        {
            return $"{(int)span.TotalMinutes} мин назад";
        }

        if (span.TotalDays < 1)
        {
            return $"{(int)span.TotalHours} ч назад";
        }

        return $"{(int)span.TotalDays} дн назад";
    }

    private static bool IsImportant(string text)
    {
        var t = text.ToLowerInvariant();
        return t.Contains("maximum life")
            || t.Contains("resistance")
            || t.Contains("attribute")
            || t.Contains("to spirit");
    }

    private Swc.Button TitleButton(string glyph) => new()
    {
        Content = glyph,
        Width = 40,
        Background = Swm.Brushes.Transparent,
        Foreground = Rgb(200, 200, 208),
        BorderThickness = new Sw.Thickness(0),
        FontWeight = Sw.FontWeights.Bold,
        Cursor = Sw.Input.Cursors.Hand,
    };

    private static Swc.Button MakeButton(string text, bool accent = false) => new()
    {
        Content = text,
        Padding = new Sw.Thickness(14, 5, 14, 5),
        Background = accent ? Brush(56, 84, 120) : Brush(46, 46, 56),
        Foreground = Rgb(232, 232, 238),
        BorderBrush = Brush(80, 80, 92),
        FontWeight = Sw.FontWeights.SemiBold,
        Cursor = Sw.Input.Cursors.Hand,
    };

    private static Swc.TextBlock SectionLabel(string text) => Line(text, 11, true, Rgb(190, 190, 198));

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
