using System;
using System.Diagnostics;
using System.IO;

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
    }
}