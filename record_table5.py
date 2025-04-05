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
errlog=[]

def record(ip, q, i):
    st = time.time()
    parts = None
    try:
        conn = pyfanuc(str(ip))
        if conn.connect():
            stat = conn.statinfo['run']
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
    print(i + 1, 'prossed(' + str(ip) + '):', t, parts)

    if(flag):
        arr = [str(ip), stat, t, parts]
    else:
        arr = [str(ip), log, t, parts]
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
newdf.drop(['parts'], axis=1, inplace=True)


# database setting
# 172.26.160.1   win11-host for wsl
table = "machines_rawdata"
sql = "Select name,datetime,ip,status from " + table
engine = sqla.create_engine(
    'mysql+mysqlconnector://usr:usr@172.26.160.1:3306/mes',
    connect_args={"connect_timeout": 30})
# print(engine)

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
f=open('errlog','a+')
f.writelines(str(datetime.now())+'\n')
for e in errlog:
    f.writelines(str(e)+'\n')
f.close()
# 新增至資料庫
newdf = newdf.reset_index()
newdf = pd.merge(newdf, parts_df, left_on='name', right_on='name', how='inner')
newdf['datetime'] = datetime.now()
print('insert------------------------------------------')
print(newdf)
newdf.to_sql(table, engine, if_exists='append', index=False)

print('--------------------------------------------------------------------------------------------------------')
print('已新增資料,共 ' + str(newdf.shape[0]) + '筆!')
print('--------------------------------------------------------------------------------------------------------')
alled = time.time()
# 列印結果
print("It cost %f sec" % (alled - allst))  # 會自動做近位
