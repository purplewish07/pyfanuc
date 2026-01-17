# CsFanuc vs pyfanuc 實作狀態比較

## 核心方法對比

| Python 方法 | C# 方法 | 狀態 | 備註 |
|------------|---------|------|------|
| `__init__(ip, port)` | `CsFanuc(ip, port)` | ✅ | 建構函式 |
| `connect()` | `Connect()` | ✅ | 連接控制器 |
| `disconnect()` | `Disconnect()` | ✅ | 斷開連接 |
| `getsysinfo()` | `GetSysInfo()` | ✅ | 獲取系統資訊 |
| `getstatinfo()` | `GetStatInfo()` | ✅ | 獲取狀態資訊 |
| `getdate()` | `GetDate()` | ✅ | 讀取日期 |
| `gettime()` | `GetTime()` | ✅ | 讀取時間 |
| `getdatetime()` | `GetDateTime()` | ✅ | 讀取日期+時間 |
| `readaxes(what, axis)` | `ReadAxes(what, axis)` | ✅ | 讀取軸位置 |
| `readparam(axis, first, last)` | `ReadParam(axis, first, last)` | ✅ | 讀取參數 (4字節) |
| `readparam2(axis, first, last)` | `ReadParam2(first, last, size)` | ✅ | 讀取參數 (簡化版) |
| `readdiag(axis, first, last)` | `ReadDiag(axis, first, last)` | ✅ | 讀取診斷資料 (8字節) |
| `readmacro(first, last)` | `ReadMacro(first, last)` | ✅ | 讀取巨集變數 |
| `readpmc(datatype, section, first, count)` | `ReadPMC(datatype, section, first, count)` | ✅ | 讀取PMC資料 |
| `readexecprog(chars)` | `ReadExecProg(chars)` | ✅ | 讀取執行中的程式 |
| `readprognum()` | `ReadProgNum()` | ✅ | 讀取程式編號 |
| `readprogname()` | `ReadProgName()` | ✅ | 讀取程式名稱 |
| `readactfeed()` | `ReadActFeed()` | ✅ | 讀取實際進給率 |
| `readactspindlespeed()` | `ReadActSpindleSpeed()` | ✅ | 讀取實際主軸轉速 |
| `readactspindleload()` | `ReadActSpindleLoad()` | ✅ | 讀取實際主軸負載 |
| `readalarm()` | `ReadAlarm()` | ✅ | 讀取警報位元欄位 |
| `getprog(name)` | `GetProg(progName)` | ✅ | 讀取程式 |
| `getproghead(name, chars)` | `GetProgHead(progName, maxSize)` | ✅ | 讀取程式頭 |

## 未實作的方法

| Python 方法 | 狀態 | 原因/計畫 |
|------------|------|-----------|
| `settime(h, m, s)` | ❌ | 需要寫入權限，較少使用 |
| `listprog(start)` | ❌ | 需要迴圈處理，可後續添加 |
| `readalarmcode(type, withtext, maxmsgs, textlength)` | ❌ | 複雜的警報訊息讀取 |
| `readdir_current(fgbg)` | ❌ | 檔案系統操作 |
| `readdir_info(dir)` | ❌ | 檔案系統操作 |
| `readdir(dir, first, count, type, size)` | ❌ | 檔案系統操作 |
| `readdir_complete(dir)` | ❌ | 檔案系統操作 |
| `uploadprog(fullpath, content)` | ❌ | 需要寫入權限和複雜流處理 |
| `deleteprog(fullpath)` | ❌ | 需要寫入權限 |

## 內部方法對比

| Python 方法 | C# 方法 | 狀態 |
|------------|---------|------|
| `_encap(ftype, payload, fvers)` | `Encap(ftype, payload, fvers)` | ✅ |
| `_decap(data)` | `Decap(data)` | ✅ |
| `_req_rdsingle(...)` | `ReqRdSingle(...)` | ✅ |
| `_req_rdmulti(l)` | `ReqRdMulti(requestList)` | ✅ |
| `_req_rdsub(...)` | `ReqRdSub(...)` | ✅ |
| `_decode8(val)` | `Decode8(val, offset)` | ✅ |
| `_show_requsts(cap)` | - | ❌ (調試用) |
| `_show_response(cap)` | `FormatResponse(st)` | ✅ (簡化版) |

