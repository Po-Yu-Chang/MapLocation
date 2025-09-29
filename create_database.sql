-- 創建 MapLocation 資料庫
-- 在執行 database_init.sql 之前先執行此腳本

-- 創建資料庫
CREATE DATABASE IF NOT EXISTS maplocation 
CHARACTER SET utf8mb4 
COLLATE utf8mb4_unicode_ci;

-- 創建用戶 (可選 - 如果需要專用用戶)
-- CREATE USER IF NOT EXISTS 'maplocation_user'@'%' IDENTIFIED BY 'your_secure_password';
-- GRANT ALL PRIVILEGES ON maplocation.* TO 'maplocation_user'@'%';
-- FLUSH PRIVILEGES;

-- 顯示資料庫創建結果
SHOW DATABASES LIKE 'maplocation';