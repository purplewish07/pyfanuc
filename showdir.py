#!/usr/bin/env python3
from pyfanuc import pyfanuc
import time
ip='192.168.1.52'
conn = pyfanuc(ip)

if conn.connect():
    print(ip+": connected")
    print('type:' + conn.sysinfo['cnctype'].decode() + 'i')
    print(conn.getstatinfo())
    # for t in conn.readdir_complete("//CNC_MEM/USER/PATH1/"):
    #     print(t)
    print("//CNC_MEM/USER/PATH1/")
    for n in conn.readdir_complete("//CNC_MEM/USER/PATH1/"):
        print(n['name']+" ("+time.strftime("%c",n['datetime'])+')' if n['type']=='F' else '<'+n['name']+'>')
    # print(conn.getdatetime())
    # print(conn.readaxis(pyfanuc.ABS | pyfanuc.DIST))
    # print(conn._req_rdsingle(1,1,0x8a))
if conn.disconnect():
    print("disconnected")