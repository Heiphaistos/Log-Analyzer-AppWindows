using System.Text.Json;

namespace WinLogAnalyzer.Core.Settings;

/// <summary>
/// Preferences utilisateur persistees dans %AppData%/WinLogAnalyzer/settings.json.
/// </summary>
public sealed class AppSettings
{
    public string SelectedLog { get; set; } = "System";
    public int MaxCount { get; set; } = 100;
    public bool LevelCritical { get; set; } = true;
    public bool LevelError { get; set; } = true;
    public bool LevelWarning { get; set; }
    public bool LevelInformation { get; set; }
    public bool GroupDuplicates { get; set; } = true;
    public bool MonitorEnabled { get; set; }

    private static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinLogAnalyzer");

    private static string FilePath => Path.Combine(Dir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Lecture settings echouee: {ex.Message}");
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Ecriture settings echouee: {ex.Message}");
        }
    }

    /// <summary>Niveaux actifs sous forme de codes Windows (1..4).</summary>
    public IReadOnlyList<int> SelectedLevels()
    {
        var levels = new List<int>();
        if (LevelCritical) levels.Add(1);
        if (LevelError) levels.Add(2);
        if (LevelWarning) levels.Add(3);
        if (LevelInformation) levels.Add(4);
        if (levels.Count == 0) { levels.Add(1); levels.Add(2); } // jamais vide
        return levels;
    }
}
