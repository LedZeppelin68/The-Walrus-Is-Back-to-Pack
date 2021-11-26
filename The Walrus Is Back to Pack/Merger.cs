using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace The_Walrus_Is_Back_to_Pack
{
    internal class Merger
    {
        internal static uint[] edc_lut = new uint[256];
        internal static byte[] ecc_f_lut = new byte[256];
        internal static byte[] ecc_b_lut = new byte[256];


        internal static void Pack(string working_dir, Options merge_options)
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

            //MemoryStream _buffer = new MemoryStream();
            //BinaryWriter mr = new BinaryWriter(_buffer);
            MemoryStream mr = new MemoryStream();


            MemoryStream all_maps = new MemoryStream();
            XmlElement map_partition_xml = main_spec.CreateElement("partition");

            if (merge_options.verbose)
            {
                Console.WriteLine(string.Format("Deduplicating and Packing the Contents of {0}\n\r", Path.GetFileName(working_dir)));
            }

            int counter = 0;
            int chunk_n = 0;
            DateTime job_time = DateTime.Now;

            for (int image = 0; image < images.Count; image++)
            {
                if (merge_options.verbose) Console.WriteLine(string.Format("Processing file: {0}", Path.GetFileName(images[image])));

                bool last = (images.Last() == images[image]) ? true : false;

                string image_type = CDDVDImage.CheckType(images[image]);

                int block_size = 0;
                switch (image_type)
                {
                    case "file":
                    case "iso":
                        block_size = 2048;
                        break;
                    case "raw":
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
                                switch (_temp[15])
                                {
                                    case 1:
                                        hash = BitConverter.ToString(MD5.Create().ComputeHash(_temp, 16, 2048));
                                        break;
                                    case 2:
                                        hash = BitConverter.ToString(MD5.Create().ComputeHash(_temp, 24, 2048));
                                        break;
                                }
                                break;
                        }

                        if (dupes.ContainsKey(hash))
                        {
                            switch(block_size)
                            {
                                case 2352:
                                    switch (_temp[15])
                                    {
                                        case 1:
                                            image_map.Write(_temp, 12, 4);
                                            break;
                                        case 2:
                                            break;
                                    }
                                    break;
                            }
                            image_map.Write(dupes[hash]);
                        }
                        else
                        {
                            dupes.Add(hash, counter);
                            
                            switch (block_size)
                            {
                                case 2048:
                                    mr.Write(_temp, 0, 2048);
                                    image_map.Write(counter);
                                    break;
                                case 2352:
                                    switch (_temp[15])
                                    {
                                        case 1:
                                            mr.Write(_temp, 16, 2048);
                                            image_map.Write(_temp, 12, 4);
                                            image_map.Write(counter);
                                            break;
                                        case 2:
                                            mr.Write(_temp, 24, 2048);
                                            break;
                                    }
                                    break;
                            }
                            counter++;
                        }

                        if (mr.Length == 1024 * 1024 * 64 || (br.BaseStream.Position == br.BaseStream.Length && last))
                        {
                            if (merge_options.verbose) Console.Write("Packing chunk {0}...", chunk_n);

                            DateTime chunk_start = DateTime.Now;
                            long chunk_length = mr.Length;

                            XmlElement partition_xml = main_spec.CreateElement("partition");
                            partitions_xml.AppendChild(partition_xml);
                            partition_xml.SetAttribute("type", "form1");
                            partition_xml.SetAttribute("offset", tr.BaseStream.Position.ToString());
                            partition_xml.SetAttribute("u_length", mr.Position.ToString());

                            long partition_offset = tr.BaseStream.Position;

                            mr.Position = 0;

                            //SevenZip.PackChunk(ref mr, ref tr);

                            using (Process sevenzip = new Process())
                            {
                                sevenzip.StartInfo.FileName = "7z";
                                sevenzip.StartInfo.Arguments = "a -txz -mx9 -an -si -so"; // -m0=LZMA2:d27 
                                sevenzip.StartInfo.RedirectStandardInput = true;
                                sevenzip.StartInfo.RedirectStandardOutput = true;
                                sevenzip.StartInfo.UseShellExecute = false;
                                sevenzip.StartInfo.CreateNoWindow = true;

                                sevenzip.Start();

                                var inputTask = Task.Run(() =>
                                {
                                    mr.CopyTo(sevenzip.StandardInput.BaseStream);
                                    sevenzip.StandardInput.Close();
                                });

                                var outputTask = Task.Run(() =>
                                {
                                    sevenzip.StandardOutput.BaseStream.CopyTo(tr.BaseStream);
                                });

                                Task.WaitAll(inputTask, outputTask);

                                sevenzip.WaitForExit();
                            }

                            mr.SetLength(0);

                            partition_xml.SetAttribute("c_length", (tr.BaseStream.Position - partition_offset).ToString());
                            long c_chunk_length = tr.BaseStream.Position - partition_offset;
                            if (merge_options.verbose) Console.WriteLine("\rPacked chunk {0} in {1}", chunk_n++, DateTime.Now - chunk_start);
                        }
                    }

                    Console.WriteLine(string.Format("\r\nProcessed: {0} in {1}\r\n", Path.GetFileName(images[image]), DateTime.Now - image_time));
                }

                XmlElement image_xml = main_spec.CreateElement("entry");
                files_xml.AppendChild(image_xml);
                image_xml.SetAttribute("type", "file");
                //image_type
                image_xml.SetAttribute("mode", image_type);
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

            if (merge_options.xml) main_spec.Save(string.Format("{0}.xml", Path.GetFileName(working_dir)));

            if (merge_options.verbose) Console.WriteLine(string.Format("Job completed in {0}", DateTime.Now - job_time));
        }

        internal static void UnPack(string mrg_arc, Options merge_options)
        {
            using (BinaryReader br = new BinaryReader(new FileStream(mrg_arc, FileMode.Open)))
            {
                string magic_word = Encoding.ASCII.GetString(br.ReadBytes(4));
                if (magic_word != "MRG1") return;
            }

            FillEDCECCLuts();

            XmlDocument xml = new XmlDocument();
            xml.LoadXml(GetMainXml(mrg_arc));

            XmlNodeList entries = xml.DocumentElement.GetElementsByTagName("entry");

            Console.WriteLine(string.Format("Listing content of \"{0}\"\n\r", Path.GetFileName(mrg_arc)));
            for (int i = 0; i < entries.Count; i++)
            {
                string name = entries[i].Attributes["name"].Value;
                string type = entries[i].Attributes["type"].Value;

                Console.WriteLine(string.Format("[{0}]: {1}", i, name));
            }
            Console.Write("Choose entry: ");
            string selection = Console.ReadLine();

            List<XmlNode> entries_to_unpack = new List<XmlNode>();
            if (selection.ToLower() == "all")
            {
                foreach (XmlNode node in entries)
                {
                    entries_to_unpack.Add(node);
                }
            }
            else
            {
                entries_to_unpack.Add(entries[int.Parse(selection)]);
            }

            foreach (XmlNode entry in entries_to_unpack)
            {
                List<XmlNode> files = new List<XmlNode>();

                if (entry.Attributes["type"].Value == "file")
                {
                    files.Add(entry);
                }
                else
                {
                    XmlNodeList all_files = entry.SelectNodes("file");
                    foreach (XmlNode file in all_files)
                    {
                        files.Add(file);
                    }
                }

                XmlNodeList partitions = xml.DocumentElement.GetElementsByTagName("partition");
                MemoryStream all_maps = ReadMap(mrg_arc, xml);
                using (BinaryReader mapr = new BinaryReader(all_maps))
                {
                    foreach (XmlNode file in files)
                    {
                        if (merge_options.verbose) Console.WriteLine(string.Format("\r\nExtracting \"{0}\"", file.Attributes["name"].Value));

                        string file_name = file.Attributes["name"].Value;
                        using (BinaryWriter dataout = new BinaryWriter(new FileStream(file_name, FileMode.Create)))
                        {
                            int buffer_length = 67108864;

                            MemoryStream data_buffer = new MemoryStream();

                            DateTime start_time = DateTime.Now;

                            List<scenario> chain = new List<scenario>();

                            long map_offset = long.Parse(file.Attributes["map_offset"].Value);
                            int map_length = int.Parse(file.Attributes["map_length"].Value);
                            string mode = file.Attributes["mode"].Value;

                            mapr.BaseStream.Seek(map_offset, SeekOrigin.Begin);

                            long position = 0;
                            switch (mode)
                            {
                                case "iso":

                                    while (mapr.BaseStream.Position != map_offset + map_length)
                                    {
                                        scenario _chain = new scenario();

                                        _chain.data_offset = (ulong)(position++ * 2048);
                                        //_chain.msfmode = mapr.ReadBytes(4);

                                        //switch (_chain.msfmode[3])
                                        //{
                                          //  case 1:
                                                long offset = mapr.ReadUInt32() * 2048;

                                                _chain.partition = (uint)(offset / buffer_length);
                                                _chain.partititon_offset = (uint)(offset % buffer_length);
                                            //    break;
                                        //}
                                        chain.Add(_chain);
                                    }
                                    break;
                                case "raw":
                                    
                                    while (mapr.BaseStream.Position != map_offset + map_length)
                                    {
                                        scenario _chain = new scenario();

                                        _chain.data_offset = (ulong)(position++ * 2352);
                                        _chain.msfmode = mapr.ReadBytes(4);

                                        switch (_chain.msfmode[3])
                                        {
                                            case 1:
                                                long offset = mapr.ReadUInt32() * 2048;

                                                _chain.partition = (uint)(offset / buffer_length);
                                                _chain.partititon_offset = (uint)(offset % buffer_length);
                                                break;
                                        }
                                        chain.Add(_chain);
                                    }
                                    break;
                            }
                            scenario[] chain_sorted = chain.OrderBy(x => x.partition).ThenBy(y => y.data_offset).ToArray();
                            int current_partition = 0;
                            for (int i = 0; i < chain_sorted.Length; i++)
                            {
                                if (current_partition != chain_sorted[i].partition || data_buffer.Length == 0)
                                {
                                    data_buffer.SetLength(0);
                                    SevenZip.UnpackBuffer(mrg_arc, ref data_buffer, partitions[(int)chain_sorted[i].partition]);
                                    current_partition = (int)chain_sorted[i].partition;
                                }

                                data_buffer.Seek(chain_sorted[i].partititon_offset, SeekOrigin.Begin);

                                switch (mode)
                                {
                                    case "iso":
                                        byte[] iso_out_buffer = new byte[2048];
                                        data_buffer.Read(iso_out_buffer, 0, 2048);
                                        dataout.BaseStream.Seek((long)chain_sorted[i].data_offset, SeekOrigin.Begin);
                                        dataout.Write(iso_out_buffer);
                                        break;
                                    case "raw":
                                        byte[] raw_out_buffer = new byte[2352];
                                        new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff }.CopyTo(raw_out_buffer, 1);
                                        chain_sorted[i].msfmode.CopyTo(raw_out_buffer, 12);
                                        data_buffer.Read(raw_out_buffer, 16, 2048);
                                        CalculateEDC(ref raw_out_buffer, 0, 2064);
                                        CalculateECCP(ref raw_out_buffer);
                                        CalculateECCQ(ref raw_out_buffer);
                                        dataout.BaseStream.Seek((long)chain_sorted[i].data_offset, SeekOrigin.Begin);
                                        dataout.Write(raw_out_buffer);
                                        break;
                                }
                            }

                            Console.WriteLine(string.Format("{0}", DateTime.Now - start_time));
                        }
                    }
                }
            }
        }

        private static void CalculateECCQ(ref byte[] raw_out_buffer)
        {
            UInt32 major_count, minor_count, major_mult, minor_inc;
            major_count = 52;
            minor_count = 43;
            major_mult = 86;
            minor_inc = 88;

            var eccsize = major_count * minor_count;
            UInt32 major, minor;
            for (major = 0; major < major_count; major++)
            {
                var index = (major >> 1) * major_mult + (major & 1);
                byte ecc_a = 0;
                byte ecc_b = 0;
                for (minor = 0; minor < minor_count; minor++)
                {
                    byte temp = raw_out_buffer[12 + index];
                    index += minor_inc;
                    if (index >= eccsize) index -= eccsize;
                    ecc_a ^= temp;
                    ecc_b ^= temp;
                    ecc_a = ecc_f_lut[ecc_a];
                }
                ecc_a = ecc_b_lut[ecc_f_lut[ecc_a] ^ ecc_b];
                raw_out_buffer[2076 + 172 + major] = ecc_a;
                raw_out_buffer[2076 + 172 + major + major_count] = (byte)(ecc_a ^ ecc_b);
            }
        }

        private static void CalculateECCP(ref byte[] raw_out_buffer)
        {
            UInt32 major_count, minor_count, major_mult, minor_inc;
            major_count = 86;
            minor_count = 24;
            major_mult = 2;
            minor_inc = 86;

            var eccsize = major_count * minor_count;
            UInt32 major, minor;
            for (major = 0; major < major_count; major++)
            {
                var index = (major >> 1) * major_mult + (major & 1);
                byte ecc_a = 0;
                byte ecc_b = 0;
                for (minor = 0; minor < minor_count; minor++)
                {
                    byte temp = raw_out_buffer[12 + index];
                    index += minor_inc;
                    if (index >= eccsize) index -= eccsize;
                    ecc_a ^= temp;
                    ecc_b ^= temp;
                    ecc_a = ecc_f_lut[ecc_a];
                }
                ecc_a = ecc_b_lut[ecc_f_lut[ecc_a] ^ ecc_b];
                raw_out_buffer[2076 + major] = ecc_a;
                raw_out_buffer[2076 + major + major_count] = (byte)(ecc_a ^ ecc_b);
            }
        }

        private static void FillEDCECCLuts()
        {
            UInt32 k, l, m;

            for (k = 0; k < 256; k++)
            {
                l = (UInt32)((k << 1) ^ ((k & 0x80) != 0 ? 0x11d : 0));
                ecc_f_lut[k] = (byte)l;
                ecc_b_lut[k ^ l] = (byte)k;
                m = k;

                for (l = 0; l < 8; l++)
                {
                    m = (m >> 1) ^ ((m & 1) != 0 ? 0xd8018001 : 0);
                }
                edc_lut[k] = m;
            }
        }

        private static void CalculateEDC(ref byte[] raw_out_buffer, int offset, int length)
        {
            UInt32 edc = 0;
            int i = 0;

            while (i != length)
            {
                edc = (UInt32)((edc >> 8) ^ edc_lut[(edc ^ (raw_out_buffer[offset + i++])) & 0xff]);
            }
            BitConverter.GetBytes(edc).CopyTo(raw_out_buffer, length);
        }

        private static MemoryStream ReadMap(string mrg_arc, XmlDocument xml)
        {
            XmlNode map_xml = xml.DocumentElement.SelectSingleNode("partitions/partition[@type='map']");

            MemoryStream all_maps = new MemoryStream();
            SevenZip.UnpackBuffer(mrg_arc, ref all_maps, map_xml);

            return all_maps;
        }

        private static string GetMainXml(string mrg_arc)
        {
            using (BinaryReader br = new BinaryReader(new FileStream(mrg_arc, FileMode.Open)))
            {
                br.BaseStream.Seek(4, SeekOrigin.Begin);
                long xml_offset = br.ReadInt64();
                int xml_length = br.ReadInt32();

                br.BaseStream.Seek(xml_offset, SeekOrigin.Begin);

                byte[] _packed_xml = br.ReadBytes(xml_length);

                using (MemoryStream input_xml = new MemoryStream(_packed_xml))
                {
                    using (MemoryStream unpacked_xml = new MemoryStream())
                    {
                        using (GZipStream unpack = new GZipStream(input_xml, CompressionMode.Decompress))
                        {
                            unpack.CopyTo(unpacked_xml);
                        }
                        return Encoding.UTF8.GetString(unpacked_xml.ToArray());
                    }
                }
            }
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
    }


    internal class scenario
    {
        public uint partition;
        public uint partititon_offset;
        public ulong data_offset;
        internal byte[] msfmode;
    }
}