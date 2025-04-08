#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# -------------------------------------------------------------------
# Date: 2020/12/14
# Author:   Shaun
# 檔案功能描述:
#   自動抓取機台狀態，更新資料庫，配合bash腳本，寫入系統排程crontab -e
# -------------------------------------------------------------------
from pyfanuc import pyfanuc
import time
import ipaddress
import threading
import pandas as pd
import sqlalchemy as sqla
from datetime import datetime
import platform
import subprocess

andon = 0
errlog=[]

# def play_mp3_with_mpg123(file_path):
#     # 確保 mpg123.exe 已在環境變數內，或指定完整路徑
#     mpg123_path = r"D:\mpg123-1.32.10-x86-64\mpg123.exe"
#     try:
#         # 執行 mpg123 播放音樂
#         subprocess.run([mpg123_path, file_path], check=True)
#     except subprocess.CalledProcessError as e:
#         print(f"播放失敗，錯誤：{e}")
# #WSL
# def play_mp3(file_path):
#     try:
#         # 播放位於專案目錄中的 sound 資料夾內的 MP3 文件
#         subprocess.run(["mpg123", file_path], check=True)
#     except subprocess.CalledProcessError as e:
#         print(f"播放失敗，錯誤：{e}")
#     except FileNotFoundError:
#         print("文件未找到或 mpg123 未安裝！")

