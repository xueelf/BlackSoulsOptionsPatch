using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace OptionsPatch;

internal static class Program
{
    private const string GameTitle = "BLACK SOULS";
    private const string BackupSuffix = ".bsopts.bak";
    private const string TempSuffix = ".bsopts.tmp";
    private static readonly string[] RomanNumerals =
    {
        "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X",
    };
    private static readonly string Version = GetVersion();
    private static readonly int GameGeneration = GetGameGeneration();
    private static readonly string GameName = $"{GameTitle} {ToRoman(GameGeneration)}".TrimEnd();
    private static readonly string ScriptTitle = GameName + " options";

    private static readonly Dictionary<string, string> _text = new();

    private static string GetVersion()
    {
        return Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
    }

    private static int GetGameGeneration()
    {
        var attribute = Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "GameGeneration");
        string? value = attribute?.Value;

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int generation) ||
            generation < 1 || generation > RomanNumerals.Length)
        {
            throw new InvalidOperationException($"Invalid GameGeneration metadata: {value ?? "(missing)"}");
        }

        return generation;
    }

    private static string ToRoman(int generation)
    {
        if (generation == 1)
        {
            return "";
        }

        return RomanNumerals[generation - 1];
    }

    private static string T(string key)
    {
        return _text[key];
    }

    private static string TF(string key, params object[] args)
    {
        return string.Format(T(key), args);
    }

    private static void LoadLocale()
    {
        var assembly = Assembly.GetExecutingAssembly();
        string resource = assembly.GetManifestResourceNames()
            .First(name => name.EndsWith("locale.json", StringComparison.OrdinalIgnoreCase));

        using Stream stream = assembly.GetManifestResourceStream(resource)!;
        using var document = JsonDocument.Parse(stream);

        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            _text[property.Name] = property.Value.GetString()!;
        }
    }

    private static int Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        try
        {
            LoadLocale();
            return RunMenu();
        }
        catch (Exception ex)
        {
            Error(TF("terminal.error", ex.Message));
            PauseExit();
            return 1;
        }
    }

    private static int RunMenu()
    {
        Console.WriteLine(TF("terminal.header", GameName, Version));
        Console.WriteLine();

        string gameDir = AppContext.BaseDirectory;
        string archivePath = Path.Combine(gameDir, "Game.rgss3a");
        string gameExePath = Path.Combine(gameDir, "Game.exe");

        if (!File.Exists(gameExePath) || !File.Exists(archivePath))
        {
            Error(T("check.root"));
            PauseExit();
            return 1;
        }

        if (!CheckGameIni(gameDir))
        {
            PauseExit();
            return 1;
        }

        string backupPath = archivePath + BackupSuffix;

        while (true)
        {
            Console.WriteLine(TF("terminal.status.dir", gameDir));
            Console.WriteLine(TF("terminal.status.patch", GetInstallState(backupPath)));
            Console.WriteLine();
            Console.WriteLine(T("terminal.action.install"));
            Console.WriteLine(T("terminal.action.uninstall"));
            Console.WriteLine(T("terminal.action.exit"));
            Console.Write(T("terminal.prompt.select"));

            string? choice = ReadMenuChoice();
            Console.WriteLine();

            if (choice == "3")
            {
                return 0;
            }

            if (choice != "1" && choice != "2")
            {
                Console.WriteLine(T("terminal.prompt.invalid"));
                continue;
            }

            if (IsGameRunning(gameDir))
            {
                Error(T("check.running"));
                PauseMenu();
                continue;
            }

            try
            {
                if (choice == "1")
                {
                    Install(archivePath, backupPath);
                }
                else
                {
                    Uninstall(archivePath, backupPath);
                }
            }
            catch (Exception ex)
            {
                Error(TF("terminal.error", ex.Message));
            }

            PauseMenu();
        }
    }

    private static string GetInstallState(string backupPath)
    {
        return File.Exists(backupPath) ? T("terminal.status.patch.installed") : T("terminal.status.patch.uninstalled");
    }

    private static string? ReadMenuChoice()
    {
        if (Console.IsInputRedirected)
        {
            return Console.ReadLine()?.Trim();
        }

        char choice = Console.ReadKey(true).KeyChar;

        Console.WriteLine(choice);
        return choice.ToString();
    }

    private static void PauseMenu()
    {
        if (Console.IsInputRedirected)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine(T("terminal.prompt.pause"));
        try { Console.ReadKey(true); } catch { }
    }

    private static void PauseExit()
    {
        if (Console.IsInputRedirected)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine(T("terminal.prompt.exit"));
        try { Console.ReadKey(true); } catch { }
    }

    private static void Install(string archivePath, string backupPath)
    {
        Info(T("install.reading"));

        var archive = Rgss3aArchive.Read(archivePath);
        var scriptsEntry = archive.Entries.FirstOrDefault(e =>
            string.Equals(NormalizeName(e.Name), "Data/Scripts.rvdata2", StringComparison.OrdinalIgnoreCase));
        if (scriptsEntry == null)
        {
            Error(T("check.archive"));
            return;
        }

        byte[] scriptsData = archive.Extract(archivePath, scriptsEntry);
        var scripts = ScriptsFile.Read(scriptsData);

        int existingIndex = scripts.Entries.FindIndex(e =>
            e.Title.IndexOf(ScriptTitle, StringComparison.OrdinalIgnoreCase) >= 0);

        int mainIndex = scripts.Entries.FindIndex(e =>
            string.Equals(e.Title.Trim(), "Main", StringComparison.OrdinalIgnoreCase));
        if (mainIndex < 0)
        {
            Error(T("check.main"));
            return;
        }

        int replaceIndex = -1;
        int insertIndex = -1;
        ScriptId newId;

        if (existingIndex >= 0)
        {
            replaceIndex = existingIndex;
            newId = scripts.Entries[existingIndex].Id;
            Info(T("install.update"));
        }
        else
        {
            insertIndex = mainIndex;
            newId = scripts.CreateNewId();
            Info(T("install.add"));
        }

        byte[] scriptCode = LoadScript();

        byte[] newScripts = ScriptsFile.Build(
            scriptsData, scripts, replaceIndex, insertIndex, newId, ScriptTitle, scriptCode);

        // Verify the modified Marshal stream before touching the archive.
        var verifyScripts = ScriptsFile.Read(newScripts);
        int expectedCount = scripts.Entries.Count + (replaceIndex < 0 ? 1 : 0);
        if (verifyScripts.Entries.Count != expectedCount)
        {
            throw new InvalidDataException("Modified Scripts.rvdata2 has an unexpected entry count.");
        }
        if (!verifyScripts.Entries.Any(e => e.Title.IndexOf(ScriptTitle, StringComparison.OrdinalIgnoreCase) >= 0))
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
        Info(TF("install.repack", archiveSizeMb));

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
        if (!checkScripts.Entries.Any(e => e.Title.IndexOf(ScriptTitle, StringComparison.OrdinalIgnoreCase) >= 0))
        {
            throw new InvalidDataException("New archive verification failed.");
        }

        if (!File.Exists(backupPath))
        {
            Info(T("install.backup.start"));
            File.Copy(archivePath, backupPath, overwrite: false);
            Info(TF("install.backup.saved", backupPath));
        }
        else
        {
            Info(TF("install.backup.exists", backupPath));
        }

        File.Move(tempPath, archivePath, overwrite: true);

        Success(T("install.done"));
        Console.WriteLine();
        Console.WriteLine(T("install.next"));
    }

    private static void Uninstall(string archivePath, string backupPath)
    {
        if (!File.Exists(backupPath))
        {
            Error(TF("uninstall.backup.missing", backupPath));
            return;
        }

        if (!Rgss3aArchive.IsArchive(backupPath))
        {
            Error(T("uninstall.backup.invalid"));
            return;
        }

        File.Copy(backupPath, archivePath, overwrite: true);
        File.Delete(backupPath);

        string optionsPath = Path.Combine(Path.GetDirectoryName(archivePath)!, "Options.ini");
        File.Delete(optionsPath);

        Success(T("uninstall.done"));
    }

    private static byte[] LoadScript()
    {
        var assembly = Assembly.GetExecutingAssembly();
        string resource = assembly.GetManifestResourceNames()
            .First(name => name.EndsWith("options.rb", StringComparison.OrdinalIgnoreCase));

        using Stream stream = assembly.GetManifestResourceStream(resource)!;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        byte[] data = memory.ToArray();

        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
        {
            data = data[3..];
        }

        string code = Encoding.UTF8.GetString(data);
        var builder = new StringBuilder();

        builder.AppendLine("module BSOPTS");
        builder.AppendLine("  TEXT = {");
        foreach (var pair in _text.Where(pair => pair.Key.StartsWith("options.", StringComparison.Ordinal)))
        {
            builder.Append("    \"")
                .Append(pair.Key)
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

    private static bool CheckGameIni(string gameDir)
    {
        string ini = Path.Combine(gameDir, "Game.ini");
        try
        {
            if (File.Exists(ini))
            {
                // Latin1 preserves the raw bytes, including CP932 title characters.
                string text = Encoding.Latin1.GetString(File.ReadAllBytes(ini));
                string? titleLine = text.Split('\n')
                    .Select(line => line.TrimEnd('\r'))
                    .FirstOrDefault(line => line.StartsWith("Title=", StringComparison.OrdinalIgnoreCase));
                if (titleLine != null)
                {
                    string title = titleLine[6..].TrimEnd(' ', '\t', '\u0081', '\u0040');

                    if (title.StartsWith(GameTitle, StringComparison.OrdinalIgnoreCase) &&
                        ExtractGameNumber(title).Equals(ToRoman(GameGeneration), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }
        catch
        {
        }

        Error(TF("check.ini", GameName));
        return false;
    }

    private static string ExtractGameNumber(string title)
    {
        string rest = title[GameTitle.Length..].TrimStart(' ', '\t', '\u0081', '\u0040');
        if (rest.Length == 0)
        {
            return "";
        }

        if (rest.Length >= 2 && rest[0] == '\u0087' && rest[1] >= '\u0054' && rest[1] <= '\u005D')
        {
            return RomanNumerals[rest[1] - '\u0054'];
        }

        int length = 0;
        while (length < rest.Length && "IVXLCDM".Contains(char.ToUpperInvariant(rest[length])))
        {
            length++;
        }

        if (length == 0)
        {
            return "?";
        }

        return rest[..length].ToUpperInvariant();
    }

    private static bool IsGameRunning(string gameDir)
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

    private static void Info(string message) => Console.WriteLine(message);

    private static void Success(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    private static void Error(string message)
    {
        Console.Out.Flush();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(message);
        Console.ResetColor();
    }
}

//==============================================================================
// RGSS3A archive reader/writer
//==============================================================================
internal sealed class ArchiveEntry
{
    public required string Name;
    public uint Offset;
    public uint OriginalOffset;
    public uint Size;
    public uint Key;
    public required byte[] EncryptedName;
    public byte[]? NewEncryptedData;
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
                Offset = offset,
                OriginalOffset = offset,
                Size = size,
                Key = key,
                EncryptedName = encryptedName,
            });
        }

        return archive;
    }

    public static bool IsArchive(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[8];
            if (stream.Read(header) != 8)
            {
                return false;
            }

            return header[0] == (byte)'R' && header[1] == (byte)'G' && header[2] == (byte)'S' &&
                   header[3] == (byte)'S' && header[4] == (byte)'A' && header[5] == (byte)'D' &&
                   header[6] == 0 && header[7] == 3;
        }
        catch
        {
            return false;
        }
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
        target.Key = RandomKey();
        target.Size = (uint)newPlainData.Length;
        target.NewEncryptedData = XorPayload(newPlainData, target.Key);

        long tocSize = 8 + 4 + 16;
        foreach (var entry in archive.Entries)
        {
            tocSize += 16 + entry.EncryptedName.Length;
        }

        long offset = tocSize;
        foreach (var entry in archive.Entries)
        {
            if (offset > uint.MaxValue)
            {
                throw new InvalidOperationException("Archive exceeds 4 GiB; RGSS3A cannot address it.");
            }
            entry.Offset = (uint)offset;
            offset += entry.Size;
        }

        if (offset > uint.MaxValue)
        {
            throw new InvalidOperationException("Archive exceeds 4 GiB; RGSS3A cannot address it.");
        }

        using var source = File.OpenRead(sourcePath);
        using var destination = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

        WriteAscii(destination, "RGSSAD\0");
        destination.WriteByte(3);
        WriteU32(destination, archive.Seed);

        foreach (var entry in archive.Entries)
        {
            WriteU32(destination, entry.Offset ^ archive.MasterKey);
            WriteU32(destination, entry.Size ^ archive.MasterKey);
            WriteU32(destination, entry.Key ^ archive.MasterKey);
            WriteU32(destination, (uint)entry.EncryptedName.Length ^ archive.MasterKey);
            destination.Write(entry.EncryptedName);
        }

        WriteU32(destination, 0 ^ archive.MasterKey);
        WriteU32(destination, 0 ^ archive.MasterKey);
        WriteU32(destination, 0 ^ archive.MasterKey);
        WriteU32(destination, 0 ^ archive.MasterKey);

        byte[] buffer = new byte[1024 * 1024];
        foreach (var entry in archive.Entries)
        {
            if (destination.Position != entry.Offset)
            {
                throw new InvalidOperationException($"Offset mismatch while writing {entry.Name}.");
            }

            if (ReferenceEquals(entry, target) && entry.NewEncryptedData != null)
            {
                destination.Write(entry.NewEncryptedData);
            }
            else
            {
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
    }

    private static uint RandomKey()
    {
        Span<byte> bytes = stackalloc byte[4];
        Random.Shared.NextBytes(bytes);
        uint key = BitConverter.ToUInt32(bytes);

        return key == 0 ? 0x12345678u : key;
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

    private static void WriteU32(Stream stream, uint value)
    {
        stream.WriteByte((byte)value);
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 24));
    }

    private static void WriteAscii(Stream stream, string text)
    {
        foreach (char c in text)
        {
            stream.WriteByte((byte)c);
        }
    }
}

//==============================================================================
// Ruby Marshal helpers for Scripts.rvdata2
//==============================================================================
internal readonly record struct ScriptId(bool IsString, long Number)
{
    public object ToMarshalValue()
    {
        if (IsString)
        {
            return new Utf8String(Encoding.UTF8.GetBytes(Number.ToString(CultureInfo.InvariantCulture)));
        }

        return checked((int)Number);
    }
}

internal sealed class ScriptEntry
{
    public ScriptId Id;
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

            if (value is not List<object?> item || item.Count < 3)
            {
                throw new InvalidDataException($"Script entry {i} is not a 3-element array.");
            }
            if (item[1] is not byte[] titleBytes || item[2] is not byte[] compressed)
            {
                throw new InvalidDataException($"Script entry {i} has invalid title or code type.");
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

    private static ScriptId ParseId(object? value, int index)
    {
        return value switch
        {
            int number => new ScriptId(false, number),
            byte[] bytes => ParseStringId(bytes, index),
            _ => throw new InvalidDataException(
                $"Script entry {index} has unsupported ID type: {value?.GetType().Name ?? "null"}"),
        };
    }

    private static ScriptId ParseStringId(byte[] bytes, int index)
    {
        string text = Encoding.UTF8.GetString(bytes);
        if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out long number))
        {
            throw new InvalidDataException($"Script entry {index} has a non-numeric string ID: {text}");
        }

        return new ScriptId(true, number);
    }

    public ScriptId CreateNewId()
    {
        if (Entries.Count == 0)
        {
            return new ScriptId(false, 1);
        }

        bool isString = Entries[0].Id.IsString;
        if (Entries.Any(e => e.Id.IsString != isString))
        {
            throw new InvalidDataException("Scripts.rvdata2 mixes integer and string script IDs.");
        }

        long max = Entries.Max(e => e.Id.Number);
        if (max == long.MaxValue || (!isString && max >= int.MaxValue))
        {
            throw new InvalidDataException("Script ID space exhausted.");
        }

        return new ScriptId(isString, max + 1);
    }

    public static byte[] Build(
        byte[] original,
        ScriptsFile scripts,
        int replaceIndex,
        int insertIndex,
        ScriptId newId,
        string title,
        byte[] codeUtf8)
    {
        byte[] compressed = Compress(codeUtf8);

        var newEntry = new List<object?>
        {
            newId.ToMarshalValue(),
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

        if (replaceIndex < 0 && insertIndex >= scripts.Entries.Count)
        {
            output.Write(newEntryBytes);
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
            case null:
                stream.WriteByte((byte)'0');
                break;
            case bool b:
                stream.WriteByte((byte)(b ? 'T' : 'F'));
                break;

            case int i:
                stream.WriteByte((byte)'i');
                stream.Write(EncodeFixnum(i));
                break;

            case byte[] bytes:
                stream.WriteByte((byte)'"');
                stream.Write(EncodeFixnum(bytes.Length));
                stream.Write(bytes);
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

            case Symbol symbol:
                stream.WriteByte((byte)':');
                byte[] name = Encoding.UTF8.GetBytes(symbol.Name);
                stream.Write(EncodeFixnum(name.Length));
                stream.Write(name);
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
                throw new NotSupportedException($"Cannot serialize {value.GetType()}.");
        }
    }
}
