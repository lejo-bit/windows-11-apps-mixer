using System;
using System.Drawing;
using System.Windows.Forms;

namespace VolumeMixer;

public sealed class RenameDialog : Form
{
    private readonly TextBox _textBox;

    public string NewName => _textBox.Text.Trim();

    public RenameDialog(string currentName)
    {
        Text = "Rename";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(380, 150);
        ShowInTaskbar = false;

        var label = new Label
        {
            Text = "New program name:",
            Location = new Point(12, 12),
            AutoSize = true
        };

        _textBox = new TextBox
        {
            Location = new Point(12, 36),
            Width = 356,
            Text = currentName
        };

        var hint = new Label
        {
            Text = "Leave empty to restore the default name.",
            Location = new Point(12, 64),
            AutoSize = true,
            ForeColor = SystemColors.GrayText
        };

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(206, 112),
            Size = new Size(80, 28)
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(288, 112),
            Size = new Size(80, 28)
        };

        AcceptButton = okButton;
        CancelButton = cancelButton;

        Controls.Add(label);
        Controls.Add(_textBox);
        Controls.Add(hint);
        Controls.Add(okButton);
        Controls.Add(cancelButton);

        Shown += (_, _) =>
        {
            _textBox.Focus();
            _textBox.SelectAll();
        };
    }
}
