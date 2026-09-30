using System;
using System.Drawing;
using System.Windows.Forms;
using NAudio.CoreAudioApi;

namespace VolumeMixer;

public sealed class SessionRow : UserControl
{
    private readonly SimpleAudioVolume? _volume;
    private readonly string _defaultName;
    private readonly string? _stableKey;
    private readonly SettingsStore _settings;
    private readonly Image? _icon;

    private readonly Label _nameLabel;
    private readonly TrackBar _volumeBar;
    private readonly Label _volumeValueLabel;
    private readonly CheckBox _muteButton;
    private readonly ToolTip _toolTip = new();

    private string _name;
    private bool _updating;

    public string SessionId { get; }

    public SessionRow(
        SimpleAudioVolume? volume,
        string displayName,
        string defaultName,
        string? stableKey,
        Image? icon,
        SettingsStore settings,
        string sessionId)
    {
        _volume = volume;
        _defaultName = defaultName;
        _stableKey = stableKey;
        _settings = settings;
        _icon = icon;
        _name = displayName;
        SessionId = sessionId;

        if (_volume is not null && _stableKey is not null)
        {
            if (_settings.GetMuted(_stableKey) is true)
            {
                try { _volume.Mute = true; } catch { }
            }
        }

        Height = 50;
        Dock = DockStyle.Top;
        Margin = new Padding(0, 0, 0, 4);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            Padding = new Padding(6, 2, 6, 2)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var iconBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            Margin = new Padding(0, 4, 2, 4)
        };
        if (icon is not null)
        {
            iconBox.Image = icon;
        }

        _nameLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Font = new Font("Segoe UI", 10f),
            Text = _name,
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 8, 0)
        };
        _nameLabel.DoubleClick += NameLabel_DoubleClick;
        _toolTip.SetToolTip(_nameLabel, "Double-click to rename");

        _volumeBar = new TrackBar
        {
            Dock = DockStyle.Fill,
            Minimum = 0,
            Maximum = 100,
            TickStyle = TickStyle.None,
            SmallChange = 1,
            LargeChange = 5
        };
        _volumeBar.ValueChanged += VolumeBar_ValueChanged;

        _volumeValueLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "0%"
        };

        _muteButton = new CheckBox
        {
            Dock = DockStyle.Fill,
            Appearance = Appearance.Button,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "Mute",
            Margin = new Padding(8, 0, 0, 0)
        };
        _muteButton.CheckedChanged += MuteButton_CheckedChanged;

        table.Controls.Add(iconBox, 0, 0);
        table.Controls.Add(_nameLabel, 1, 0);
        table.Controls.Add(_volumeBar, 2, 0);
        table.Controls.Add(_volumeValueLabel, 3, 0);
        table.Controls.Add(_muteButton, 4, 0);

        Controls.Add(table);
    }

    public void RefreshState()
    {
        _updating = true;
        try
        {
            _nameLabel.Text = _name;

            if (_volume is not null)
            {
                int value = (int)Math.Round(_volume.Volume * 100f);
                value = Math.Clamp(value, 0, 100);

                if (!_volumeBar.Capture)
                {
                    _volumeBar.Value = value;
                    _volumeValueLabel.Text = $"{value}%";
                }

                _muteButton.Checked = _volume.Mute;
            }
            else
            {
                _volumeBar.Enabled = false;
                _muteButton.Enabled = false;
                _volumeValueLabel.Text = "—";
            }

            UpdateMuteAppearance();
        }
        catch
        {
        }
        finally
        {
            _updating = false;
        }
    }

    private void VolumeBar_ValueChanged(object? sender, EventArgs e)
    {
        if (_updating || _volume is null)
        {
            return;
        }

        _volume.Volume = _volumeBar.Value / 100f;
        _volumeValueLabel.Text = $"{_volumeBar.Value}%";
    }

    private void MuteButton_CheckedChanged(object? sender, EventArgs e)
    {
        if (_updating || _volume is null)
        {
            return;
        }

        _volume.Mute = _muteButton.Checked;
        UpdateMuteAppearance();

        if (_stableKey is not null)
        {
            _settings.SetMuted(_stableKey, _muteButton.Checked);
        }
    }

    private void NameLabel_DoubleClick(object? sender, EventArgs e)
    {
        using var dialog = new RenameDialog(_nameLabel.Text);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        string newName = dialog.NewName;
        if (string.IsNullOrWhiteSpace(newName))
        {
            if (_stableKey is not null)
            {
                _settings.ClearCustomName(_stableKey);
            }

            _name = _defaultName;
        }
        else
        {
            if (_stableKey is not null)
            {
                _settings.SetCustomName(_stableKey, newName);
            }

            _name = newName;
        }

        _nameLabel.Text = _name;
    }

    private void UpdateMuteAppearance()
    {
        if (_muteButton.Checked)
        {
            _muteButton.Text = "Muted";
            _muteButton.BackColor = Color.FromArgb(210, 70, 70);
            _muteButton.ForeColor = Color.White;
        }
        else
        {
            _muteButton.Text = "Mute";
            _muteButton.BackColor = SystemColors.Control;
            _muteButton.ForeColor = SystemColors.ControlText;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
            _icon?.Dispose();
            _volume?.Dispose();
        }

        base.Dispose(disposing);
    }
}
