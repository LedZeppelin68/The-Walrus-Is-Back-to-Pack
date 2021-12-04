using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Xml;

namespace The_Walrus_Is_Back_to_Pack
{
    internal class SevenZip
    {
        internal static MemoryStream CompressStream(MemoryStream input_stream)
        {
            MemoryStream output_stream = new MemoryStream();
            using (Process seven_zip = new Process())
            {
                seven_zip.StartInfo.FileName = "7za";
                seven_zip.StartInfo.Arguments = "a -txz -mx9 -an -si -so";
                seven_zip.StartInfo.RedirectStandardInput = true;
                seven_zip.StartInfo.RedirectStandardOutput = true;
                seven_zip.StartInfo.UseShellExecute = false;
                seven_zip.StartInfo.CreateNoWindow = true;

                Console.InputEncoding = System.Text.Encoding.ASCII;
                seven_zip.Start();

                input_stream.Position = 0;

                Task input_task = Task.Run(() =>
                {
                    input_stream.CopyTo(seven_zip.StandardInput.BaseStream);
                    seven_zip.StandardInput.Close();
                });

                Task output_task = Task.Run(() =>
                {
                    seven_zip.StandardOutput.BaseStream.CopyTo(output_stream);
                });

                Task.WaitAll(input_task, output_task);

                seven_zip.WaitForExit();
            }
            return output_stream;
        }

        internal static void PackChunk(MemoryStream input_chunk, BinaryWriter output_file)
        {
            using (Process seven_zip = new Process())
            {
                seven_zip.StartInfo.FileName = "7za";
                seven_zip.StartInfo.Arguments = "a -txz -mx9 -an -si -so"; // -m0=LZMA2:d27 -m0=LZMA2:d=26:fb=273
                seven_zip.StartInfo.RedirectStandardInput = true;
                seven_zip.StartInfo.RedirectStandardOutput = true;
                seven_zip.StartInfo.UseShellExecute = false;
                seven_zip.StartInfo.CreateNoWindow = true;

                Console.InputEncoding = System.Text.Encoding.ASCII;
                seven_zip.Start();
                System.Text.Encoding g = seven_zip.StandardInput.Encoding;

                Task input_task = Task.Run(() =>
                {
                    input_chunk.CopyTo(seven_zip.StandardInput.BaseStream);
                    seven_zip.StandardInput.Close();
                });

                Task output_task = Task.Run(() =>
                {
                    seven_zip.StandardOutput.BaseStream.CopyTo(output_file.BaseStream);
                });

                Task.WaitAll(input_task, output_task);

                seven_zip.WaitForExit();
            }
        }

        internal static void UnpackBuffer(string input_file, MemoryStream output_stream, XmlNode partition_xml)
        {
            long partition_offset = Convert.ToInt64(partition_xml.Attributes["offset"].Value);
            int partition_length = Convert.ToInt32(partition_xml.Attributes["c_length"].Value);
            int partition_ulength = Convert.ToInt32(partition_xml.Attributes["u_length"].Value);

            using (Process seven_zip = new Process())
            {
                seven_zip.StartInfo.FileName = "7za";
                seven_zip.StartInfo.Arguments = "e -txz -si -so";
                seven_zip.StartInfo.RedirectStandardInput = true;
                seven_zip.StartInfo.RedirectStandardOutput = true;
                seven_zip.StartInfo.RedirectStandardError = true;
                seven_zip.StartInfo.CreateNoWindow = true;
                seven_zip.StartInfo.UseShellExecute = false;
                seven_zip.StartInfo.StandardOutputEncoding = System.Text.Encoding.ASCII;
                //seven_zip.ErrorDataReceived += (sender, EventArgs) =>
                //{
                //    Console.WriteLine(EventArgs.Data);
                //};

                seven_zip.Start();
                //seven_zip.BeginErrorReadLine();

                Task input_task = Task.Run(() =>
                {
                    using (BinaryReader input_file_reader = new BinaryReader(new FileStream(input_file, FileMode.Open)))
                    {
                        input_file_reader.BaseStream.Seek(partition_offset, SeekOrigin.Begin);

                        using (BinaryWriter standard_input_writer = new BinaryWriter(seven_zip.StandardInput.BaseStream))
                        {
                            standard_input_writer.Write(input_file_reader.ReadBytes(partition_length));
                        }
                    }
                });

                Task output_task = Task.Run(() =>
                {
                    seven_zip.StandardOutput.BaseStream.CopyTo(output_stream);
                });

                Task.WaitAll(input_task, output_task);

                seven_zip.WaitForExit();
            }
        }
    }
}