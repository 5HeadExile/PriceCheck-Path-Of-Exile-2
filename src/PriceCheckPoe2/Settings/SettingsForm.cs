using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using PriceCheckPoe2.Config;
using PriceCheckPoe2.Theme;

namespace PriceCheckPoe2.Settings;

/// <summary>
/// Окно настроек (дизайн-система «Воронёная сталь и сдержанное золото»).
/// Секции Общие · Хоткеи · Внешний вид · Отладка; слайдеры со значением,
/// keycap-поля хоткеев, приглушённая секция «Отладка». Сохраняет в
/// <see cref="AppConfig"/>.
/// </summary>
public sealed class SettingsForm : RoundedForm
{
    private static int Pad => Ui.S(16);
    private static int LabelW => Ui.S(150);
    private readonly AppConfig _config;

    private readonly TextBox _league = Field();
    private readonly TextBox _apiUrl = Field(muted: true);
    private readonly NumericUpDown _refresh = new() { Minimum = 1, Maximum = 240 };
    private readonly NumericUpDown _ocrThreshold = new() { Minimum = 0, Maximum = 255 };
    private readonly KeycapInput _menuHotkey = new();
    private readonly KeycapInput _recalibrateHotkey = new();
    private readonly SliderControl _dim = new();
    private readonly SliderControl _overlayOpacity = new();
    private readonly ThemedCheckBox _saveDebug = new();

    public SettingsForm(AppConfig config)
    {
        _config = config;
        TopMost = true;
        KeyPreview = true;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(Ui.S(440), Ui.S(540));
        StyleNumeric(_refresh);
        StyleNumeric(_ocrThreshold);

        BuildLayout();
        LoadFromConfig();
    }

    private void BuildLayout()
    {
        var w = ClientSize.Width - Pad * 2;
        int y = Ui.S(56);

        // ОБЩИЕ
        Section("Общие", ref y, w);
        Row("Лига", _league, ref y, w);
        Row("Обновление цен", _refresh, ref y, w, controlWidth: Ui.S(90));
        y += Ui.S(6);

        // ХОТКЕИ
        Section("Хоткеи", ref y, w);
        Row("Хоткей меню", _menuHotkey, ref y, w, controlWidth: Ui.S(60));
        Row("Хоткей калибровки", _recalibrateHotkey, ref y, w, controlWidth: Ui.S(60));
        y += Ui.S(6);

        // ВНЕШНИЙ ВИД
        Section("Внешний вид", ref y, w);
        Row("Затемнение меню", _dim, ref y, w, controlWidth: w - LabelW);
        Row("Прозрачность", _overlayOpacity, ref y, w, controlWidth: w - LabelW);
        y += Ui.S(6);

        // ОТЛАДКА (приглушённая)
        Controls.Add(new SectionHeader("Отладка", SectionTone.Faint, "ДОПОЛНИТЕЛЬНО")
        {
            Location = new Point(Pad, y),
            Size = new Size(w, Ui.S(18)),
        });
        y += Ui.S(22);
        Row("URL цен", _apiUrl, ref y, w, muted: true);
        Row("Порог OCR", _ocrThreshold, ref y, w, controlWidth: Ui.S(90), muted: true);
        Row("Отладка OCR", _saveDebug, ref y, w, muted: true);

        BuildFooter();
    }

    private void BuildFooter()
    {
        var footer = new Panel { Dock = DockStyle.Bottom, Height = Ui.S(54), BackColor = Color.Transparent };
        footer.Paint += (_, pe) =>
        {
            using var pen = new Pen(Palette.BorderFaint, 1f);
            pe.Graphics.DrawLine(pen, Pad, 0, footer.Width - Pad, 0);
        };
        Controls.Add(footer); // добавляем первым — Dock задаёт реальную ширину

        int bw = Ui.S(120), cbw = Ui.S(100), bh = Ui.S(32), by = Ui.S(11);
        var save = new ThemedButton { Text = "Сохранить", Variant = ButtonVariant.GoldFill };
        save.SetBounds(footer.Width - Pad - bw, by, bw, bh);
        save.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        save.Click += (_, _) => { SaveToConfig(); DialogResult = DialogResult.OK; Close(); };

        var cancel = new ThemedButton { Text = "Отмена", Variant = ButtonVariant.Normal };
        cancel.SetBounds(footer.Width - Pad - bw - cbw - Ui.S(8), by, cbw, bh);
        cancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        footer.Controls.Add(save);
        footer.Controls.Add(cancel);
    }

    private void Section(string caption, ref int y, int w)
    {
        Controls.Add(new SectionHeader(caption) { Location = new Point(Pad, y), Size = new Size(w, Ui.S(18)) });
        y += Ui.S(22);
    }

