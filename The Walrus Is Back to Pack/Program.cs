using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Xml;
using System.IO.Compression;

namespace The_Walrus_Is_Back_to_Pack
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Return of the Walrus by LedZeppelin68\r\n");
            bool verbose = true;

            foreach (string working_dir in args)
            {
                List<string> images = new List<string>();
                images.AddRange(Directory.GetFiles(working_dir, "*.*", SearchOption.AllDirectories));

                Dictionary<string, int> dupes = new Dictionary<string, int>();

                XmlDocument main_spec = new XmlDocument();
                main_spec.LoadXml("<root />");

                XmlElement partitions_xml = main_spec.CreateElement("partitions");
                main_spec.DocumentElement.AppendChild(partitions_xml);

                XmlElement files_xml = main_spec.CreateElement("entries");
                main_spec.DocumentElement.AppendChild(files_xml);

                BinaryWriter tr = new BinaryWriter(new FileStream(string.Format(Path.GetFileName(working_dir) + ".mrg"), FileMode.Create));
                tr.BaseStream.Seek(32, SeekOrigin.Begin);

                MemoryStream _buffer = new MemoryStream();
                BinaryWriter mr = new BinaryWriter(_buffer);

                MemoryStream all_maps = new MemoryStream();
                XmlElement map_partition_xml = main_spec.CreateElement("partition");

                if (verbose)
                {
                    Console.WriteLine(string.Format("Deduplicating and Packing the Contents of {0}\n\r", Path.GetFileName(working_dir)));
                }

                int counter = 0;
                int chunk_n = 0;
                DateTime job_time = DateTime.Now;

                for (int image = 0; image < images.Count; image++)
                {
                    if (verbose) Console.WriteLine(string.Format("Processing file: {0}", Path.GetFileName(images[image])));

                    bool last = (images.Last() == images[image]) ? true : false;

                    string image_type = CheckImageType(images[image]);

                    int block_size = 0;
                    switch (image_type)
                    {
                        case "file":
                        case "iso":
                            block_size = 2048;
                            break;
                        case "bin":
                            block_size = 2352;
                            break;
                    }

                    BinaryWriter image_map = new BinaryWriter(new MemoryStream());

                    using (BinaryReader br = new BinaryReader(new FileStream(images[image], FileMode.Open)))
                    {
                        DateTime image_time = DateTime.Now;

                        while (br.BaseStream.Position != br.BaseStream.Length)
                        {
                            byte[] _temp = br.ReadBytes(block_size);
                            if (_temp.Length < 2048)
                            {
                                byte[] _zero = new byte[2048];
                                _temp.CopyTo(_zero, 0);
                                _temp = _zero;
                            }
                            string hash = string.Empty;
                            switch (block_size)
                            {
                                case 2048:
                                    hash = BitConverter.ToString(MD5.Create().ComputeHash(_temp, 0, 2048));
                                    break;
                                case 2352:
                                    hash = BitConverter.ToString(MD5.Create().ComputeHash(_temp, 24, 2048));
                                    break;
                            }

                            if (dupes.ContainsKey(hash))
                            {
                                image_map.Write(dupes[hash]);
                            }
                            else
                            {
                                dupes.Add(hash, counter);
                                image_map.Write(counter);
                                switch (block_size)
                                {
                                    case 2048:
                                        mr.Write(_temp, 0, 2048);
                                        break;
                                    case 2352:
                                        mr.Write(_temp, 24, 2048);
                                        break;
                                }
                                counter++;
                            }

                            if (mr.BaseStream.Length == 1024 * 1024 * 64 || (br.BaseStream.Position == br.BaseStream.Length && last))
                            {
                                if (verbose) Console.Write("Packing chunk {0}...", chunk_n);

                                DateTime chunk_start = DateTime.Now;
                                long chunk_length = mr.BaseStream.Length;

                                XmlElement partition_xml = main_spec.CreateElement("partition");
                                partitions_xml.AppendChild(partition_xml);
                                partition_xml.SetAttribute("offset", tr.BaseStream.Position.ToString());
                                partition_xml.SetAttribute("u_length", mr.BaseStream.Position.ToString());

                                long partition_offset = tr.BaseStream.Position;

                                using (Process sevenzip = new Process())
                                {
                                    sevenzip.StartInfo.FileName = "7z.exe";
                                    sevenzip.StartInfo.Arguments = "a -txz -mx9 -an -si -so"; // -m0=LZMA2:d27 
                                    sevenzip.StartInfo.RedirectStandardInput = true;
                                    sevenzip.StartInfo.RedirectStandardOutput = true;
                                    sevenzip.StartInfo.UseShellExecute = false;
                                    sevenzip.StartInfo.CreateNoWindow = true;

                                    sevenzip.Start();

                                    mr.BaseStream.Position = 0;
                                    mr.BaseStream.CopyTo(sevenzip.StandardInput.BaseStream);
                                    sevenzip.StandardInput.Close();

                                    BinaryReader szr = new BinaryReader(sevenzip.StandardOutput.BaseStream);

                                    byte[] _tmp7z;

                                    do
                                    {
                                        _tmp7z = szr.ReadBytes(1024 * 1024 * 32);
                                        tr.Write(_tmp7z);
                                    }
                                    while (_tmp7z.Length == 1024 * 1024 * 32);


                                    mr.BaseStream.SetLength(0);

                                    sevenzip.WaitForExit();
                                }

                                partition_xml.SetAttribute("c_length", (tr.BaseStream.Position - partition_offset).ToString());
                                long c_chunk_length = tr.BaseStream.Position - partition_offset;
                                if (verbose) Console.WriteLine("\rPacked chunk {0} in {1}", chunk_n++, DateTime.Now - chunk_start);
                            }
                        }

                        Console.WriteLine(string.Format("\r\nProcessed: {0} in {1}\r\n", Path.GetFileName(images[image]), DateTime.Now - image_time));
                    }

                    XmlElement image_xml = main_spec.CreateElement("entry");
                    files_xml.AppendChild(image_xml);
                    image_xml.SetAttribute("type", "file");
                    image_xml.SetAttribute("name", Path.GetFileName(images[image]));
                    image_xml.SetAttribute("map_offset", all_maps.Position.ToString());
                    image_xml.SetAttribute("map_length", image_map.BaseStream.Length.ToString());

                    image_map.BaseStream.Position = 0;
                    image_map.BaseStream.CopyTo(all_maps);
                    image_map.Close();
                }

                MemoryStream c_all_maps = SevenZip.CompressStream(ref all_maps);
                map_partition_xml.SetAttribute("type", "map");
                map_partition_xml.SetAttribute("offset", tr.BaseStream.Position.ToString());
                map_partition_xml.SetAttribute("c_length", c_all_maps.Length.ToString());
                map_partition_xml.SetAttribute("u_length", all_maps.Length.ToString());
                partitions_xml.AppendChild(map_partition_xml);
                c_all_maps.CopyTo(tr.BaseStream);


                byte[] packed_xml = PackXML(main_spec);
                byte[] md5_xml = MD5.Create().ComputeHash(packed_xml, 0, packed_xml.Length);
                long xml_offset = tr.BaseStream.Position;
                int xml_length = (int)packed_xml.Length;
                tr.Write(packed_xml);

                tr.BaseStream.Seek(0, SeekOrigin.Begin);
                tr.Write(Encoding.ASCII.GetBytes("MRG1"));
                tr.Write(xml_offset);
                tr.Write(xml_length);
                tr.Write(md5_xml);
                tr.Close();

                //main_spec.Save(string.Format("{0}.xml", Path.GetFileName(working_dir)));

                if (verbose) Console.WriteLine(string.Format("Job completed in {0}", DateTime.Now - job_time));
            }
            Console.ReadKey();
        }

        private static byte[] PackXML(XmlDocument main_spec)
        {
            byte[] _buffer = Encoding.UTF8.GetBytes(main_spec.OuterXml);

            using (MemoryStream compressed_xml = new MemoryStream())
            {
                using (GZipStream compressor = new GZipStream(compressed_xml, CompressionLevel.Optimal))
                {
                    compressor.Write(_buffer, 0, _buffer.Length);
                }
                return compressed_xml.ToArray();
            }
        }

        private static string CheckImageType(string image)
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
