﻿using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PBIX_to_Flat;
using Newtonsoft.Json.Linq;

namespace AddToDbFunction
{
    internal class MyService
    {
        byte[] responseBody;
        public MyService(byte[] responseBody)
        {
            this.responseBody = responseBody; 
        }
        public byte[] GetSourceFileFromZip()
        {
            byte[] outputBytes = new byte[1];
            using (var fileToCompressStream = new MemoryStream(responseBody))
            {
                using (var zip = new ZipArchive(fileToCompressStream, ZipArchiveMode.Read))
                {
                    foreach (var entry in zip.Entries)
                    {
                        using (MemoryStream POTOK = new MemoryStream())
                        {
                            entry.Open().CopyTo(POTOK);
                            POTOK.Position = 0;
                            outputBytes = POTOK.ToArray();
                        }
                    }
                }
            }
            return outputBytes;
        }

        public OutputObject ParseFileBytes(byte[] fileBytes)
        {
            string fileName = "Report.zip";
            byte[] compressedBytes;

            OutputObject outputFilter = null;

            using (var outStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(outStream, ZipArchiveMode.Create, true))
                {
                    var fileInArchive = archive.CreateEntry(fileName, CompressionLevel.Optimal);
                    using (var entryStream = fileInArchive.Open())
                    using (var fileToCompressStream = new MemoryStream(fileBytes))
                    {
                        fileToCompressStream.CopyTo(entryStream);
                    }
                }
                compressedBytes = outStream.ToArray();

                using (var zip = new ZipArchive(outStream, ZipArchiveMode.Read))
                {
                    foreach (var entry in zip.Entries)
                    {
                        var innerZip = new ZipArchive(entry.Open(), ZipArchiveMode.Read);
                        foreach (var innerEntry in innerZip.Entries)
                        {
                            using (var stream = entry.Open())
                            {
                                if (innerEntry.Name == "Layout")
                                {
                                    using (MemoryStream POTOK = new MemoryStream())
                                    {
                                        innerEntry.Open().CopyTo(POTOK);
                                        POTOK.Position = 0;
                                        var extractedBytes = POTOK.ToArray();
                                        var str = Encoding.Unicode.GetString(extractedBytes);
                                        outputFilter = new OutputObject(JObject.Parse(str));

                                    }
                                }
                            }
                        }
                    }

                }
            }

            return outputFilter;
        }

    }
}
