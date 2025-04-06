import time
import subprocess

while True:
    subprocess.run(["python3", "record_table5.py"])
    time.sleep(10)  # 暫停 10 秒