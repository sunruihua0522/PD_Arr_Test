namespace PLC_Test_PD_Array.Instrument
{
    public interface ICommunationPort
    {
        void Write(string Command);
        string Read();
    }
}
