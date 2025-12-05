-- machines_andon 簡化版資料表
-- 用途: 設定每台機器是否啟用安燈音效

CREATE TABLE IF NOT EXISTS machines_andon (
    machine_name VARCHAR(50) PRIMARY KEY COMMENT '機台名稱 (例如: CNC01, CNC02)',
    andon_enabled TINYINT(1) DEFAULT 1 COMMENT '是否啟用安燈音效 (0: 關閉, 1: 開啟)',
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新時間'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='機台安燈啟用設定表';

-- 插入範例資料
INSERT INTO machines_andon (machine_name, andon_enabled) VALUES
('CNC01', 1),
('CNC02', 1)
ON DUPLICATE KEY UPDATE andon_enabled = VALUES(andon_enabled);

-- 查詢範例
-- SELECT * FROM machines_andon WHERE andon_enabled = 1;

-- 更新範例: 停用 CNC03 的安燈
-- UPDATE machines_andon SET andon_enabled = 0 WHERE machine_name = 'CNC03';

-- 更新範例: 啟用 CNC03 的安燈
-- UPDATE machines_andon SET andon_enabled = 1 WHERE machine_name = 'CNC03';
