using System.Text.Json;
using System.Timers;

namespace MapLocationApp.Services
{
    public interface ITeamLocationService
    {
        Task<bool> CreateTeamAsync(string teamName, string description);
        Task<bool> JoinTeamAsync(string teamCode, string memberName);
        Task<bool> LeaveTeamAsync(string teamId);
        Task<List<Team>> GetUserTeamsAsync();
        Task<List<TeamMember>> GetTeamMembersAsync(string teamId);
        Task<bool> ShareLocationAsync(string teamId, double latitude, double longitude);
        Task<bool> StopLocationSharingAsync(string teamId);
        Task<List<MemberLocation>> GetTeamMemberLocationsAsync(string teamId);
        Task<bool> SendLocationAlertAsync(string teamId, string message, double latitude, double longitude);
        Task<List<LocationAlert>> GetLocationAlertsAsync(string teamId);
        Task<bool> SetLocationSharingSettingsAsync(string teamId, LocationSharingSettings settings);
        Task<LocationSharingSettings> GetLocationSharingSettingsAsync(string teamId);
        void StartLocationSharing(string teamId);
        void StopLocationSharing(string teamId);
    }

    public class TeamLocationService : ITeamLocationService
    {
        private readonly ILocationService _locationService;
        private readonly string _teamsFile;
        private readonly string _membersFile;
        private readonly string _locationsFile;
        private readonly string _alertsFile;
        private readonly string _settingsFile;
        private readonly Dictionary<string, System.Timers.Timer> _locationTimers;
        private readonly Dictionary<string, LocationSharingSettings> _sharingSettings;

        public TeamLocationService(ILocationService locationService)
        {
            _locationService = locationService;
            _teamsFile = Path.Combine(FileSystem.AppDataDirectory, "teams.json");
            _membersFile = Path.Combine(FileSystem.AppDataDirectory, "team_members.json");
            _locationsFile = Path.Combine(FileSystem.AppDataDirectory, "member_locations.json");
            _alertsFile = Path.Combine(FileSystem.AppDataDirectory, "location_alerts.json");
            _settingsFile = Path.Combine(FileSystem.AppDataDirectory, "location_settings.json");
            _locationTimers = new Dictionary<string, System.Timers.Timer>();
            _sharingSettings = new Dictionary<string, LocationSharingSettings>();
        }

