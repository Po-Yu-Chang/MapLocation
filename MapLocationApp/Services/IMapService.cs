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
}