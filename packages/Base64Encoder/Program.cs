using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace Base64Encoder
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine($"Base64 Encoder version {Assembly.GetEntryAssembly().GetName().Version}");
            Console.WriteLine("Copyright 2021 Amazon Web Services.");

            if (args == null || args.Length == 0)
            {
                Console.WriteLine("Please provide the path to the file you need to encode.");
                return;
            }

            if (!File.Exists(args[0]))
            {
                Console.WriteLine($"File '{args[0]}' not found.");
                return;
            }

            var tryWrite = args.Length >= 2;

            string writePath = null;
            if (tryWrite)
            {
                writePath = args[1];
                if (File.Exists(writePath))
                {
                    Console.WriteLine($"File {writePath} already exists.");
                    return;
                }
            }


            Console.WriteLine("");
            var fileLength = new FileInfo(args[0]).Length;
            using (var fs = File.OpenRead(args[0]))
            {
                var ms = new MemoryStream();
                fs.CopyTo(ms);
                var strBase64 = Convert.ToBase64String(ms.ToArray());
                Console.Write(strBase64);
                if (tryWrite)
                    using (var fsOut = File.Open(writePath, FileMode.CreateNew))
                    {
                        var base64Bytes = Encoding.UTF8.GetBytes(strBase64);
                        fsOut.Write(base64Bytes, 0, base64Bytes.Length);
                    }
            }

            Console.WriteLine("");
            Console.WriteLine($"Encoded {fileLength} bytes into Base64");
            if (tryWrite) Console.WriteLine($"Wrote file to {writePath}");
        }
    }
}