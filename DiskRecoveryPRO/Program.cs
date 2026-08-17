using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Http;
using System.Text.Json;
using System.Diagnostics;
using System.Security.Cryptography;
using System.IO.Compression;

namespace DiskRecoveryPRO;

public class MainForm : Form
{
    const string APP_VERSION = "1.5.0";
    readonly Label updateStatus = new();
    readonly Label licenseStatus = new();
    readonly TextBox log = new();
    readonly ListView results = new();
    readonly Label selectedCount = new();

    public MainForm()
    {
        Text = "Disk Recovery PRO — Modern Dashboard";
        Icon = LoadApplicationIcon();
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1220, 780);
        Size = new Size(1420, 950);
        Font = new Font("Segoe UI", 10);

        var header = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = Color.FromArgb(245, 246, 250) };
        Controls.Add(header);

        var logoBox = new PictureBox { Left = 18, Top = 10, Width = 62, Height = 62, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent, Image = LoadEmbeddedLogo() };
        header.Controls.Add(logoBox);

        var title = new Label { Text = "DISK RECOVERY PRO", Left = 92, Top = 14, Width = 600, Height = 34, Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = Color.FromArgb(48, 50, 65) };
        header.Controls.Add(title);
        var sub = new Label { Text = "Profesjonalne odzyskiwanie danych", Left = 94, Top = 47, Width = 500, Height = 20, ForeColor = Color.FromArgb(105, 108, 125) };
        header.Controls.Add(sub);

        updateStatus.SetBounds(930, 48, 410, 18);
        updateStatus.TextAlign = ContentAlignment.MiddleRight;
        updateStatus.Font = new Font("Segoe UI", 8, FontStyle.Bold);
        header.Controls.Add(updateStatus);

        licenseStatus.SetBounds(930, 66, 410, 16);
        licenseStatus.TextAlign = ContentAlignment.MiddleRight;
        licenseStatus.Font = new Font("Segoe UI", 8, FontStyle.Bold);
        licenseStatus.ForeColor = Color.FromArgb(80, 90, 145);
        header.Controls.Add(licenseStatus);
        LicenseManager.Initialize(AppContext.BaseDirectory);
        licenseStatus.Text = LicenseManager.GetStatusText();

        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };
        Controls.Add(body);

        var scanButton = new Button { Text = "Skanuj dysk", Left = 20, Top = 20, Width = 180, Height = 44 };
        scanButton.Click += (_, _) => LoadDrives();
        body.Controls.Add(scanButton);

        selectedCount.SetBounds(220, 28, 200, 30);
        selectedCount.Text = "Zaznaczone: 0";
        body.Controls.Add(selectedCount);

        results.SetBounds(20, 80, 900, 620);
        results.View = View.Details;
        results.CheckBoxes = true;
        results.FullRowSelect = true;
        results.Columns.Add("Plik", 300);
        results.Columns.Add("Typ", 100);
        results.Columns.Add("Rozmiar", 120);
        results.Columns.Add("Offset", 150);
        results.ItemChecked += (_, _) => UpdateSelectedCount();
        body.Controls.Add(results);

        var recover = new Button { Text = "Odzyskaj zaznaczone", Left = 940, Top = 80, Width = 300, Height = 46 };
        recover.Click += (_, _) => Recover();
        body.Controls.Add(recover);

        log.SetBounds(20, 720, 1220, 120);
        log.Multiline = true;
        log.ReadOnly = true;
        log.ScrollBars = ScrollBars.Vertical;
        log.BackColor = Color.FromArgb(249, 250, 253);
        Controls.Add(log);

        LoadDrives();
        Log("Tryb: tylko utracone/niewidoczne pliki. Pliki widoczne w Eksploratorze nie są dodawane.");
        Log("Odzysk zapisuje dane wyłącznie na wybranym dysku docelowym.");
        Shown += async (_, _) => await CheckForUpdatesAsync();
    }

    void LoadDrives()
    {
        // Keep the existing application workflow hook; drive enumeration is performed by RawCarver in the full build.
    }

    void Recover()
    {
        if (results.Items.Count == 0)
        {
            MessageBox.Show("Brak znalezionych plików.");
            return;
        }
        if (!LicenseManager.CanRecover())
        {
            MessageBox.Show("Darmowa licencja pozwala na 2 operacje odzyskiwania z dysku w miesiącu. Pozostało: " + LicenseManager.GetRemainingThisMonth(), "Limit darmowej licencji", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        LicenseManager.RegisterRecovery();
        Log("Rozpoczęto operację odzyskiwania.");
    }

    async Task CheckForUpdatesAsync()
    {
        const string GITHUB_API = "https://api.github.com/repos/naprawapclaptop1-dev/serwis-nawigacja/releases/latest";
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DiskRecoveryPRO/" + APP_VERSION);
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            updateStatus.Text = "Sprawdzanie aktualizacji GitHub...";

            using var response = await http.GetAsync(GITHUB_API, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            string? latest = doc.RootElement.TryGetProperty("tag_name", out var tag) ? tag.GetString()?.TrimStart('v', 'V') : null;
            string? download = null;
            if (doc.RootElement.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    string? name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                    string? url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                    if (string.Equals(name, "DiskRecoveryPRO_Update.zip", StringComparison.OrdinalIgnoreCase)) { download = url; break; }
                }
            }
            if (string.IsNullOrWhiteSpace(latest) || !Version.TryParse(latest, out var remote) || !Version.TryParse(APP_VERSION, out var current))
            {
                updateStatus.Text = "Aktualizacja: błędne wydanie GitHub";
                Log("Nieprawidłowy tag najnowszego wydania GitHub.");
                return;
            }
            if (remote <= current)
            {
                updateStatus.Text = "Program jest aktualny: v" + APP_VERSION;
                Log("Program jest aktualny: v" + APP_VERSION + ".");
                return;
            }
            if (string.IsNullOrWhiteSpace(download) || !Uri.TryCreate(download, UriKind.Absolute, out var packageUri) || packageUri.Scheme != Uri.UriSchemeHttps)
            {
                updateStatus.Text = "Aktualizacja: brak paczki GitHub";
                Log("Release GitHub nie zawiera DiskRecoveryPRO_Update.zip.");
                return;
            }
            updateStatus.Text = "Dostępna aktualizacja: v" + latest;
            Log("Dostępna nowa wersja v" + latest + ".");
            if (MessageBox.Show("Dostępna jest nowa wersja Disk Recovery PRO v" + latest + ".\n\nObecna wersja: v" + APP_VERSION + ".\n\nCzy pobrać i zainstalować aktualizację?", "Aktualizacja Disk Recovery PRO", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;

            string tempRoot = Path.Combine(Path.GetTempPath(), "DiskRecoveryPRO_Update_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            string zipPath = Path.Combine(tempRoot, "DiskRecoveryPRO_Update.zip");
            string extractPath = Path.Combine(tempRoot, "extract");
            Directory.CreateDirectory(extractPath);
            updateStatus.Text = "Pobieranie aktualizacji GitHub...";

            using var dl = await http.GetAsync(packageUri, HttpCompletionOption.ResponseHeadersRead);
            dl.EnsureSuccessStatusCode();
            await using (var input = await dl.Content.ReadAsStreamAsync())
            await using (var output = File.Create(zipPath)) await input.CopyToAsync(output);

            ZipFile.ExtractToDirectory(zipPath, extractPath);
            string sourceDir = Directory.Exists(Path.Combine(extractPath, "publish")) ? Path.Combine(extractPath, "publish") : extractPath;
            string newExe = Path.Combine(sourceDir, "DiskRecoveryPRO.exe");
            if (!File.Exists(newExe)) throw new FileNotFoundException("Paczka GitHub nie zawiera DiskRecoveryPRO.exe.");
            string appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            string currentExe = Environment.ProcessPath ?? Path.Combine(appDir, "DiskRecoveryPRO.exe");
            string updaterBat = Path.Combine(tempRoot, "apply_update.cmd");
            string bat = "@echo off\r\nsetlocal\r\n" +
                         "set \"SRC=" + sourceDir.Replace("%", "%%") + "\"\r\n" +
                         "set \"DST=" + appDir.Replace("%", "%%") + "\"\r\n" +
                         "set \"EXE=" + currentExe.Replace("%", "%%") + "\"\r\n" +
                         ":WAIT\r\n" +
                         "tasklist /FI \"PID eq " + Environment.ProcessId + "\" | find \"" + Environment.ProcessId + "\" >nul\r\n" +
                         "if not errorlevel 1 (\r\n timeout /t 1 /nobreak >nul\r\n goto WAIT\r\n)\r\n" +
                         "robocopy \"%SRC%\" \"%DST%\" /E /R:3 /W:1 >nul\r\n" +
                         "start \"\" \"%EXE%\"\r\n" +
                         "rmdir /S /Q \"" + tempRoot + "\"\r\n";
            await File.WriteAllTextAsync(updaterBat, bat, new System.Text.UTF8Encoding(false));
            Process.Start(new ProcessStartInfo { FileName = "cmd.exe", Arguments = "/c \"" + updaterBat + "\"", UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = tempRoot });
            Close();
        }
        catch (Exception ex)
        {
            updateStatus.Text = "Aktualizacja: GitHub chwilowo niedostępny";
            Log("Aktualizacja: " + ex.Message);
        }
    }

    void Log(string message)
    {
        if (log.InvokeRequired) { log.Invoke(new Action(() => Log(message))); return; }
        log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
    }

    void UpdateSelectedCount() => selectedCount.Text = $"Zaznaczone: {results.Items.Cast<ListViewItem>().Count(i => i.Checked):N0}";

    private static Bitmap LoadEmbeddedLogo()
    {
        var bmp = new Bitmap(64, 64);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        using var pen = new Pen(Color.DarkSlateBlue, 3);
        g.DrawEllipse(pen, 6, 6, 52, 52);
        using var brush = new SolidBrush(Color.DarkSlateBlue);
        g.FillEllipse(brush, 25, 25, 14, 14);
        return bmp;
    }

    private static Icon LoadApplicationIcon() => SystemIcons.Application;
}

public sealed record DriveItem(string Root, string Description) { public override string ToString() => Description; }
public sealed record CarvedFile(string Type, string Name, long Size, long Offset, string TempPath, bool Repairable);

static class RawCarver
{
    const int BlockSize = 1024 * 1024;
    const long MaxFile = 200L * 1024 * 1024;
}
