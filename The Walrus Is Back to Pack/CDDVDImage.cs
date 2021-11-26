using System;
using System.IO;
using System.Text;
using System.Linq;

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
                    //3DO ISO
                    br.BaseStream.Seek(0, SeekOrigin.Begin);
                    byte[] _3doiso = br.ReadBytes(7);
                    if (_3doiso.SequenceEqual(new byte[] { 0x01, 0x5A, 0x5A, 0x5A, 0x5A, 0x5A, 0x01 }))
                    {
                        return "iso";
                    }

                    //3DO RAW
                    br.BaseStream.Seek(16, SeekOrigin.Begin);
                    byte[] _3doraw = br.ReadBytes(7);
                    if (_3doraw.SequenceEqual(new byte[] { 0x01, 0x5A, 0x5A, 0x5A, 0x5A, 0x5A, 0x01 }))
                    {
                        return "raw";
                    }

                    //PS2 PSP ISO
                    br.BaseStream.Seek(32769, SeekOrigin.Begin);
                    string _iso = Encoding.ASCII.GetString(br.ReadBytes(5));
                    if (_iso == "CD001")
                    {
                        return "iso";
                    }

                    //PLAYSTATION

                    //PS2 RAW
                    br.BaseStream.Seek(37657, SeekOrigin.Begin);
                    string _raw = Encoding.ASCII.GetString(br.ReadBytes(5));

                    if (_raw == "CD001")
                    {
                        return "raw";
                    }
                }
            }
            return "file";
        }
    }
}