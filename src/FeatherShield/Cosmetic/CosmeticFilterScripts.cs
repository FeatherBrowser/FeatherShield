using System.Text;

namespace FeatherShield.Cosmetic;

public static class CosmeticFilterScripts
{
    private static readonly Lazy<string> Normal = new(() => Load("cosmetic-filter.js"));
    private static readonly Lazy<string> Strict = new(() => Load("cosmetic-filter-strict.js"));

    public static string Get(bool strict) => strict ? Strict.Value : Normal.Value;

    private static string Load(string name)
    {
        string resourceName = $"FeatherShield.Assets.{name}";
        using Stream stream = typeof(CosmeticFilterScripts).Assembly
            .GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded cosmetic filter was not found: {resourceName}");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
