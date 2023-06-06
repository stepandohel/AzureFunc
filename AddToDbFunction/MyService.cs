using System;
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
        DateTime _dateTime;
        public MyService(DateTime dateTime)
        {
            _dateTime = dateTime;
        }

        public byte[] GetSourceFilesFromZip(byte[] responseBody)
        {
            byte[] outputBytes = new byte[1];
            using (var fileToCompressStream = new MemoryStream(responseBody,false))
            {
                using (var zip = new ZipArchive(fileToCompressStream, ZipArchiveMode.Read))
                {
                    foreach (var entry in zip.Entries)
                    {
                        if (entry.Name.Substring(entry.Name.Length - 4) == "pbix" && entry.Name.Length > 5)
                        {
                            using (MemoryStream memStream = new MemoryStream())
                            {
                                entry.Open().CopyTo(memStream);
                                memStream.Position = 0;
                                outputBytes = memStream.ToArray();
                            }
                        }
                    }
                }
            }
            return outputBytes;
        }

        public OutputObject ParseFileBytes(byte[] fileBytes, string reportName)
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
                                    using (MemoryStream memString = new MemoryStream())
                                    {
                                        innerEntry.Open().CopyTo(memString);
                                        memString.Position = 0;
                                        var extractedBytes = memString.ToArray();
                                        var str = Encoding.Unicode.GetString(extractedBytes);
                                        outputFilter = new OutputObject(JObject.Parse(str), reportName, _dateTime);
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
