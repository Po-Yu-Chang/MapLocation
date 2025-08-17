using MapLocationApp.Models;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace MapLocationApp.Services;

public class MapService : IMapService
{
    private TileProvider _currentProvider = TileProvider.OpenStreetMap;
    private readonly List<TileProvider> _availableProviders = TileProvider.GetDefaultProviders();

    public Mapsui.Map CreateMap()
    {
        var map = new Mapsui.Map();
        
        // 添加預設圖磚層
        var tileLayer = CreateTileLayer(_currentProvider);
        map.Layers.Add(tileLayer);
        
        return map;
    }

    public void SwitchTileProvider(MapControl mapControl, TileProvider provider)
    {
        if (mapControl.Map == null) return;

        // 移除現有的圖磚層
        var tileLayers = mapControl.Map.Layers.Where(l => l is TileLayer).ToList();
        foreach (var layer in tileLayers)
        {
            mapControl.Map.Layers.Remove(layer);
        }

        // 添加新的圖磚層
        var newTileLayer = CreateTileLayer(provider);
        mapControl.Map.Layers.Insert(0, newTileLayer); // 插入到底層

        _currentProvider = provider;
        
        // 更新 attribution
        UpdateAttribution(mapControl, provider.Attribution);
        
        // 觸發地圖重繪
        mapControl.Refresh();
    }

    private TileLayer CreateTileLayer(TileProvider provider)
    {
        var httpTileSource = new HttpTileSource(
            new GlobalSphericalMercator(0, provider.MaxZoom),
            provider.UrlTemplate,
            serverNodes: new[] { "a", "b", "c" },
            name: provider.Name,
            attribution: new Attribution(provider.Attribution)
        );

        return new TileLayer(httpTileSource) { Name = provider.Name };
    }

    public void UpdateAttribution(MapControl mapControl, string attribution)
    {
        // 這裡可以更新 UI 上的 attribution 顯示
        // 實際實作會在頁面層級處理
    }

    public List<TileProvider> GetAvailableProviders()
    {
        return _availableProviders;
    }

    public TileProvider GetCurrentProvider()
    {
        return _currentProvider;
    }

    public void AddGeofenceLayer(Mapsui.Map map, IEnumerable<GeofenceRegion> geofences)
    {
        // 移除現有的地理圍欄層
        var existingLayer = map.Layers.FirstOrDefault(l => l.Name == "Geofences");
        if (existingLayer != null)
            map.Layers.Remove(existingLayer);

        if (!geofences.Any()) return;

        var features = new List<IFeature>();

        foreach (var geofence in geofences)
        {
            // 建立圓形幾何
            var point = new Point(geofence.Longitude, geofence.Latitude);
            var circle = point.Buffer(geofence.RadiusMeters / 111320.0); // 概略轉換為度數

            var feature = new Feature
            {
                Geometry = circle,
                ["Name"] = geofence.Name,
                ["Id"] = geofence.Id,
                ["Category"] = geofence.Category
            };

            features.Add(feature);
        }

        var memoryProvider = new MemoryProvider(features);
        var geofenceLayer = new Layer("Geofences")
        {
            DataSource = memoryProvider,
            Style = new VectorStyle
            {
                Fill = new Brush { Color = Color.FromArgb(50, 0, 123, 255) },
                Outline = new Pen { Color = Color.Blue, Width = 2 }
            }
        };

        map.Layers.Add(geofenceLayer);
    }

    public void AddLocationMarker(Mapsui.Map map, double latitude, double longitude, string? label = null)
    {
        // 移除現有的位置標記層
        var existingLayer = map.Layers.FirstOrDefault(l => l.Name == "LocationMarker");
        if (existingLayer != null)
            map.Layers.Remove(existingLayer);

        var point = new Point(longitude, latitude);
        var feature = new Feature
        {
            Geometry = point,
            ["Label"] = label ?? "Current Location"
        };

        var memoryProvider = new MemoryProvider(new[] { feature });
        var markerLayer = new Layer("LocationMarker")
        {
            DataSource = memoryProvider,
            Style = new SymbolStyle
            {
                SymbolScale = 0.8,
                Fill = new Brush { Color = Color.Red },
                Outline = new Pen { Color = Color.White, Width = 2 }
            }
        };

        map.Layers.Add(markerLayer);
    }

    public void CenterMap(MapControl mapControl, double latitude, double longitude, int zoomLevel = 15)
    {
        if (mapControl.Map == null) return;

        var sphericalMercatorCoordinate = Mapsui.Projections.SphericalMercator.FromLonLat(longitude, latitude);
        mapControl.Map.Home = n => n.CenterOnAndZoomTo(sphericalMercatorCoordinate, mapControl.Map.Resolutions[zoomLevel]);
        mapControl.Map.Home(mapControl.Map.Navigator);
    }
}