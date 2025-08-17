namespace MapLocationApp.Models;

public class TileProvider
{
    public string Name { get; set; } = string.Empty;
    public string UrlTemplate { get; set; } = string.Empty;
    public string Attribution { get; set; } = string.Empty;
    public int MaxZoom { get; set; } = 19;
    public bool RequiresApiKey { get; set; } = false;
    public string? ApiKey { get; set; }

    public static readonly TileProvider OpenStreetMap = new()
    {
        Name = "OpenStreetMap",
        UrlTemplate = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png",
        Attribution = "© OpenStreetMap contributors",
        MaxZoom = 19
    };

    public static readonly TileProvider OpenStreetMapDE = new()
    {
        Name = "OpenStreetMap DE",
        UrlTemplate = "https://{s}.tile.openstreetmap.de/{z}/{x}/{y}.png",
        Attribution = "© OpenStreetMap contributors",
        MaxZoom = 18
    };

    public static readonly TileProvider CartoDB = new()
    {
        Name = "CartoDB Positron",
        UrlTemplate = "https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}.png",
        Attribution = "© CartoDB © OpenStreetMap contributors",
        MaxZoom = 18
    };

    public static readonly TileProvider StamenTerrain = new()
    {
        Name = "Stamen Terrain",
        UrlTemplate = "https://tiles.stadiamaps.com/tiles/stamen_terrain/{z}/{x}/{y}.png",
        Attribution = "© Stamen Design © OpenStreetMap contributors",
        MaxZoom = 16
    };

    public static List<TileProvider> GetDefaultProviders() => new()
    {
        OpenStreetMap,
        OpenStreetMapDE,
        CartoDB,
        StamenTerrain
    };
}