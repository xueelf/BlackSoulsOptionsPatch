using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace OptionsPatch;

internal sealed class InstallerForm : Form
{
    private const string BlogUrl = "https://blog.yuki.sh";
    private const string RepositoryUrl = "https://github.com/xueelf/BlackSoulsQoLPatch";

    private readonly string _gameDir = AppContext.BaseDirectory;
    private readonly string _archivePath;
    private readonly string _backupPath;
    private readonly Label _gameValue;
    private readonly Label _platformValue;
    private readonly Label _languageValue;
    private readonly Label _statusValue;
    private readonly Button _installButton;
    private readonly Button _uninstallButton;
    private readonly Label _message;
    private readonly ProgressBar _progress;
    private readonly Font _footerFont;
    private readonly Image _coverImage;
    private readonly bool _detected;
    private bool _busy;

    public InstallerForm()
    {
        _archivePath = Path.Combine(_gameDir, "Game.rgss3a");
        _backupPath = _archivePath + Program.BackupSuffix;

        Text = Program.ProductName;
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(420, 626);
        Font = SystemFonts.MessageBoxFont;

        _coverImage = LoadCover();
        var cover = new PictureBox
        {
            Location = new Point(16, 12),
            Size = new Size(388, 291),
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle,
            Image = _coverImage,
        };
        Controls.Add(cover);

        var info = new TableLayoutPanel
        {
            Location = new Point(16, 316),
            Size = new Size(388, 88),
            ColumnCount = 2,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        string[] captions =
        {
            Program.T("installer.gui.game"),
            Program.T("installer.gui.platform"),
            Program.T("installer.gui.language"),
            Program.T("installer.gui.status"),
        };
        int captionWidth = captions.Max(caption => TextRenderer.MeasureText(caption, Font).Width) + 4;
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, captionWidth));
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (int i = 0; i < 4; i++)
        {
            info.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        }

        _gameValue = AddInfoRow(info, 0, captions[0]);
        _platformValue = AddInfoRow(info, 1, captions[1]);
        _languageValue = AddInfoRow(info, 2, captions[2]);
        _statusValue = AddInfoRow(info, 3, captions[3]);
        Controls.Add(info);

        _installButton = new Button
        {
            Location = new Point(16, 416),
            Size = new Size(388, 34),
            TextAlign = ContentAlignment.MiddleCenter,
            UseVisualStyleBackColor = true,
        };
        _installButton.Click += async (_, _) => await RunOperationAsync(true);
        Controls.Add(_installButton);

        _uninstallButton = new Button
        {
            Location = new Point(16, 458),
            Size = new Size(388, 34),
            TextAlign = ContentAlignment.MiddleCenter,
            UseVisualStyleBackColor = true,
        };
        _uninstallButton.Click += async (_, _) => await RunOperationAsync(false);
        Controls.Add(_uninstallButton);

        _message = new Label
        {
            Location = new Point(16, 502),
            Size = new Size(388, 66),
            AutoSize = false,
            TextAlign = ContentAlignment.TopLeft,
            UseMnemonic = false,
        };
        Controls.Add(_message);

        _progress = new ProgressBar
        {
            Location = new Point(16, 572),
            Size = new Size(388, 14),
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30,
            Visible = false,
        };
        Controls.Add(_progress);

        _footerFont = new Font(Font.FontFamily, 7.5F);
        var authorLink = new LinkLabel
        {
            Text = "Copyright © 2026 Yuki",
            AutoSize = true,
            Font = _footerFont,
            LinkBehavior = LinkBehavior.AlwaysUnderline,
            Location = new Point(16, 594),
        };
        int authorStart = authorLink.Text.IndexOf("Yuki", StringComparison.Ordinal);
        authorLink.LinkArea = new LinkArea(authorStart, "Yuki".Length);
        authorLink.LinkClicked += (_, _) => OpenUrl(BlogUrl);
        Controls.Add(authorLink);

        string versionText = "v" + Program.Version;
        var versionLink = new LinkLabel
        {
            Text = versionText,
            AutoSize = true,
            Font = _footerFont,
            LinkBehavior = LinkBehavior.AlwaysUnderline,
        };
        versionLink.Location = new Point(
            16 + 388 - TextRenderer.MeasureText(versionText, _footerFont).Width,
            594);
        versionLink.LinkClicked += (_, _) => OpenUrl(RepositoryUrl);
        Controls.Add(versionLink);

