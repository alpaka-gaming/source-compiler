using System.IO;

namespace SourceSDK.Models
{
    public class FileData
    {
        public DataBlock headnode;

        public FileData(string filename)
        {
            var s = new StreamReader(filename);
            headnode = DataBlock.FromStream(ref s);
            s.Close();
        }
    }
}