    private void Row(string label, Control control, ref int y, int w, int? controlWidth = null, bool muted = false)
    {
        var lbl = new Label
        {
            Text = label,
            ForeColor = muted ? Palette.TextFaint : Palette.TextMuted,
            Font = Palette.Label(),
            AutoSize = false,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Location = new Point(Pad, y),
            Size = new Size(LabelW - Ui.S(8), Ui.S(30)),
            BackColor = Color.Transparent,
        };
        Controls.Add(lbl);

        var rowH = Ui.S(30);
        var cw = controlWidth ?? (w - LabelW);
        if (control is TextBox)
        {
            control.Size = new Size(cw, Ui.S(26));
        }
        else if (control is not NumericUpDown)
        {
            control.Width = cw;
        }

        control.Location = new Point(Pad + LabelW, y + (rowH - control.Height) / 2);
        Controls.Add(control);
        y += Ui.S(34);
    }

    private static TextBox Field(bool muted = false) => new()
    {
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = muted ? Palette.FieldMutedBg : Palette.InputBg,
        ForeColor = muted ? Palette.Hex("#7A766C") : Palette.Text,
        Font = Palette.FieldText(),
    };

    private static void StyleNumeric(NumericUpDown n)
    {
        n.BorderStyle = BorderStyle.FixedSingle;
        n.BackColor = Palette.InputBg;
        n.ForeColor = Palette.Text;
        n.Font = Palette.FieldText();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // Титулбар
        var barH = Ui.S(46);
        var dsz = Ui.S(10);
        var drect = new Rectangle(-dsz / 2, -dsz / 2, dsz, dsz);
        var state = g.Save();
        g.TranslateTransform(Ui.S(20), barH / 2);
        g.RotateTransform(45);
        using (var brush = new LinearGradientBrush(drect,
            Palette.AccentLighter, Palette.AccentDark, LinearGradientMode.ForwardDiagonal))
        {
            g.FillRectangle(brush, drect);
        }

        g.Restore(state);

        using var title = Palette.Title();
        TextRenderer.DrawText(g, "Настройки", title, new Rectangle(Ui.S(36), 0, Ui.S(200), barH), Palette.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        // Кнопка закрытия (рисуем глиф; клик ловим в OnMouseDown)
        using var icon = Palette.Icon(12f);
        TextRenderer.DrawText(g, "", icon, new Rectangle(ClientSize.Width - Ui.S(40), 0, Ui.S(28), barH),
            Palette.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        using var pen = new Pen(Palette.BorderFaint, 1f);
        g.DrawLine(pen, 1, barH, ClientSize.Width - 2, barH);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        base.OnKeyDown(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        // Клик по «✕» в правом верхнем углу — закрыть как «Отмена».
        var closeRect = new Rectangle(ClientSize.Width - Ui.S(40), Ui.S(10), Ui.S(30), Ui.S(28));
        if (closeRect.Contains(e.Location))
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }

        base.OnMouseDown(e);
    }

    private void LoadFromConfig()
    {
        _league.Text = _config.League;
        _apiUrl.Text = _config.PriceApiBaseUrl;
        _refresh.Value = Math.Clamp(_config.PriceRefreshMinutes, 1, 240);
        _menuHotkey.SetKey(_config.MenuHotkey);
        _recalibrateHotkey.SetKey(_config.RecalibrateHotkey);
        _dim.Value = (int)Math.Clamp(_config.MenuDimOpacity * 100, 0, 100);
        _overlayOpacity.Value = (int)Math.Clamp(_config.PriceOverlayOpacity * 100, 0, 100);
        _ocrThreshold.Value = Math.Clamp(_config.OcrThreshold, 0, 255);
        _saveDebug.Checked = _config.SaveOcrDebugImages;
    }

    private void SaveToConfig()
    {
        _config.League = _league.Text.Trim();
        _config.PriceApiBaseUrl = _apiUrl.Text.Trim();
        _config.PriceRefreshMinutes = (int)_refresh.Value;
        if (!string.IsNullOrEmpty(_menuHotkey.KeyCodeName)) _config.MenuHotkey = _menuHotkey.KeyCodeName;
        if (!string.IsNullOrEmpty(_recalibrateHotkey.KeyCodeName)) _config.RecalibrateHotkey = _recalibrateHotkey.KeyCodeName;
        _config.MenuDimOpacity = _dim.Value / 100.0;
        _config.PriceOverlayOpacity = _overlayOpacity.Value / 100.0;
        _config.OcrThreshold = (int)_ocrThreshold.Value;
        _config.SaveOcrDebugImages = _saveDebug.Checked;
        _config.Save();
    }
}
