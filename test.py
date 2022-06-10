#!/usr/bin/env python3
from pyfanuc import pyfanuc

conn = pyfanuc('192.168.1.52')
if conn.connect():
    # parts = conn.readmacro(3901)
    tool = conn.readmacro(first=11025,last=0)
    # tool = conn.readmacro(first=11001)
    # tool = conn.readparam(-1 , 10001,10002)
    # cycle = conn.readparam(-1 , 1240,1250)
    # print(parts)
    print(tool)
    # print(cycle)
    print("Verbunden")
    print("Lese SPS-Wert D2204 als 16 Bit")
    n = conn.readpmc(1, 9, 2204, 1)
    if n is not None:
        print("Leitwert %0.1f" % (n[2204] / 48))
    n = conn.readpmc(2, 9, 1870, 2)
    print(n)
    print("Lese SPS-Wert D1870 & 1874 als 32 Bit")
    if n is not None and n[1874]!=0:
        print("Laenge: %i von %i (%0.1f %%)" %
              (n[1870], n[1874], n[1870] / n[1874] * 100))
    # print("Lese Achsen")
    # for key, val in conn.readaxis(conn.ABS | conn.SKIP | conn.REL | conn.REF).items():
    #     print(key, val)
    print("exec Programm")
    print(conn.readprognum())
    # print("Lese Programm O1")
    # prog=conn.getprog("O1")
    # f=open('./O1','a+')
    # f.writelines(prog)
    # f.close
    print("machine status")
    print(conn.sysinfo)
    print(conn.statinfo)
    for key, val in conn.statinfo.items():
        print(key, val)

