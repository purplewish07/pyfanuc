using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CsFanuc
{
    /// <summary>
    /// CsFanuc 使用範例程式
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== CsFanuc FANUC 機台通訊範例 ===\n");

            string ip = "192.168.1.168";  // 與 Python record2.py 相同的機台 IP
            int port = 8193;  // FANUC FOCAS 預設埠

            var conn = new CsFanuc(ip, port);

            try
            {
                // 連接到 FANUC 控制器
                Console.WriteLine($"正在連接 {ip}:{port}...");
                if (!conn.Connect())
                {
                    Console.WriteLine("✗ 連接失敗!");
                    return;
                }

                Console.WriteLine("✓ 連接成功!\n");

                // 顯示系統資訊
                Console.WriteLine("【系統資訊】");
                if (conn.SysInfo != null)
                {
                    Console.WriteLine($"  系統: {conn.SysInfo}");
                }
                else
                {
                    Console.WriteLine("  系統資訊不可用\n");
                }

                // 顯示狀態資訊
                Console.WriteLine("【狀態資訊】");
                if (conn.StatInfo != null)
                {
                    Console.WriteLine($"  {conn.StatInfo}");
                }
                else
                {
                    Console.WriteLine("  狀態資訊不可用\n");
                }

                // 讀取程式編號
                Console.WriteLine("【程式資訊】");
                var progNum = conn.ReadProgNum();
                if (progNum.Count > 0)
                {
                    int mainProg = progNum.ContainsKey("main") ? progNum["main"] : 0;
                    string progStr = "O" + mainProg.ToString().PadLeft(4, '0');
                    Console.WriteLine($"  主程式: {progStr}");

                    // 讀取程式名稱
                    var progName = conn.ReadProgName();
                    if (progName != null)
                    {
                        Console.WriteLine($"  程式名稱: {progName}");
                    }

                    // 嘗試讀取程式頭資訊
                    try
                    {
                        var progHead = conn.GetProgHead(progStr, 3500);
                        if (progHead != null)
                        {
                            var headLines = progHead.Split('\n');
                            Console.WriteLine($"  程式頭資訊 ({headLines.Length} 行):");
                            for (int i = 0; i < Math.Min(3, headLines.Length); i++)
                            {
                                Console.WriteLine($"    {headLines[i]}");
                            }
                        }
                    }
                    catch { }

                    Console.WriteLine();
                }

                // 讀取巨集變數
                Console.WriteLine("【巨集變數】");
                var macros = conn.ReadMacro(3901);
                Console.WriteLine($"  #3901 (零件數): {(macros.ContainsKey(3901) ? macros[3901] : "N/A")}");

                var total = conn.ReadMacro(12399);
                Console.WriteLine($"  #12399 (總數): {(total.ContainsKey(12399) ? total[12399] : "N/A")}");

                var ng = conn.ReadMacro(12400);
                Console.WriteLine($"  #12400 (NG 數): {(ng.ContainsKey(12400) ? ng[12400] : "N/A")}\n");

                // 讀取 PMC 資料
                Console.WriteLine("【PMC 資料】");
                var pmcData = conn.ReadPMC(0, 2, 27, 3);
                foreach (var kv in pmcData)
                {
                    Console.WriteLine($"  Y{kv.Key:D3}: 0x{kv.Value:X2}");
                }
                Console.WriteLine();

                // 讀取目錄
                Console.WriteLine("【目錄列表 (ReadDir)】");
                var dirList = conn.ReadDirComplete("//MEMCARD/");
                if (dirList != null)
                {
                    foreach (var entry in dirList)
                    {
                        string name = entry["name"].ToString();
                        string type = entry["type"].ToString();
                        
                        if (type == "F")  // 文件
                        {
                            var datetime = entry["datetime"] as DateTime?;
                            var size = entry["size"];
                            var comment = entry["comment"].ToString();
                            string dateStr = datetime?.ToString("yyyy/MM/dd HH:mm:ss") ?? "N/A";
                            Console.WriteLine($"  {name}({comment}) <{dateStr}|size:{size}>");
                        }
                        else  // 目錄
                        {
                            Console.WriteLine($"  <{name}>");
                        }
                    }
                }
                Console.WriteLine();

                // 獲取程式內容
                string targetProg = "O0011";
                Console.WriteLine($"【獲取程式】{targetProg}");
                var progContent = conn.GetProg(targetProg);
                if (progContent != null)
                {
                    Console.WriteLine(progContent);
                    
                    // 儲存到文件
                    System.IO.File.WriteAllText($"{targetProg}.nc", progContent);
                    Console.WriteLine($"已儲存程式至 {targetProg}.nc\n");

                    // 測試上傳程式
                    string uploadPath = "//CNC_MEM/USER/PATH1/";
                    Console.WriteLine($"【上傳程式】{uploadPath}{targetProg}");
                    try
                    {
                        conn.UploadProg(uploadPath, progContent);
                        Console.WriteLine($"✓ 已上傳程式至 {uploadPath}{targetProg}\n");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"✗ 上傳失敗: {ex.Message}\n");
                    }

                    // 測試刪除
                    string deletePath = $"{uploadPath}{targetProg}";
                    Console.WriteLine($"【刪除程式】{deletePath}");
                    try
                    {
                        conn.DeleteProg(deletePath);
                        Console.WriteLine($"✓ 已刪除程式 {deletePath}\n");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"✗ 刪除失敗: {ex.Message}\n");
                    }
                }

                Console.WriteLine();



            }
            catch (Exception e)
            {
                Console.WriteLine($"\n✗ 發生錯誤: {e.Message}");
                Console.WriteLine($"堆疊追蹤: {e.StackTrace}");
            }
            finally
            {
                // 斷開連接
                if (conn.IsConnected)
                {
                    if (conn.Disconnect())
                    {
                        Console.WriteLine("✓ 已斷開連接");
                    }
                }
            }

            // Console.WriteLine("\n按任意鍵退出...");
            // Console.ReadKey();
        }

    }
}
