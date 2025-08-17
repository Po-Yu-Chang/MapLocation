using MapLocationApp.Models;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Tiling.Layers;
using Mapsui.UI.Maui;
using Mapsui.Projections;

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
        // 使用預設的 OSM 圖磚層
        return Mapsui.Tiling.OpenStreetMap.CreateTileLayer();
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

        var features = new List<Mapsui.IFeature>();

        foreach (var geofence in geofences)
        {
            // 建立點特徵
            var center = SphericalMercator.FromLonLat(geofence.Longitude, geofence.Latitude);
            var feature = new PointFeature(new MPoint(center.x, center.y));
            features.Add(feature);
        }

        var memoryProvider = new MemoryProvider(features);
        var geofenceLayer = new Layer("Geofences")
        {
            DataSource = memoryProvider,
            Style = new VectorStyle
            {
                Fill = new Mapsui.Styles.Brush { Color = Mapsui.Styles.Color.FromArgb(50, 0, 123, 255) },
                Outline = new Pen { Color = Mapsui.Styles.Color.Blue, Width = 2 }
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

        var position = SphericalMercator.FromLonLat(longitude, latitude);
        var feature = new PointFeature(new MPoint(position.x, position.y));

        var memoryProvider = new MemoryProvider(feature);
        var markerLayer = new Layer("LocationMarker")
        {
            DataSource = memoryProvider,
            Style = new SymbolStyle
            {
                SymbolScale = 0.8,
                Fill = new Mapsui.Styles.Brush { Color = Mapsui.Styles.Color.Red },
                Outline = new Pen { Color = Mapsui.Styles.Color.White, Width = 2 }
            }
        };

        map.Layers.Add(markerLayer);
    }

    public void CenterMap(MapControl mapControl, double latitude, double longitude, int zoomLevel = 15)
    {
        if (mapControl.Map == null) return;

        var sphericalMercatorCoordinate = SphericalMercator.FromLonLat(longitude, latitude);
        var point = new MPoint(sphericalMercatorCoordinate.x, sphericalMercatorCoordinate.y);
        
        // 直接設定地圖中心和縮放等級
        mapControl.Map.Navigator.CenterOn(point);
        mapControl.Map.Navigator.ZoomTo(mapControl.Map.Navigator.Resolutions[Math.Min(zoomLevel, mapControl.Map.Navigator.Resolutions.Count - 1)]);
    }
}