        var inspection = Program.InspectGame(_gameDir, _archivePath);
        _detected = inspection.Detected;
        if (_detected)
        {
            _gameValue.Text = inspection.Name;
            _platformValue.Text = inspection.Platform;
            _languageValue.Text = inspection.Language;
            UpdateState();
            _message.Text = Program.T("installer.gui.ready");
        }
        else
        {
            _gameValue.Text = Program.T("installer.gui.not_detected");
            _platformValue.Text = "—";
            _languageValue.Text = "—";
            _statusValue.Text = "—";
            _installButton.Text = Program.T("installer.gui.action.install");
            _uninstallButton.Text = Program.T("installer.gui.action.uninstall");
            _installButton.Enabled = false;
            _uninstallButton.Enabled = false;
            _message.ForeColor = Color.Firebrick;
            _message.Text = inspection.Error!;
        }

        Program.StatusSink = message => RunOnUi(() =>
        {
            if (!_busy)
            {
                return;
            }

            _message.ForeColor = SystemColors.ControlText;
            _message.Text = message + Environment.NewLine + Program.T("installer.gui.please_wait");
        });
        Program.ErrorSink = message => RunOnUi(() =>
        {
            _message.ForeColor = Color.Firebrick;
            _message.Text = message;
        });

        FormClosing += OnFormClosing;
        FormClosed += (_, _) =>
        {
            Program.StatusSink = null;
            Program.ErrorSink = null;
        };
    }

    private static Label AddInfoRow(TableLayoutPanel table, int row, string caption)
    {
        var key = new Label
        {
            Text = caption,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty,
            AutoSize = false,
        };
        var value = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty,
            AutoSize = false,
        };
        table.Controls.Add(key, 0, row);
        table.Controls.Add(value, 1, row);
        return value;
    }

    private static Image LoadCover()
    {
        using Stream stream = Program.OpenResource("Cover.png");
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }

    private void UpdateState()
    {
        bool installed = File.Exists(_backupPath);
        _statusValue.Text = Program.T(installed
            ? "installer.gui.status.installed"
            : "installer.gui.status.uninstalled");
        _installButton.Text = Program.T(installed
            ? "installer.gui.action.reinstall"
            : "installer.gui.action.install");
        _uninstallButton.Text = Program.T("installer.gui.action.uninstall");
        _installButton.Enabled = _detected;
        _uninstallButton.Enabled = _detected && installed;
    }

    private async Task RunOperationAsync(bool install)
    {
        if (_busy || !_detected)
        {
            return;
        }

        if (Program.IsGameRunning(_gameDir))
        {
            ShowError(Program.T("installer.check.running"));
            return;
        }

        _busy = true;
        _installButton.Enabled = false;
        _uninstallButton.Enabled = false;
        if (install)
        {
            _installButton.Text = Program.T("installer.gui.installing");
        }
        else
        {
            _uninstallButton.Text = Program.T("installer.gui.uninstalling");
        }

        _message.ForeColor = SystemColors.ControlText;
        _message.Text = Program.T(install
            ? "installer.gui.installing"
            : "installer.gui.uninstalling") + Environment.NewLine + Program.T("installer.gui.please_wait");
        _progress.Visible = true;
        UseWaitCursor = true;

        bool success;
        try
        {
            success = await Task.Run(() => install
                ? Program.Install(_archivePath, _backupPath)
                : Program.Uninstall(_archivePath, _backupPath));
        }
        catch (Exception ex)
        {
            Program.LastError = Program.TF("installer.gui.error", ex.Message);
            success = false;
        }

        _busy = false;
        UseWaitCursor = false;
        _progress.Visible = false;
        UpdateState();

        if (success)
        {
            _message.ForeColor = SystemColors.ControlText;
            _message.Text = Program.T(install
                ? "installer.install.done"
                : "installer.uninstall.done");
        }
        else
        {
            ShowError(Program.LastError!);
        }
    }

    private void ShowError(string message)
    {
        _message.ForeColor = Color.Firebrick;
        _message.Text = message;
        MessageBox.Show(message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void RunOnUi(Action action)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(action);
        }
        else
        {
            action();
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_busy)
        {
            return;
        }

        e.Cancel = true;
        MessageBox.Show(
            Program.T("installer.gui.busy"),
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, Program.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _coverImage.Dispose();
            _footerFont.Dispose();
        }

        base.Dispose(disposing);
    }
}
