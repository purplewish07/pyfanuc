import time
import subprocess
import platform
import logging
import psutil
import os
from datetime import datetime, timedelta

# 設定簡潔的日誌 - 只記錄錯誤和關鍵資訊
logging.basicConfig(
    level=logging.WARNING,
    format='%(asctime)s - %(message)s',
    handlers=[
        logging.FileHandler('winloop_error.log', encoding='utf-8')
    ]
)

# 新增：監控相關變數
last_successful_time = datetime.now()
health_check_interval = 300  # 5分鐘健康檢查間隔
last_health_check = datetime.now()

def check_system_health():
    """檢查系統健康狀態"""
    global last_health_check
    
    current_time = datetime.now()
    if (current_time - last_health_check).total_seconds() < health_check_interval:
        return True
    
    last_health_check = current_time
    
    try:
        # 檢查記憶體使用率
        memory = psutil.virtual_memory()
        if memory.percent > 90:
            logging.warning(f"記憶體使用率過高: {memory.percent}%")
            return False
        
        # 檢查 CPU 使用率
        cpu_percent = psutil.cpu_percent(interval=1)
        if cpu_percent > 95:
            logging.warning(f"CPU 使用率過高: {cpu_percent}%")
        
        # 檢查磁碟空間
        disk = psutil.disk_usage('C:' if platform.system() == "Windows" else '/')
        if disk.percent > 95:
            logging.warning(f"磁碟空間不足: {disk.percent}%")
            return False
        
        return True
    except Exception as e:
        logging.error(f"健康檢查失敗: {e}")
        return False

def check_record_table5_stuck():
    """檢查 record_table5.py 是否卡住"""
    global last_successful_time
    
    # 如果超過 5 分鐘沒有成功執行，認為可能卡住
    time_since_success = datetime.now() - last_successful_time
    if time_since_success.total_seconds() > 300:  # 5分鐘
        logging.error(f"record_table5.py 超過 {time_since_success} 未成功執行，可能卡住")
        return True
    return False

def kill_stuck_processes():
    """終止可能卡住的 record_table5.py 進程"""
    try:
        for process in psutil.process_iter(['pid', 'name', 'cmdline']):
            try:
                if (process.info['name'] == 'python.exe' and 
                    process.info['cmdline'] and 
                    any('record_table5.py' in arg for arg in process.info['cmdline'])):
                    
                    # 檢查進程運行時間
                    create_time = datetime.fromtimestamp(process.create_time())
                    if (datetime.now() - create_time).total_seconds() > 60:  # 超過1分鐘
                        logging.warning(f"終止卡住的 record_table5.py 進程 (PID: {process.info['pid']})")
                        process.kill()
                        time.sleep(2)  # 等待進程完全終止
                        
            except (psutil.NoSuchProcess, psutil.AccessDenied):
                continue
                
    except Exception as e:
        logging.error(f"清理卡住進程時發生錯誤: {e}")

def self_restart_if_needed():
    """自我重啟機制"""
    try:
        # 檢查自身運行時間，如果超過24小時則重啟
        current_process = psutil.Process(os.getpid())
        create_time = datetime.fromtimestamp(current_process.create_time())
        running_time = datetime.now() - create_time
        
        if running_time.total_seconds() > 86400:  # 24小時
            logging.warning("winloop.py 已運行超過24小時，準備重啟")
            # 啟動新實例
            subprocess.Popen([
                "python", __file__
            ], cwd=os.path.dirname(__file__))
            
            # 延遲5秒後退出當前實例
            time.sleep(5)
            exit(0)
            
    except Exception as e:
        logging.error(f"自我重啟檢查失敗: {e}")

def main():
    global last_successful_time
    
    execution_count = 0
    consecutive_failures = 0
    start_time = datetime.now()
    
    print("winloop.py 開始運行...")
    
    while True:
        try:
            execution_count += 1
            
            # 防止計數器過大，每 1,000,000 次重置
            if execution_count > 1000000:
                execution_count = 1
                start_time = datetime.now()
                print(f"{datetime.now().strftime('%H:%M:%S')} - 計數器已重置")
            
            # 新增：檢查是否有卡住的進程
            if check_record_table5_stuck():
                kill_stuck_processes()
                time.sleep(5)  # 等待清理完成
            
            # 新增：系統健康檢查
            if not check_system_health():
                logging.warning("系統健康檢查未通過，但繼續執行")
            
            # 偵測系統並執行
            if platform.system() == "Windows":
                result = subprocess.run(
                    ["python", "record_table5.py"], 
                    timeout=30,
                    capture_output=True,
                    text=True
                )
            else:
                result = subprocess.run(
                    ["python3", "record_table5.py"], 
                    timeout=30,
                    capture_output=True,
                    text=True
                )
            
            if result.returncode == 0:
                consecutive_failures = 0
                last_successful_time = datetime.now()  # 更新最後成功時間
                
                # 調整顯示頻率，避免過多輸出
                if execution_count % 60 == 0:  # 每 10 分鐘顯示一次
                    runtime = datetime.now() - start_time
                    print(f"{datetime.now().strftime('%H:%M:%S')} - 已成功執行 {execution_count} 次，運行時間: {runtime}")
            else:
                consecutive_failures += 1
                logging.warning(f"第 {execution_count} 次執行失敗 (連續失敗 {consecutive_failures} 次): {result.stderr}")
                
                # 新增：如果連續失敗超過3次，清理可能的卡住進程
                if consecutive_failures >= 3:
                    kill_stuck_processes()
                
        except subprocess.TimeoutExpired:
            consecutive_failures += 1
            logging.warning(f"第 {execution_count} 次執行超時 (連續失敗 {consecutive_failures} 次)")
            
            # 新增：超時時清理卡住的進程
            kill_stuck_processes()
            
        except Exception as e:
            consecutive_failures += 1
            logging.error(f"第 {execution_count} 次執行異常 (連續失敗 {consecutive_failures} 次): {e}")
        
        # 如果連續失敗太多次，記錄警告並採取行動
        if consecutive_failures >= 5:
            logging.error(f"連續失敗 {consecutive_failures} 次，系統可能有問題")
            kill_stuck_processes()  # 清理可能卡住的進程
            time.sleep(30)  # 延長等待時間
        elif consecutive_failures >= 10:
            logging.error("連續失敗次數過多，考慮自我重啟")
            self_restart_if_needed()
        
        # 新增：定期自我重啟檢查
        if execution_count % 360 == 0:  # 每小時檢查一次
            self_restart_if_needed()
        
        time.sleep(10)

if __name__ == "__main__":
    main()