        public async Task<bool> CreateTeamAsync(string teamName, string description)
        {
            try
            {
                var teams = await GetUserTeamsAsync();
                var teamCode = GenerateTeamCode();
                
                var newTeam = new Team
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = teamName,
                    Description = description,
                    Code = teamCode,
                    CreatedDate = DateTime.Now,
                    CreatedBy = GetCurrentUserId(),
                    IsActive = true
                };

                teams.Add(newTeam);
                await SaveTeamsAsync(teams);

                // 建立創建者作為團隊成員
                var member = new TeamMember
                {
                    Id = Guid.NewGuid().ToString(),
                    TeamId = newTeam.Id,
                    UserId = GetCurrentUserId(),
                    Name = "Me", // 可以從使用者設定中取得
                    Role = TeamRole.Owner,
                    JoinedDate = DateTime.Now,
                    IsLocationSharingEnabled = false
                };

                var members = await GetTeamMembersListAsync();
                members.Add(member);
                await SaveTeamMembersAsync(members);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Create team error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> JoinTeamAsync(string teamCode, string memberName)
        {
            try
            {
                var teams = await GetUserTeamsAsync();
                var team = teams.FirstOrDefault(t => t.Code.Equals(teamCode, StringComparison.OrdinalIgnoreCase));
                
                if (team == null || !team.IsActive)
                    return false;

                var members = await GetTeamMembersListAsync();
                var existingMember = members.FirstOrDefault(m => m.TeamId == team.Id && m.UserId == GetCurrentUserId());
                
                if (existingMember != null)
                    return false; // 已經是團隊成員

                var newMember = new TeamMember
                {
                    Id = Guid.NewGuid().ToString(),
                    TeamId = team.Id,
                    UserId = GetCurrentUserId(),
                    Name = memberName,
                    Role = TeamRole.Member,
                    JoinedDate = DateTime.Now,
                    IsLocationSharingEnabled = false
                };

                members.Add(newMember);
                await SaveTeamMembersAsync(members);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Join team error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> LeaveTeamAsync(string teamId)
        {
            try
            {
                var members = await GetTeamMembersListAsync();
                var member = members.FirstOrDefault(m => m.TeamId == teamId && m.UserId == GetCurrentUserId());
                
                if (member != null)
                {
                    StopLocationSharing(teamId);
                    members.Remove(member);
                    await SaveTeamMembersAsync(members);
                    return true;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<Team>> GetUserTeamsAsync()
        {
            try
            {
                if (!File.Exists(_teamsFile))
                    return new List<Team>();

                var json = await File.ReadAllTextAsync(_teamsFile);
                var allTeams = JsonSerializer.Deserialize<List<Team>>(json) ?? new List<Team>();
                
                // 取得使用者所屬的團隊
                var members = await GetTeamMembersListAsync();
                var userTeamIds = members.Where(m => m.UserId == GetCurrentUserId()).Select(m => m.TeamId).ToList();
                
                return allTeams.Where(t => userTeamIds.Contains(t.Id)).ToList();
            }
            catch
            {
                return new List<Team>();
            }
        }

        public async Task<List<TeamMember>> GetTeamMembersAsync(string teamId)
        {
            try
            {
                var members = await GetTeamMembersListAsync();
                return members.Where(m => m.TeamId == teamId).ToList();
            }
            catch
            {
                return new List<TeamMember>();
            }
        }

        public async Task<bool> ShareLocationAsync(string teamId, double latitude, double longitude)
        {
            try
            {
                var locations = await GetMemberLocationsListAsync();
                var currentUserId = GetCurrentUserId();
                
                // 移除舊的位置記錄
                locations.RemoveAll(l => l.TeamId == teamId && l.UserId == currentUserId);
                
                // 新增新的位置記錄
                var location = new MemberLocation
                {
                    Id = Guid.NewGuid().ToString(),
                    TeamId = teamId,
                    UserId = currentUserId,
                    Latitude = latitude,
                    Longitude = longitude,
                    Timestamp = DateTime.Now,
                    Accuracy = 10.0 // 預設精確度
                };

                locations.Add(location);
                await SaveMemberLocationsAsync(locations);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Share location error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> StopLocationSharingAsync(string teamId)
        {
            try
            {
                StopLocationSharing(teamId);
                
                // 移除位置記錄
                var locations = await GetMemberLocationsListAsync();
                locations.RemoveAll(l => l.TeamId == teamId && l.UserId == GetCurrentUserId());
                await SaveMemberLocationsAsync(locations);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<MemberLocation>> GetTeamMemberLocationsAsync(string teamId)
        {
            try
            {
                var locations = await GetMemberLocationsListAsync();
                var teamLocations = locations.Where(l => l.TeamId == teamId).ToList();
                
                // 只返回最近 10 分鐘內的位置
                var cutoffTime = DateTime.Now.AddMinutes(-10);
                return teamLocations.Where(l => l.Timestamp > cutoffTime).ToList();
            }
            catch
            {
                return new List<MemberLocation>();
            }
        }

        public async Task<bool> SendLocationAlertAsync(string teamId, string message, double latitude, double longitude)
        {
            try
            {
                var alerts = await GetLocationAlertsListAsync();
                
                var alert = new LocationAlert
                {
                    Id = Guid.NewGuid().ToString(),
                    TeamId = teamId,
                    SenderId = GetCurrentUserId(),
                    Message = message,
                    Latitude = latitude,
                    Longitude = longitude,
                    Timestamp = DateTime.Now,
                    IsRead = false
                };

                alerts.Add(alert);
                await SaveLocationAlertsAsync(alerts);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Send alert error: {ex.Message}");
                return false;
            }
        }

        public async Task<List<LocationAlert>> GetLocationAlertsAsync(string teamId)
        {
            try
            {
                var alerts = await GetLocationAlertsListAsync();
                return alerts.Where(a => a.TeamId == teamId)
                           .OrderByDescending(a => a.Timestamp)
                           .Take(50)
                           .ToList();
            }
            catch
            {
                return new List<LocationAlert>();
            }
        }

        public async Task<bool> SetLocationSharingSettingsAsync(string teamId, LocationSharingSettings settings)
        {
            try
            {
                _sharingSettings[teamId] = settings;
                
                var allSettings = await GetAllLocationSettingsAsync();
                allSettings[teamId] = settings;
                
                var json = JsonSerializer.Serialize(allSettings, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_settingsFile, json);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<LocationSharingSettings> GetLocationSharingSettingsAsync(string teamId)
        {
            try
            {
                if (_sharingSettings.TryGetValue(teamId, out var cachedSettings))
                    return cachedSettings;

                var allSettings = await GetAllLocationSettingsAsync();
                if (allSettings.TryGetValue(teamId, out var settings))
                {
                    _sharingSettings[teamId] = settings;
                    return settings;
                }

                // 預設設定
                var defaultSettings = new LocationSharingSettings
                {
                    UpdateIntervalSeconds = 30,
                    IsAutoSharingEnabled = false,
                    ShareWithAllMembers = true,
                    MaxLocationHistory = 24
                };

                _sharingSettings[teamId] = defaultSettings;
                return defaultSettings;
            }
            catch
            {
                return new LocationSharingSettings();
            }
        }

        public void StartLocationSharing(string teamId)
        {
            try
            {
                StopLocationSharing(teamId); // 停止現有的定時器

                var timer = new System.Timers.Timer();
                timer.Elapsed += (sender, e) => 
                {
                    _ = Task.Run(async () => await OnLocationTimer(teamId));
                };
                timer.AutoReset = true;
                
                // 使用預設間隔，稍後在後台更新
                timer.Interval = 30000; // 30秒預設間隔
                
                _locationTimers[teamId] = timer;
                timer.Start();
                
                // 在後台獲取實際設定並更新間隔
                _ = Task.Run(async () => 
                {
                    try
                    {
                        var settings = await GetLocationSharingSettingsAsync(teamId);
                        timer.Interval = settings.UpdateIntervalSeconds * 1000;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to update timer settings: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Start location sharing error: {ex.Message}");
            }
        }

        public void StopLocationSharing(string teamId)
        {
            try
            {
                if (_locationTimers.TryGetValue(teamId, out var timer))
                {
                    timer.Stop();
                    timer.Dispose();
                    _locationTimers.Remove(teamId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Stop location sharing error: {ex.Message}");
            }
        }

        private async Task OnLocationTimer(string teamId)
        {
            try
            {
                var location = await _locationService.GetCurrentLocationAsync();
                if (location != null)
                {
                    await ShareLocationAsync(teamId, location.Latitude, location.Longitude);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Location timer error: {ex.Message}");
            }
        }

        private string GenerateTeamCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 6)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private string GetCurrentUserId()
        {
            // 這裡應該從使用者認證服務取得當前使用者 ID
            // 目前使用裝置 ID 作為臨時解決方案
            return DeviceInfo.Current.Name ?? Guid.NewGuid().ToString();
        }

        // 檔案操作方法
        private async Task SaveTeamsAsync(List<Team> teams)
        {
            var json = JsonSerializer.Serialize(teams, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_teamsFile, json);
        }

        private async Task<List<TeamMember>> GetTeamMembersListAsync()
        {
            try
            {
                if (!File.Exists(_membersFile))
                    return new List<TeamMember>();

                var json = await File.ReadAllTextAsync(_membersFile);
                return JsonSerializer.Deserialize<List<TeamMember>>(json) ?? new List<TeamMember>();
            }
            catch
            {
                return new List<TeamMember>();
            }
        }

        private async Task SaveTeamMembersAsync(List<TeamMember> members)
        {
            var json = JsonSerializer.Serialize(members, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_membersFile, json);
        }

        private async Task<List<MemberLocation>> GetMemberLocationsListAsync()
        {
            try
            {
                if (!File.Exists(_locationsFile))
                    return new List<MemberLocation>();

                var json = await File.ReadAllTextAsync(_locationsFile);
                return JsonSerializer.Deserialize<List<MemberLocation>>(json) ?? new List<MemberLocation>();
            }
            catch
            {
                return new List<MemberLocation>();
            }
        }

        private async Task SaveMemberLocationsAsync(List<MemberLocation> locations)
        {
            var json = JsonSerializer.Serialize(locations, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_locationsFile, json);
        }

        private async Task<List<LocationAlert>> GetLocationAlertsListAsync()
        {
            try
            {
                if (!File.Exists(_alertsFile))
                    return new List<LocationAlert>();

                var json = await File.ReadAllTextAsync(_alertsFile);
                return JsonSerializer.Deserialize<List<LocationAlert>>(json) ?? new List<LocationAlert>();
            }
            catch
            {
                return new List<LocationAlert>();
            }
        }

        private async Task SaveLocationAlertsAsync(List<LocationAlert> alerts)
        {
            var json = JsonSerializer.Serialize(alerts, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_alertsFile, json);
        }

        private async Task<Dictionary<string, LocationSharingSettings>> GetAllLocationSettingsAsync()
        {
            try
            {
                if (!File.Exists(_settingsFile))
                    return new Dictionary<string, LocationSharingSettings>();

                var json = await File.ReadAllTextAsync(_settingsFile);
                return JsonSerializer.Deserialize<Dictionary<string, LocationSharingSettings>>(json) ?? 
                       new Dictionary<string, LocationSharingSettings>();
            }
            catch
            {
                return new Dictionary<string, LocationSharingSettings>();
            }
        }
    }

    // 資料模型
    public class Team
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Code { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
        public bool IsActive { get; set; }
    }

    public class TeamMember
    {
        public string Id { get; set; }
        public string TeamId { get; set; }
        public string UserId { get; set; }
        public string Name { get; set; }
        public TeamRole Role { get; set; }
        public DateTime JoinedDate { get; set; }
        public bool IsLocationSharingEnabled { get; set; }
    }

    public class MemberLocation
    {
        public string Id { get; set; }
        public string TeamId { get; set; }
        public string UserId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime Timestamp { get; set; }
        public double Accuracy { get; set; }
    }

    public class LocationAlert
    {
        public string Id { get; set; }
        public string TeamId { get; set; }
        public string SenderId { get; set; }
        public string Message { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
    }

    public class LocationSharingSettings
    {
        public int UpdateIntervalSeconds { get; set; } = 30;
        public bool IsAutoSharingEnabled { get; set; } = false;
        public bool ShareWithAllMembers { get; set; } = true;
        public int MaxLocationHistory { get; set; } = 24; // 小時
    }

    public enum TeamRole
    {
        Owner,
        Admin,
        Member
    }
}