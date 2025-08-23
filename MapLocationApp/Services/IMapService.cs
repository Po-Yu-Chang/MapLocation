using MapLocationApp.Models;
using Mapsui.UI.Maui;

namespace MapLocationApp.Services;

public interface IMapService
{
    Mapsui.Map CreateMap();
    void SwitchTileProvider(MapControl mapControl, TileProvider provider);
    void UpdateAttribution(MapControl mapControl, string attribution);
    List<TileProvider> GetAvailableProviders();
    TileProvider GetCurrentProvider();
    void AddGeofenceLayer(Mapsui.Map map, IEnumerable<GeofenceRegion> geofences);
    void AddLocationMarker(Mapsui.Map map, double latitude, double longitude, string? label = null);
    void CenterMap(MapControl mapControl, double latitude, double longitude, int zoomLevel = 15);
    
    // 新增路線渲染功能
    void DrawRoute(Mapsui.Map map, Route route, string routeColor = "#2196F3", int width = 5);
    void DrawAlternativeRoutes(Mapsui.Map map, List<Route> routes);
    void ClearRoutes(Mapsui.Map map);
    void ShowRouteDirectionArrows(Mapsui.Map map, Route route);
    void HighlightActiveRoute(Mapsui.Map map, Route activeRoute);
    
    // 新增用戶位置追蹤功能
    void UpdateUserLocation(Mapsui.Map map, double latitude, double longitude, float bearing = 0, double accuracy = 0);
    void EnableLocationFollowMode(MapControl mapControl, bool followUser);
    void ShowLocationAccuracyCircle(Mapsui.Map map, double latitude, double longitude, double accuracy);
    
    // 新增地圖視圖控制
    void AnimateToLocation(MapControl mapControl, double latitude, double longitude, int zoomLevel = 15);
    void AnimateToRoute(MapControl mapControl, Route route, int padding = 50);
    void SetMapBearing(MapControl mapControl, float bearing);
    
    // 新增座標轉換功能
    (double latitude, double longitude)? ScreenToWorldCoordinates(MapControl mapControl, double screenX, double screenY);
}