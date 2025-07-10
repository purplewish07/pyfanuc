#!/usr/bin/env python3
from pyfanuc import pyfanuc
import time
ip = '192.168.1.168'
conn = pyfanuc(ip)
st = time.time()
i = 0
stat=9
statinfo = None
try:
    conn = pyfanuc(str(ip))
    if conn.connect():
        #print(conn.sysinfo)
        stat = conn.statinfo['run']
        statinfo = conn.statinfo
        # print(ip,'|type:' + conn.sysinfo['cnctype'].decode() + 'i','|run_status: ' ,stat)
        # if stat ==0:
        # parts = conn.readmacro(3901)
        # cycle = conn.readparam2(-1, 1861,1869)
        # cycle = conn.readparam2(0, 6711)
        # cycle = conn.readparam2(-1 , 6700,6750)
        #cycle = conn.readdiag(-1,4920)
        # cycle = conn.readparam2(-1 , 5312)
        # n = conn.readpmc(2, 9, 99, 2)  # OK

        # print(parts)
        # print(parts.get(3901))
        # print(cycle)
        # print(n)
        # conn.cnc_rdparam()
        #print("SHOW //CNC_MEM/USER/PATH1/")
        #data1 = conn.readdir_complete("//CNC_MEM/USER/PATH1/")
        # print(data1)
        # for n in data1:
        #     print(n['name']+" ("+time.strftime("%c",n['datetime'])+')' if n['type']=='F' else '<'+n['name']+'>')
        # data2 = conn.getprog("O3000")
        # print("\nGET PROGRAM O3000")
        # print(data2)

        #程式啟動 手動燈 Y29.1
        #異警 紅燈 Y27.2
        #完成 黃燈 Y27.3

        y= conn.readpmc(0, 2, 27, 3)
        print(y)
        # 檢測燈光狀態
        if (y.get(29) & 1):  # 綠燈
            stat = 3
        elif (y.get(27) & 8):  # 黃燈
            stat = 1
        elif (y.get(27) & 4):  # 紅燈
            stat = 4
        else:
            stat = 0  # 沒有燈光
        print(f"燈光狀態: {stat}")


        alarm = conn.readalarmcode(2,0)
        print(alarm)
        flag = True
    else:
        # conn.disconnect()
        log = 'socket_timeout!'
        print('(' + str(ip) + ')' + log)
        flag = False

except OSError as err:
    flag = False
    if str(err) == 'timed out':
        print(err)
        log = 'socket_timeout!'
        print('(' + str(ip) + ')' + log)
        # err='(' + str(ip) + ')' + log
    else:
        log = "OS error: {0}".format(err)
        print('(' + str(ip) + ')' + log)
        # err='(' + str(ip) + ')' + log
        log = 999

finally:
    conn.disconnect()

end = time.time()
t = round(end - st, 3)
# print(i + 1, 'prossed(' + str(ip) + '):', t)
print(i + 1, 'prossed(' + str(ip) + '):', t, stat)
print(statinfo)

# if(flag):
#     arr = [str(ip), stat, t]
# else:
#     arr = [str(ip), log, t]
arr= [str(ip), stat, t]
print(arr)
