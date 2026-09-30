using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using NAudio.CoreAudioApi;

namespace VolumeMixer;

public sealed class MainForm : Form
{
    private MMDeviceEnumerator? _enumerator;
    private MMDevice? _device;
    private AudioSessionManager? _sessionManager;
    private AudioEndpointVolume? _masterVolume;

    private readonly Panel _sessionsPanel = new();
    private readonly Label _statusLabel = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new();

    private readonly TrackBar _masterTrackBar = new();
    private readonly CheckBox _masterMuteButton = new();
    private readonly Label _masterValueLabel = new();

    private readonly Dictionary<string, SessionRow> _rows = new();
    private List<string> _order = new();
    private bool _updatingMaster;
    private readonly SettingsStore _settings = new(SettingsStore.GetDefaultPath());
    private int _lastAdjustedRowCount = -1;

    private NotifyIcon? _notifyIcon;
    private Icon? _trayIcon;
    private IntPtr _trayIconHandle;
    private bool _isExiting;
    private bool _trayHintShown;

    public MainForm()
    {
        Text = "Volume Mixer";
        MinimumSize = new Size(680, 400);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);
        ClientSize = new Size(900, 520);

        BuildUi();

        SetupTray();

        _refreshTimer.Interval = 1500;
        _refreshTimer.Tick += (_, _) => RefreshAll();

