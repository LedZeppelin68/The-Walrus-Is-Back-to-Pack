using System;
using System.IO;
using System.Text;

namespace The_Walrus_Is_Back_to_Pack
{
    internal class CDDVDImage
    {
        internal static string CheckType(string image)
        {
            string type = string.Empty;
            using (BinaryReader br = new BinaryReader(new FileStream(image, FileMode.Open)))
            {
                if (br.BaseStream.Length > 32768)
                {
                    br.BaseStream.Seek(32769, SeekOrigin.Begin);
                    string mwiso = Encoding.ASCII.GetString(br.ReadBytes(5));
                    if (mwiso == "CD001")
                    {
                        return "iso";
                    }

                    br.BaseStream.Seek(37657, SeekOrigin.Begin);
                    string mwbin = Encoding.ASCII.GetString(br.ReadBytes(5));

                    if (mwbin == "CD001")
                    {
                        return "bin";
                    }
                }
            }
            return "file";
        }
    }
}