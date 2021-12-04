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

            BinaryWriter tr = new BinaryWriter(new FileStream(string.Format(new DirectoryInfo(working_dir).Name + ".mrg"), FileMode.Create));
            tr.BaseStream.Seek(32, SeekOrigin.Begin);

            //MemoryStream _buffer = new MemoryStream();
            //BinaryWriter mr = new BinaryWriter(_buffer);
            MemoryStream mr = new MemoryStream();

            string form2_name = new DirectoryInfo(working_dir).Name + ".form2";
            MemoryStream form2 = new MemoryStream();
            BinaryWriter form2r = new BinaryWriter(new FileStream(form2_name, FileMode.Create));
            List<Form2Info> form2s = new List<Form2Info>();

            string audio_name = new DirectoryInfo(working_dir).Name + ".audio";
            MemoryStream audio_stream = new MemoryStream();
            BinaryWriter audior = new BinaryWriter(new FileStream(audio_name, FileMode.Create));
            List<AudioInfo> audios = new List<AudioInfo>();

            MemoryStream all_maps = new MemoryStream();
            XmlElement map_partition_xml = main_spec.CreateElement("partition");

            merge_options.volume = 64;

            int buffer_form1_size = merge_options.volume * 1048576;
            int buffer_form2_size = merge_options.volume * 1189888;
            int buffer_audio_size = 10584000;

            if (merge_options.verbose)
            {
                Console.WriteLine(string.Format("Deduplicating and Packing the Contents of {0}\n\r", Path.GetFileName(working_dir)));
            }

            int counter = 0;
            int counter_form2 = 0;
            int counter_audio = 0;

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
                    case "audio":
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
                                if (CheckSync(_temp))
                                {
                                    switch (_temp[15])
                                    {
                                        case 1:
                                            hash = BitConverter.ToString(MD5.Create().ComputeHash(_temp, 16, 2048));
                                            break;
                                        case 2:
                                            switch ((_temp[18] & 0x20) == 0x20)
                                            {
                                                case false:
                                                    hash = BitConverter.ToString(MD5.Create().ComputeHash(_temp, 24, 2048));
                                                    break;
                                                case true:
                                                    hash = BitConverter.ToString(MD5.Create().ComputeHash(_temp, 24, 2324));
                                                    break;
                                            }

                                            break;
                                    }
                                }
                                else
                                {
                                    hash = BitConverter.ToString(MD5.Create().ComputeHash(_temp, 0, 2352));
                                }
                                break;
                        }

                        //psx specific keys
                        if (block_size == 2352)
                        {
                            if (_temp[15] == 2)
                            {
                                if ((_temp[18] & 0x20) == 0x20)
                                {
                                    bool psx_null_edc = PsxNullSequenceCheck(ref _temp, 2348, 4);
                                    if (psx_null_edc)
                                    {
                                        _temp[15] |= 0x80;
                                    }
                                }
                                if (PsxNullSequenceCheck(ref _temp, 16, 8))
                                {
                                    if (!PsxNullSequenceCheck(ref _temp, 2076, 276))
                                    {
                                        _temp[15] |= 0x40;
                                    }
                                }
                            }
                        }

                        if (dupes.ContainsKey(hash))
                        {
                            switch(block_size)
                            {
                                case 2048:
                                    image_map.Write(dupes[hash]);
                                    break;
                                case 2352:
                                    switch (_temp[15] & 0b11)
                                    {
                                        case 1:
                                            image_map.Write(_temp, 12, 4);
                                            image_map.Write(dupes[hash]);
                                            break;
                                        case 2:
                                            image_map.Write(_temp, 12, 4);
                                            image_map.Write(_temp, 16, 8);
                                            image_map.Write(dupes[hash]);
                                            break;
                                    }
                                    break;
                            }
                        }
                        else
                        {
                            switch (block_size)
                            {
                                case 2048:
                                    mr.Write(_temp, 0, 2048);
                                    image_map.Write(counter);
                                    dupes.Add(hash, counter++);
                                    break;
                                case 2352:
                                    switch (_temp[15] & 0b11)
                                    {
                                        case 1:
                                            mr.Write(_temp, 16, 2048);
                                            image_map.Write(_temp, 12, 4);
                                            image_map.Write(counter);
                                            dupes.Add(hash, counter++);
                                            break;
                                        case 2:
                                            switch((_temp[18] & 0x20) == 0x20)
                                            {
                                                case false:
                                                    mr.Write(_temp, 24, 2048);
                                                    image_map.Write(_temp, 12, 4);
                                                    image_map.Write(_temp, 16, 8);
                                                    image_map.Write(counter);
                                                    dupes.Add(hash, counter++);
                                                    break;
                                                case true:
                                                    //form2
                                                    //null_edc
                                                    form2.Write(_temp, 24, 2324);
                                                    image_map.Write(_temp, 12, 4);
                                                    image_map.Write(_temp, 16, 8);
                                                    image_map.Write(counter_form2);
                                                    dupes.Add(hash, counter_form2++);
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            
                            //counter++;
                        }

                        if (mr.Length == buffer_form1_size || (br.BaseStream.Position == br.BaseStream.Length && last))
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

                            SevenZip.PackChunk(mr, tr);

                            mr.SetLength(0);

                            partition_xml.SetAttribute("c_length", (tr.BaseStream.Position - partition_offset).ToString());
                            //long c_chunk_length = tr.BaseStream.Position - partition_offset;
                            if (merge_options.verbose) Console.WriteLine("\rPacked chunk {0} in {1}", chunk_n++, DateTime.Now - chunk_start);
                        }

                        //form2 buffer
                        if (form2.Length == buffer_form2_size || (br.BaseStream.Position == br.BaseStream.Length && last && form2.Length != 0))
                        {
                            //if (form2.Length != 0)
                            {
                                Form2Info form2Info = new Form2Info();

                                //if (merge_options.verbose) Console.Write("Packing chunk {0}...", chunk_n);

                                //DateTime chunk_start = DateTime.Now;
                                form2Info.chunk_length = form2.Length;
                                form2Info.offset = form2r.BaseStream.Position;
                                form2Info.length = form2.Position;
                                //long chunk_length = form2.Length;

                                //XmlElement partition_xml = main_spec.CreateElement("partition");
                                //partitions_xml.AppendChild(partition_xml);
                                //partition_xml.SetAttribute("type", "form2");
                                //partition_xml.SetAttribute("offset", form2r.BaseStream.Position.ToString());
                                //partition_xml.SetAttribute("u_length", form2.Position.ToString());

                                long partition_offset = form2r.BaseStream.Position;

                                form2.Position = 0;
                                SevenZip.PackChunk(form2, form2r);
                                form2.SetLength(0);

                                form2Info.c_length = form2r.BaseStream.Position - partition_offset;

                                form2s.Add(form2Info);
                                //partition_xml.SetAttribute("c_length", (form2r.BaseStream.Position - partition_offset).ToString());
                                //long c_chunk_length = form2r.BaseStream.Position - partition_offset;
                                //if (merge_options.verbose) Console.WriteLine("\rPacked chunk {0} in {1}", chunk_n++, DateTime.Now - chunk_start);
                            }
                        }
                    }

                    Console.WriteLine(string.Format("\r\nProcessed: {0} in {1}\r\n", Path.GetFileName(images[image]), DateTime.Now - image_time));
                }

                XmlElement image_xml = main_spec.CreateElement("entry");
                files_xml.AppendChild(image_xml);
                image_xml.SetAttribute("type", "file");
                //image_type
                image_xml.SetAttribute("mode", image_type);
                image_xml.SetAttribute("name", new FileInfo(images[image]).Name);
                image_xml.SetAttribute("length", new FileInfo(images[image]).Length.ToString());
                image_xml.SetAttribute("map_offset", all_maps.Position.ToString());
                image_xml.SetAttribute("map_length", image_map.BaseStream.Length.ToString());

                image_map.BaseStream.Position = 0;
                image_map.BaseStream.CopyTo(all_maps);
                image_map.Close();
            }

            //test map
            //all_maps.Position = 0;
            //File.WriteAllBytes("test.map", all_maps.ToArray());
            //form2.Position = 0;
            //File.WriteAllBytes("test.form2", form2.ToArray());
            if (form2s.Count != 0)
            {
                for (int i = 0; i < form2s.Count; i++)
                {
                    XmlElement partition_xml = main_spec.CreateElement("partition");
                    partitions_xml.AppendChild(partition_xml);
                    partition_xml.SetAttribute("type", "form2");
                    partition_xml.SetAttribute("offset", (form2s[i].offset + tr.BaseStream.Position).ToString());
                    partition_xml.SetAttribute("u_length", form2s[i].length.ToString());
                    partition_xml.SetAttribute("c_length", form2s[i].c_length.ToString());
                    //}

                    //if (form2r.BaseStream.Length != 0)
                    //{
                }
                form2r.BaseStream.Position = 0;
                form2r.BaseStream.CopyTo(tr.BaseStream);
                
            }
            form2r.Close();
            File.Delete(form2_name);

            MemoryStream c_all_maps = SevenZip.CompressStream(all_maps);
            map_partition_xml.SetAttribute("type", "map");
            map_partition_xml.SetAttribute("offset", tr.BaseStream.Position.ToString());
            map_partition_xml.SetAttribute("c_length", c_all_maps.Length.ToString());
            map_partition_xml.SetAttribute("u_length", all_maps.Length.ToString());
            partitions_xml.AppendChild(map_partition_xml);
            c_all_maps.Position = 0;
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

            //merge_options.xml = true;
            if (merge_options.xml) main_spec.Save(string.Format("{0}.xml", new DirectoryInfo(working_dir).Name));

            if (merge_options.verbose) Console.WriteLine(string.Format("Job completed in {0}", DateTime.Now - job_time));
        }


        static byte[] sync = { 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x00 };
        private static bool CheckSync(byte[] temp)
        {
            for (int i = 0; i < sync.Length; i++)
            {
                if (temp[i] != sync[i]) return false;
            }
            return true;
        }

        private static bool PsxNullSequenceCheck(ref byte[] temp, int offset, int length)
        {
            for (int i = 0; i < length; i++)
            {
                if (temp[i + offset] != 0) return false;
            }
            return true;
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

                //XmlNodeList partitions = xml.DocumentElement.GetElementsByTagName("partition");
                XmlNodeList partitions = xml.DocumentElement.SelectNodes("partitions/partition[@type='form1']");
                XmlNodeList partitions_form2 = xml.DocumentElement.SelectNodes("partitions/partition[@type='form2']");

                MemoryStream all_maps = ReadMap(mrg_arc, xml);
                using (BinaryReader mapr = new BinaryReader(all_maps))
                {
                    foreach (XmlNode file in files)
                    {
                        if (merge_options.verbose) Console.WriteLine(string.Format("\r\nExtracting \"{0}\"", file.Attributes["name"].Value));

                        string file_name = file.Attributes["name"].Value;
                        using (BinaryWriter dataout = new BinaryWriter(new FileStream(file_name, FileMode.Create)))
                        {
                            int buffer_length_form1 = 67108864;
                            int buffer_length_form2 = 76152832;

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
                                case "file":
                                case "iso":
                                    while (mapr.BaseStream.Position != map_offset + map_length)
                                    {
                                        scenario _chain = new scenario();

                                        _chain.data_offset = (ulong)(position++ * 2048);
                                        long offset = mapr.ReadUInt32() * 2048;

                                        _chain.partition = (uint)(offset / buffer_length_form1);
                                        _chain.partititon_offset = (uint)(offset % buffer_length_form1);

                                        chain.Add(_chain);
                                    }
                                    break;
                                case "raw":
                                    while (mapr.BaseStream.Position != map_offset + map_length)
                                    {
                                        scenario _chain = new scenario();

                                        if (_chain.partition == 1)
                                        {
                                            int h = 0;
                                        }
                                        _chain.data_offset = (ulong)(position++ * 2352);
                                        _chain.msfmode = mapr.ReadBytes(4);

                                        //test mark psx null edc
                                        switch (_chain.msfmode[3] & 0b11)
                                        {
                                            //case 1:
                                            //    long offset = mapr.ReadUInt32() * 2048;
                                            //
                                            //    _chain.partition = (uint)(offset / buffer_length);
                                            //    _chain.partititon_offset = (uint)(offset % buffer_length);
                                            //    break;
                                            case 2:
                                                _chain.header = mapr.ReadBytes(8);
                                                _chain.form = ((_chain.header[2] & 0x20) == 0x20) ? 2 : 1;
                                                //long offset = mapr.ReadUInt32() * 2048;
                                                //
                                                //_chain.partition = (uint)(offset / buffer_length);
                                                //_chain.partititon_offset = (uint)(offset % buffer_length);
                                                break;
                                        }

                                        long offset = mapr.ReadUInt32();// * 2048;

                                        //if (offset >= 1024 * 1024 * 64)
                                        //{
                                        //    int j = 0;
                                        //}

                                        switch(_chain.form)
                                        {
                                            case 0:
                                            case 1:
                                                offset = offset * 2048;
                                                _chain.partition = (uint)(offset / buffer_length_form1);
                                                _chain.partititon_offset = (uint)(offset % buffer_length_form1);
                                                break;
                                            case 2:
                                                offset = offset * 2324;
                                                _chain.partition = (uint)(offset / buffer_length_form2);
                                                _chain.partititon_offset = (uint)(offset % buffer_length_form2);
                                                break;
                                        }
                                        

                                        //if (_chain.partition == 2)
                                        //{
                                        //    int h = 0;
                                        //}

                                        chain.Add(_chain);
                                    }
                                    break;
                            }
                            scenario[] chain_form1_sorted = chain.Where(x => x.form == 0 || x.form == 1).OrderBy(x => x.partition).ThenBy(y => y.data_offset).ToArray();
                            scenario[] chain_form2_sorted = chain.Where(x => x.form == 2).OrderBy(x => x.partition).ThenBy(y => y.data_offset).ToArray();
                            //scenario[] chain_sorted = chain.OrderBy(x => x.form).ThenBy(x => x.partition).ThenBy(y => y.data_offset).ToArray();
                            int current_partition = -1;
                            long file_length = Convert.ToInt64(file.Attributes["length"].Value);
                            for (int i = 0; i < chain_form1_sorted.Length; i++)
                            {
                                //if(i == 32984)
                                //{
                                //    int y = 0;
                                //    var f = chain_sorted[i];
                                //}

                                if (current_partition != chain_form1_sorted[i].partition) // || data_buffer.Length == 0)
                                {
                                    data_buffer.SetLength(0);
                                    SevenZip.UnpackBuffer(mrg_arc, data_buffer, partitions[(int)chain_form1_sorted[i].partition]);
                                    current_partition = (int)chain_form1_sorted[i].partition;
                                }

                                data_buffer.Seek(chain_form1_sorted[i].partititon_offset, SeekOrigin.Begin);

                                switch (mode)
                                {
                                    case "file":
                                        byte[] file_out_buffer = (file_length <= 2048) ? new byte[file_length] : new byte[2048];
                                        file_length -= 2048;
                                        data_buffer.Read(file_out_buffer, 0, file_out_buffer.Length);
                                        dataout.BaseStream.Seek((long)chain_form1_sorted[i].data_offset, SeekOrigin.Begin);
                                        dataout.Write(file_out_buffer);
                                        break;
                                    case "iso":
                                        byte[] iso_out_buffer = new byte[2048];
                                        data_buffer.Read(iso_out_buffer, 0, 2048);
                                        dataout.BaseStream.Seek((long)chain_form1_sorted[i].data_offset, SeekOrigin.Begin);
                                        dataout.Write(iso_out_buffer);
                                        break;
                                    case "raw":
                                        byte[] raw_out_buffer = new byte[2352];

                                        switch (chain_form1_sorted[i].msfmode[3] & 0b11)
                                        {
                                            case 1:
                                                new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff }.CopyTo(raw_out_buffer, 1);
                                                chain_form1_sorted[i].msfmode.CopyTo(raw_out_buffer, 12);
                                                data_buffer.Read(raw_out_buffer, 16, 2048);
                                                CalculateEDC(ref raw_out_buffer, 0, 2064);
                                                CalculateECCP(ref raw_out_buffer);
                                                CalculateECCQ(ref raw_out_buffer);
                                                dataout.BaseStream.Seek((long)chain_form1_sorted[i].data_offset, SeekOrigin.Begin);
                                                dataout.Write(raw_out_buffer);
                                                break;
                                            case 2:
                                                if ((chain_form1_sorted[i].msfmode[3] & 0x40) == 0x40)
                                                {
                                                    chain_form1_sorted[i].msfmode.CopyTo(raw_out_buffer, 12);
                                                    raw_out_buffer[15] &= 3;
                                                    chain_form1_sorted[i].header.CopyTo(raw_out_buffer, 16);
                                                    data_buffer.Read(raw_out_buffer, 24, 2048);
                                                    CalculateEDC(ref raw_out_buffer, 16, 2056);
                                                    CalculateECCP(ref raw_out_buffer);
                                                    CalculateECCQ(ref raw_out_buffer);
                                                    new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff }.CopyTo(raw_out_buffer, 1);
                                                }
                                                else
                                                {
                                                    chain_form1_sorted[i].header.CopyTo(raw_out_buffer, 16);
                                                    data_buffer.Read(raw_out_buffer, 24, 2048);
                                                    CalculateEDC(ref raw_out_buffer, 16, 2056);
                                                    CalculateECCP(ref raw_out_buffer);
                                                    CalculateECCQ(ref raw_out_buffer);
                                                    new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff }.CopyTo(raw_out_buffer, 1);
                                                    chain_form1_sorted[i].msfmode.CopyTo(raw_out_buffer, 12);
                                                }
                                                dataout.BaseStream.Seek((long)chain_form1_sorted[i].data_offset, SeekOrigin.Begin);
                                                dataout.Write(raw_out_buffer);
                                                break;
                                        }
                                        break;
                                }
                            }
                            int current_partition_form2 = -1;
                            for (int i = 0; i < chain_form2_sorted.Length; i++)
                            {
                                if (current_partition_form2 != chain_form2_sorted[i].partition) // || data_buffer.Length == 0)
                                {
                                    data_buffer.SetLength(0);
                                    SevenZip.UnpackBuffer(mrg_arc, data_buffer, partitions_form2[(int)chain_form2_sorted[i].partition]);
                                    current_partition_form2 = (int)chain_form2_sorted[i].partition;
                                }

                                data_buffer.Seek(chain_form2_sorted[i].partititon_offset, SeekOrigin.Begin);

                                byte[] raw_out_buffer = new byte[2352];

                                new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff }.CopyTo(raw_out_buffer, 1);
                                chain_form2_sorted[i].msfmode.CopyTo(raw_out_buffer, 12);
                                chain_form2_sorted[i].header.CopyTo(raw_out_buffer, 16);
                                data_buffer.Read(raw_out_buffer, 24, 2324);
                                if ((raw_out_buffer[15] & 0x80) != 0x80)
                                {
                                    CalculateEDC(ref raw_out_buffer, 16, 2332);
                                }
                                else
                                {
                                    raw_out_buffer[15] ^= 0x80;
                                }
                                dataout.BaseStream.Seek((long)chain_form2_sorted[i].data_offset, SeekOrigin.Begin);
                                dataout.Write(raw_out_buffer);
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
            BitConverter.GetBytes(edc).CopyTo(raw_out_buffer, offset + length);
        }

        private static MemoryStream ReadMap(string mrg_arc, XmlDocument xml)
        {
            XmlNode map_xml = xml.DocumentElement.SelectSingleNode("partitions/partition[@type='map']");

            MemoryStream all_maps = new MemoryStream();
            SevenZip.UnpackBuffer(mrg_arc, all_maps, map_xml);

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

    internal class AudioInfo
    {
        internal long chunk_length;
        internal long offset;
        internal long length;
        internal long c_length;
    }

    internal class Form2Info
    {
        internal long chunk_length;
        internal long offset;
        internal long length;
        internal long c_length;
    }

    internal class scenario
    {
        public uint partition;
        public uint partititon_offset;
        public ulong data_offset;
        internal byte[] msfmode;
        internal byte[] header;
        internal int form;
    }
}