def play_mp3(file_path, volume=0):
    """
    播放 MP3 文件，並調整音量。
    :param file_path: MP3 文件的路徑
    :param volume: 音量增益，範圍為 0 到 32768，預設為 32768 (100%)
    """
    try:
        if platform.system() == "Windows":
            # Windows 系統，使用 mpg123.exe 的完整路徑
            mpg123_path = r"D:\mpg123-1.32.10-x86-64\mpg123.exe"
            subprocess.run([mpg123_path, "-f", str(volume), file_path], check=True, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
        else:
            # 非 Windows 系統 (假設為 WSL 或 Linux)
            subprocess.run(["mpg123", "-f", str(volume), file_path], check=True, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
    except subprocess.CalledProcessError as e:
        print(f"播放失敗，錯誤：{e.stderr.decode()}")
    except FileNotFoundError:
        print("文件未找到或 mpg123 未安裝！")
    

def record(ip, q, i):
    st = time.time()
    stat=9
    parts = None
    statinfo = None
    try:
        conn = pyfanuc(str(ip))
        if conn.connect():
            stat = conn.statinfo['run']
            statinfo = conn.statinfo
            # print(ip,'|type:' + conn.sysinfo['cnctype'].decode() + 'i','|run_status: ' ,stat)
            parts = conn.readmacro(3901).get(3901)
            flag = True
        else:
            conn.disconnect()
            log = 'socket_timeout!'
            print('(' + str(ip) + ')' + log)
            flag = False

    except OSError as err:
        flag = False
        global errlog
        if str(err) == 'timed out':
            print(err)
            log = 'socket_timeout!'
            print('(' + str(ip) + ')' + log)
            errlog.append(['(' + str(ip) + ')' + log])
        elif str(err).startswith('[Errno 111]'):
            log = "OS error: {0}".format(err)
            print('(' + str(ip) + ')' + log)
            errlog.append(['(' + str(ip) + ')' + log])
            log = 4
        else:
            log = "OS error: {0}".format(err)
            print('(' + str(ip) + ')' + log)
            errlog.append(['(' + str(ip) + ')' + log])
            log = 999

    finally:
        conn.disconnect()

    end = time.time()
    t = round(end - st, 3)
    print(i + 1, 'prossed(' + str(ip) + '):', t, stat, parts)
    print(statinfo)
    # if(flag):
    #     arr = [str(ip), stat, t, parts]
    # else:
    #     arr = [str(ip), log, t, parts]
    arr= [str(ip), stat, t, parts]
    q[i] = arr
    


# end def------------------------------------------------------------
allst = time.time()
threads = []
ip1 = '192.168.1.168'
ip2 = '192.168.1.170'
band = [4, 5, 24, 27, 28, 29, 36, 43, 44, 45, 46, 66, 67,
        70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89]
stip = ipaddress.ip_address(ip1)
edip = ipaddress.ip_address(ip2)
n = int(edip) - int(stip) + 1
q = [[] for _ in range(n)]
print('from', stip, 'to', edip, 'seq', n)
print('------------------------------------------')
for i in range(n):
    ip = stip + i
    threads.append(threading.Thread(target=record, args=(ip, q, i)))
    if not(i + 1 in band):
        threads[i].start()
    # else:
    #     q[i]=[str(ip),'not support',0]

for i in range(n):
    if not(i + 1 in band):
        threads[i].join()
ls = []
for i in range(n):
    if not(i + 1 in band):
        ls.append(['CNC' + str(i + 1).zfill(2), q[i][0], q[i][1], q[i][3]])
newdf = pd.DataFrame(ls, columns=['name', 'ip', 'status', 'parts'])
newdf['status'] = newdf['status'].apply(
    lambda x: pd.to_numeric(x, errors='coerce'))
newdf.dropna(subset=['status'], inplace=True)
newdf['status'] = newdf['status'].apply(lambda x: 3 if x == 2 else x)
parts_df = newdf[['name', 'parts']]
newdf = newdf.set_index('name')
#newdf.drop(['parts'], axis=1, inplace=True)

# 使用絕對路徑，指向 sound 資料夾內的 MP3 文件
#play_mp3("/home/que/github_repo/pyfanuc/sound/CNC01stop.mp3")
for name, row in newdf.iterrows():
     # 獲取當前時間
    current_time = datetime.now().time()
    

    if row['status'] != 3 and row['status'] != 9:
        # 播放音樂檔案
        # 檢查是否在 12:00 至 13:00 之間
        if current_time >= datetime.strptime("12:00", "%H:%M").time() and current_time < datetime.strptime("13:00", "%H:%M").time():
            print(f"當前時間 {current_time} {name}已停機: 在 12:00~13:00 範圍內，跳過播放音效。")
            continue
        filename = fr".\sound\{name}已停機.mp3"
        print(f"播放音樂檔案: {filename}")
        play_mp3(filename)
        andon += 0
        time.sleep(5)

# database setting
# 172.26.160.1   win11-host for wsl
table = "machines_rawdata"
realtime_table = "machines_realtime"
engine = sqla.create_engine(
    'mysql+pymysql://usr:usr@DESKTOP-8GND4HG:3306/mes',
    connect_args={"connect_timeout": 30})
# print(engine)

#update to db machines_realtime
realtimedf = newdf.copy()
realtimedf.reset_index(inplace=True)
updatetime = datetime.now()
realtimedf['datetime'] = updatetime
# 調整欄位順序，將 'datetime' 放在 'name' 之前
columns_order = ['datetime', 'name'] + [col for col in realtimedf.columns if col not in ['datetime', 'name']]
realtimedf = realtimedf[columns_order]

print("realtime_table")
print(realtimedf)
# 將資料儲存到 machines_realtime 表
realtimedf.to_sql(realtime_table, engine, if_exists='replace', index=True)

# compare status
sql = "Select * from " + table
predf = pd.read_sql_query(sql, engine)
preid = predf[['id', 'name']].groupby(['name']).max().reset_index()
preid.drop(['name'], axis=1, inplace=True)
print(preid.shape[0])
if preid.shape[0] != 0:
    predf = pd.merge(predf, preid, how='inner')
    predf.drop(['id', 'datetime', 'parts'], axis=1, inplace=True)
    predf = predf.set_index(['name']).sort_index()

    print('pre------------------------------------------')
    print(predf)
    print('new------------------------------------------')
    print(newdf)
    newdf = newdf[predf.ne(newdf).any(axis=1)]

# global errlog
# f=open('errlog','a+')
# f.writelines(str(datetime.now())+'\n')
# for e in errlog:
#     f.writelines(str(e)+'\n')
# f.close()
# 新增至資料庫
newdf = newdf.reset_index()
newdf = pd.merge(newdf, parts_df, left_on='name', right_on='name', how='inner')
newdf['datetime'] = updatetime
print('insert------------------------------------------')
print(newdf)
newdf.to_sql(table, engine, if_exists='append', index=False)

print('--------------------------------------------------------------------------------------------------------')
print('已新增資料,共 ' + str(newdf.shape[0]) + '筆!')
print('--------------------------------------------------------------------------------------------------------')
alled = time.time()
# 列印結果
print("It cost %f sec" % (alled - allst))  # 會自動做近位