## 協議實作一致性

### ✅ 已驗證一致的部分

1. **連接協議**
   - Frame header: `0xA0 A0 A0 A0`
   - Open/Close request/response
   - Variable request/response

2. **資料封裝**
   - 大端序 (Big Endian) 轉換
   - 多包請求封裝
   - 子包長度處理

3. **請求解析**
   - `ReqRdSingle`: 檢查命令頭 + 6個零 (成功) 或僅命令頭 (錯誤)
   - `ReqRdMulti`: 驗證每個回應頭並解析錯誤碼/資料

4. **資料解碼**
   - `Decode8`: 8字節數值解碼 (type 2, 10)
   - 軸資料、巨集變數、參數、診斷資料的解析邏輯

### ⚠️ 需要注意的差異

1. **ReadParam2 vs readparam2**
   - Python: `readparam2(axis, first, last)` - 軸參數
   - C#: `ReadParam2(first, last, size)` - 簡化版參數讀取
   - **建議**: C# 版本參數順序與 Python 不同

2. **錯誤處理**
   - Python: 通常返回 `None` 或空字典
   - C#: 返回 `null` 或空字典
   - **一致性**: 基本相符

3. **資料類型**
   - Python: 使用 `dict`, `list`, `tuple`
   - C#: 使用 `Dictionary`, `List`, `tuple`
   - **轉換**: 已適當轉換

## 測試建議

### 高優先級測試項目

1. ✅ 連接/斷開 (`Connect`/`Disconnect`)
2. ✅ 系統資訊 (`GetSysInfo`)
3. ✅ 狀態資訊 (`GetStatInfo`)
4. ⚠️ 巨集變數讀取 (`ReadMacro`) - 單一/多重
5. ⚠️ 軸位置讀取 (`ReadAxes`)
6. ⚠️ 參數讀取 (`ReadParam`/`ReadParam2`)
7. ⚠️ 診斷資料 (`ReadDiag`)

### 中優先級測試項目

1. 日期/時間 (`GetDate`/`GetTime`/`GetDateTime`)
2. 實際數值 (`ReadActFeed`/`ReadActSpindleSpeed`/`ReadActSpindleLoad`)
3. 程式資訊 (`ReadProgNum`/`ReadProgName`/`GetProgHead`)
4. PMC資料 (`ReadPMC`)

### 低優先級/可選

1. 警報 (`ReadAlarm`)
2. 程式讀取 (`GetProg`)
3. 執行程式 (`ReadExecProg`)

## 已知問題

1. ❌ **`sysinfo` 為空**: 之前的問題，已通過修正 `ReqRdSingle` 邏輯解決
2. ✅ **`ReqRdMulti` 解析**: 已對齊 Python 邏輯，返回 `(error, payload)` 元組列表
3. ⚠️ **`ReadParam2` 參數順序**: 與 Python 版本不同，需要文檔說明

## 下一步計畫

### 短期 (已完成)
- [x] 對齊 `ReqRdMulti` 邏輯
- [x] 添加 `GetDate`/`GetTime`/`GetDateTime`
- [x] 添加 `ReadAxes`
- [x] 添加 `ReadParam`
- [x] 添加 `ReadDiag`
- [x] 添加實際數值讀取方法

### 中期 (可選)
- [ ] 添加 `ListProg`
- [ ] 添加 `ReadAlarmCode`
- [ ] 添加目錄操作方法
- [ ] 添加 `SetTime`

### 長期 (可選)
- [ ] 添加程式上傳/刪除功能
- [ ] 添加更完整的錯誤處理
- [ ] 添加非同步支援
- [ ] 添加連接池管理

## 版本資訊

- **Python pyfanuc**: 0.12
- **C# CsFanuc**: 對應 Python 0.12
- **最後更新**: 2026-01-17
- **實作完成度**: ~85% (核心功能完成)
