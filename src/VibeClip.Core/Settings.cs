using System.Text.Json;

namespace VibeClip.Core;

public sealed record AudioSource(string Name, string Target, bool Enabled, bool IsDesktop = false);

public sealed record AppSettings
{
    public int Width { get; init; } = 1920;
    public int Height { get; init; } = 1080;
    public int FramesPerSecond { get; init; } = 60;
    public int ClipSeconds { get; init; } = 60;
    public string ClipFolder { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "VibeClip");
    public int HotkeyKey { get; init; } = 120; // F9 virtual key
    public uint HotkeyModifiers { get; init; } = 6; // Control + Shift
    public bool ShowCursor { get; init; } = true;
    public bool SeparateTracks { get; init; } = true;
    public bool NotifyOnSave { get; init; } = true;
    public AudioSource[] AudioSources { get; init; } =
    [
        new("Escritorio", "Salida predeterminada de Windows", true, true),
        new("Juego", "Sin seleccionar", false),
        new("Micrófono", "Dispositivo predeterminado", false),
        new("Música", "Sin seleccionar", false),
        new("Discord", "Sin seleccionar", false)
    ];

    public void Validate()
    {
        if (Width is < 320 or > 7680 || Height is < 240 or > 4320)
            throw new ArgumentException("Resolución fuera del rango admitido.");
        if (FramesPerSecond is not (30 or 60)) throw new ArgumentException("Selecciona 30 o 60 FPS.");
        if (ClipSeconds is < 5 or > 600) throw new ArgumentException("La duración debe estar entre 5 y 600 segundos.");
        if (string.IsNullOrWhiteSpace(ClipFolder) || !Path.IsPathFullyQualified(ClipFolder) || ClipFolder.StartsWith(@"\\", StringComparison.Ordinal))
            throw new ArgumentException("Selecciona una carpeta local con ruta absoluta.");
        if (HotkeyKey is < 1 or > 254 || (HotkeyModifiers & ~15u) != 0 || HotkeyModifiers == 0)
            throw new ArgumentException("El atajo necesita una tecla y al menos un modificador.");
        if (AudioSources is null || AudioSources.Length > 16 || AudioSources.Any(x => x is null || string.IsNullOrWhiteSpace(x.Name)))
            throw new ArgumentException("La configuración de audio no es válida.");
    }
}

public sealed class SettingsStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string? LoadWarning { get; private set; }

    public AppSettings Load()
    {
        if (!File.Exists(path)) return new();
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions)
                ?? throw new JsonException("Configuración vacía.");
            settings.Validate();
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            LoadWarning = "No se pudo leer la configuración. Se usarán los valores predeterminados; el archivo original no se ha modificado.";
            return new();
        }
    }

    public void Save(AppSettings settings)
    {
        settings.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
