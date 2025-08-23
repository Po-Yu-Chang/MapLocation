using MapLocationApp.Models;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Tiling.Layers;
using Mapsui.UI.Maui;
using Mapsui.Projections;
using Mapsui.Nts;
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
        try
        {
            // 移除現有的位置標記層
            var existingLayer = map.Layers.FirstOrDefault(l => l.Name == "LocationMarker");
            if (existingLayer != null)
                map.Layers.Remove(existingLayer);

            // 使用 NetTopologySuite 創建點幾何
            var position = SphericalMercator.FromLonLat(longitude, latitude);
            var point = new NetTopologySuite.Geometries.Point(position.x, position.y);
            var feature = new GeometryFeature(point);
            
            // 添加屬性
            if (!string.IsNullOrEmpty(label))
            {
                feature["Label"] = label;
            }

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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"添加位置標記錯誤: {ex.Message}");
            
            // 使用簡化的標記方法作為備用
            try
            {
                AddSimpleLocationMarker(map, latitude, longitude, label);
            }
            catch (Exception fallbackEx)
            {
                System.Diagnostics.Debug.WriteLine($"備用標記方法也失敗: {fallbackEx.Message}");
            }
        }
    }
    
    private void AddSimpleLocationMarker(Mapsui.Map map, double latitude, double longitude, string? label = null)
    {
        // 簡化版本：只添加一個最基本的標記層
        var existingLayer = map.Layers.FirstOrDefault(l => l.Name == "SimpleLocationMarker");
        if (existingLayer != null)
            map.Layers.Remove(existingLayer);
            
        var position = SphericalMercator.FromLonLat(longitude, latitude);
        var point = new NetTopologySuite.Geometries.Point(position.x, position.y);
        var feature = new GeometryFeature(point);
        
        var memoryProvider = new MemoryProvider(feature);
        var markerLayer = new Layer("SimpleLocationMarker")
        {
            DataSource = memoryProvider,
            Style = new SymbolStyle
            {
                SymbolScale = 1.0,
                Fill = new Mapsui.Styles.Brush { Color = Mapsui.Styles.Color.Blue }
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

    // 路線渲染功能實作
    public void DrawRoute(Mapsui.Map map, Route route, string routeColor = "#2196F3", int width = 5)
    {
        if (map == null || route == null) return;

        try
        {
            // 移除現有的路線層
            var existingRouteLayer = map.Layers.FirstOrDefault(l => l.Name == "ActiveRoute");
            if (existingRouteLayer != null)
                map.Layers.Remove(existingRouteLayer);

            // 創建路線點集合 - 使用更詳細的路線步驟
            var coordinates = new List<Coordinate>();
            
            // 起點
            coordinates.Add(new Coordinate(route.StartLongitude, route.StartLatitude));
            
            // 如果有路線步驟，加入所有步驟點創建更平滑的路線
            if (route.Steps != null && route.Steps.Any())
            {
                foreach (var step in route.Steps)
                {
                    // 如果步驟有起點和終點，可以創建更詳細的路線
                    if (Math.Abs(step.StartLatitude - step.EndLatitude) > 0.001 || 
                        Math.Abs(step.StartLongitude - step.EndLongitude) > 0.001)
                    {
                        // 在長距離步驟中插入中間點使路線更平滑
                        var stepDistance = CalculateStepDistance(step.StartLatitude, step.StartLongitude, 
                                                               step.EndLatitude, step.EndLongitude);
                        if (stepDistance > 0.01) // 如果步驟超過1公里，插入中間點
                        {
                            int intermediatePoints = Math.Min(3, (int)(stepDistance * 10)); // 每0.1km一個點，最多3個
                            for (int i = 1; i <= intermediatePoints; i++)
                            {
                                double ratio = (double)i / (intermediatePoints + 1);
                                var intermediateLat = step.StartLatitude + (step.EndLatitude - step.StartLatitude) * ratio;
                                var intermediateLng = step.StartLongitude + (step.EndLongitude - step.StartLongitude) * ratio;
                                coordinates.Add(new Coordinate(intermediateLng, intermediateLat));
                            }
                        }
                    }
                    
                    // 添加步驟終點
                    coordinates.Add(new Coordinate(step.EndLongitude, step.EndLatitude));
                }
            }
            else
            {
                // 如果沒有詳細步驟，創建直線路線
                coordinates.Add(new Coordinate(route.EndLongitude, route.EndLatitude));
            }

            // 創建線幾何
            var lineString = new LineString(coordinates.ToArray());
            var feature = new GeometryFeature(lineString);

            // 設定路線樣式
            var routeStyle = new VectorStyle
            {
                Line = new Pen
                {
                    Color = Mapsui.Styles.Color.FromString(routeColor),
                    Width = width,
                    PenStyle = PenStyle.Solid
                }
            };

            var memoryProvider = new MemoryProvider(feature);
            var routeLayer = new Layer("ActiveRoute")
            {
                DataSource = memoryProvider,
                Style = routeStyle
            };

            map.Layers.Add(routeLayer);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"繪製路線錯誤: {ex.Message}");
        }
    }

    public void DrawAlternativeRoutes(Mapsui.Map map, List<Route> routes)
    {
        if (map == null || routes == null || !routes.Any()) return;

        // 清除現有的替代路線
        var existingAltRoutes = map.Layers.Where(l => l.Name?.StartsWith("AltRoute_") == true).ToList();
        foreach (var layer in existingAltRoutes)
        {
            map.Layers.Remove(layer);
        }

        // 繪製每條替代路線
        var colors = new[] { "#9E9E9E", "#757575", "#616161" };
        for (int i = 0; i < Math.Min(routes.Count, colors.Length); i++)
        {
            var route = routes[i];
            var color = colors[i];
            
            try
            {
                var coordinates = new List<Coordinate>
                {
                    new Coordinate(route.StartLongitude, route.StartLatitude),
                    new Coordinate(route.EndLongitude, route.EndLatitude)
                };

                var lineString = new LineString(coordinates.ToArray());
                var feature = new GeometryFeature(lineString);

                var altRouteStyle = new VectorStyle
                {
                    Line = new Pen
                    {
                        Color = Mapsui.Styles.Color.FromString(color),
                        Width = 3,
                        PenStyle = PenStyle.Dash
                    }
                };

                var memoryProvider = new MemoryProvider(feature);
                var altRouteLayer = new Layer($"AltRoute_{i}")
                {
                    DataSource = memoryProvider,
                    Style = altRouteStyle
                };

                map.Layers.Add(altRouteLayer);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"繪製替代路線 {i} 錯誤: {ex.Message}");
            }
        }
    }

    public void ClearRoutes(Mapsui.Map map)
    {
        if (map == null) return;

        var routeLayers = map.Layers.Where(l => 
            l.Name == "ActiveRoute" || 
            l.Name?.StartsWith("AltRoute_") == true ||
            l.Name == "RouteDirections").ToList();

        foreach (var layer in routeLayers)
        {
            map.Layers.Remove(layer);
        }
    }

    public void ShowRouteDirectionArrows(Mapsui.Map map, Route route)
    {
        // 簡化版本：在路線上顯示方向指示
        // 完整實作需要計算路線方向並在地圖上放置箭頭圖標
        System.Diagnostics.Debug.WriteLine("顯示路線方向箭頭功能待實作");
    }

    public void HighlightActiveRoute(Mapsui.Map map, Route activeRoute)
    {
        if (activeRoute != null)
        {
            DrawRoute(map, activeRoute, "#2196F3", 6); // 較粗的藍色線
        }
    }

    // 用戶位置追蹤功能
    public void UpdateUserLocation(Mapsui.Map map, double latitude, double longitude, float bearing = 0, double accuracy = 0)
    {
        if (map == null) return;

        try
        {
            // 移除現有的用戶位置標記
            var existingUserLocation = map.Layers.FirstOrDefault(l => l.Name == "UserLocation");
            if (existingUserLocation != null)
                map.Layers.Remove(existingUserLocation);

            // 創建用戶位置點
            var position = SphericalMercator.FromLonLat(longitude, latitude);
            var feature = new PointFeature(new MPoint(position.x, position.y));

            // 設定用戶位置樣式（藍色圓點）
            var userLocationStyle = new SymbolStyle
            {
                SymbolScale = 0.8,
                Fill = new Mapsui.Styles.Brush { Color = Mapsui.Styles.Color.FromArgb(255, 33, 150, 243) }, // 藍色
                Outline = new Pen { Color = Mapsui.Styles.Color.White, Width = 3 }
            };

            var memoryProvider = new MemoryProvider(feature);
            var userLocationLayer = new Layer("UserLocation")
            {
                DataSource = memoryProvider,
                Style = userLocationStyle
            };

            map.Layers.Add(userLocationLayer);

            // 如果有精確度資訊，顯示精確度圓圈
            if (accuracy > 0)
            {
                ShowLocationAccuracyCircle(map, latitude, longitude, accuracy);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"更新用戶位置錯誤: {ex.Message}");
        }
    }

    public void EnableLocationFollowMode(MapControl mapControl, bool followUser)
    {
        // 這個功能需要在位置更新時自動調整地圖中心
        // 實作會在位置服務層面處理
        System.Diagnostics.Debug.WriteLine($"位置跟隨模式: {(followUser ? "啟用" : "停用")}");
    }

    public void ShowLocationAccuracyCircle(Mapsui.Map map, double latitude, double longitude, double accuracy)
    {
        if (map == null || accuracy <= 0) return;

        try
        {
            // 移除現有的精確度圓圈
            var existingAccuracyCircle = map.Layers.FirstOrDefault(l => l.Name == "AccuracyCircle");
            if (existingAccuracyCircle != null)
                map.Layers.Remove(existingAccuracyCircle);

            // 創建精確度圓圈（簡化版本）
            var center = SphericalMercator.FromLonLat(longitude, latitude);
            var feature = new PointFeature(new MPoint(center.x, center.y));

            var accuracyStyle = new SymbolStyle
            {
                SymbolScale = Math.Max(0.1, accuracy / 100), // 根據精確度調整大小
                Fill = new Mapsui.Styles.Brush { Color = Mapsui.Styles.Color.FromArgb(50, 33, 150, 243) }, // 半透明藍色
                Outline = new Pen { Color = Mapsui.Styles.Color.FromArgb(100, 33, 150, 243), Width = 1 }
            };

            var memoryProvider = new MemoryProvider(feature);
            var accuracyLayer = new Layer("AccuracyCircle")
            {
                DataSource = memoryProvider,
                Style = accuracyStyle
            };

            map.Layers.Add(accuracyLayer);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"顯示精確度圓圈錯誤: {ex.Message}");
        }
    }

    // 地圖視圖控制
    public void AnimateToLocation(MapControl mapControl, double latitude, double longitude, int zoomLevel = 15)
    {
        if (mapControl?.Map == null) return;

        try
        {
            var sphericalMercatorCoordinate = SphericalMercator.FromLonLat(longitude, latitude);
            var point = new MPoint(sphericalMercatorCoordinate.x, sphericalMercatorCoordinate.y);
            
            // 使用動畫移動到指定位置
            mapControl.Map.Navigator.CenterOn(point);
            if (zoomLevel > 0 && zoomLevel < mapControl.Map.Navigator.Resolutions.Count)
            {
                mapControl.Map.Navigator.ZoomTo(mapControl.Map.Navigator.Resolutions[zoomLevel]);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"動畫移動到位置錯誤: {ex.Message}");
        }
    }

    public void AnimateToRoute(MapControl mapControl, Route route, int padding = 50)
    {
        if (mapControl?.Map == null || route == null) return;

        try
        {
            // 計算路線的邊界框
            var minLat = Math.Min(route.StartLatitude, route.EndLatitude);
            var maxLat = Math.Max(route.StartLatitude, route.EndLatitude);
            var minLng = Math.Min(route.StartLongitude, route.EndLongitude);
            var maxLng = Math.Max(route.StartLongitude, route.EndLongitude);

            // 轉換為地圖座標
            var southwest = SphericalMercator.FromLonLat(minLng, minLat);
            var northeast = SphericalMercator.FromLonLat(maxLng, maxLat);

            // 創建邊界框並縮放到適合的層級
            var bbox = new MRect(southwest.x, southwest.y, northeast.x, northeast.y);
            mapControl.Map.Navigator.ZoomToBox(bbox);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"縮放到路線錯誤: {ex.Message}");
        }
    }

    public void SetMapBearing(MapControl mapControl, float bearing)
    {
        if (mapControl?.Map == null) return;

        try
        {
            // 設定地圖旋轉角度
            mapControl.Map.Navigator.RotateTo(bearing);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"設定地圖方向錯誤: {ex.Message}");
        }
    }

    private double CalculateStepDistance(double lat1, double lng1, double lat2, double lng2)
    {
        const double earthRadius = 6371; // 地球半徑（公里）
        
        var lat1Rad = lat1 * Math.PI / 180;
        var lat2Rad = lat2 * Math.PI / 180;
        var deltaLatRad = (lat2 - lat1) * Math.PI / 180;
        var deltaLngRad = (lng2 - lng1) * Math.PI / 180;

        var a = Math.Sin(deltaLatRad / 2) * Math.Sin(deltaLatRad / 2) +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                Math.Sin(deltaLngRad / 2) * Math.Sin(deltaLngRad / 2);
        
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        
        return earthRadius * c;
    }
    
    public (double latitude, double longitude)? ScreenToWorldCoordinates(MapControl mapControl, double screenX, double screenY)
    {
        try
        {
            if (mapControl?.Map == null) return null;
            
            // 使用 MapControl 提供的座標轉換方法
            var viewport = mapControl.Map.Navigator.Viewport;
            var centerX = viewport.CenterX;
            var centerY = viewport.CenterY;
            var resolution = viewport.Resolution;
            
            // 計算世界座標
            var worldX = centerX + (screenX - viewport.Width / 2.0) * resolution;
            var worldY = centerY - (screenY - viewport.Height / 2.0) * resolution;
            
            // 轉換為地理座標 (WGS84)
            var lonLat = Mapsui.Projections.SphericalMercator.ToLonLat(worldX, worldY);
            
            return (lonLat.lat, lonLat.lon);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"座標轉換錯誤: {ex.Message}");
            return null;
        }
    }
}