namespace CsFanuc
{
    public class StatusInfo
    {
        public ushort Auto { get; set; }
        public ushort Run { get; set; }
        public ushort Motion { get; set; }
        public ushort Mstb { get; set; }
        public ushort Emergency { get; set; }
        public ushort Alarm { get; set; }
        public ushort Edit { get; set; }

        public override string ToString()
        {
            return $"Auto: {Auto}, Run: {Run}, Motion: {Motion}, Emergency: {Emergency}, Alarm: {Alarm}";
        }
    }
}