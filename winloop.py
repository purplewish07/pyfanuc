import time
import subprocess
import platform

while True:
    try:
        # 偵測系統
        if platform.system() == "Windows":
            # Windows 系統
            subprocess.run(["python", "record_table5.py"], timeout=30)
        else:
            # 非 Windows 系統 (假設為 WSL 或 Linux)
            subprocess.run(["python3", "record_table5.py"], timeout=30)
    except subprocess.TimeoutExpired:
        print("record_table5.py 執行超時，自動跳過本次循環。")
    except Exception as e:
        print(f"record_table5.py 執行異常: {e}")
    time.sleep(10)  # 暫停 10 秒