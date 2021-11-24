using System;
using System.Diagnostics;
using System.IO;
using System.Xml;

namespace The_Walrus_Is_Back_to_Pack
{
    internal class SevenZip
    {
        internal static MemoryStream CompressStream(ref MemoryStream all_maps)
        {
            MemoryStream c_maps = new MemoryStream();
            using (Process sevenzip = new Process())
            {
                sevenzip.StartInfo.FileName = "7z.exe";
                sevenzip.StartInfo.Arguments = "a -txz -mx9 -an -si -so";
                sevenzip.StartInfo.RedirectStandardInput = true;
                sevenzip.StartInfo.RedirectStandardOutput = true;
                sevenzip.StartInfo.UseShellExecute = false;
                sevenzip.StartInfo.CreateNoWindow = true;

                sevenzip.Start();

                all_maps.Position = 0;
                all_maps.CopyTo(sevenzip.StandardInput.BaseStream);
                sevenzip.StandardInput.Close();

                BinaryReader szr = new BinaryReader(sevenzip.StandardOutput.BaseStream);

                byte[] _tmp7z;

                do
                {
                    _tmp7z = szr.ReadBytes(2048);
                    c_maps.Write(_tmp7z, 0, _tmp7z.Length);
                }
                while (_tmp7z.Length == 2048);

                c_maps.Position = 0;

                sevenzip.WaitForExit();
            }
            return c_maps;
        }

        internal static void PackChunk(ref BinaryWriter mr, ref BinaryWriter tr)
        {
            throw new NotImplementedException();
        }

        internal static void UnpackBuffer(string mrg_arc, ref MemoryStream all_maps, XmlNode map_xml)
        {
            long partition_offset = Convert.ToInt64(map_xml.Attributes["offset"].Value);
            int partition_length = Convert.ToInt32(map_xml.Attributes["c_length"].Value);
            int partition_ulength = Convert.ToInt32(map_xml.Attributes["u_length"].Value);

            using (Process zip = new Process())
            {
                zip.StartInfo.FileName = "7z.exe";
                zip.StartInfo.Arguments = string.Format("e -txz -si -so", mrg_arc);
                zip.StartInfo.RedirectStandardInput = true;
                zip.StartInfo.RedirectStandardOutput = true;
                zip.StartInfo.CreateNoWindow = true;
                zip.StartInfo.UseShellExecute = false;

                zip.Start();

                using (BinaryReader br = new BinaryReader(new FileStream(mrg_arc, FileMode.Open)))
                {
                    br.BaseStream.Seek(partition_offset, SeekOrigin.Begin);
                    //br.BaseStream.CopyTo(zip.StandardInput.BaseStream);

                    using (BinaryWriter stdinw = new BinaryWriter(zip.StandardInput.BaseStream))
                    {
                        stdinw.Write(br.ReadBytes(partition_length));
                    }
                }
                //zip.StandardInput.Close();

                using (BinaryReader bo = new BinaryReader(zip.StandardOutput.BaseStream))
                {
                    //byte[] _buffer2;
                    //using (BinaryWriter filer = new BinaryWriter(data_buffer))
                    //{

                    //do
                    //{
                    byte[] _buffer2 = bo.ReadBytes(partition_ulength);
                    //filer.Write(_buffer2);
                    all_maps.Write(_buffer2, 0, partition_ulength);
                    //}
                    //while (_buffer2.Length == 1024 * 1024 * 64);
                    //}
                }

                zip.WaitForExit();
            }
        }
    }
}