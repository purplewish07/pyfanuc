namespace CsFanuc
{
    public class SystemInfo
    {
        public ushort AddInfo { get; set; }
        public ushort MaxAxis { get; set; }
        public string CncType { get; set; }
        public string MtType { get; set; }
        public string Series { get; set; }
        public string Version { get; set; }
        public string Axes { get; set; }

        public override string ToString()
        {
            return $"CncType: {CncType}, Series: {Series}, MaxAxis: {MaxAxis}, Version: {Version}";
        }
    }
}