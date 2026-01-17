# CsFanuc - FANUC CNC 通訊庫 (C# 版本)

FANUC FOCAS 通訊協議的 C# 完整實現，移植自 Python 的 `pyfanuc` 庫。

## 目錄

- [功能特性](#功能特性)
- [快速開始](#快速開始)
- [API 參考](#api-參考)
- [常用常數](#常用常數)
- [目錄與檔案操作](#目錄與檔案操作)
- [範例代碼](#範例代碼)
- [編譯與執行](#編譯與執行)
- [常見機台位址](#常見機台位址)
- [網路設定](#網路設定)
- [錯誤排查](#錯誤排查)
- [實作狀態](#實作狀態)
- [性能優化建議](#性能優化建議)
- [版本對應](#版本對應)

## 功能特性

✅ **完整的 FOCAS 協議支持**
- 開啟/關閉連接
- 讀取系統資訊
- 讀取機台狀態
- 讀取程式資訊

✅ **豐富的資料讀取功能**
- 巨集變數 (`ReadMacro`)
- 參數資料 (`ReadParam`, `ReadParam2`)
- 診斷資料 (`ReadDiag`)
- PMC 資料 (`ReadPMC`)
- 軸位置 (`ReadAxes`)
- 程式編號 (`ReadProgNum`)
- 程式名稱 (`ReadProgName`)
- 程式內容 (`GetProg`, `GetProgHead`)
- 執行中程式 (`ReadExecProg`)
- 實際進給率 (`ReadActFeed`)
- 實際主軸轉速 (`ReadActSpindleSpeed`)
- 實際主軸負載 (`ReadActSpindleLoad`)
- 警報代碼 (`ReadAlarm`)
- 日期時間 (`GetDate`, `GetTime`, `GetDateTime`)

✅ **目錄與檔案操作**
- 程式列表 (`ListProg`)
- 目錄資訊 (`ReadDirInfo`)
- 目錄內容 (`ReadDir`, `ReadDirComplete`)
- 程式上傳 (`UploadProg`)
- 程式刪除 (`DeleteProg`)

✅ **完善的錯誤處理和大小端轉換**

## 快速開始

### 1. 基本連接

```csharp
using CsFanuc;

// 建立連接
var conn = new CsFanuc("192.168.1.168");

if (conn.Connect())
{
    Console.WriteLine("✓ 連接成功");
    
    // 使用各項功能...
    
    conn.Disconnect();
}
```

### 2. 讀取系統資訊

```csharp
// 自動在 Connect() 時取得
Console.WriteLine($"系統: {conn.SysInfo.Series}");
Console.WriteLine($"版本: {conn.SysInfo.Version}");
Console.WriteLine($"最大軸: {conn.SysInfo.MaxAxis}");
```

### 3. 讀取機台狀態

```csharp
var status = conn.StatInfo;
Console.WriteLine($"自動: {status.Auto}");
Console.WriteLine($"執行: {status.Run}");
Console.WriteLine($"運動: {status.Motion}");
Console.WriteLine($"警報: {status.Alarm}");
```

### 4. 讀取程式編號

```csharp
var progNum = conn.ReadProgNum();
if (progNum.ContainsKey("main"))
{
    string mainProg = "O" + progNum["main"].ToString().PadLeft(4, '0');
    Console.WriteLine($"主程式: {mainProg}");
}
```

### 5. 讀取巨集變數

```csharp
// 單一巨集
var result = conn.ReadMacro(3901);
if (result.ContainsKey(3901))
{
    Console.WriteLine($"零件數: {result[3901]}");
}

// 多個巨集
var macros = conn.ReadMacro(12399, 12401);
foreach (var macro in macros)
{
    Console.WriteLine($"#${macro.Key}: {macro.Value}");
}
```

### 6. 讀取參數

```csharp
// 單一參數
var param = conn.ReadParam2(6757);

// 多個參數
var params = conn.ReadParam2(6757, 6758);
foreach (var p in params)
{
    var data = (int[])p.Value["data"];
    Console.WriteLine($"參數 #{p.Key}: {data[0]}");
}
```

### 7. 讀取 PMC 資料

```csharp
// 讀取 PMC R 區段，位址 27-29，字節型 (datatype=0)
var pmcData = conn.ReadPMC(0, 2, 27, 3);
foreach (var pmc in pmcData)
{
    Console.WriteLine($"R{pmc.Key}: 0x{pmc.Value:X2}");
}

// PMC 資料型別
// 0: 字節 (1 位元組)
// 1: 字 (2 位元組)
// 2: 雙字 (4 位元組)
```

### 8. 讀取程式內容

```csharp
// 讀取程式頭
string progHead = conn.GetProgHead("O0002", 3500);
Console.WriteLine(progHead);

// 讀取完整程式
string fullProg = conn.GetProg("O0002");
// 存檔
File.WriteAllText("O0002.nc", fullProg, Encoding.UTF8);
```

### 9. 讀取執行中程式

```csharp
var execProg = conn.ReadExecProg(256);
Console.WriteLine($"區塊: {execProg["block"]}");
Console.WriteLine($"程式:\n{execProg["text"]}");
```

### 10. 讀取警報

```csharp
var alarm = conn.ReadAlarm();
if (alarm.HasValue)
{
    if (alarm == 0)
        Console.WriteLine("無警報");
    else
        Console.WriteLine($"警報代碼: 0x{alarm:X8}");
}
```

### 11. 讀取軸位置

```csharp
// 讀取絕對位置
var axes = conn.ReadAxes(CsFanuc.ABS);
if (axes.ContainsKey("ABS"))
{
    var positions = axes["ABS"];
    for (int i = 0; i < positions.Count; i++)
    {
        Console.WriteLine($"軸 {i+1}: {positions[i]}");
    }
}

// 讀取多種位置
var multiAxes = conn.ReadAxes(CsFanuc.ABS | CsFanuc.REL);
```

### 12. 讀取診斷資料

```csharp
// 讀取診斷資料
var diag = conn.ReadDiag(-1, 300, 310);
foreach (var d in diag)
{
    Console.WriteLine($"診斷 {d.Key}: {string.Join(", ", (double[])d.Value["data"])}");
}
```

## 目錄與檔案操作

### 列出程式清單

```csharp
// 取得所有 NC 程式及其大小和註解
var progs = conn.ListProg();
foreach (var prog in progs.OrderBy(p => p.Key))
{
    Console.WriteLine($"O{prog.Key:D4} - size:{prog.Value["size"]} bytes, comment:{prog.Value["comment"]}");
}
```

### 讀取目錄資訊

```csharp
// 取得目錄統計資訊
var dirInfo = conn.ReadDirInfo("//MEMCARD/");
Console.WriteLine($"目錄數: {dirInfo["dirs"]}");
Console.WriteLine($"檔案數: {dirInfo["files"]}");
```

### 讀取目錄內容

```csharp
// 讀取目錄內的檔案和子目錄
var dirList = conn.ReadDir("//MEMCARD/", 0, 10);
foreach (var entry in dirList)
{
    string name = entry["name"].ToString();
    string type = entry["type"].ToString();
    
    if (type == "F")  // 檔案
    {
        var datetime = entry["datetime"] as DateTime?;
        var size = entry["size"];
        var comment = entry["comment"].ToString();
        Console.WriteLine($"{name}({comment}) <{datetime:yyyy/MM/dd HH:mm:ss}|size:{size}>");
    }
    else  // 目錄
    {
        Console.WriteLine($"<{name}>");
    }
}

// 讀取完整目錄內容（自動分頁）
var allFiles = conn.ReadDirComplete("//CNC_MEM/USER/PATH1/");
```

### 上傳程式

```csharp
// 上傳程式到指定資料夾
string content = "O0011\nG00 X0 Y0\nM30\n%";
try
{
    conn.UploadProg("//CNC_MEM/USER/PATH1/", content);
    Console.WriteLine("上傳成功");
}
catch (Exception ex)
{
    Console.WriteLine($"上傳失敗: {ex.Message}");
}
```

### 刪除程式

```csharp
// 刪除指定路徑的程式
try
{
    conn.DeleteProg("//CNC_MEM/USER/PATH1/O0011");
    Console.WriteLine("刪除成功");
}
catch (Exception ex)
{
    Console.WriteLine($"刪除失敗: {ex.Message}");
}
```

## API 參考

### 連接管理

```csharp
// 連接到機台
bool Connect()

// 斷開連接
bool Disconnect()

// 檢查連接狀態
public bool IsConnected { get; }

// 取得遠端 IP
public string RemoteIP { get; }
```

### 資訊讀取

```csharp
// 系統資訊 (自動在 Connect 時取得)
public SystemInfo SysInfo { get; }

// 狀態資訊 (自動在 Connect 時取得)
public StatusInfo StatInfo { get; }

// 讀取日期
Tuple<ushort, ushort, ushort> GetDate()

// 讀取時間
Tuple<ushort, ushort, ushort> GetTime()

// 讀取日期時間
DateTime? GetDateTime()

// 讀取巨集變數
Dictionary<int, double?> ReadMacro(int first, int last = 0)

// 讀取參數 (4字節版本)
Dictionary<int, Dictionary<string, object>> ReadParam(int axis, int first, int last = 0)

// 讀取參數 (簡化版本)
Dictionary<int, Dictionary<string, object>> ReadParam2(int first, int last = 0, int size = 1)

// 讀取診斷資料
Dictionary<int, Dictionary<string, object>> ReadDiag(int axis, int first, int last = 0)

// 讀取軸位置
Dictionary<string, List<double?>> ReadAxes(int what = ABS, int axis = ALLAXIS)

// 讀取 PMC 資料
Dictionary<int, uint> ReadPMC(int datatype, int section, int first, int count = 1)

// 讀取程式編號
Dictionary<string, int> ReadProgNum()

// 讀取程式名稱
string ReadProgName()

// 讀取執行中程式
Dictionary<string, object> ReadExecProg(int chars = 256)

// 讀取警報
uint? ReadAlarm()

// 讀取實際進給率
double? ReadActFeed()

// 讀取實際主軸轉速
double? ReadActSpindleSpeed()

// 讀取實際主軸負載
double? ReadActSpindleLoad()

// 讀取程式頭
string GetProgHead(string progName, int maxSize = 3500)

// 讀取程式
string GetProg(string progName)
```

### 目錄與檔案操作

```csharp
// 列出所有程式
Dictionary<int, Dictionary<string, object>> ListProg(int start = 1)

// 讀取目錄資訊
Dictionary<string, int> ReadDirInfo(string dir)

// 讀取目錄內容
List<Dictionary<string, object>> ReadDir(string dir, int first = 0, int count = 10, int type = 0, int size = 1)

// 讀取完整目錄內容
List<Dictionary<string, object>> ReadDirComplete(string dir)

// 上傳程式
bool UploadProg(string fullpath, string content)

// 刪除程式
bool DeleteProg(string fullpath)
```

## 常用常數

```csharp
// 軸定義
const int ABS = 1;      // 絕對位置
const int REL = 2;      // 相對位置
const int REF = 4;      // 參考位置
const int SKIP = 8;     // 跳過
const int DIST = 16;    // 距離
const int ALLAXIS = -1; // 所有軸

// PMC 資料型別
// 0: 字節 (Byte)
// 1: 字 (Word, 2 bytes)
// 2: 雙字 (DWord, 4 bytes)

// PMC 常見位址
// R27: 紅燈
// R28: 黃燈
// R29: 綠燈
```

## 範例代碼

完整的使用範例見 `Program.cs`

## 編譯與執行

```bash
# 編譯專案
dotnet build

# 執行程式
dotnet run

# 發佈獨立執行檔
dotnet publish -c Release -r win-x64 --self-contained
```

## 常見機台位址

### 巨集變數 (#)
| 變數編號 | 用途 |
|---------|------|
| 3901 | 零件計數器 (Parts Counter) |
| 12399 | 生產數量 (Production Count) |
| 12400 | NG 數量 (NG Count) |
| 3008 | 執行中程式編號 |

### 參數 (P)
| 參數編號 | 用途 |
|---------|------|
| 6757 | 單位時間 (CT 秒) |
| 6758 | 單位時間 (CT 分) |

### PMC 位址 (R/Y)
| 位址 | 用途 | 含義 |
|-----|------|------|
| Y27.2 (R27 bit 2) | 紅燈 | 機台故障/停機 (1=點亮) |
| Y27.3 (R27 bit 3) | 黃燈 | 警告/待機 (1=點亮) |
| Y29.1 (R29 bit 1) | 綠燈 | 正常運行 (1=點亮) |

## 網路設定

- **協議**: FANUC FOCAS (Fanuc Open CNC APIs)
- **埠號**: 8193 (預設，可配置)
- **傳輸**: TCP/IP
- **位元組序**: 大端序 (Big Endian)

1. 確保 FANUC 控制器開啟了 FOCAS 服務
2. 確認 PC 和機台在同一網段或網路可達
3. 檢查防火牆設定

## 錯誤排查

### 故障排查清單

- [ ] 檢查機台 IP 和埠號
- [ ] 驗證網路連接 (ping 測試)
- [ ] 確認機台 FOCAS 服務已啟動
- [ ] 檢查防火牆設定
- [ ] 查看機台警報狀態
- [ ] 測試使用 telnet 連接: `telnet <IP> 8193`

### 無法連接
- 檢查 IP 地址是否正確
- 檢查埠號是否為 8193
- 檢查網路連接和防火牆設定
- 檢查機台是否開啟 FOCAS 服務

### 讀取資料失敗
- 檢查機台是否在運行
- 確認讀取的變數/參數編號是否存在
- 查看機台的警報狀態

### 上傳/刪除失敗
- 確認路徑格式正確（必須以 `//` 開頭）
- 檢查檔案是否已存在（上傳時）
- 確認機台有寫入權限
- 檢查目標路徑是否存在

## 實作狀態

### ✅ 已完成的功能

| 功能分類 | Python 方法 | C# 方法 | 狀態 |
|---------|------------|---------|------|
| **連接管理** | | | |
| 連接控制器 | `connect()` | `Connect()` | ✅ |
| 斷開連接 | `disconnect()` | `Disconnect()` | ✅ |
| **系統資訊** | | | |
| 獲取系統資訊 | `getsysinfo()` | `GetSysInfo()` | ✅ |
| 獲取狀態資訊 | `getstatinfo()` | `GetStatInfo()` | ✅ |
| 讀取日期 | `getdate()` | `GetDate()` | ✅ |
| 讀取時間 | `gettime()` | `GetTime()` | ✅ |
| 讀取日期時間 | `getdatetime()` | `GetDateTime()` | ✅ |
| **資料讀取** | | | |
| 讀取軸位置 | `readaxes()` | `ReadAxes()` | ✅ |
| 讀取巨集變數 | `readmacro()` | `ReadMacro()` | ✅ |
| 讀取參數 (4字節) | `readparam()` | `ReadParam()` | ✅ |
| 讀取參數 (8字節) | `readparam2()` | `ReadParam2()` | ✅ |
| 讀取診斷資料 | `readdiag()` | `ReadDiag()` | ✅ |
| 讀取 PMC | `readpmc()` | `ReadPMC()` | ✅ |
| **程式操作** | | | |
| 讀取程式編號 | `readprognum()` | `ReadProgNum()` | ✅ |
| 讀取程式名稱 | `readprogname()` | `ReadProgName()` | ✅ |
| 讀取執行中程式 | `readexecprog()` | `ReadExecProg()` | ✅ |
| 讀取程式頭 | `getproghead()` | `GetProgHead()` | ✅ |
| 讀取程式 | `getprog()` | `GetProg()` | ✅ |
| **實際數值** | | | |
| 讀取進給率 | `readactfeed()` | `ReadActFeed()` | ✅ |
| 讀取主軸轉速 | `readactspindlespeed()` | `ReadActSpindleSpeed()` | ✅ |
| 讀取主軸負載 | `readactspindleload()` | `ReadActSpindleLoad()` | ✅ |
| **警報** | | | |
| 讀取警報 | `readalarm()` | `ReadAlarm()` | ✅ |
| **目錄與檔案** | | | |
| 列出程式 | `listprog()` | `ListProg()` | ✅ |
| 目錄資訊 | `readdir_info()` | `ReadDirInfo()` | ✅ |
| 讀取目錄 | `readdir()` | `ReadDir()` | ✅ |
| 讀取完整目錄 | `readdir_complete()` | `ReadDirComplete()` | ✅ |
| 上傳程式 | `uploadprog()` | `UploadProg()` | ✅ |
| 刪除程式 | `deleteprog()` | `DeleteProg()` | ✅ |

### ⚠️ 已知差異

1. **ReadParam2 參數順序**
   - Python: `readparam2(axis, first, last)`
   - C#: `ReadParam2(first, last, size)`
   - C# 版本簡化了參數，預設讀取所有軸

2. **錯誤處理**
   - Python: 返回 `None` 或空字典
   - C#: 返回 `null` 或空字典

### ❌ 未實作功能

| 功能 | 原因 |
|-----|------|
| `settime()` | 寫入操作，較少使用 |
| `readalarmcode()` | 複雜的警報訊息讀取 |
| `readdir_current()` | 較少使用 |

## 性能優化建議

### 1. 批量讀取
盡量使用範圍讀取而非單一讀取：

```csharp
// ✅ 推薦: 批量讀取
var macros = conn.ReadMacro(12399, 12401);

// ❌ 不推薦: 多次單一讀取
var m1 = conn.ReadMacro(12399);
var m2 = conn.ReadMacro(12400);
var m3 = conn.ReadMacro(12401);
```

### 2. 連接復用
盡量保持連接而不要反覆連/斷：

```csharp
// ✅ 推薦
var conn = new CsFanuc(ip);
conn.Connect();
// 執行多個操作...
conn.Disconnect();

// ❌ 不推薦
for (int i = 0; i < 100; i++)
{
    var conn = new CsFanuc(ip);
    conn.Connect();
    // 單一操作
    conn.Disconnect();
}
```

### 3. 異常處理
妥善處理可能的異常情況：

```csharp
try
{
    var result = conn.ReadMacro(3901);
}
catch (Exception ex)
{
    Console.WriteLine($"讀取失敗: {ex.Message}");
}
```

## 類別架構

```
CsFanuc (主類別)
├── SystemInfo (系統資訊)
├── StatusInfo (狀態資訊)
└── 各種讀取方法
```

## 協議實作細節

### 已驗證一致的部分

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
   - `Decode8`: 8字節數值解碼
   - 軸資料、巨集變數、參數、診斷資料的解析邏輯

## 版本對應

| C# 版本 | Python pyfanuc | 完成度 | 狀態 |
|---------|----------------|--------|------|
| 1.0.0   | 0.12+          | ~95%   | ✓ 生產就緒 |

**實作完成度**: 95% (核心功能 + 目錄檔案操作完成)

## 相關文件

- `CsFanuc.cs` - 主要通訊類別
- `SystemInfo.cs` - 系統資訊類別
- `StatusInfo.cs` - 狀態資訊類別
- `Program.cs` - 完整使用範例

## 參考資源

- [FANUC FOCAS API 文件]
- [Python pyfanuc 原始庫](https://github.com/c-logic/pyfanuc)

---

**最後更新**: 2026-01-17  
**作者**: purplewish07  
**專案**: [pyfanuc](https://github.com/purplewish07/pyfanuc)