        Load += (_, _) =>
        {
            try
            {
                InitializeAudio();
                RefreshAll();
                _refreshTimer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Failed to initialize the audio system:\n" + ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        };

        FormClosing += (_, e) =>
        {
            if (e.CloseReason == CloseReason.WindowsShutDown ||
                e.CloseReason == CloseReason.TaskManagerClosing)
            {
                _isExiting = true;
                return;
            }

            if (!_isExiting)
            {
                e.Cancel = true;
                HideToTray();
            }
        };

        FormClosed += (_, _) =>
        {
            _refreshTimer.Stop();
            DisposeTray();
            _enumerator?.Dispose();
            _device?.Dispose();
        };

        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized)
            {
                HideToTray();
            }
        };
    }

    private void BuildUi()
    {
        var masterPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 66,
            Padding = new Padding(8, 8, 8, 4)
        };

        var masterTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1
        };
        masterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        masterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        masterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
        masterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        masterTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        var masterLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Master volume:",
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };

        _masterTrackBar.Minimum = 0;
        _masterTrackBar.Maximum = 100;
        _masterTrackBar.TickStyle = TickStyle.None;
        _masterTrackBar.Dock = DockStyle.Fill;
        _masterTrackBar.ValueChanged += (_, _) =>
        {
            if (_updatingMaster || _masterVolume is null)
            {
                return;
            }

            _masterVolume.MasterVolumeLevelScalar = _masterTrackBar.Value / 100f;
            _masterValueLabel.Text = $"{_masterTrackBar.Value}%";
        };

        _masterValueLabel.Dock = DockStyle.Fill;
        _masterValueLabel.TextAlign = ContentAlignment.MiddleCenter;
        _masterValueLabel.Text = "—";

        _masterMuteButton.Dock = DockStyle.Fill;
        _masterMuteButton.Appearance = Appearance.Button;
        _masterMuteButton.FlatStyle = FlatStyle.Flat;
        _masterMuteButton.TextAlign = ContentAlignment.MiddleCenter;
        _masterMuteButton.Text = "Mute";
        _masterMuteButton.CheckedChanged += (_, _) =>
        {
            if (_updatingMaster || _masterVolume is null)
            {
                return;
            }

            _masterVolume.Mute = _masterMuteButton.Checked;
            UpdateMasterMuteAppearance();
        };

        masterTable.Controls.Add(masterLabel, 0, 0);
        masterTable.Controls.Add(_masterTrackBar, 1, 0);
        masterTable.Controls.Add(_masterValueLabel, 2, 0);
        masterTable.Controls.Add(_masterMuteButton, 3, 0);

        masterPanel.Controls.Add(masterTable);

        _sessionsPanel.Dock = DockStyle.Fill;
        _sessionsPanel.AutoScroll = true;
        _sessionsPanel.Padding = new Padding(8, 4, 8, 4);

        _statusLabel.Dock = DockStyle.Bottom;
        _statusLabel.Height = 26;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.Padding = new Padding(8, 0, 0, 0);
        _statusLabel.Text = "Loading...";

        Controls.Add(_sessionsPanel);
        Controls.Add(_statusLabel);
        Controls.Add(masterPanel);
    }

    private void InitializeAudio()
    {
        _enumerator = new MMDeviceEnumerator();
        _device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        _sessionManager = _device.AudioSessionManager;
        _masterVolume = _device.AudioEndpointVolume;
    }

    private void RefreshAll()
    {
        RefreshMaster();
        RefreshSessions();
    }

    private void RefreshMaster()
    {
        if (_masterVolume is null)
        {
            return;
        }

        _updatingMaster = true;
        try
        {
            if (!_masterTrackBar.Capture)
            {
                int value = (int)Math.Round(_masterVolume.MasterVolumeLevelScalar * 100f);
                _masterTrackBar.Value = Math.Clamp(value, 0, 100);
            }

            _masterValueLabel.Text = $"{_masterTrackBar.Value}%";
            _masterMuteButton.Checked = _masterVolume.Mute;
            UpdateMasterMuteAppearance();
        }
        catch
        {
        }
        finally
        {
            _updatingMaster = false;
        }
    }

    private void UpdateMasterMuteAppearance()
    {
        if (_masterMuteButton.Checked)
        {
            _masterMuteButton.Text = "Muted";
            _masterMuteButton.BackColor = Color.FromArgb(210, 70, 70);
            _masterMuteButton.ForeColor = Color.White;
        }
        else
        {
            _masterMuteButton.Text = "Mute";
            _masterMuteButton.BackColor = SystemColors.Control;
            _masterMuteButton.ForeColor = SystemColors.ControlText;
        }
    }

    private void RefreshSessions()
    {
        if (_sessionManager is null)
        {
            return;
        }

        List<AudioSessionControl> sessions = new();
        try
        {
            _sessionManager.RefreshSessions();
            SessionCollection sessionCollection = _sessionManager.Sessions;
            for (int i = 0; i < sessionCollection.Count; i++)
            {
                sessions.Add(sessionCollection[i]);
            }
        }
        catch
        {
            return;
        }

        var infos = new List<SessionInfo>();
        foreach (AudioSessionControl session in sessions)
        {
            (string defaultName, string? key) = ResolveSessionInfo(session);
            string displayName = key is not null ? (_settings.GetCustomName(key) ?? defaultName) : defaultName;
            infos.Add(new SessionInfo(session, GetSessionId(session), key, defaultName, displayName));
        }

        infos.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase));

        var newOrder = new List<string>();

        foreach (SessionInfo info in infos)
        {
            newOrder.Add(info.Id);

            if (_rows.ContainsKey(info.Id))
            {
                continue;
            }

            try
            {
                SimpleAudioVolume? volume = null;
                try
                {
                    volume = info.Session.SimpleAudioVolume;
                }
                catch
                {
                }

                Image? icon = ExtractIcon(info.Session);
                _rows[info.Id] = new SessionRow(volume, info.DisplayName, info.DefaultName, info.Key, icon, _settings, info.Id);
            }
            catch
            {
            }
        }

        foreach (string id in _rows.Keys.ToList())
        {
            if (!newOrder.Contains(id))
            {
                _rows[id].Dispose();
                _rows.Remove(id);
            }
        }

        bool orderChanged = !_order.SequenceEqual(newOrder);
        if (orderChanged)
        {
            RebuildPanel(newOrder);
        }

        _order = newOrder;

        foreach (string id in newOrder)
        {
            if (_rows.TryGetValue(id, out SessionRow? row))
            {
                row.RefreshState();
            }
        }

        UpdateStatus();
    }

    private void RebuildPanel(IReadOnlyList<string> order)
    {
        _sessionsPanel.SuspendLayout();
        _sessionsPanel.Controls.Clear();

        for (int i = order.Count - 1; i >= 0; i--)
        {
            if (_rows.TryGetValue(order[i], out SessionRow? row))
            {
                _sessionsPanel.Controls.Add(row);
            }
        }

        _sessionsPanel.ResumeLayout();
    }

    private void UpdateStatus()
    {
        _statusLabel.Text = _rows.Count == 0
            ? "No active audio programs."
            : $"Active audio programs: {_rows.Count}";

        AdjustWindowToContent();
    }

    private void AdjustWindowToContent()
    {
        if (WindowState == FormWindowState.Maximized)
        {
            return;
        }

        int rows = _rows.Count;
        if (rows == _lastAdjustedRowCount)
        {
            return;
        }

        _lastAdjustedRowCount = rows;

        const int rowExtent = 54;    // row height (50) + bottom margin (4)
        const int chromeHeight = 100; // master panel + status bar + list padding
        const int minHeight = 520;    // never shrink below the startup size

        int target = chromeHeight + rows * rowExtent + 10;
        var screen = Screen.FromControl(this);
        int maxHeight = Math.Max(minHeight, (int)(screen.WorkingArea.Height * 0.9));
        int newHeight = Math.Clamp(target, minHeight, maxHeight);

        if (ClientSize.Height != newHeight)
        {
            ClientSize = new Size(ClientSize.Width, newHeight);
        }
    }

    private void SetupTray()
    {
        _notifyIcon = new NotifyIcon
        {
            Text = "Volume Mixer",
            Visible = true
        };

        using var bmp = CreateSpeakerIcon();
        _trayIconHandle = bmp.GetHicon();
        _trayIcon = Icon.FromHandle(_trayIconHandle);
        _notifyIcon.Icon = _trayIcon;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Show Volume Mixer", null, (_, _) => ShowWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());
        _notifyIcon.ContextMenuStrip = menu;

        _notifyIcon.DoubleClick += (_, _) => ShowWindow();
    }

    private void HideToTray()
    {
        // Restore from minimized before hiding, otherwise the window gets
        // stuck in a hidden+minimized state and cannot come back.
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        Hide();
        ShowInTaskbar = false;

        if (!_trayHintShown && _notifyIcon is not null)
        {
            _trayHintShown = true;
            _notifyIcon.ShowBalloonTip(2000, "Volume Mixer", "Still running in the system tray.", ToolTipIcon.Info);
        }
    }

    private void ShowWindow()
    {
        WindowState = FormWindowState.Normal;
        Show();
        ShowInTaskbar = true;
        Activate();
        BringToFront();
    }

    private void ExitApplication()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        Close();
    }

    private void DisposeTray()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _trayIcon?.Dispose();
        _trayIcon = null;

        if (_trayIconHandle != IntPtr.Zero)
        {
            DestroyIcon(_trayIconHandle);
            _trayIconHandle = IntPtr.Zero;
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private static string GetSessionId(AudioSessionControl session)
    {
        try
        {
            string? id = session.GetSessionIdentifier;
            if (!string.IsNullOrEmpty(id))
            {
                return id;
            }
        }
        catch
        {
        }

        try
        {
            return $"{session.GetProcessID}_{session.DisplayName}";
        }
        catch
        {
            return Guid.NewGuid().ToString();
        }
    }

    private static (string Name, string? Key) ResolveSessionInfo(AudioSessionControl session)
    {
        string name = "";
        string? key = null;
        bool resourceReference = false;

        // Some sessions (e.g. "System Sounds") report a raw resource reference
        // like "@%SystemRoot%\System32\AudioSrv.Dll,-202" instead of a name.
        try
        {
            string displayName = session.DisplayName;
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                if (displayName.StartsWith("@"))
                {
                    resourceReference = true;
                }
                else
                {
                    name = displayName;
                }
            }
        }
        catch
        {
        }

        try
        {
            uint pid = session.GetProcessID;
            if (pid > 0)
            {
                using var process = System.Diagnostics.Process.GetProcessById((int)pid);
                if (!string.IsNullOrWhiteSpace(process.ProcessName))
                {
                    key = process.ProcessName.ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        name = process.ProcessName;
                    }
                }
            }
        }
        catch
        {
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = resourceReference ? "System Sounds" : "Unknown program";
        }

        return (name, key);
    }

    private static Image ExtractIcon(AudioSessionControl session)
    {
        try
        {
            uint pid = session.GetProcessID;
            if (pid == 0)
            {
                return CreateSpeakerIcon();
            }

            using var process = System.Diagnostics.Process.GetProcessById((int)pid);
            string? path = null;
            try
            {
                path = process.MainModule?.FileName;
            }
            catch
            {
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return CreateSpeakerIcon();
            }

            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon is null)
            {
                return CreateSpeakerIcon();
            }

            return icon.ToBitmap();
        }
        catch
        {
            return CreateSpeakerIcon();
        }
    }

    private static Bitmap CreateSpeakerIcon()
    {
        const int size = 32;
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        float s = size / 256f;

        // Rounded-square background with a vertical gradient.
        var rect = new RectangleF(4 * s, 4 * s, 248 * s, 248 * s);
        using (var path = RoundedRect(rect, 58 * s))
        using (var brush = new LinearGradientBrush(
            rect,
            Color.FromArgb(90, 200, 250),
            Color.FromArgb(0, 122, 255),
            LinearGradientMode.Vertical))
        {
            g.FillPath(brush, path);
        }

        // White speaker glyph.
        using (var white = new SolidBrush(Color.White))
        {
            g.FillRectangle(white, 48 * s, 112 * s, 32 * s, 32 * s);
            g.FillPolygon(white, new[]
            {
                new PointF(80 * s, 112 * s),
                new PointF(140 * s, 72 * s),
                new PointF(140 * s, 184 * s),
                new PointF(80 * s, 144 * s)
            });
        }

        // Sound waves.
        using (var pen = new Pen(Color.White, 16 * s))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            g.DrawArc(pen, 100 * s, 88 * s, 80 * s, 80 * s, -70, 140);
            g.DrawArc(pen, 76 * s, 64 * s, 128 * s, 128 * s, -70, 140);
        }

        return bmp;
    }

    private static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private sealed record SessionInfo(
        AudioSessionControl Session,
        string Id,
        string? Key,
        string DefaultName,
        string DisplayName);
}
