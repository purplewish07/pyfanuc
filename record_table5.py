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
volume = 13107 # 音量增益，範圍為 0 到 32768，預設為 32768 (100%)
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

def play_mp3(file_path, timeout=10): # volume=0~32768
    global volume
    """
    播放 MP3 文件，並調整音量。
    :param file_path: MP3 文件的路徑
    :param volume: 音量增益，範圍為 0 到 32768，預設為 32768 (100%)
    """
    try:
        if platform.system() == "Windows":
            # Windows 系統，使用 mpg123.exe 的完整路徑
            mpg123_path = r"D:\mpg123-1.32.10-x86-64\mpg123.exe"
            subprocess.run([mpg123_path, "-f", str(volume), file_path],
                          check=True, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE, timeout=timeout)
        else:
            # 非 Windows 系統 (假設為 WSL 或 Linux)
            subprocess.run(["mpg123", "-f", str(volume), file_path],
                          check=True, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE, timeout=timeout)
    
    except subprocess.TimeoutExpired:
        print(f"播放超時，自動跳過: {file_path}")
    except subprocess.CalledProcessError as e:
        print(f"播放失敗，錯誤：{e.stderr.decode()}")
    except FileNotFoundError:
        print("文件未找到或 mpg123 未安裝！")
    

def record(ip, q, i):
    st = time.time()
    stat = 9
    parts = None
    statinfo = None
    prog = None
    dwgno = None
    total = None
    ng = None
    tool_list = []
    ct = None
    flag = False

    def get_tool_list(data_list):
        """
        找到開頭為'N1 '但不是'(N1 '的項目，並回傳在它之前的所有項目
        """
        if not isinstance(data_list, list):
            return []
            
        for i, item in enumerate(data_list):
            # 檢查是否以'N1 '開頭但不以'(N1 '開頭
            if item.startswith('N1 ') and not item.startswith('(N1 '):
                result = data_list[2:i]  # 回傳在該項目之前的所有項目
                
                # 處理結果：移除包含'T-'的項目並格式化
                formatted_result = []
                for line in result:
                    # 檢查是否包含'T-'，如果包含則跳過
                    if 'T-' not in line:
                        # 分割並取[1:2]（即取第二個元素）
                        info = line.split('   ')
                        if len(info) > 1:
                            formatted_result.extend([info[1:3]])

                # 根據第一個元素（工具編號）排序
                try:
                    return sorted(formatted_result, key=lambda x: int(x[0][1:]))  # 排序時忽略'T'字元
                except (IndexError, ValueError):
                    return formatted_result

        # 如果沒找到符合條件的項目，回傳空列表
        return []
    
    def get_ct_formatted(ct):
        # 將ct轉換為HH:MM:SS格式
        if ct and 6757 in ct and 6758 in ct:
            try:
                seconds = int(ct[6757]['data'][0]//1000)  # 累計秒數
                minutes = int(ct[6758]['data'][0])  # 累計分鐘數
                
                # 計算總時間
                total_seconds = seconds + (minutes * 60)
                
                hours = total_seconds // 3600
                remaining_minutes = (total_seconds % 3600) // 60
                remaining_seconds = total_seconds % 60
                
                ct_formatted = f"{hours:02d}:{remaining_minutes:02d}:{remaining_seconds:02d}"
                return ct_formatted
            except (KeyError, TypeError, ValueError):
                return None
        else:
            return None

    try:
        conn = pyfanuc(str(ip))
        if conn.connect():
            statinfo = conn.statinfo
            y = conn.readpmc(0, 2, 27, 3)
            
            # 檢測燈光狀態
            if (y.get(29) & 1):  # 綠燈
                stat = 3
            elif (y.get(27) & 8):  # 黃燈
                stat = 1
            elif (y.get(27) & 4):  # 紅燈
                stat = 4
            else:
                stat = 0  # 沒有燈光
            
            parts = conn.readmacro(3901).get(3901)
            total_raw = conn.readmacro(12399).get(12399)
            ng_raw = conn.readmacro(12400).get(12400)
            total = int(total_raw) if total_raw is not None else None
            ng = int(ng_raw) if ng_raw is not None else None
            ct = get_ct_formatted(conn.readparam2(-1, 6757, 6758))
            
            # 將數字轉成文字並前置填0湊滿4字元
            prog = "O" + str(conn.readprognum()['main']).zfill(4)
            
            # 修正: 檢查 getproghead 的返回值類型
            try:
                prog_head = conn.getproghead(prog, 3500)
                if isinstance(prog_head, str):
                    file = prog_head.split('\n')  # 程式內容
                    dwgno = file[1].replace(prog, '').strip('() ') if len(file) > 1 else "unknown"
                    tool_list = get_tool_list(file)
                else:
                    # 如果返回的不是字串（可能是錯誤碼），使用預設值
                    print(f"警告: getproghead 返回非字串值: {prog_head}")
                    dwgno = "unknown"
                    tool_list = []
            except Exception as e:
                print(f"警告: 讀取程式內容失敗 {ip}: {e}")
                dwgno = "unknown"
                tool_list = []
            
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
    except Exception as e:
        # 捕獲所有其他異常
        flag = False
        log = f"Unexpected error: {e}"
        print('(' + str(ip) + ')' + log)
        errlog.append(['(' + str(ip) + ')' + log])

    finally:
        try:
            conn.disconnect()
        except:
            pass

    end = time.time()
    t = round(end - st, 3)
    print(i + 1, 'processed(' + str(ip) + '):', t, stat, parts)
    print(statinfo)
    
    # 修正: 確保 q[i] 總是有完整的資料結構，即使發生錯誤
    if flag:
        arr = [str(ip), stat, t, parts, prog, dwgno, total, ng, ct, tool_list]
    else:
        # 發生錯誤時，填入預設值以保持資料結構完整
        arr = [str(ip), stat, t, None, None, None, None, None, None, []]

    q[i] = arr
    


# end def------------------------------------------------------------
allst = time.time()
threads = []
ip1 = '192.168.1.168'
ip2 = '192.168.1.169'
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
realtimels = []
for i in range(n):
    if not(i + 1 in band):
        ls.append(['CNC' + str(i + 1).zfill(2), q[i][0], q[i][1], q[i][3], q[i][4], q[i][5], q[i][6], q[i][7],q[i][8]])
        realtimels.append(['CNC' + str(i + 1).zfill(2), q[i][8], q[i][9]])
newdf = pd.DataFrame(ls, columns=['name', 'ip', 'status', 'parts', 'prog', 'dwgno', 'total', 'ng', 'ct'])
newdf['status'] = newdf['status'].apply(
    lambda x: pd.to_numeric(x, errors='coerce'))
newdf.dropna(subset=['status'], inplace=True)
newdf['status'] = newdf['status'].apply(lambda x: 3 if x == 2 else x)
parts_df = newdf[['name', 'parts']]
newdf = newdf.set_index('name')
#newdf.drop(['parts'], axis=1, inplace=True)

# database setting
# 172.26.160.1   win11-host for wsl
table = "machines_rawdata"
realtime_table = "machines_realtime"
futuretime_table = "machines_futuretime"
engine = sqla.create_engine(
    'mysql+pymysql://usr:usr@DESKTOP-8GND4HG:3306/mes',
    connect_args={"connect_timeout": 30})
# print(engine)

# 讀取安燈啟用設定
try:
    andon_config = pd.read_sql_query("SELECT machine_name, andon_enabled FROM machines_andon WHERE andon_enabled = 1", engine)
    andon_enabled_machines = set(andon_config['machine_name'].tolist())
    print(f"已啟用安燈的機台: {andon_enabled_machines}")
except Exception as e:
    print(f"讀取安燈設定失敗: {e}, 預設全部啟用")
    andon_enabled_machines = None

# 檢查機台狀態並播放音效
for name, row in newdf.iterrows():
    if row['status'] != 3 and row['status'] != 9:
        # 檢查該機台是否啟用安燈
        if andon_enabled_machines is not None and name not in andon_enabled_machines:
            print(f"{name} 安燈已停用，跳過播放音效")
            continue
        
        # 獲取當前時間
        current_time = datetime.now().time()
        
        # 檢查是否在 12:00 至 13:00 之間（午休時段）
        if current_time >= datetime.strptime("12:00", "%H:%M").time() and current_time < datetime.strptime("13:00", "%H:%M").time():
            print(f"當前時間 {current_time} {name}已停機: 在 12:00~13:00 範圍內，跳過播放音效。")
            continue
        
        # 播放音樂檔案
        filename = fr".\sound\{name}已停機.mp3"
        print(f"播放音樂檔案: {filename}")
        play_mp3(filename)
        andon += 0
        time.sleep(5)

#update to db machines_realtime
realtimedf = newdf.copy()
realtimedf.reset_index(inplace=True)
updatetime = datetime.now()
realtimedf.loc[:,'datetime'] = updatetime


# 檢查並處理 total 和 ng 欄位為 null 的情況
# 如果 total 或 ng 為 null，則從資料庫中讀取現有值來保留
try:
    # 讀取現有的 realtime 資料
    existing_realtime = pd.read_sql_query(f"SELECT name, total, ng FROM {realtime_table}", engine)
    existing_realtime = existing_realtime.set_index('name')
    
    # 對於 total 和 ng 為 null 的情況，使用現有值
    for idx, row in realtimedf.iterrows():
        name = row['name']
        if pd.isna(row['total']) and name in existing_realtime.index:
            if not pd.isna(existing_realtime.loc[name, 'total']):
                realtimedf.loc[idx, 'total'] = existing_realtime.loc[name, 'total']
        
        if pd.isna(row['ng']) and name in existing_realtime.index:
            if not pd.isna(existing_realtime.loc[name, 'ng']):
                realtimedf.loc[idx, 'ng'] = existing_realtime.loc[name, 'ng']
                
except Exception as e:
    print(f"讀取現有 realtime 資料時發生錯誤: {e}")
    # 如果讀取失敗，保持原始邏輯

# 調整欄位順序，將 'datetime' 放在 'name' 之前
columns_order = ['datetime', 'name'] + [col for col in realtimedf.columns if col not in ['datetime', 'name']]
realtimedf = realtimedf[columns_order]

# # 建立 ct_df DataFrame - 從 realtimels 提取 name 和 ct
# ct_data = []
# for item in realtimels:
#     if len(item) >= 2:  # 確保有足夠的元素
#         name = item[0]    # 機台名稱
#         ct = item[1]      # CT 值
#         ct_data.append([name, ct])

# ct_df = pd.DataFrame(ct_data, columns=['name', 'ct'])

# # 根據 name 合併 realtimedf 和 ct_df
# realtimedf = realtimedf.merge(ct_df, on='name', how='left')

dtype = {'ct': sqla.types.TIME}

print("realtime_table")
print(realtimedf)
# 將資料儲存到 machines_realtime 表
realtimedf.to_sql(realtime_table, engine, if_exists='replace', index=True, dtype=dtype)

# 建立 tool_df，將 realtimels[0] 和 realtimels[2] 進行扁平化
toolhouse_data = []
for item in realtimels:
    if len(item) >= 3:  # 確保有足夠的元素
        machine_name = item[0]  # realtimels[0] - 機台名稱
        tool_list = item[2]     # realtimels[2] - 工具清單
        
        # 扁平化 tool_list，並過濾 null/None/NaN 資料
        if tool_list and isinstance(tool_list, list):
            for tool_info in tool_list:
                if isinstance(tool_info, list) and len(tool_info) >= 2:
                    tool_number = tool_info[0]  # 工具編號 (如 T1, T2...)
                    tool_name = tool_info[1]    # 工具名稱 (如 FEM63., EM6....)
                    
                    # 過濾 null, None, NaN 資料
                    if (machine_name and pd.notna(machine_name) and 
                        tool_number and pd.notna(tool_number) and 
                        tool_name and pd.notna(tool_name)):
                        toolhouse_data.append({
                            'machine': str(machine_name).strip(),
                            'toolno': str(tool_number).strip(),
                            'toolname': str(tool_name).strip(),
                            'datetime': updatetime
                        })

if toolhouse_data:
    new_toolhouse_df = pd.DataFrame(toolhouse_data)
    
    # 讀取現有的 toolhouse 資料進行比較
    try:
        # 取得每個機台每個工具的最新資料 - 你的 SQL 語法是正確的
        toolhouse_sql = """
        SELECT t1.*
        FROM toolhouse t1
        INNER JOIN (
            SELECT machine, toolno, MAX(datetime) AS max_datetime
            FROM toolhouse
            GROUP BY machine, toolno
        ) t2
        ON t1.machine = t2.machine AND t1.toolno = t2.toolno AND t1.datetime = t2.max_datetime
        """
        
        existing_toolhouse = pd.read_sql_query(toolhouse_sql, engine)
        
        # 比較新舊工具資料，只保留 toolname 有變化的記錄
        updated_tools = []
        for _, new_row in new_toolhouse_df.iterrows():
            machine = new_row['machine']
            toolno = new_row['toolno']
            new_toolname = new_row['toolname']
            
            # 查找對應的舊記錄
            matching_old = existing_toolhouse[
                (existing_toolhouse['machine'] == machine) & 
                (existing_toolhouse['toolno'] == toolno)
            ]
            
            if matching_old.empty:
                # 如果是新工具，直接加入
                updated_tools.append(new_row.to_dict())
            else:
                # 如果工具名稱有變化，才加入
                old_toolname = matching_old.iloc[0]['toolname']
                if new_toolname != old_toolname:
                    updated_tools.append(new_row.to_dict())
        
        # 只保留有變化的工具資料
        if updated_tools:
            updated_toolhouse_df = pd.DataFrame(updated_tools)
            updated_toolhouse_df.to_sql('toolhouse', engine, if_exists='append', index=False)
            print(f"工具名稱有變化的記錄: {len(updated_tools)} 筆")
            print(f"已追加寫入 toolhouse 表，共 {len(updated_tools)} 筆工具資料")
            
            # 顯示變化詳情
            for tool in updated_tools:
                print(f"機台: {tool['machine']}, 工具: {tool['toolno']}, 新名稱: {tool['toolname']}")
        else:
            print("所有工具名稱無變化，跳過寫入 toolhouse")
            
    except Exception as e:
        # 如果是第一次執行或查詢失敗，直接寫入所有資料
        print(f"讀取現有 toolhouse 資料時發生錯誤: {e}")
        new_toolhouse_df.to_sql('toolhouse', engine, if_exists='append', index=False)
        print(f"已追加寫入 toolhouse 表，共 {len(toolhouse_data)} 筆工具資料")
else:
    print("無工具資料需要寫入 toolhouse 表")


# compare status
#sql = "Select * from " + table
sql = """
SELECT t1.*
FROM machines_rawdata t1
INNER JOIN (
    SELECT name, MAX(datetime) AS max_datetime
    FROM machines_rawdata
    GROUP BY name
) t2
ON t1.name = t2.name AND t1.datetime = t2.max_datetime
"""

# 確保 predf 的索引與 newdf 一致
predf = pd.read_sql_query(sql, engine)
predf = predf.set_index('name')

# 格式化 ct 欄位（如果資料庫讀取的是 timedelta）
if 'ct' in predf.columns:
    predf['ct'] = predf['ct'].apply(lambda x: str(x).replace('0 days ', '') if pd.notna(x) else None)

# 比較 newdf 和 predf，僅保留有更新的資料
updateddf = newdf[
    (newdf['status'] != predf['status']) |  # 比較 status 是否不同
    (
        (newdf['parts'].notna() & predf['parts'].notna() & (newdf['parts'] != predf['parts'])) |  # 兩者都不為 NaN 且值不同
        (newdf['parts'].isna() & predf['parts'].notna()) |  # newdf 為 NaN，predf 不為 NaN
        (newdf['parts'].notna() & predf['parts'].isna())    # newdf 不為 NaN，predf 為 NaN
    ) |
    (
        (newdf['prog'].notna() & predf['prog'].notna() & (newdf['prog'] != predf['prog'])) |
        (newdf['prog'].isna() & predf['prog'].notna()) |
        (newdf['prog'].notna() & predf['prog'].isna())
    ) |
    (
        (newdf['dwgno'].notna() & predf['dwgno'].notna() & (newdf['dwgno'] != predf['dwgno'])) |
        (newdf['dwgno'].isna() & predf['dwgno'].notna()) |
        (newdf['dwgno'].notna() & predf['dwgno'].isna())
    ) |
    (
        (newdf['total'].notna() & predf['total'].notna() & (newdf['total'] != predf['total'])) |
        (newdf['total'].isna() & predf['total'].notna()) |
        (newdf['total'].notna() & predf['total'].isna())
    ) |
    (
        (newdf['ng'].notna() & predf['ng'].notna() & (newdf['ng'] != predf['ng'])) |
        (newdf['ng'].isna() & predf['ng'].notna()) |
        (newdf['ng'].notna() & predf['ng'].isna())
    )
]

# 如果有更新的資料，插入資料庫
if not updateddf.empty:
    updateddf = updateddf.reset_index()  # 重置索引，確保 name 成為普通欄位
    updateddf.loc[:, 'datetime'] = updatetime  # 新增 datetime 欄位
    print('insert------------------------------------------')
    print(updateddf)
    updateddf.to_sql(table, engine, if_exists='append', index=False)
    print('--------------------------------------------------------------------------------------------------------')
    print('已新增資料,共 ' + str(updateddf.shape[0]) + '筆!')
    print('--------------------------------------------------------------------------------------------------------')
else:
    print('--------------------------------------------------------------------------------------------------------')
    print('無資料更新!')
    print('--------------------------------------------------------------------------------------------------------')

futuretime = datetime.now()
futuredf = predf.copy()
futuredf.reset_index(inplace=True)
futuredf.loc[:, 'datetime'] = futuretime  # 新增 datetime 欄位
futuredf['status'] = 885
#.to_sql(table, engine, if_exists='append', index=False)
print("futuredf")
print(futuredf)
futuredf.to_sql(futuretime_table, engine, if_exists='replace', index=True, dtype=dtype)


alled = time.time()
# 列印結果
print("It cost %f sec" % (alled - allst))  # 會自動做近位
