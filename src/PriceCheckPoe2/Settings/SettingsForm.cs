using System.Drawing;
using System.Windows.Forms;
using PriceCheckPoe2.Config;

namespace PriceCheckPoe2.Settings;

/// <summary>
/// Окно настроек (WinForms). Правит лигу, источник цен, интервал обновления,
/// хоткеи и прозрачности, сохраняет в <see cref="AppConfig"/>.
/// Примечание: план предполагал WPF+MahApps; для единого UI-стека и надёжной
/// сборки сделано на WinForms — стилизацию можно вернуть позже.
/// </summary>
public sealed class SettingsForm : Form
{
    private readonly AppConfig _config;

    private readonly TextBox _league = new();
    private readonly TextBox _apiUrl = new();
    private readonly NumericUpDown _refresh = new() { Minimum = 1, Maximum = 240 };
    private readonly TextBox _menuHotkey = new();
    private readonly TextBox _recalibrateHotkey = new();
    private readonly TrackBar _dim = new() { Minimum = 0, Maximum = 100 };
    private readonly TrackBar _overlayOpacity = new() { Minimum = 0, Maximum = 100 };
    private readonly NumericUpDown _ocrThreshold = new() { Minimum = 0, Maximum = 255 };
    private readonly CheckBox _saveDebug = new() { Text = "Сохранять кадры OCR" };
    private readonly TextBox _itemHotkey = new();
    private readonly CheckBox _itemUseCtrl = new() { Text = "с Ctrl" };
    private readonly CheckBox _itemSendCopy = new() { Text = "слать Ctrl+C (для не-Ctrl+C хоткея)" };

    public SettingsForm(AppConfig config)
    {
        _config = config;

        Text = "PriceCheck PoE2 — Настройки";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(460, 560);

        BuildLayout();
        LoadFromConfig();
    }

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
            AutoSize = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(layout, "Лига", _league);
        AddRow(layout, "URL цен (poe.ninja)", _apiUrl);
        AddRow(layout, "Обновление цен, мин", _refresh);
        AddRow(layout, "Хоткей меню", _menuHotkey);
        AddRow(layout, "Хоткей калибровки", _recalibrateHotkey);
        AddRow(layout, "Затемнение меню, %", _dim);
        AddRow(layout, "Прозрачность оверлея, %", _overlayOpacity);
        AddRow(layout, "Порог OCR (0..255)", _ocrThreshold);
        AddRow(layout, "Отладка OCR", _saveDebug);
        AddRow(layout, "Хоткей предмета (KeyCode)", _itemHotkey);
        AddRow(layout, "Модификатор предмета", _itemUseCtrl);
        AddRow(layout, "Авто-копирование", _itemSendCopy);

        var save = new Button { Text = "Сохранить", DialogResult = DialogResult.OK, Width = 100 };
        save.Click += (_, _) => SaveToConfig();
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Width = 100 };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 44,
            Padding = new Padding(8),
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);

        Controls.Add(layout);
        Controls.Add(buttons);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private static void AddRow(TableLayoutPanel layout, string label, Control control)
    {
        control.Dock = DockStyle.Fill;
        layout.Controls.Add(new Label
        {
            Text = label,
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill,
        });
        layout.Controls.Add(control);
    }

    private void LoadFromConfig()
    {
        _league.Text = _config.League;
        _apiUrl.Text = _config.PriceApiBaseUrl;
        _refresh.Value = Math.Clamp(_config.PriceRefreshMinutes, 1, 240);
        _menuHotkey.Text = _config.MenuHotkey;
        _recalibrateHotkey.Text = _config.RecalibrateHotkey;
        _dim.Value = (int)Math.Clamp(_config.MenuDimOpacity * 100, 0, 100);
        _overlayOpacity.Value = (int)Math.Clamp(_config.PriceOverlayOpacity * 100, 0, 100);
        _ocrThreshold.Value = Math.Clamp(_config.OcrThreshold, 0, 255);
        _saveDebug.Checked = _config.SaveOcrDebugImages;
        _itemHotkey.Text = _config.ItemCheckHotkey;
        _itemUseCtrl.Checked = _config.ItemCheckUseCtrl;
        _itemSendCopy.Checked = _config.ItemCheckSendCopy;
    }

    private void SaveToConfig()
    {
        _config.League = _league.Text.Trim();
        _config.PriceApiBaseUrl = _apiUrl.Text.Trim();
        _config.PriceRefreshMinutes = (int)_refresh.Value;
        _config.MenuHotkey = _menuHotkey.Text.Trim();
        _config.RecalibrateHotkey = _recalibrateHotkey.Text.Trim();
        _config.MenuDimOpacity = _dim.Value / 100.0;
        _config.PriceOverlayOpacity = _overlayOpacity.Value / 100.0;
        _config.OcrThreshold = (int)_ocrThreshold.Value;
        _config.SaveOcrDebugImages = _saveDebug.Checked;
        _config.ItemCheckHotkey = _itemHotkey.Text.Trim();
        _config.ItemCheckUseCtrl = _itemUseCtrl.Checked;
        _config.ItemCheckSendCopy = _itemSendCopy.Checked;
        _config.Save();
    }
}
