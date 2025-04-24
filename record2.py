#!/usr/bin/env python3
from pyfanuc import pyfanuc
import time
ip = '192.168.1.169'
conn = pyfanuc(ip)
st = time.time()
i = 0
try:
    conn = pyfanuc(str(ip))
    if conn.connect():
        print(conn.sysinfo)
        stat = conn.statinfo['run']
        # print(ip,'|type:' + conn.sysinfo['cnctype'].decode() + 'i','|run_status: ' ,stat)
        # if stat ==0:
        # parts = conn.readmacro(3901)
        # cycle = conn.readparam2(-1, 1861,1869)
        # cycle = conn.readparam2(0, 6711)
        # cycle = conn.readparam2(-1 , 6700,6750)
        cycle = conn.readdiag(-1,4920)
        # cycle = conn.readparam2(-1 , 5312)
        # n = conn.readpmc(2, 9, 99, 2)  # OK

        # print(parts)
        # print(parts.get(3901))
        print(cycle)
        # print(n)
        # conn.cnc_rdparam()
        print("SHOW //CNC_MEM/USER/PATH1/")
        data1 = conn.readdir_complete("//CNC_MEM/USER/PATH1/")
        # print(data1)
        for n in data1:
            print(n['name']+" ("+time.strftime("%c",n['datetime'])+')' if n['type']=='F' else '<'+n['name']+'>')
        data2 = conn.getprog("O3000")
        print("\nGET PROGRAM O3000")
        print(data2)
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
print(i + 1, 'prossed(' + str(ip) + '):', t)

if(flag):
    arr = [str(ip), stat, t]
else:
    arr = [str(ip), log, t]

print(arr)
