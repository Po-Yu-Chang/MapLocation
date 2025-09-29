-- MapLocation 資料庫初始化腳本
-- 建立 MapLocation 資料庫的所有必要表格

-- 確保使用正確的資料庫
USE maplocation;

-- 1. 使用者表格 (users)
CREATE TABLE IF NOT EXISTS users (
    id INT AUTO_INCREMENT PRIMARY KEY,
    username VARCHAR(50) NOT NULL UNIQUE,
    password VARCHAR(255) NOT NULL,  -- 用於存儲雜湊後的密碼
    email VARCHAR(100),
    full_name VARCHAR(100),
    department VARCHAR(50),
    position VARCHAR(50),
    is_active BOOLEAN DEFAULT TRUE,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    last_login_at DATETIME NULL,
    INDEX idx_username (username),
    INDEX idx_active (is_active)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2. 地理圍欄區域表格 (geofence_regions)
CREATE TABLE IF NOT EXISTS geofence_regions (
    id VARCHAR(36) PRIMARY KEY,  -- GUID
    name VARCHAR(100) NOT NULL,
    latitude DOUBLE NOT NULL,
    longitude DOUBLE NOT NULL,
    radius_meters DOUBLE NOT NULL,
    is_active BOOLEAN DEFAULT TRUE,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    transition_type TINYINT DEFAULT 3,  -- 1=Enter, 2=Exit, 3=Both
    category VARCHAR(50) DEFAULT '',
    description TEXT,
    INDEX idx_active (is_active),
    INDEX idx_location (latitude, longitude),
    INDEX idx_category (category)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 3. 打卡記錄表格 (check_in_records)
CREATE TABLE IF NOT EXISTS check_in_records (
    id VARCHAR(36) PRIMARY KEY,  -- GUID
    user_id VARCHAR(10) NOT NULL,  -- 對應 users.username 或 id
    geofence_id VARCHAR(36),
    geofence_name VARCHAR(100),
    check_in_time DATETIME DEFAULT CURRENT_TIMESTAMP,
    check_out_time DATETIME NULL,
    latitude DOUBLE,
    longitude DOUBLE,
    notes TEXT,
    type TINYINT DEFAULT 0,  -- 0=Manual, 1=Automatic, 2=GPS
    INDEX idx_user_id (user_id),
    INDEX idx_geofence_id (geofence_id),
    INDEX idx_check_in_time (check_in_time),
    FOREIGN KEY (geofence_id) REFERENCES geofence_regions(id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 4. 地理圍欄事件表格 (geofence_events)
CREATE TABLE IF NOT EXISTS geofence_events (
    id INT AUTO_INCREMENT PRIMARY KEY,
    geofence_id VARCHAR(36) NOT NULL,
    geofence_name VARCHAR(100),
    transition_type TINYINT NOT NULL,  -- 1=Enter, 2=Exit, 3=Both
    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    latitude DOUBLE,
    longitude DOUBLE,
    accuracy DOUBLE DEFAULT 0,
    user_id VARCHAR(10) NOT NULL,
    INDEX idx_geofence_id (geofence_id),
    INDEX idx_user_id (user_id),
    INDEX idx_timestamp (timestamp),
    INDEX idx_transition_type (transition_type),
    FOREIGN KEY (geofence_id) REFERENCES geofence_regions(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 插入預設管理員帳號
-- 密碼: admin123 (雜湊後)
INSERT IGNORE INTO users (username, password, email, full_name, department, position, is_active) 
VALUES 
('admin', '$2a$10$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy', 'admin@maplocation.com', '系統管理員', 'IT', '系統管理員', TRUE),
('demo', '$2a$10$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy', 'demo@maplocation.com', '示範使用者', '業務', '業務員', TRUE);

-- 插入示範地理圍欄區域
INSERT IGNORE INTO geofence_regions (id, name, latitude, longitude, radius_meters, category, description) 
VALUES 
('office-main', '總公司', 25.047924, 121.517081, 100.0, '辦公室', '公司總部辦公室'),
('warehouse-a', '倉庫A', 25.048000, 121.517200, 50.0, '倉庫', '主要倉庫區域'),
('client-abc', '客戶ABC公司', 25.047800, 121.516900, 30.0, '客戶', 'ABC公司辦公室');

-- 創建用於測試的視圖
CREATE OR REPLACE VIEW user_check_in_summary AS
SELECT 
    u.username,
    u.full_name,
    COUNT(c.id) as total_checkins,
    MAX(c.check_in_time) as last_checkin,
    COUNT(CASE WHEN DATE(c.check_in_time) = CURDATE() THEN 1 END) as today_checkins
FROM users u
LEFT JOIN check_in_records c ON u.username = c.user_id
WHERE u.is_active = TRUE
GROUP BY u.id, u.username, u.full_name;

-- 顯示創建結果
SELECT 'Database tables created successfully' as result;
SELECT TABLE_NAME, TABLE_ROWS FROM information_schema.TABLES 
WHERE TABLE_SCHEMA = 'maplocation' AND TABLE_TYPE = 'BASE TABLE';