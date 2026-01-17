#!/usr/bin/env python3
from pyfanuc import pyfanuc
import time
ip = '192.168.1.201'
conn = pyfanuc(ip)
st = time.time()
i = 0
stat=9
statinfo = None
def get_tool_list(data_list):
    """
    找到開頭為'N1 '但不是'(N1 '的項目，並回傳在它之前的所有項目
    """
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
            return sorted(formatted_result, key=lambda x: int(x[0][1:]))  # 排序時忽略'T'字元

    # 如果沒找到符合條件的項目，回傳整個列表
    return []

try:
    conn = pyfanuc(str(ip))
    if conn.connect():
        #print(conn.sysinfo)
        stat = conn.statinfo['run']
        statinfo = conn.statinfo
        print(f"機台狀態代碼: {stat}")
        print(f"機台狀態資訊: {statinfo}")
        # print(ip,'|type:' + conn.sysinfo['cnctype'].decode() + 'i','|run_status: ' ,stat)
        # if stat ==0:
        f = conn.readpmc(0, 1, 0, 1)
        if not (f.get(0) & 32):
            stl =0
        else:
            stl =1
        print(f"STL: {stl}")
        parts = conn.readmacro(3901)
        total,ng = conn.readmacro(12399,12400)
        # ng = conn.readmacro(12400)
        print(parts)
        print(total)
        print(ng)
        pn = conn.readmacro(4114)
        n = conn.readmacro(4314)
        p = conn.readmacro(4315)
        pp = conn.readmacro(4115)
        t = conn.readmacro(4320)
        pt = conn.readmacro(4120)
        print(pn, n, pp, p, pt, t)
        print(conn.readmacro(600, 630))
        # ct = conn.readmacro(6757,6758)

        prun = conn.readmacro(3008)
        print("prun:", prun)
        ct = conn.readparam2(-1, 6757, 6758)
        # 將ct轉換為HH:MM:SS格式
        if ct and 6757 in ct and 6758 in ct:
            seconds = int(ct[6757]['data'][0]//1000)  # 累計秒數
            minutes = int(ct[6758]['data'][0])  # 累計分鐘數
            
            # 計算總時間
            total_seconds = seconds + (minutes * 60)
            
            hours = total_seconds // 3600
            remaining_minutes = (total_seconds % 3600) // 60
            remaining_seconds = total_seconds % 60
            
            ct_formatted = f"{hours:02d}:{remaining_minutes:02d}:{remaining_seconds:02d}"
            print(f"CT: {ct_formatted}")
        else:
            print("CT: 無資料")
        print(ct)
        # 將ct轉換為HH:MM:SS格式
        # if ct and 6757 in ct and 6758 in ct:
        #     seconds = ct[6757]
        #     hours = int(seconds / 3600)
        #     minutes = int((seconds % 3600) / 60)
        #     seconds = int(seconds % 60)
        #     ct_formatted = f"{hours:02d}:{minutes:02d}:{seconds:02d}"
        #     print(f"CT: {ct_formatted}")
        # else:
        #     print("CT: 無資料")
        # cycle = conn.readparam2(-1, 1861,1869)
        # cycle = conn.readparam2(0, 6711)
        # cycle = conn.readparam2(-1 , 6700,6750)
        # cycle = conn.readdiag(-1,4920)
        # cycle = conn.readparam2(-1 , 5312)
        # n = conn.readpmc(2, 9, 99, 2)  # OK


        # spindle_speed = conn.readactspindlespeed()
        # print(spindle_speed)
        # spindle_load = conn.readactspindleload()
        # print(spindle_load)
        # position = conn.readaxes()
        # print(position)

        
        # print(parts.get(3901))
        # print(cycle)
        # print(n)
        # conn.cnc_rdparam()
        # print("SHOW dir")
        # data1 = conn.readdir_complete("//MEMCARD/")
        # print(data1)

        # print("SHOW //CNC_MEM/USER/PATH1/")
        # data1 = conn.readdir_complete("//CNC_MEM/USER/PATH1/")
        # print(data1)
        # for n in data1:
        #     print(n['name']+" ("+time.strftime("%c",n['datetime'])+ "|size:"+str(n['size'])+')'  if n['type']=='F' else '<'+n['name']+'>')

        # pname = "O0011"
        # data2 = conn.getprog(pname)
        # print("\nGET PROGRAM", pname)
        # print(data2)
        # with open(pname + ".nc", "w", encoding="utf-8") as f:
        #     f.write(data2)
        # print(f"已儲存程式至 {pname}.nc")

        # conn.uploadprog(fullpath="//CNC_MEM/USER/PATH1/", content=data2)    
        # print(f"已上傳程式至 //CNC_MEM/USER/PATH1/{pname}")

        # conn.deleteprog(f"//CNC_MEM/USER/PATH1/{pname}")
        # print(f"已刪除程式 //CNC_MEM/USER/PATH1/{pname}")

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


        # alarm = conn.readalarmcode(2,0)
        # print(alarm)
        #| readexecprog	| execute linecode |
        #| readprognum | actual main/run program |
        # print(conn.readexecprog(1000)) #當前執行code
        #print(conn.readexecprog(100)['text'].split('\n')[0])
        print(conn.readprognum())
        # exec_prog_num = conn.readprognum() or {}
        # 將數字轉成文字並前置填0湊滿4字元
        # exec_prog_str = "O" + str(exec_prog_num.get('main')).zfill(4)
        exec_prog_str = "O" + str(conn.readprognum()['main']).zfill(4)
        #print(exec_prog_str)
        #print(conn.readprogname())
        file = conn.getproghead(exec_prog_str,3500).split('\n') #程式內容
        dwgno = file[1].replace(exec_prog_str,'').strip('() ')
        tool_list = get_tool_list(file)
        print(dwgno)
        for tool in tool_list:
            print(tool)

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
