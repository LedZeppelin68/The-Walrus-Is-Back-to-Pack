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

            string[] entries = args.Where(x => !x.StartsWith("--")).ToArray();
            string[] options = args.Where(x => x.StartsWith("--")).ToArray();

            if (entries.Length == 0)
            {
                Console.WriteLine("Usage: walrus.exe [directory or mrg]");
                Console.ReadKey(true);
            }

            Options merge_options = new Options(options);
            merge_options.verbose = true;

            foreach (string entry in entries)
            {
                if (new DirectoryInfo(entry).Attributes.HasFlag(FileAttributes.Directory))
                {
                    Merger.Pack(entry, merge_options);
                }
                else
                {
                    Merger.UnPack(entry, merge_options);
                }
            }
            Console.ReadKey();
        }
    }

    internal class Options
    {
        internal bool verbose;
        internal bool xml;
        internal int volume;

        public Options(string[] options)
        {
            verbose = options.Contains("--verbose") ? true : false;
            xml = options.Contains("--xml") ? true : false;
        }
    }
}
