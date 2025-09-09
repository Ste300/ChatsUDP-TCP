public class ReceivedFile
{
    public string FileName;
    public byte[] Data;

    public ReceivedFile(string fileName, byte[] data)
    {
        FileName = fileName;
        Data = data;
    }
}
