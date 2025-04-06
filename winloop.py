import time
import subprocess
import platform

while True:
    # 偵測系統
    if platform.system() == "Windows":
        # Windows 系統
        subprocess.run(["python", "record_table5.py"])
    else:
        # 非 Windows 系統 (假設為 WSL 或 Linux)
        subprocess.run(["python3", "record_table5.py"])
    
    time.sleep(10)  # 暫停 10 秒