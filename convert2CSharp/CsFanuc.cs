using System;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace CsFanuc
{
    /// <summary>
    /// FANUC CNC 控制器通訊類別 (C# 版本 pyfanuc)
    /// 通訊協議: FANUC FOCAS 協議
    /// </summary>
    public class CsFanuc
    {
        private Socket sock;
        private string ip;
        private int port;
        private bool connected;

        // 常數定義
        private const ushort FTYPE_OPN_REQU = 0x0101;
        private const ushort FTYPE_OPN_RESP = 0x0102;
        private const ushort FTYPE_VAR_REQU = 0x2101;
        private const ushort FTYPE_VAR_RESP = 0x2102;
        private const ushort FTYPE_CLS_REQU = 0x0201;
        private const ushort FTYPE_CLS_RESP = 0x0202;

        private static readonly byte[] FRAME_SRC = { 0x00, 0x01 };
        private static readonly byte[] FRAME_DST = { 0x00, 0x02 };
        private static readonly byte[] FRAME_DST2 = { 0x00, 0x01 };
        private static readonly byte[] FRAMEHEAD = { 0xa0, 0xa0, 0xa0, 0xa0 };

        public const int ABS = 1;
        public const int REL = 2;
        public const int REF = 4;
        public const int SKIP = 8;
        public const int DIST = 16;
        public const int ALLAXIS = -1;

        public SystemInfo SysInfo { get; private set; }
        public StatusInfo StatInfo { get; private set; }

        public CsFanuc(string ip, int port = 8193)
        {
            this.ip = ip;
            this.port = port;
            this.connected = false;
            this.sock = null;
        }

        public bool IsConnected => connected;
        public string RemoteIP => ip;

        /// <summary>
        /// 建立連接到 FANUC 控制器
        /// </summary>
        public bool Connect()
        {
            try
            {
                Console.WriteLine("cnning1");
                sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                sock.ReceiveTimeout = 5000;
                sock.SendTimeout = 5000;

                Console.WriteLine("cnning2");
                sock.Connect(ip, port);

                Console.WriteLine("cnning3");
                byte[] openRequest = Encap(FTYPE_OPN_REQU, FRAME_DST);
                sock.SendAll(openRequest);

                Console.WriteLine("cnning4");
                byte[] responseData = new byte[1500];
                int received = sock.Receive(responseData);
                Array.Resize(ref responseData, received);

                var data = Decap(responseData);
                if (data != null && data["ftype"] is ushort ftype && ftype == FTYPE_OPN_RESP)
                {
                    connected = true;
                    GetSysInfo();
                    GetStatInfo();
                    Console.WriteLine("cnn ok");
                    return true;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"連接失敗: {e.Message}");
                connected = false;
            }

            return false;
        }

        /// <summary>
        /// 斷開連接
        /// </summary>
        public bool Disconnect()
        {
            try
            {
                if (sock != null && connected)
                {
                    sock.ReceiveTimeout = 1000;
                    byte[] closeRequest = Encap(FTYPE_CLS_REQU, new byte[0]);
                    sock.SendAll(closeRequest);

                    byte[] responseData = new byte[1500];
                    int received = sock.Receive(responseData);
                    Array.Resize(ref responseData, received);

                    var data = Decap(responseData);
                    if (data != null && data["ftype"] is ushort ftype && ftype == FTYPE_CLS_RESP)
                    {
                        sock.Close();
                        connected = false;
                        return true;
                    }

                    sock.Shutdown(SocketShutdown.Both);
                    sock.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"斷開連接錯誤: {e.Message}");
                try
                {
                    sock?.Shutdown(SocketShutdown.Both);
                    sock?.Close();
                }
                catch { }
            }

            connected = false;
            return false;
        }

        /// <summary>
        /// 封裝資料包
        /// </summary>
        private byte[] Encap(ushort ftype, byte[] payload, ushort fvers = 1)
        {
            byte[] frameHead = FRAMEHEAD;
            byte[] header = new byte[6];
            
            // 大端序編碼
            Buffer.BlockCopy(BitConverter.GetBytes(ReverseBytes(fvers)), 0, header, 0, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(ReverseBytes(ftype)), 0, header, 2, 2);
            
            // VAR_REQU 需要 subpacket 封裝
            if (ftype == FTYPE_VAR_REQU)
            {
                // 添加 subpacket 封裝: [count(2)][len(2)][payload]
                ushort subpacketLen = (ushort)(payload.Length + 2);
                byte[] wrappedPayload = new byte[4 + payload.Length];
                Buffer.BlockCopy(BitConverter.GetBytes(ReverseBytes((ushort)1)), 0, wrappedPayload, 0, 2);  // count=1
                Buffer.BlockCopy(BitConverter.GetBytes(ReverseBytes(subpacketLen)), 0, wrappedPayload, 2, 2);  // len
                Buffer.BlockCopy(payload, 0, wrappedPayload, 4, payload.Length);
                payload = wrappedPayload;
            }
            
            int payloadLen = payload.Length;
            Buffer.BlockCopy(BitConverter.GetBytes(ReverseBytes((ushort)payloadLen)), 0, header, 4, 2);

            byte[] result = new byte[frameHead.Length + header.Length + payload.Length];
            Buffer.BlockCopy(frameHead, 0, result, 0, frameHead.Length);
            Buffer.BlockCopy(header, 0, result, frameHead.Length, header.Length);
            Buffer.BlockCopy(payload, 0, result, frameHead.Length + header.Length, payload.Length);

            return result;
        }

        /// <summary>
        /// 解封資料包
        /// </summary>
        private Dictionary<string, object> Decap(byte[] data)
        {
            if (data.Length < 10)
                return new Dictionary<string, object> { { "len", -1 } };

            if (!data.Take(4).SequenceEqual(FRAMEHEAD))
                return new Dictionary<string, object> { { "len", -1 } };

            ushort fvers = ReverseBytes(BitConverter.ToUInt16(data, 4));
            ushort ftype = ReverseBytes(BitConverter.ToUInt16(data, 6));
            ushort len1 = ReverseBytes(BitConverter.ToUInt16(data, 8));

            if (len1 + 10 != data.Length)
                return new Dictionary<string, object> { { "len", -1 } };

            var result = new Dictionary<string, object>
            {
                ["ftype"] = ftype,
                ["fvers"] = fvers,
                ["len"] = (int)len1
            };

            if (len1 == 0)
            {
                result["data"] = new byte[0];
                return result;
            }

            byte[] payloadData = new byte[len1];
            Buffer.BlockCopy(data, 10, payloadData, 0, len1);

            if (ftype == FTYPE_VAR_RESP)
            {
                var dataList = new List<byte[]>();
                ushort qu = ReverseBytes(BitConverter.ToUInt16(payloadData, 0));
                int offset = 2;

                for (int i = 0; i < qu; i++)
                {
                    ushort le = ReverseBytes(BitConverter.ToUInt16(payloadData, offset));
                    byte[] chunk = new byte[le - 2];
                    Buffer.BlockCopy(payloadData, offset + 2, chunk, 0, le - 2);
                    dataList.Add(chunk);
                    offset += le;
                }

                result["data"] = dataList;
            }
            else
            {
                result["data"] = payloadData;
            }

            return result;
        }

        // Pretty-print a response dictionary for debugging
        private static string FormatResponse(Dictionary<string, object> st)
        {
            if (st == null) return "null";
            var sb = new StringBuilder();
            sb.AppendLine("Response {");
            foreach (var kvp in st)
            {
                string key = kvp.Key;
                object val = kvp.Value;
                sb.Append("  ").Append(key).Append(": ");
                if (val is byte[] b)
                {
                    int show = Math.Min(16, b.Length);
                    sb.Append($"byte[{b.Length}] 0x").Append(BitConverter.ToString(b, 0, show).Replace("-", ""));
                    if (b.Length > show) sb.Append("...");
                }
                else if (val is List<byte[]> list)
                {
                    sb.Append("List<byte[]>(").Append(list.Count).Append(")\n");
                    int idx = 0;
                    foreach (var chunk in list)
                    {
                        int show = Math.Min(16, chunk.Length);
                        sb.Append("    [").Append(idx++).Append("] byte[").Append(chunk.Length).Append("] 0x")
                          .Append(BitConverter.ToString(chunk, 0, show).Replace("-", ""));
                        if (chunk.Length > show) sb.Append("...");
                        sb.Append('\n');
                    }
                    continue;
                }
                else
                {
                    sb.Append(val);
                }
                sb.Append('\n');
            }
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// 獲取系統資訊
        /// </summary>
        public void GetSysInfo()
        {
            var st = ReqRdSingle(1, 1, 0x18);
            if (st != null && st.ContainsKey("len") && (int)st["len"] == 0x12)
            {
                byte[] data = (byte[])st["data"];
                SysInfo = new SystemInfo
                {
                    AddInfo = ReverseBytes(BitConverter.ToUInt16(data, 0)),
                    MaxAxis = ReverseBytes(BitConverter.ToUInt16(data, 2)),
                    CncType = Encoding.ASCII.GetString(data, 4, 2).Trim(),
                    MtType = Encoding.ASCII.GetString(data, 6, 2).Trim(),
                    Series = Encoding.ASCII.GetString(data, 8, 4).Trim(),
                    Version = Encoding.ASCII.GetString(data, 12, 4).Trim(),
                    Axes = Encoding.ASCII.GetString(data, 16, 2).Trim()
                };
            }
        }

        /// <summary>
        /// 獲取狀態資訊
        /// </summary>
        public void GetStatInfo()
        {
            var st = ReqRdSingle(1, 1, 0x19, 0);
            if (st != null && st.ContainsKey("len") && (int)st["len"] == 0x0e)
            {
                byte[] data = (byte[])st["data"];
                StatInfo = new StatusInfo
                {
                    Auto = ReverseBytes(BitConverter.ToUInt16(data, 0)),
                    Run = ReverseBytes(BitConverter.ToUInt16(data, 2)),
                    Motion = ReverseBytes(BitConverter.ToUInt16(data, 4)),
                    Mstb = ReverseBytes(BitConverter.ToUInt16(data, 6)),
                    Emergency = ReverseBytes(BitConverter.ToUInt16(data, 8)),
                    Alarm = ReverseBytes(BitConverter.ToUInt16(data, 10)),
                    Edit = ReverseBytes(BitConverter.ToUInt16(data, 12))
                };
            }
        }

        /// <summary>
        /// 讀取巨集變數
        /// </summary>
        public Dictionary<int, double?> ReadMacro(int first, int last = 0)
        {
            if (last == 0) last = first;

            var result = new Dictionary<int, double?>();

            if (first == last)
            {
                var st = ReqRdSingle(1, 1, 0x15, first, last);
                if (st != null && st.ContainsKey("len") && (int)st["len"] > 0)
                {
                    byte[] data = (byte[])st["data"];
                    result[first] = Decode8(data, 0);
                }
            }
            else
            {
                var requests = new List<byte[]>();
                for (int macro_num = first; macro_num <= last; macro_num++)
                {
                    requests.Add(ReqRdSub(1, 1, 0x15, macro_num, macro_num));
                }

                var st = ReqRdMulti(requests);
                if (st != null && st.ContainsKey("data"))
                {
                    int macro = first;
                    foreach (var tuple in (List<(short error, byte[] payload)>)st["data"])
                    {
                        if (tuple.error != 0)
                        {
                            result[macro] = null;
                        }
                        else
                        {
                            ushort dataLen = (ushort)(tuple.payload.Length >= 2 ? ReverseBytes(BitConverter.ToUInt16(tuple.payload, 0)) : 0);
                            if (dataLen > 2)
                            {
                                result[macro] = Decode8(tuple.payload, 2);
                            }
                        }
                        macro++;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 讀取程式編號
        /// </summary>
        public Dictionary<string, int> ReadProgNum()
        {
            var result = new Dictionary<string, int>();
            var st = ReqRdSingle(1, 1, 0x1c);

            if (st != null && st.ContainsKey("len") && (int)st["len"] >= 8)
            {
                byte[] data = (byte[])st["data"];
                result["run"] = ReverseBytes(BitConverter.ToInt32(data, 0));
                result["main"] = ReverseBytes(BitConverter.ToInt32(data, 4));
            }

            return result;
        }

        /// <summary>
        /// 讀取程式名稱
        /// </summary>
        public string ReadProgName()
        {
            var st = ReqRdSingle(1, 1, 0xb9);
            if (st != null && st.ContainsKey("len") && (int)st["len"] >= 0)
            {
                byte[] data = (byte[])st["data"];
                int nullIndex = Array.IndexOf(data, (byte)0);
                if (nullIndex > 0)
                {
                    return Encoding.UTF8.GetString(data, 0, nullIndex);
                }
            }
            return null;
        }

        /// <summary>
        /// 讀取執行中的程式區塊
        /// </summary>
        public Dictionary<string, object> ReadExecProg(int chars = 256)
        {
            var result = new Dictionary<string, object>();
            var st = ReqRdSingle(1, 1, 0x20, chars);

            if (st != null && st.ContainsKey("len") && (int)st["len"] > 4)
            {
                byte[] data = (byte[])st["data"];
                result["block"] = ReverseBytes(BitConverter.ToInt32(data, 0));
                result["text"] = Encoding.UTF8.GetString(data, 4, (int)st["len"] - 4);
            }

            return result;
        }

        /// <summary>
        /// 讀取警報代碼
        /// </summary>
        public uint? ReadAlarm()
        {
            var st = ReqRdSingle(1, 1, 0x1a);
            if (st != null && st.ContainsKey("len") && (int)st["len"] == 4)
            {
                byte[] data = (byte[])st["data"];
                return ReverseBytes(BitConverter.ToUInt32(data, 0));
            }
            return null;
        }

        /// <summary>
        /// 讀取參數 (Param2)
        /// </summary>
        public Dictionary<int, Dictionary<string, object>> ReadParam2(int first, int last = 0, int size = 1)
        {
            if (last == 0) last = first;
            var result = new Dictionary<int, Dictionary<string, object>>();

            if (first == last)
            {
                var st = ReqRdSingle(2, 1, 0x0a, first, last, 0, 0);
                if (st != null && st.ContainsKey("len") && (int)st["len"] > 0)
                {
                    byte[] data = (byte[])st["data"];
                    int value = ReverseBytes(BitConverter.ToInt32(data, 0));
                    result[first] = new Dictionary<string, object>
                    {
                        { "data", new int[] { value } }
                    };
                }
            }
            else
            {
                var requests = new List<byte[]>();
                for (int param_num = first; param_num <= last; param_num += size)
                {
                    requests.Add(ReqRdSub(2, 1, 0x0a, param_num, param_num + size - 1));
                }

                var st = ReqRdMulti(requests);
                if (st != null && st.ContainsKey("data"))
                {
                    int param = first;
                    foreach (var tuple in (List<(short error, byte[] payload)>)st["data"])
                    {
                        if (tuple.error == 0 && tuple.payload.Length >= 4)
                        {
                            int value = ReverseBytes(BitConverter.ToInt32(tuple.payload, 0));
                            result[param] = new Dictionary<string, object>
                            {
                                { "data", new int[] { value } }
                            };
                        }
                        param += size;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 讀取程式頭資訊
        /// </summary>
        public string GetProgHead(string progName, int maxSize = 3500)
        {
            try
            {
                // 構建請求數據
                byte[] nameBytes = Encoding.UTF8.GetBytes(progName);
                byte[] payload = new byte[nameBytes.Length + 4];
                Buffer.BlockCopy(BitConverter.GetBytes(ReverseBytes(maxSize)), 0, payload, 0, 4);
                Buffer.BlockCopy(nameBytes, 0, payload, 4, nameBytes.Length);

                var st = ReqRdSingle(1, 1, 0x0c, 0, 0, 0, 0, 0, payload);
                if (st != null && st.ContainsKey("len") && (int)st["len"] > 0)
                {
                    byte[] data = (byte[])st["data"];
                    int nullIndex = Array.IndexOf(data, (byte)0);
                    if (nullIndex > 0)
                    {
                        return Encoding.UTF8.GetString(data, 0, nullIndex);
                    }
                    return Encoding.UTF8.GetString(data);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"讀取程式頭失敗: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// 讀取 PMC 資料
        /// </summary>
        public Dictionary<int, uint> ReadPMC(int datatype, int section, int first, int count = 1)
        {
            var result = new Dictionary<int, uint>();

            int last = first + ((1 << datatype) * count) - 1;
            var st = ReqRdSingle(2, 1, 0x8001, first, last, section, datatype);

            if (st != null && st.ContainsKey("len") && (int)st["len"] > 0)
            {
                byte[] data = (byte[])st["data"];
                int length = (int)st["len"];

                for (int x = 0; x < (length >> datatype); x++)
                {
                    int pos = (1 << datatype) * x;
                    uint value = 0;

                    if (datatype == 0)
                    {
                        value = data[pos];
                    }
                    else if (datatype == 1)
                    {
                        value = ReverseBytes(BitConverter.ToUInt16(data, pos));
                    }
                    else if (datatype == 2)
                    {
                        value = ReverseBytes(BitConverter.ToUInt32(data, pos));
                    }

                    result[first + (1 << datatype) * x] = value;
                }
            }

            return result;
        }

        /// <summary>
        /// 讀取日期
        /// </summary>
        public ushort[] GetDate()
        {
            var st = ReqRdSingle(1, 1, 0x45, 0);
            if (st != null && st.ContainsKey("len") && (int)st["len"] == 0xc)
            {
                byte[] data = (byte[])st["data"];
                return new ushort[]
                {
                    ReverseBytes(BitConverter.ToUInt16(data, 0)),
                    ReverseBytes(BitConverter.ToUInt16(data, 2)),
                    ReverseBytes(BitConverter.ToUInt16(data, 4))
                };
            }
            return null;
        }

        /// <summary>
        /// 讀取時間
        /// </summary>
        public ushort[] GetTime()
        {
            var st = ReqRdSingle(1, 1, 0x45, 1);
            if (st != null && st.ContainsKey("len") && (int)st["len"] == 0xc)
            {
                byte[] data = (byte[])st["data"];
                return new ushort[]
                {
                    ReverseBytes(BitConverter.ToUInt16(data, 6)),
                    ReverseBytes(BitConverter.ToUInt16(data, 8)),
                    ReverseBytes(BitConverter.ToUInt16(data, 10))
                };
            }
            return null;
        }

        /// <summary>
        /// 讀取日期和時間
        /// </summary>
        public DateTime? GetDateTime()
        {
            var requests = new List<byte[]>
            {
                ReqRdSub(1, 1, 0x45, 0),
                ReqRdSub(1, 1, 0x45, 1)
            };

            var st = ReqRdMulti(requests);
            if (st == null || !st.ContainsKey("len") || (int)st["len"] < 0)
                return null;

            var dataList = (List<(short error, byte[] payload)>)st["data"];
            if (dataList.Count != 2)
                return null;

            if (dataList[0].error != 0 || dataList[1].error != 0)
                return null;

            if (dataList[0].payload.Length < 8 || dataList[1].payload.Length < 8)
                return null;

            ushort year = ReverseBytes(BitConverter.ToUInt16(dataList[0].payload, 2));
            ushort month = ReverseBytes(BitConverter.ToUInt16(dataList[0].payload, 4));
            ushort day = ReverseBytes(BitConverter.ToUInt16(dataList[0].payload, 6));
            ushort hour = ReverseBytes(BitConverter.ToUInt16(dataList[1].payload, 2));
            ushort minute = ReverseBytes(BitConverter.ToUInt16(dataList[1].payload, 4));
            ushort second = ReverseBytes(BitConverter.ToUInt16(dataList[1].payload, 6));

            try
            {
                return new DateTime(year, month, day, hour, minute, second);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 讀取軸位置
        /// </summary>
        public Dictionary<string, List<double?>> ReadAxes(int what = ABS, int axis = ALLAXIS)
        {
            var requests = new List<byte[]>();
            var axisTypes = new List<(string name, int flag, int code)>
            {
                ("ABS", ABS, 4),
                ("REL", REL, 6),
                ("REF", REF, 1),
                ("SKIP", SKIP, 8),
                ("DIST", DIST, 7)
            };

            foreach (var (name, flag, code) in axisTypes)
            {
                if ((what & flag) != 0)
                {
                    requests.Add(ReqRdSub(1, 1, 0x26, code, axis));
                }
            }

            var st = ReqRdMulti(requests);
            if (st == null || !st.ContainsKey("len") || (int)st["len"] < 0)
                return null;

            var result = new Dictionary<string, List<double?>>();
            var dataList = (List<(short error, byte[] payload)>)st["data"];
            int idx = 0;

            foreach (var (name, flag, code) in axisTypes)
            {
                if ((what & flag) != 0)
                {
                    if (idx >= dataList.Count)
                        break;

                    var tuple = dataList[idx++];
                    if (tuple.error != 0)
                    {
                        result[name] = null;
                    }
                    else
                    {
                        var values = new List<double?>();
                        ushort dataLen = tuple.payload.Length >= 2 ? ReverseBytes(BitConverter.ToUInt16(tuple.payload, 0)) : (ushort)0;
                        for (int pos = 2; pos < dataLen + 2 && pos + 8 <= tuple.payload.Length; pos += 8)
                        {
                            values.Add(Decode8(tuple.payload, pos));
                        }
                        result[name] = values;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 讀取參數 (4字節版本)
        /// </summary>
        public Dictionary<int, Dictionary<string, object>> ReadParam(int axis, int first, int last = 0)
        {
            if (last == 0) last = first;
            if (SysInfo == null) return null;

            var st = ReqRdSingle(1, 1, 0x8d, first, last, axis);
            if (st == null || !st.ContainsKey("len") || (int)st["len"] < 0)
                return null;

            var result = new Dictionary<int, Dictionary<string, object>>();
            byte[] data = (byte[])st["data"];
            int dataLen = (int)st["len"];

            for (int pos = 0; pos < dataLen; pos += SysInfo.MaxAxis * 4 + 8)
            {
                if (pos + 8 > data.Length) break;

                int varname = ReverseBytes(BitConverter.ToInt32(data, pos));
                short axiscount = (short)ReverseBytes(BitConverter.ToUInt16(data, pos + 4));
                ushort valtype = ReverseBytes(BitConverter.ToUInt16(data, pos + 6));

                var values = new Dictionary<string, object>
                {
                    { "type", valtype },
                    { "axis", axiscount },
                    { "data", new List<object>() }
                };

                var dataValues = (List<object>)values["data"];

                for (int n = pos + 8; n < pos + SysInfo.MaxAxis * 4 + 8 && n + 4 <= data.Length; n += 4)
                {
                    object value = null;

                    if (valtype == 0)
                        value = data[n + 3]; // byte
                    else if (valtype == 1)
                    {
                        // bit array
                        byte b = data[n + 3];
                        var bits = new bool[8];
                        for (int i = 0; i < 8; i++)
                            bits[7 - i] = ((b >> i) & 1) == 1;
                        value = bits;
                    }
                    else if (valtype == 2)
                        value = (short)ReverseBytes(BitConverter.ToUInt16(data, n + 2)); // short
                    else if (valtype == 3)
                        value = ReverseBytes(BitConverter.ToInt32(data, n)); // int

                    dataValues.Add(value);

                    if (axiscount != -1)
                        break;
                }

                result[varname] = values;
            }

            return result;
        }

        /// <summary>
        /// 讀取診斷資料 (8字節版本)
        /// </summary>
        public Dictionary<int, Dictionary<string, object>> ReadDiag(int axis, int first, int last = 0)
        {
            if (last == 0) last = first;
            if (SysInfo == null) return null;

            var st = ReqRdSingle(1, 1, 0x93, first, last, axis);
            if (st == null || !st.ContainsKey("len") || (int)st["len"] < 0)
                return null;

            var result = new Dictionary<int, Dictionary<string, object>>();
            byte[] data = (byte[])st["data"];
            int dataLen = (int)st["len"];

            for (int pos = 0; pos < dataLen; pos += SysInfo.MaxAxis * 8 + 8)
            {
                if (pos + 8 > data.Length) break;

                int varname = ReverseBytes(BitConverter.ToInt32(data, pos));
                short axiscount = (short)ReverseBytes(BitConverter.ToUInt16(data, pos + 4));
                ushort valtype = ReverseBytes(BitConverter.ToUInt16(data, pos + 6));

                var values = new Dictionary<string, object>
                {
                    { "type", valtype },
                    { "axis", axiscount },
                    { "data", new List<object>() }
                };

                var dataValues = (List<object>)values["data"];

                for (int n = pos + 8; n < pos + SysInfo.MaxAxis * 8 + 8 && n + 8 <= data.Length; n += 8)
                {
                    object value = null;

                    if (valtype == 0)
                        value = data[n + 7]; // byte
                    else if (valtype == 1)
                    {
                        // bit array
                        byte b = data[n + 7];
                        var bits = new bool[8];
                        for (int i = 0; i < 8; i++)
                            bits[7 - i] = ((b >> i) & 1) == 1;
                        value = bits;
                    }
                    else if (valtype == 2 || valtype == 3 || valtype == 4)
                        value = Decode8(data, n); // real/long

                    dataValues.Add(value);

                    if (axiscount != -1)
                        break;
                }

                result[varname] = values;
            }

            return result;
        }

        /// <summary>
        /// 讀取實際進給率
        /// </summary>
        public double? ReadActFeed()
        {
            var st = ReqRdSingle(1, 1, 0x24);
            if (st != null && st.ContainsKey("len") && (int)st["len"] == 8)
            {
                byte[] data = (byte[])st["data"];
                return Decode8(data, 0);
            }
            return null;
        }

        /// <summary>
        /// 讀取實際主軸轉速
        /// </summary>
        public double? ReadActSpindleSpeed()
        {
            var st = ReqRdSingle(1, 1, 0x25);
            if (st != null && st.ContainsKey("len") && (int)st["len"] == 8)
            {
                byte[] data = (byte[])st["data"];
                return Decode8(data, 0);
            }
            return null;
        }

        /// <summary>
        /// 讀取實際主軸負載
        /// </summary>
        public double? ReadActSpindleLoad()
        {
            var st = ReqRdSingle(1, 1, 0x40);
            if (st != null && st.ContainsKey("len") && (int)st["len"] == 8)
            {
                byte[] data = (byte[])st["data"];
                return Decode8(data, 0);
            }
            return null;
        }

        /// <summary>
        /// 單一請求
        /// </summary>
        private Dictionary<string, object> ReqRdSingle(int c1, int c2, int c3, int v1 = 0, int v2 = 0, int v3 = 0, int v4 = 0, int v5 = 0, byte[] pl = null)
        {
            if (pl == null) pl = new byte[0];

            byte[] cmd = BuildCommand(c1, c2, c3, v1, v2, v3, v4, v5, pl);
            
            try
            {
                sock.SendAll(Encap(FTYPE_VAR_REQU, cmd));
                
                // 先讀取 10-byte header 以獲取 payload 長度
                byte[] header = new byte[10];
                sock.ReceiveAll(header);
                
                if (!header.Take(4).SequenceEqual(new byte[] { 0xA0, 0xA0, 0xA0, 0xA0 }))
                    return new Dictionary<string, object> { { "len", -1 } };
                
                ushort payloadLen = (ushort)((header[8] << 8) | header[9]);
                
                // 讀取完整 payload
                byte[] payload = new byte[payloadLen];
                if (payloadLen > 0)
                {
                    sock.ReceiveAll(payload);
                }
                
                // 組合完整 packet
                byte[] responseData = new byte[10 + payloadLen];
                Buffer.BlockCopy(header, 0, responseData, 0, 10);
                Buffer.BlockCopy(payload, 0, responseData, 10, payloadLen);

                var t = Decap(responseData);

                if (!t.ContainsKey("len") || !t.ContainsKey("ftype"))
                    return new Dictionary<string, object> { { "len", -1 } };

                int tLen = (int)t["len"];
                ushort tFtype = (ushort)t["ftype"];
                
                if (tLen == 0 || tFtype != FTYPE_VAR_RESP)
                    return new Dictionary<string, object> { { "len", -1 } };

                var dataList = (List<byte[]>)t["data"];
                if (dataList.Count == 0)
                    return new Dictionary<string, object> { { "len", -1 } };

                byte[] responseCmd = dataList[0];

                // Python 邏輯檢查兩種格式:
                // 1. t["data"][0].startswith(cmd+b'\x00'*6) - 完整格式
                // 2. t["data"][0].startswith(cmd) - 簡化格式

                byte[] cmdHeader = new byte[6];
                cmdHeader[0] = (byte)(c1 >> 8);
                cmdHeader[1] = (byte)(c1 & 0xFF);
                cmdHeader[2] = (byte)(c2 >> 8);
                cmdHeader[3] = (byte)(c2 & 0xFF);
                cmdHeader[4] = (byte)(c3 >> 8);
                cmdHeader[5] = (byte)(c3 & 0xFF);

                // 檢查完整格式: [cmd(6)][zeros(6)][len(2)][data...]
                if (responseCmd.Length >= 14)
                {
                    bool fullMatch = true;
                    for (int i = 0; i < 6; i++)
                    {
                        if (responseCmd[i] != cmdHeader[i])
                        {
                            fullMatch = false;
                            break;
                        }
                    }
                    
                    if (fullMatch)
                    {
                        bool hasZeros = true;
                        for (int i = 6; i < 12; i++)
                        {
                            if (responseCmd[i] != 0)
                            {
                                hasZeros = false;
                                break;
                            }
                        }
                        
                        if (hasZeros)
                        {
                            ushort dataLen = ReverseBytes(BitConverter.ToUInt16(responseCmd, 12));
                            return new Dictionary<string, object>
                            {
                                { "len", (int)dataLen },
                                { "data", responseCmd.Length > 14 ? responseCmd.Skip(14).ToArray() : new byte[0] }
                            };
                        }
                    }
                }

                // 檢查簡化格式: [cmd(6)][error(2)][data...]
                if (responseCmd.Length >= 8)
                {
                    bool cmdMatch = true;
                    for (int i = 0; i < 6; i++)
                    {
                        if (responseCmd[i] != cmdHeader[i])
                        {
                            cmdMatch = false;
                            break;
                        }
                    }
                    
                    if (cmdMatch)
                    {
                        short errorCode = (short)ReverseBytes(BitConverter.ToInt16(responseCmd, 6));
                        return new Dictionary<string, object>
                        {
                            { "len", 0 },
                            { "data", responseCmd.Length > 8 ? responseCmd.Skip(8).ToArray() : new byte[0] },
                            { "error", errorCode }
                        };
                    }
                }

                return new Dictionary<string, object> { { "len", -1 } };
            }
            catch (Exception e)
            {
                Console.WriteLine($"ReqRdSingle 錯誤: {e.Message}");
            }

            return new Dictionary<string, object> { { "len", -1 } };
        }

        /// <summary>
        /// 多重請求
        /// </summary>
        private Dictionary<string, object> ReqRdMulti(List<byte[]> requestList)
        {
            try
            {
                // 組合多個請求
                byte[] combined = CombineRequests(requestList);
                sock.SendAll(Encap(FTYPE_VAR_REQU, combined));

                // 先讀取 10-byte header 以獲取 payload 長度
                byte[] header = new byte[10];
                sock.ReceiveAll(header);
                
                if (!header.Take(4).SequenceEqual(new byte[] { 0xA0, 0xA0, 0xA0, 0xA0 }))
                {
                    return null;
                }
                
                ushort payloadLen = (ushort)((header[8] << 8) | header[9]);
                
                // 讀取完整 payload
                byte[] payloadData = new byte[payloadLen];
                if (payloadLen > 0)
                {
                    sock.ReceiveAll(payloadData);
                }
                
                // 組合完整 packet
                byte[] responseData = new byte[10 + payloadLen];
                Buffer.BlockCopy(header, 0, responseData, 0, 10);
                Buffer.BlockCopy(payloadData, 0, responseData, 10, payloadLen);

                var t = Decap(responseData);

                // Python parity: basic guards
                if (!t.ContainsKey("len") || !t.ContainsKey("ftype"))
                    return new Dictionary<string, object> { { "len", -1 } };

                int tLen = (int)t["len"];
                ushort tFtype = (ushort)t["ftype"];
                if (tLen == 0 || tFtype != FTYPE_VAR_RESP)
                    return new Dictionary<string, object> { { "len", -1 } };

                var rawList = t["data"] as List<byte[]>;
                if (rawList == null || rawList.Count != requestList.Count)
                    return new Dictionary<string, object> { { "len", -1 } };

                var resultData = new List<(short error, byte[] payload)>();

                for (int i = 0; i < rawList.Count; i++)
                {
                    byte[] resp = rawList[i];
                    byte[] req = requestList[i];

                    if (resp.Length < 4 || req.Length < 6)
                        return new Dictionary<string, object> { { "len", -1 } };

                    // 回應格式: [c3(2)][錯誤碼(2)][v1(4)]...
                    // 請求格式: [c1(2)][c2(2)][c3(2)][v1(4)]...
                    // 檢查 c3 (請求的位置 4-5 vs 回應的位置 0-1)
                    if (resp[0] != req[4] || resp[1] != req[5])
                        return new Dictionary<string, object> { { "len", -1 } };

                    // 讀取錯誤碼 (位置 2-3)
                    ushort errorCodeU = ReverseBytes(BitConverter.ToUInt16(resp, 2));
                    short errorCode = (short)errorCodeU;

                    // 提取 payload: 若成功且有 len 欄位 (>=12 bytes)，從位置 10 開始；否則從 4 開始
                    byte[] payload;
                    if (errorCodeU == 0 && resp.Length >= 12)
                    {
                        // 成功: [c3(2)][0000(2)][v1(4)][v2(4)][len(2)][data...]
                        payload = resp.Skip(10).ToArray();
                    }
                    else if (errorCodeU == 0)
                    {
                        // 成功但無額外資料
                        payload = Array.Empty<byte>();
                    }
                    else
                    {
                        // 錯誤: 從 4 開始是錯誤詳情
                        payload = resp.Length > 4 ? resp.Skip(4).ToArray() : Array.Empty<byte>();
                    }

                    resultData.Add((errorCode, payload));
                }

                return new Dictionary<string, object>
                {
                    { "len", tLen },
                    { "ftype", tFtype },
                    { "fvers", t["fvers"] },
                    { "data", resultData }
                };
            }
            catch (Exception e)
            {
                Console.WriteLine($"ReqRdMulti 錯誤: {e.Message}");
            }

            return new Dictionary<string, object> { { "len", -1 } };
        }

        /// <summary>
        /// 組建子命令
        /// </summary>
        private byte[] ReqRdSub(int c1, int c2, int c3, int v1 = 0, int v2 = 0, int v3 = 0, int v4 = 0, int v5 = 0)
        {
            byte[] result = new byte[20];
            
            Buffer.BlockCopy(BitConverter.GetBytes(ReverseBytes((ushort)c1)), 0, result, 0, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(ReverseBytes((ushort)c2)), 0, result, 2, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(ReverseBytes((ushort)c3)), 0, result, 4, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(ReverseBytes(v1)), 0, result, 6, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(ReverseBytes(v2)), 0, result, 10, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(ReverseBytes(v3)), 0, result, 14, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(ReverseBytes(v4)), 0, result, 18, 4);

            return result;
        }

        /// <summary>
        /// 組建命令
        /// </summary>
        private byte[] BuildCommand(int c1, int c2, int c3, int v1, int v2, int v3, int v4, int v5, byte[] pl)
        {
            byte[] cmd = new byte[26 + pl.Length];

            Buffer.BlockCopy(BitConverter.GetBytes(ReverseBytes((ushort)c1)), 0, cmd, 0, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(ReverseBytes((ushort)c2)), 0, cmd, 2, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(ReverseBytes((ushort)c3)), 0, cmd, 4, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(ReverseBytes(v1)), 0, cmd, 6, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(ReverseBytes(v2)), 0, cmd, 10, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(ReverseBytes(v3)), 0, cmd, 14, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(ReverseBytes(v4)), 0, cmd, 18, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(ReverseBytes(v5)), 0, cmd, 22, 4);
            
            if (pl.Length > 0)
            {
                Buffer.BlockCopy(pl, 0, cmd, 26, pl.Length);
            }

            return cmd;
        }

        /// <summary>
        /// 組合多個請求
        /// </summary>
        private byte[] CombineRequests(List<byte[]> requests)
        {
            ushort count = (ushort)requests.Count;
            int totalLen = 2; // 計數欄位

            foreach (var req in requests)
            {
                totalLen += 2 + req.Length; // 長度欄位 + 請求資料
            }

            byte[] result = new byte[totalLen];
            Buffer.BlockCopy(BitConverter.GetBytes(ReverseBytes(count)), 0, result, 0, 2);

            int offset = 2;
            foreach (var req in requests)
            {
                ushort len = (ushort)(req.Length + 2);
                Buffer.BlockCopy(BitConverter.GetBytes(ReverseBytes(len)), 0, result, offset, 2);
                Buffer.BlockCopy(req, 0, result, offset + 2, req.Length);
                offset += len;
            }

            return result;
        }

        /// <summary>
        /// 解碼 8 字節數值
        /// </summary>
        private double? Decode8(byte[] val, int offset)
        {
            if (val.Length < offset + 8)
                return null;

            byte[] data = new byte[8];
            Buffer.BlockCopy(val, offset, data, 0, 8);

            if (data[5] == 2 || data[5] == 10)
            {
                if (data[6] == 0xff && data[7] == 0xff)
                    return null;

                int intVal = ReverseBytes(BitConverter.ToInt32(data, 0));
                double divisor = Math.Pow(data[5], data[7]);
                return intVal / divisor;
            }

            return null;
        }

        /// <summary>
        /// 讀取目錄資訊
        /// </summary>
        public Dictionary<string, int> ReadDirInfo(string dir)
        {
            byte[] buffer = new byte[0x100];
            byte[] dirBytes = Encoding.UTF8.GetBytes(dir);
            Buffer.BlockCopy(dirBytes, 0, buffer, 0, dirBytes.Length);
            
            var st = ReqRdSingle(1, 1, 0xb4, 0, 0, 0, 0, 256, buffer);
            if (st != null && st.ContainsKey("len") && (int)st["len"] >= 8)
            {
                byte[] data = (byte[])st["data"];
                return new Dictionary<string, int>
                {
                    { "dirs", ReverseBytes(BitConverter.ToInt32(data, 0)) },
                    { "files", ReverseBytes(BitConverter.ToInt32(data, 4)) }
                };
            }
            return null;
        }

        /// <summary>
        /// 讀取目錄內容
        /// </summary>
        public List<Dictionary<string, object>> ReadDir(string dir, int first = 0, int count = 10, int type = 1, int size = 1)
        {
            byte[] buffer = new byte[0x100];
            byte[] dirBytes = Encoding.UTF8.GetBytes(dir);
            Buffer.BlockCopy(dirBytes, 0, buffer, 0, dirBytes.Length);
            
            var st = ReqRdSingle(1, 1, 0xb3, first, count, type, size, 256, buffer);
            var result = new List<Dictionary<string, object>>();
            
            if (st != null && st.ContainsKey("len") && (int)st["len"] >= 8)
            {
                byte[] data = (byte[])st["data"];
                int dataLen = (int)st["len"];
                
                for (int t = 0; t < dataLen; t += 128)
                {
                    if (t + 128 > data.Length) break;
                    
                    var entry = new Dictionary<string, object>();
                    
                    // 解析結構: type(2) datetime(12) unkn(6) size(4) attr(4) name(36) comment(52) proctimestamp(12)
                    // Type 欄位：0x0000 = 目錄, 0x0001 = 檔案 (注意: 使用 unsigned)
                    ushort entryType = ReverseBytes(BitConverter.ToUInt16(data, t));
                    
                    // 文件名 (36 bytes, offset 28 = 2+12+6+4+4)
                    int nameNullIdx = Array.IndexOf(data, (byte)0, t + 28, 36);
                    int nameLen = nameNullIdx > t + 28 ? nameNullIdx - (t + 28) : 36;
                    
                    // 使用 Latin-1 編碼並在第一個 null 處截斷
                    string rawName = Encoding.GetEncoding("ISO-8859-1").GetString(data, t + 28, Math.Min(nameLen, 36));
                    // 確保只取到第一個 null 之前的內容
                    int firstNull = rawName.IndexOf('\0');
                    entry["name"] = firstNull >= 0 ? rawName.Substring(0, firstNull) : rawName.TrimEnd('\0');
                    entry["type"] = entryType == 0 ? "D" : "F";
                    
                    // 解析日期時間 (12 bytes, offset 2)
                    if (entryType != 0)  // 文件 (type != 0)
                    {
                        ushort year = ReverseBytes(BitConverter.ToUInt16(data, t + 2));
                        ushort month = ReverseBytes(BitConverter.ToUInt16(data, t + 4));
                        ushort day = ReverseBytes(BitConverter.ToUInt16(data, t + 6));
                        ushort hour = ReverseBytes(BitConverter.ToUInt16(data, t + 8));
                        ushort minute = ReverseBytes(BitConverter.ToUInt16(data, t + 10));
                        ushort second = ReverseBytes(BitConverter.ToUInt16(data, t + 12));
                        
                        try
                        {
                            entry["datetime"] = new DateTime(year, month, day, hour, minute, second);
                        }
                        catch
                        {
                            entry["datetime"] = null;
                        }
                        
                        // 文件大小 (4 bytes, offset 20 = 2+12+6)
                        // 注意: FANUC 檔案系統目錄(ReadDir)不返回程式大小，請使用 ListProg 取得真實大小
                        entry["size"] = ReverseBytes(BitConverter.ToUInt32(data, t + 20));
                        
                        // 註解 (52 bytes, offset 64 = 2+12+6+4+4+36)
                        int commentNullIdx = Array.IndexOf(data, (byte)0, t + 64, 52);
                        int commentLen = commentNullIdx >= t + 64 ? commentNullIdx - (t + 64) : 52;
                        if (commentLen > 0)
                            // 使用 Latin-1 編碼，保持與 Python 一致
                            entry["comment"] = Encoding.GetEncoding("ISO-8859-1").GetString(data, t + 64, Math.Min(commentLen, 52)).TrimEnd('\0');
                        else
                            entry["comment"] = "";
                    }
                    else  // 目錄
                    {
                        entry["datetime"] = null;
                        entry["size"] = null;
                        entry["comment"] = null;
                    }
                    
                    result.Add(entry);
                }
            }
            
            return result;
        }

        /// <summary>
        /// 讀取完整目錄內容
        /// </summary>
        public List<Dictionary<string, object>> ReadDirComplete(string dir)
        {
            var info = ReadDirInfo(dir);
            if (info == null) return null;
            
            int total = info["dirs"] + info["files"];
            var result = new List<Dictionary<string, object>>();
            
            for (int i = 0; i < total; i += 10)
            {
                var entries = ReadDir(dir, i, 10);
                if (entries != null)
                {
                    result.AddRange(entries);
                }
                else
                {
                    break;
                }
            }
            
            return result;
        }

        /// <summary>
        /// 列出所有已註冊的 NC 程式 (cnc_rdprogdir2)
        /// 使用 opcode 0x06，可獲取程式編號、大小和註解
        /// </summary>
        /// <param name="start">起始程式編號</param>
        /// <param name="count">每次讀取的數量 (預設 19)</param>
        /// <param name="type">讀取類型 (0:僅編號, 1:編號+註解, 2:編號+註解+大小)</param>
        /// <returns>程式列表字典，key 為程式編號，value 包含 size 和 comment</returns>
        public Dictionary<int, Dictionary<string, object>> ListProg(int start = 1, int count = 0x13, int type = 2)
        {
            var result = new Dictionary<int, Dictionary<string, object>>();
            
            while (true)
            {
                var st = ReqRdSingle(1, 1, 0x06, start, count, type);
                
                if (st == null || !st.ContainsKey("len"))
                    return null;
                
                int len = (int)st["len"];
                if (len < 0)
                    return null;
                else if (len == 0)
                    return result;
                
                byte[] data = (byte[])st["data"];
                
                // 每個程式條目 72 bytes: number(4) + size(4) + comment(64)
                for (int t = 0; t < len; t += 72)
                {
                    if (t + 72 > data.Length) break;
                    
                    // 讀取程式編號 (4 bytes, big-endian)
                    uint number = ReverseBytes(BitConverter.ToUInt32(data, t));
                    
                    // 讀取程式大小 (4 bytes, big-endian)
                    uint size = ReverseBytes(BitConverter.ToUInt32(data, t + 4));
                    
                    // 讀取註解 (64 bytes)
                    int commentNullIdx = Array.IndexOf(data, (byte)0, t + 8, 64);
                    int commentLen = commentNullIdx > t + 8 ? commentNullIdx - (t + 8) : 64;
                    string comment = Encoding.GetEncoding("ISO-8859-1").GetString(data, t + 8, Math.Min(commentLen, 64)).TrimEnd('\0');
                    
                    result[(int)number] = new Dictionary<string, object>
                    {
                        ["size"] = (int)size,
                        ["comment"] = comment
                    };
                    
                    start = (int)number + 1;
                }
            }
        }

        /// <summary>
        /// 獲取程式內容（使用第二個 Socket 連接）
        /// </summary>
        public string GetProg(string name)
        {
            string query;
            if (int.TryParse(name.Replace("O", ""), out int progNum))
            {
                query = $"O{progNum:D4}-O{progNum:D4}";
            }
            else
            {
                name = name.ToUpper();
                if (!name.StartsWith("O"))
                    name = "O" + name;
                if (!name.Contains("-"))
                    query = name + "-" + name;
                else
                    query = name;
            }
            
            Socket sock2 = null;
            try
            {
                sock2 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                sock2.ReceiveTimeout = 1000;
                sock2.SendTimeout = 1000;
                sock2.Connect(ip, port);
                
                // 打開連接
                sock2.SendAll(Encap(FTYPE_OPN_REQU, FRAME_DST2));
                byte[] respData = new byte[1500];
                int received = sock2.Receive(respData);
                Array.Resize(ref respData, received);
                Decap(respData);
                
                // 發送讀取請求
                byte[] buffer = new byte[0x204];
                buffer[0] = 0x00;
                buffer[1] = 0x00;
                buffer[2] = 0x00;
                buffer[3] = 0x01;
                byte[] queryBytes = Encoding.ASCII.GetBytes(query);
                Buffer.BlockCopy(queryBytes, 0, buffer, 4, queryBytes.Length);
                
                sock2.SendAll(Encap(0x1501, buffer));
                received = sock2.Receive(respData);
                Array.Resize(ref respData, received);
                Decap(respData);
                
                // 接收程式內容
                var contentBuilder = new StringBuilder();
                byte[] recvBuffer = new byte[1500];
                
                while (true)
                {
                    int bytesRead = sock2.Receive(recvBuffer);
                    if (bytesRead == 0) break;
                    
                    for (int i = 0; i < bytesRead;)
                    {
                        if (i + 10 > bytesRead) break;
                        
                        if (recvBuffer[i] == 0xa0 && recvBuffer[i + 1] == 0xa0 &&
                            recvBuffer[i + 2] == 0xa0 && recvBuffer[i + 3] == 0xa0)
                        {
                            ushort ftype = (ushort)((recvBuffer[i + 6] << 8) | recvBuffer[i + 7]);
                            ushort flen = (ushort)((recvBuffer[i + 8] << 8) | recvBuffer[i + 9]);
                            
                            if (ftype == 0x1604)  // 數據包
                            {
                                int contentStart = i + 10;
                                int contentLen = Math.Min(flen, bytesRead - contentStart);
                                contentBuilder.Append(Encoding.UTF8.GetString(recvBuffer, contentStart, contentLen));
                                i += 10 + flen;
                            }
                            else if (ftype == 0x1701)  // 結束包
                            {
                                sock2.SendAll(Encap(0x1702, new byte[0]));
                                return contentBuilder.ToString();
                            }
                            else
                            {
                                i++;
                            }
                        }
                        else
                        {
                            i++;
                        }
                    }
                }
                
                return contentBuilder.ToString();
            }
            catch (Exception e)
            {
                Console.WriteLine($"GetProg 錯誤: {e.Message}");
                return null;
            }
            finally
            {
                sock2?.Close();
            }
        }

        /// <summary>
        /// 上傳程式到 CNC (File I/O 模式)
        /// </summary>
        /// <param name="fullpath">完整資料夾路徑，例如 "//MEMCARD/" 或 "//CNC_MEM/USER/PATH1/"（不包含檔名）</param>
        /// <param name="content">程式內容，包含 Oxxxx</param>
        public bool UploadProg(string fullpath, string content)
        {
            // --- 1. 檢查路徑格式 ---
            if (!fullpath.StartsWith("//"))
                throw new Exception($"FULL PATH must start with '//', got: {fullpath}");

            string folder = "N:" + fullpath;  // e.g. N://MEMCARD/
            byte[] folderBytes = Encoding.UTF8.GetBytes(folder);

            Socket sock2 = null;
            try
            {
                // --- 2. 建立 socket ---
                sock2 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                sock2.ReceiveTimeout = 1000;
                sock2.SendTimeout = 1000;
                sock2.Connect(ip, port);

                // --- 3. Open Request ---
                sock2.Send(Encap(FTYPE_OPN_REQU, FRAME_DST2));
                byte[] buffer1 = new byte[1500];
                int len1 = sock2.Receive(buffer1);
                var data = Decap(buffer1.Take(len1).ToArray());

                // --- 4. Write Program Request (0x1101) ---
                byte[] buffer = new byte[0x204];
                buffer[0] = 0x00; buffer[1] = 0x00; buffer[2] = 0x00; buffer[3] = 0x01;
                Buffer.BlockCopy(folderBytes, 0, buffer, 4, folderBytes.Length);

                sock2.Send(Encap(0x1101, buffer));
                byte[] buffer2 = new byte[1500];
                int len2 = sock2.Receive(buffer2);
                data = Decap(buffer2.Take(len2).ToArray());

                // --- 5. 分段送程式內容 (0x1204) ---
                byte[] contentBytes = Encoding.UTF8.GetBytes(content);
                int pos = 0;
                int MAXLEN = 0xF0;

                while (pos < contentBytes.Length)
                {
                    int chunkSize = Math.Min(MAXLEN, contentBytes.Length - pos);
                    byte[] chunk = new byte[chunkSize];
                    Buffer.BlockCopy(contentBytes, pos, chunk, 0, chunkSize);
                    sock2.Send(Encap(0x1204, chunk));
                    pos += MAXLEN;
                }

                // --- 6. Write End (0x1301) ---
                sock2.Send(Encap(0x1301, new byte[0]));

                // --- 7. CNC 回應 (1302 or 1404) ---
                byte[] buffer3 = new byte[1500];
                int len3 = sock2.Receive(buffer3);
                data = Decap(buffer3.Take(len3).ToArray());

                if (data == null || !data.ContainsKey("ftype"))
                    throw new Exception("CNC did not respond after 1301 (Write End).");

                ushort ftype = (ushort)data["ftype"];

                // 成功
                if (ftype == 0x1302)
                    return true;

                // 錯誤：1404
                if (ftype == 0x1404)
                {
                    byte[] raw = (byte[])data["data"];
                    ushort errorCode = (ushort)((raw[0] << 8) | raw[1]);
                    ushort subcode = (ushort)((raw[2] << 8) | raw[3]);
                    ushort detail = (ushort)((raw[4] << 8) | raw[5]);

                    if (errorCode == 0x2006 && subcode == 0x0005 && detail == 0x0004)
                        throw new Exception("CNC Error 1404: File already exists and cannot be overwritten.");

                    throw new Exception($"CNC Error 1404: Write failed (code=0x{errorCode:X4}, sub=0x{subcode:X4}, detail=0x{detail:X4})");
                }

                // 未知回應
                throw new Exception($"Unexpected CNC response: ftype=0x{ftype:X4}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"UploadProg 錯誤: {e.Message}");
                throw;
            }
            finally
            {
                sock2?.Close();
            }
        }

        /// <summary>
        /// 刪除程式
        /// </summary>
        public bool DeleteProg(string fullpath)
        {
            if (!fullpath.StartsWith("//"))
                throw new Exception("FULL PATH must start with '//'");
            
            byte[] buffer = new byte[0x100];
            byte[] pathBytes = Encoding.UTF8.GetBytes(fullpath);
            Buffer.BlockCopy(pathBytes, 0, buffer, 0, pathBytes.Length);
            
            var st = ReqRdSingle(1, 1, 0xb6, 0, 0, 0, 0, 256, buffer);
            
            if (st != null && st.ContainsKey("len") && (int)st["len"] >= 0)
                return true;
            
            if (st != null && st.ContainsKey("error"))
                throw new Exception($"Delete failed, error={st["error"]}");
            
            throw new Exception("Delete failed (unknown error)");
        }

        /// <summary>
        /// 大小端轉換
        /// </summary>
        private ushort ReverseBytes(ushort value)
        {
            return (ushort)(((value & 0xFF) << 8) | ((value >> 8) & 0xFF));
        }

        private int ReverseBytes(int value)
        {
            return (int)(((value & 0xFF) << 24) |
                        (((value >> 8) & 0xFF) << 16) |
                        (((value >> 16) & 0xFF) << 8) |
                        ((value >> 24) & 0xFF));
        }

        private uint ReverseBytes(uint value)
        {
            return ((value & 0xFF) << 24) |
                   (((value >> 8) & 0xFF) << 16) |
                   (((value >> 16) & 0xFF) << 8) |
                   ((value >> 24) & 0xFF);
        }
    }

    /// <summary>
    /// Socket 擴充方法
    /// </summary>
    public static class SocketExtensions
    {
        public static void SendAll(this Socket socket, byte[] data)
        {
            int sent = 0;
            while (sent < data.Length)
            {
                sent += socket.Send(data, sent, data.Length - sent, SocketFlags.None);
            }
        }

        public static void ReceiveAll(this Socket socket, byte[] buffer)
        {
            int received = 0;
            while (received < buffer.Length)
            {
                int bytesRead = socket.Receive(buffer, received, buffer.Length - received, SocketFlags.None);
                if (bytesRead == 0)
                {
                    throw new SocketException((int)SocketError.ConnectionReset);
                }
                received += bytesRead;
            }
        }
    }
}