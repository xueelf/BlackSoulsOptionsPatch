using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace OptionsPatch;

[SupportedOSPlatform("windows")]
internal static class Program
{
    private const string GameTitle = "BLACK SOULS";
    internal const string BackupSuffix = ".bsopts.bak";
    private const string TempSuffix = ".bsopts.tmp";
    private const string DpiLayer = "HIGHDPIAWARE";
    private const string CompatLayersKey = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
    internal static readonly string Version = GetVersion();
    private static int _gameGeneration;
    private static bool _isSteam;
    private static string _gameLanguage = "";
    private static string _gameName = "";
    private static string _detectionError = "";
    private static string _scriptTitle = "";
    private static string _legacyScriptTitle = "";
    private static readonly Dictionary<string, string> _installerText = new();
    private static readonly Dictionary<string, string> _gameText = new();

    internal static Action<string>? StatusSink;
    internal static Action<string>? ErrorSink;
    internal static string? LastError { get; set; }

    private static string GetVersion()
    {
        return Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
    }

    internal static string T(string key)
    {
        return _installerText[key];
    }

    internal static string TF(string key, params object[] args)
    {
        return string.Format(T(key), args);
    }

    internal static Stream OpenResource(string name)
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing embedded resource: {name}");
    }

    private static void LoadJsonResource(string name, Dictionary<string, string> target)
    {
        using Stream stream = OpenResource(name);
        using var document = JsonDocument.Parse(stream);

        target.Clear();
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            target[property.Name] = property.Value.GetString()!;
        }
    }

    private static void LoadInstallerLocale()
    {
        string language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (language != "zh" && language != "ja")
        {
            language = "en";
        }

        LoadJsonResource($"locale.installer.{language}.json", _installerText);
    }

    private static void LoadGameLocale(string language)
    {
        LoadJsonResource($"locale.game.{language}.json", _gameText);

        string version = _gameGeneration == 1 ? "i" : "ii";
        string store = _isSteam ? "steam" : "dlsite";
        _gameText["game.options.label.title"] =
            _gameText[$"game.options.label.title.{version}.{store}"];
    }

    [STAThread]
    private static void Main()
    {
        try
        {
            LoadInstallerLocale();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "BLACK SOULS QoL Patch", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new InstallerForm());
    }

    internal sealed record GameInspection(
        bool Detected,
        string Name,
        string Platform,
        string Language,
        string Status,
        string? Error);

    internal static GameInspection InspectGame(string gameDir, string archivePath, string backupPath)
    {
        if (!File.Exists(Path.Combine(gameDir, "Game.exe")) || !File.Exists(archivePath))
        {
            return new GameInspection(
                false, "", "", "", "",
                T("installer.gui.no_game") + "\n" + T("installer.gui.no_game_hint"));
        }

        if (!DetectGame(gameDir))
        {
            return new GameInspection(false, "", "", "", "", _detectionError);
        }

        try
        {
            _gameLanguage = DetectGameLanguageFromArchive(archivePath);
            LoadGameLocale(_gameLanguage);
        }
        catch (Exception ex)
        {
            return new GameInspection(
                false, _gameName, _isSteam ? "Steam" : "DLsite", "", "",
                TF("installer.gui.error", ex.Message));
        }

        return new GameInspection(
            true,
            _gameName,
            _isSteam ? "Steam" : "DLsite",
            LanguageLabel(_gameLanguage),
            GetInstallState(backupPath),
            null);
    }

    private static string DetectGameLanguageFromArchive(string archivePath)
    {
        var archive = Rgss3aArchive.Read(archivePath);
        var scriptsEntry = archive.Entries.FirstOrDefault(e =>
            string.Equals(NormalizeName(e.Name), "Data/Scripts.rvdata2", StringComparison.OrdinalIgnoreCase));
        if (scriptsEntry == null)
        {
            throw new InvalidDataException(T("installer.check.archive"));
        }

        var scripts = ScriptsFile.Read(archive.Extract(archivePath, scriptsEntry));
        return DetectGameLanguage(scripts);
    }

    private static string LanguageLabel(string language)
    {
        return language switch
        {
            "zh" => "简体中文",
            "ja" => "日本語",
            "en" => "English",
            _ => "—",
        };
    }

    internal static string GetInstallState(string backupPath)
    {
        return File.Exists(backupPath) ? T("installer.gui.status.installed") : T("installer.gui.status.uninstalled");
    }

    internal static bool Install(string archivePath, string backupPath)
    {
        LastError = null;
        Info(T("installer.install.reading"));

        var archive = Rgss3aArchive.Read(archivePath);
        var scriptsEntry = archive.Entries.FirstOrDefault(e =>
            string.Equals(NormalizeName(e.Name), "Data/Scripts.rvdata2", StringComparison.OrdinalIgnoreCase));
        if (scriptsEntry == null)
        {
            Error(T("installer.check.archive"));
            return false;
        }

        byte[] scriptsData = archive.Extract(archivePath, scriptsEntry);
        var scripts = ScriptsFile.Read(scriptsData);
        LoadGameLocale(_gameLanguage);

        int existingIndex = scripts.Entries.FindIndex(e =>
            e.Title.IndexOf(_scriptTitle, StringComparison.OrdinalIgnoreCase) >= 0 ||
            e.Title.IndexOf(_legacyScriptTitle, StringComparison.OrdinalIgnoreCase) >= 0);

        int mainIndex = scripts.Entries.FindIndex(e =>
            string.Equals(e.Title.Trim(), "Main", StringComparison.OrdinalIgnoreCase));
        if (mainIndex < 0)
        {
            Error(T("installer.check.main"));
            return false;
        }

        bool isUpdate = existingIndex >= 0;
        int newId = isUpdate ? scripts.Entries[existingIndex].Id : scripts.CreateNewId();
        Info(T(isUpdate ? "installer.install.update" : "installer.install.add"));

        byte[] scriptCode = LoadScript();

        byte[] newScripts = isUpdate
            ? ScriptsFile.Replace(scriptsData, scripts, existingIndex, newId, _scriptTitle, scriptCode)
            : ScriptsFile.Insert(scriptsData, scripts, mainIndex, newId, _scriptTitle, scriptCode);

        // Verify the modified Marshal stream before touching the archive.
        var verifyScripts = ScriptsFile.Read(newScripts);
        int expectedCount = scripts.Entries.Count + (isUpdate ? 0 : 1);
        if (verifyScripts.Entries.Count != expectedCount)
        {
            throw new InvalidDataException("Modified Scripts.rvdata2 has an unexpected entry count.");
        }
        if (!verifyScripts.Entries.Any(e => e.Title.IndexOf(_scriptTitle, StringComparison.OrdinalIgnoreCase) >= 0))
        {
            throw new InvalidDataException("Patch script missing from modified Scripts.rvdata2.");
        }
        if (!verifyScripts.Entries.Any(e =>
                string.Equals(e.Title.Trim(), "Main", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("Main missing from modified Scripts.rvdata2.");
        }

        string tempPath = archivePath + TempSuffix;
        File.Delete(tempPath);

        long archiveSizeMb = (new FileInfo(archivePath).Length + 1024 * 1024 - 1) / (1024 * 1024);
        Info(TF("installer.install.repack", archiveSizeMb));

        Rgss3aArchive.Replace(archivePath, tempPath, archive, scriptsEntry, newScripts);

        // Verify the new archive itself.
        var checkArchive = Rgss3aArchive.Read(tempPath);
        var checkScriptsEntry = checkArchive.Entries.FirstOrDefault(e =>
            string.Equals(NormalizeName(e.Name), "Data/Scripts.rvdata2", StringComparison.OrdinalIgnoreCase));
        if (checkScriptsEntry == null)
        {
            throw new InvalidDataException("Data/Scripts.rvdata2 missing from new archive.");
        }
        var checkScripts = ScriptsFile.Read(checkArchive.Extract(tempPath, checkScriptsEntry));
        if (!checkScripts.Entries.Any(e => e.Title.IndexOf(_scriptTitle, StringComparison.OrdinalIgnoreCase) >= 0))
        {
            throw new InvalidDataException("New archive verification failed.");
        }

        if (!File.Exists(backupPath))
        {
            Info(T("installer.install.backup.start"));
            File.Copy(archivePath, backupPath, overwrite: false);
            Info(TF("installer.install.backup.saved", backupPath));
        }
        else
        {
            Info(TF("installer.install.backup.exists", backupPath));
        }

        File.Move(tempPath, archivePath, overwrite: true);
        AddHighDpiAware(Path.GetDirectoryName(archivePath)!);

        Success(T("installer.install.done"));
        return true;
    }

    internal static bool Uninstall(string archivePath, string backupPath)
    {
        LastError = null;
        if (!File.Exists(backupPath))
        {
            Error(TF("installer.uninstall.backup.missing", backupPath));
            return false;
        }

        File.Copy(backupPath, archivePath, overwrite: true);
        File.Delete(backupPath);

        string optionsPath = Path.Combine(Path.GetDirectoryName(archivePath)!, "Options.ini");
        File.Delete(optionsPath);
        RemoveHighDpiAware(Path.GetDirectoryName(archivePath)!);

        Success(T("installer.uninstall.done"));
        return true;
    }

    private static void AddHighDpiAware(string gameDir)
    {
        try
        {
            string exePath = Path.GetFullPath(Path.Combine(gameDir, "Game.exe"));
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(CompatLayersKey)!;
            string? value = key.GetValue(exePath) as string;
            string[] layers = (value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (layers.Any(layer => string.Equals(layer, DpiLayer, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            string updated = layers.Length == 0
                ? $"~ {DpiLayer}"
                : $"{string.Join(' ', layers)} {DpiLayer}";
            key.SetValue(exePath, updated, RegistryValueKind.String);
        }
        catch
        {
        }
    }

    private static void RemoveHighDpiAware(string gameDir)
    {
        try
        {
            string exePath = Path.GetFullPath(Path.Combine(gameDir, "Game.exe"));
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(CompatLayersKey, writable: true);
            if (key?.GetValue(exePath) is not string value)
            {
                return;
            }

            string[] layers = value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(layer => !string.Equals(layer, DpiLayer, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (layers.Length == 0 || (layers.Length == 1 && layers[0] == "~"))
            {
                key.DeleteValue(exePath, throwOnMissingValue: false);
            }
            else
            {
                key.SetValue(exePath, string.Join(' ', layers), RegistryValueKind.String);
            }
        }
        catch
        {
        }
    }

    private static byte[] LoadScript()
    {
        using Stream stream = OpenResource("options.rb");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        byte[] data = memory.ToArray();

        string code = Encoding.UTF8.GetString(data);
        var builder = new StringBuilder();

        builder.AppendLine("module BSOPTS");
        builder.AppendLine("  TEXT = {");
        foreach (var pair in _gameText.Where(pair =>
                     pair.Key.StartsWith("game.", StringComparison.Ordinal) &&
                     !pair.Key.StartsWith("game.options.label.title.", StringComparison.Ordinal)))
        {
            builder.Append("    \"")
                .Append(pair.Key["game.".Length..])
                .Append("\" => '")
                .Append(EscapeRuby(pair.Value))
                .AppendLine("',");
        }
        builder.AppendLine("  }");
        builder.AppendLine("end");
        builder.AppendLine();
        builder.Append(code);
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static string EscapeRuby(string value)
    {
        return value.Replace("\\", "\\\\").Replace("'", "\\'");
    }

    private static bool DetectGame(string gameDir)
    {
        _isSteam = File.Exists(Path.Combine(gameDir, "steam_api.dll"));

        string ini = Path.Combine(gameDir, "Game.ini");
        string? titleLine = File.Exists(ini)
            ? Encoding.Latin1.GetString(File.ReadAllBytes(ini))
                .Split('\n')
                .Select(line => line.TrimEnd('\r'))
                .FirstOrDefault(line => line.StartsWith("Title=", StringComparison.OrdinalIgnoreCase))
            : null;

        if (titleLine != null)
        {
            string title = titleLine[6..].Trim();
            if (title.StartsWith(GameTitle, StringComparison.OrdinalIgnoreCase))
            {
                string suffix = title[GameTitle.Length..].TrimStart();

                // Steam uses ASCII II; the DLsite Chinese build uses GBK full-width II.
                if (suffix.StartsWith("II", StringComparison.Ordinal) ||
                    suffix.StartsWith("\u00A2\u00F2", StringComparison.Ordinal))
                {
                    SetGame(2);
                    return true;
                }

                SetGame(1);
                return true;
            }
        }

        _gameLanguage = "";
        _detectionError = T("installer.gui.no_game") + "\n" + T("installer.gui.no_game_hint");
        return false;
    }

    private static void SetGame(int generation)
    {
        _gameGeneration = generation;
        _gameName = generation == 1 ? GameTitle : GameTitle + " II";
        _scriptTitle = _gameName + " QoL";
        _legacyScriptTitle = _gameName + " options";
    }

    private static string DetectGameLanguage(ScriptsFile scripts)
    {
        var vocab = scripts.Entries.FirstOrDefault(e =>
            string.Equals(e.Title.Trim(), "Vocab", StringComparison.OrdinalIgnoreCase));
        if (vocab == null)
        {
            throw new InvalidDataException("Vocab script missing.");
        }

        using var input = new MemoryStream(vocab.Compressed);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        string code = Regex.Replace(
            Encoding.UTF8.GetString(output.ToArray()), "#.*$", "", RegexOptions.Multiline);

        int kana = Regex.Matches(code, "[\u3040-\u30FF]").Count;
        int cjk = Regex.Matches(code, "[\u4E00-\u9FFF]").Count;
        // Chinese patches may leave a few untranslated Japanese lines.
        if (Regex.IsMatch(code, "[买卖弃档经验伤败胜闪击护复]"))
        {
            return "zh";
        }
        if (kana > 0 && kana * 2 > cjk)
        {
            return "ja";
        }
        if (cjk > 0)
        {
            return "zh";
        }
        return "en";
    }

    internal static bool IsGameRunning(string gameDir)
    {
        string fullDir = Path.GetFullPath(gameDir)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        foreach (var process in Process.GetProcessesByName("Game"))
        {
            try
            {
                string? path = process.MainModule?.FileName;
                if (path != null && Path.GetFullPath(path).StartsWith(fullDir, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
                // Access denied or process exited; ignore.
            }
        }

        return false;
    }

    private static string NormalizeName(string name) => name.Replace('\\', '/');

    private static void Info(string message) => StatusSink?.Invoke(message);

    private static void Success(string message)
    {
        LastError = null;
        StatusSink?.Invoke(message);
    }

    private static void Error(string message)
    {
        LastError = message;
        ErrorSink?.Invoke(message);
    }
}

//==============================================================================
// RGSS3A archive reader/writer
//==============================================================================
internal sealed class ArchiveEntry
{
    public required string Name;
    public uint OriginalOffset;
    public uint Size;
    public uint Key;
    public required byte[] EncryptedName;
}

internal sealed class Rgss3aArchive
{
    public uint Seed;
    public uint MasterKey;
    public List<ArchiveEntry> Entries = new();

    public static Rgss3aArchive Read(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        byte[] magic = reader.ReadBytes(8);
        if (magic.Length != 8 ||
            Encoding.ASCII.GetString(magic, 0, 7) != "RGSSAD\0" ||
            magic[7] != 3)
        {
            throw new InvalidDataException("Not an RGSS3A (version 3) archive.");
        }

        uint seed = reader.ReadUInt32();
        uint masterKey = unchecked(seed * 9 + 3);
        var archive = new Rgss3aArchive { Seed = seed, MasterKey = masterKey };

        while (true)
        {
            if (stream.Position + 16 > stream.Length)
            {
                throw new InvalidDataException("Unexpected end of archive table of contents.");
            }

            uint offset = reader.ReadUInt32() ^ masterKey;
            uint size = reader.ReadUInt32() ^ masterKey;
            uint key = reader.ReadUInt32() ^ masterKey;
            uint nameLength = reader.ReadUInt32() ^ masterKey;

            if (offset == 0)
            {
                break;
            }

            if (nameLength > 1_000_000)
            {
                throw new InvalidDataException($"Invalid filename length: {nameLength}");
            }

            byte[] encryptedName = reader.ReadBytes((int)nameLength);
            if (encryptedName.Length != nameLength)
            {
                throw new InvalidDataException("Unexpected end of archive filename.");
            }

            byte[] nameBytes = new byte[encryptedName.Length];
            for (int i = 0; i < nameBytes.Length; i++)
            {
                nameBytes[i] = (byte)(encryptedName[i] ^ (byte)(masterKey >> (8 * (i % 4))));
            }

            archive.Entries.Add(new ArchiveEntry
            {
                Name = Encoding.UTF8.GetString(nameBytes),
                OriginalOffset = offset,
                Size = size,
                Key = key,
                EncryptedName = encryptedName,
            });
        }

        return archive;
    }

    public byte[] Extract(string archivePath, ArchiveEntry entry)
    {
        using var stream = File.OpenRead(archivePath);
        stream.Position = entry.OriginalOffset;

        byte[] encrypted = new byte[entry.Size];
        int read = 0;

        while (read < encrypted.Length)
        {
            int n = stream.Read(encrypted, read, encrypted.Length - read);
            if (n == 0)
            {
                throw new InvalidDataException($"Unexpected end of archive while reading {entry.Name}.");
            }
            read += n;
        }

        return XorPayload(encrypted, entry.Key);
    }

    public static void Replace(
        string sourcePath,
        string destinationPath,
        Rgss3aArchive archive,
        ArchiveEntry target,
        byte[] newPlainData)
    {
        uint newKey = RandomKey();
        uint newSize = (uint)newPlainData.Length;
        byte[] newEncryptedData = XorPayload(newPlainData, newKey);

        long tocSize = 8 + 4 + 16;
        foreach (var entry in archive.Entries)
        {
            tocSize += 16 + entry.EncryptedName.Length;
        }

        var offsets = new List<uint>(archive.Entries.Count);
        long offset = tocSize;
        foreach (var entry in archive.Entries)
        {
            if (offset > uint.MaxValue)
            {
                throw new InvalidOperationException("Archive exceeds 4 GiB; RGSS3A cannot address it.");
            }

            offsets.Add((uint)offset);
            offset += ReferenceEquals(entry, target) ? newSize : entry.Size;
        }

        if (offset > uint.MaxValue)
        {
            throw new InvalidOperationException("Archive exceeds 4 GiB; RGSS3A cannot address it.");
        }

        using var source = File.OpenRead(sourcePath);
        using var destination = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(destination, Encoding.UTF8, leaveOpen: true);

        writer.Write(Encoding.ASCII.GetBytes("RGSSAD\0"));
        writer.Write((byte)3);
        writer.Write(archive.Seed);

        for (int i = 0; i < archive.Entries.Count; i++)
        {
            ArchiveEntry entry = archive.Entries[i];
            uint size = ReferenceEquals(entry, target) ? newSize : entry.Size;
            uint key = ReferenceEquals(entry, target) ? newKey : entry.Key;

            writer.Write(offsets[i] ^ archive.MasterKey);
            writer.Write(size ^ archive.MasterKey);
            writer.Write(key ^ archive.MasterKey);
            writer.Write((uint)entry.EncryptedName.Length ^ archive.MasterKey);
            writer.Write(entry.EncryptedName);
        }

        for (int i = 0; i < 4; i++)
        {
            writer.Write(0u ^ archive.MasterKey);
        }

        byte[] buffer = new byte[1024 * 1024];
        for (int i = 0; i < archive.Entries.Count; i++)
        {
            ArchiveEntry entry = archive.Entries[i];
            if (destination.Position != offsets[i])
            {
                throw new InvalidOperationException($"Offset mismatch while writing {entry.Name}.");
            }

            if (ReferenceEquals(entry, target))
            {
                destination.Write(newEncryptedData);
                continue;
            }

            source.Position = entry.OriginalOffset;
            long remaining = entry.Size;
            while (remaining > 0)
            {
                int n = source.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                if (n == 0)
                {
                    throw new InvalidDataException($"Unexpected end of source archive while copying {entry.Name}.");
                }

                destination.Write(buffer, 0, n);
                remaining -= n;
            }
        }
    }

    private static uint RandomKey()
    {
        Span<byte> bytes = stackalloc byte[4];
        Random.Shared.NextBytes(bytes);
        return BitConverter.ToUInt32(bytes);
    }

    private static byte[] XorPayload(byte[] data, uint key)
    {
        var result = new byte[data.Length];
        int index = 0;

        while (index < data.Length)
        {
            int count = Math.Min(4, data.Length - index);
            for (int i = 0; i < count; i++)
            {
                result[index + i] = (byte)(data[index + i] ^ (byte)(key >> (8 * i)));
            }
            index += count;
            if (index < data.Length)
            {
                key = unchecked(key * 7 + 3);
            }
        }

        return result;
    }
}

//==============================================================================
// Ruby Marshal helpers for Scripts.rvdata2
//==============================================================================
internal sealed class ScriptEntry
{
    public int Id;
    public string Title = "";
    public byte[] Compressed = Array.Empty<byte>();
    public int RawStart;
    public int RawEnd;
}

internal sealed class ScriptsFile
{
    public List<ScriptEntry> Entries = new();

    public static ScriptsFile Read(byte[] data)
    {
        var reader = new MarshalReader(data);

        if (reader.ReadByte() != 0x04 || reader.ReadByte() != 0x08 || reader.ReadByte() != (byte)'[')
        {
            throw new InvalidDataException("Not a Ruby Marshal array (Scripts.rvdata2).");
        }

        int count = reader.ReadFixnum();
        var result = new ScriptsFile();

        for (int i = 0; i < count; i++)
        {
            int start = reader.Position;
            object? value = reader.ReadValue();
            int end = reader.Position;

            if (value is not List<object?> item || item.Count != 3)
            {
                throw new InvalidDataException($"Script entry {i} is not a 3-element array.");
            }
            if (item[1] is not byte[] titleBytes)
            {
                throw new InvalidDataException($"Script entry {i} has an invalid title.");
            }
            if (item[2] is not byte[] compressed)
            {
                throw new InvalidDataException($"Script entry {i} has invalid code.");
            }

            result.Entries.Add(new ScriptEntry
            {
                Id = ParseId(item[0], i),
                Title = Encoding.UTF8.GetString(titleBytes),
                Compressed = compressed,
                RawStart = start,
                RawEnd = end,
            });
        }

        if (reader.Position != data.Length)
        {
            throw new InvalidDataException("Trailing data after Scripts.rvdata2.");
        }

        return result;
    }

    private static int ParseId(object? value, int index)
    {
        return value is int number
            ? number
            : throw new InvalidDataException($"Script entry {index} has an invalid ID type.");
    }

    public int CreateNewId()
    {
        return Entries.Max(e => e.Id) + 1;
    }

    public static byte[] Replace(
        byte[] original,
        ScriptsFile scripts,
        int index,
        int id,
        string title,
        byte[] codeUtf8)
    {
        return Build(original, scripts, index, -1, id, title, codeUtf8);
    }

    public static byte[] Insert(
        byte[] original,
        ScriptsFile scripts,
        int index,
        int id,
        string title,
        byte[] codeUtf8)
    {
        return Build(original, scripts, -1, index, id, title, codeUtf8);
    }

    private static byte[] Build(
        byte[] original,
        ScriptsFile scripts,
        int replaceIndex,
        int insertIndex,
        int id,
        string title,
        byte[] codeUtf8)
    {
        byte[] compressed = Compress(codeUtf8);

        var newEntry = new List<object?>
        {
            id,
            new Utf8String(Encoding.UTF8.GetBytes(title)),
            new Utf8String(compressed),
        };
        byte[] newEntryBytes = MarshalWriter.Serialize(newEntry);

        int count = scripts.Entries.Count + (replaceIndex < 0 ? 1 : 0);
        using var output = new MemoryStream();

        output.WriteByte(0x04);
        output.WriteByte(0x08);
        output.WriteByte((byte)'[');
        output.Write(MarshalWriter.EncodeFixnum(count));

        for (int i = 0; i < scripts.Entries.Count; i++)
        {
            if (replaceIndex >= 0 && i == replaceIndex)
            {
                output.Write(newEntryBytes);
                continue;
            }
            if (replaceIndex < 0 && i == insertIndex)
            {
                output.Write(newEntryBytes);
            }

            var entry = scripts.Entries[i];
            output.Write(original, entry.RawStart, entry.RawEnd - entry.RawStart);
        }

        return output.ToArray();
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();

        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(data, 0, data.Length);
        }

        return output.ToArray();
    }
}

internal sealed record Symbol(string Name);

internal sealed record Utf8String(byte[] Bytes);

internal sealed class MarshalReader
{
    private readonly byte[] _data;
    private readonly List<string> _symbols = new();
    public int Position { get; private set; }

    public MarshalReader(byte[] data)
    {
        _data = data;
    }

    public byte ReadByte()
    {
        if (Position >= _data.Length)
        {
            throw new EndOfStreamException("Unexpected end of Marshal data.");
        }

        return _data[Position++];
    }

    public byte[] ReadBytes(int count)
    {
        if (Position + count > _data.Length)
        {
            throw new EndOfStreamException("Unexpected end of Marshal data.");
        }

        byte[] result = new byte[count];
        Buffer.BlockCopy(_data, Position, result, 0, count);
        Position += count;

        return result;
    }

    public int ReadFixnum()
    {
        sbyte first = unchecked((sbyte)ReadByte());

        if (first == 0)
        {
            return 0;
        }

        if (first > 0 && first < 5)
        {
            return ReadLittleEndian(first);
        }

        if (first < 0 && first > -5)
        {
            return ~ReadLittleEndian(-first);
        }

        if (first > 4)
        {
            return first - 5;
        }

        return first + 5;
    }

    private int ReadLittleEndian(int count)
    {
        int value = 0;

        for (int i = 0; i < count; i++)
        {
            value |= ReadByte() << (8 * i);
        }

        return value;
    }

    public object? ReadValue()
    {
        byte type = ReadByte();

        switch ((char)type)
        {
            case '0':
                return null;
            case 'T':
                return true;
            case 'F':
                return false;
            case 'i':
                return ReadFixnum();
            case '"':
                return ReadStringBody();
            case ':':
                return ReadSymbol();
            case ';':
                int symbolIndex = ReadFixnum();
                if (symbolIndex < 0 || symbolIndex >= _symbols.Count)
                {
                    throw new InvalidDataException($"Invalid Marshal symbol link index: {symbolIndex}");
                }
                return new Symbol(_symbols[symbolIndex]);
            case '[':
                return ReadArray();
            case 'I':
                return ReadIvarWrapper();
            default:
                throw new NotSupportedException($"Unsupported Marshal type 0x{type:X2}.");
        }
    }

    private Symbol ReadSymbol()
    {
        int length = ReadFixnum();
        byte[] name = ReadBytes(length);
        string symbol = Encoding.UTF8.GetString(name);

        _symbols.Add(symbol);

        return new Symbol(symbol);
    }

    private List<object?> ReadArray()
    {
        int count = ReadFixnum();
        var result = new List<object?>(count);

        for (int i = 0; i < count; i++)
        {
            result.Add(ReadValue());
        }

        return result;
    }

    private object ReadIvarWrapper()
    {
        object? value = ReadValue();
        int ivarCount = ReadFixnum();

        for (int i = 0; i < ivarCount; i++)
        {
            ReadValue();
            ReadValue();
        }

        return value ?? throw new InvalidDataException("I wrapper is empty.");
    }

    private byte[] ReadStringBody()
    {
        int length = ReadFixnum();

        return ReadBytes(length);
    }
}

internal static class MarshalWriter
{
    public static byte[] EncodeFixnum(int value)
    {
        if (value == 0)
        {
            return new byte[] { 0 };
        }

        if (value > 0)
        {
            if (value < 123)
            {
                return new[] { (byte)(value + 5) };
            }

            return EncodeLittleEndian(value, false);
        }

        if (value > -124)
        {
            return new[] { unchecked((byte)(value - 5)) };
        }

        return EncodeLittleEndian(value, true);
    }

    private static byte[] EncodeLittleEndian(int value, bool negative)
    {
        uint bits = negative ? (uint)~value : (uint)value;
        int length = 1;

        while (length < 4 && (bits >> (8 * length)) != 0)
        {
            length++;
        }

        var result = new byte[1 + length];
        result[0] = unchecked((byte)(negative ? -length : length));

        for (int i = 0; i < length; i++)
        {
            result[1 + i] = (byte)(bits >> (8 * i));
        }

        return result;
    }

    public static byte[] Serialize(object? value)
    {
        using var stream = new MemoryStream();

        Write(stream, value);

        return stream.ToArray();
    }

    private static void Write(Stream stream, object? value)
    {
        switch (value)
        {
            case int i:
                stream.WriteByte((byte)'i');
                stream.Write(EncodeFixnum(i));
                break;

            case Utf8String utf8:
                stream.WriteByte((byte)'I');
                stream.WriteByte((byte)'"');
                stream.Write(EncodeFixnum(utf8.Bytes.Length));
                stream.Write(utf8.Bytes);

                // Marshal fixnum 1: one instance variable (:E => true).
                stream.WriteByte(6);
                stream.WriteByte((byte)':');
                stream.Write(EncodeFixnum(1));
                stream.WriteByte((byte)'E');
                stream.WriteByte((byte)'T');
                break;

            case List<object?> list:
                stream.WriteByte((byte)'[');
                stream.Write(EncodeFixnum(list.Count));

                foreach (object? item in list)
                {
                    Write(stream, item);
                }

                break;

            default:
                throw new NotSupportedException($"Cannot serialize {value?.GetType().Name ?? "null"}.");
        }
    }
}
