// ErrorDbGen — genere data/errordb.json : tout le catalogue d'erreurs connu de Windows.
// Enumere les tables systeme (Win32, HRESULT COM/E_*, NTSTATUS via ntdll, Windows Update
// via wuapi) avec FormatMessage. A lancer une fois ; le JSON est embarque dans l'app.
//
//   dotnet run --project tools/ErrorDbGen -- <chemin_sortie_errordb.json>

using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

const uint FROM_HMODULE = 0x00000800;
const uint FROM_SYSTEM = 0x00001000;
const uint IGNORE_INSERTS = 0x00000200;

[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
static extern int FormatMessage(uint flags, IntPtr src, uint id, uint lang,
    StringBuilder buf, int size, IntPtr args);

[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
static extern IntPtr LoadLibrary(string name);

string outPath = args.Length > 0
    ? args[0]
    : Path.Combine("src", "WinLogAnalyzer.App", "data", "errordb.json");

var db = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

string? Fmt(uint flags, IntPtr module, uint id)
{
    var sb = new StringBuilder(4096);
    int len = FormatMessage(flags | IGNORE_INSERTS, module, id, 0, sb, sb.Capacity, IntPtr.Zero);
    if (len <= 0) return null;
    var s = sb.ToString().Replace("\r", " ").Replace("\n", " ").Trim();
    while (s.Contains("  ")) s = s.Replace("  ", " ");
    return string.IsNullOrWhiteSpace(s) ? null : s;
}

void Add(uint code, string? msg)
{
    if (msg is null) return;
    db["0x" + code.ToString("X8")] = msg;
}

// 1) Win32 : 0..15999 (table systeme principale).
for (uint c = 0; c <= 15999; c++)
    Add(c, Fmt(FROM_SYSTEM, IntPtr.Zero, c));

// 2) HRESULT systeme : E_* et COM (0x8000xxxx, 0x8004xxxx), Win32 wrappes (0x8007xxxx).
foreach (uint baseHi in new uint[] { 0x80000000, 0x80040000, 0x80070000, 0x80090000, 0x800B0000 })
    for (uint lo = 0; lo <= 0xFFFF; lo++)
        Add(baseHi | lo, Fmt(FROM_SYSTEM, IntPtr.Zero, baseHi | lo));

// 3) NTSTATUS via ntdll : info (0x4000), warning (0x8000), error (0xC000).
var ntdll = LoadLibrary("ntdll.dll");
if (ntdll != IntPtr.Zero)
    foreach (uint baseHi in new uint[] { 0x40000000, 0x80000000, 0xC0000000 })
        for (uint lo = 0; lo <= 0xFFFF; lo++)
            Add(baseHi | lo, Fmt(FROM_HMODULE, ntdll, baseHi | lo));

// 4) Windows Update via wuapi.dll : 0x80240000..0x80249FFF.
var wuapi = LoadLibrary("wuapi.dll");
if (wuapi != IntPtr.Zero)
    for (uint c = 0x80240000; c <= 0x80249FFF; c++)
        Add(c, Fmt(FROM_HMODULE, wuapi, c));

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
var json = JsonSerializer.Serialize(db, new JsonSerializerOptions
{
    WriteIndented = false,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
});
File.WriteAllText(outPath, json, new UTF8Encoding(false));

Console.WriteLine($"errordb.json genere : {db.Count} codes -> {Path.GetFullPath(outPath)}");
