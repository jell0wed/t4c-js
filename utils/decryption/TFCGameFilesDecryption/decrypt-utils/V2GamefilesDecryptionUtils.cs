using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TFCGameFilesDecryption.decrypt_utils
{
    struct V2GameFile
    {
        public string strChksumMd5; // len 33
        public ulong ulUnpackSize;
        public ulong ulPackSize;
        public byte uchChksum;
        public int indicesCount;

        public byte[] uncompressedData;
    }

    class V2GamefilesDecryptionUtils
    {
        public static V2GameFile DecryptV2Gamefile(string filePath)
        {
            V2GameFile rawGameFile = new V2GameFile();
            using (Stream src = File.OpenRead(filePath))
            {
                int read = 0;

                // first 16 bytes are first part of md5 checksum
                byte[] chkSumMd5Part1 = new byte[16];
                read = src.Read(chkSumMd5Part1, 0, 16);
                Debug.Assert(read == 16, "unable to read first part of checksum");

                // second 4 byte is unpackSize
                byte[] unpackSize = new byte[4];
                read = src.Read(unpackSize, 0, 4);
                Debug.Assert(read == 4, "unable to read unpack size");

                // third 4 byte is packSize
                byte[] packSize = new byte[4];
                read = src.Read(packSize, 0, 4);
                Debug.Assert(read == 4, "unable to read pack size");

                // next 17 bytes are second part of md5 chksum
                byte[] chkSumMd5Part2 = new byte[17];
                read = src.Read(chkSumMd5Part2, 0, 17);
                Debug.Assert(read == 17, "unable to read second part of checksum");

                // checksum
                rawGameFile.strChksumMd5 = System.Text.Encoding.ASCII.GetString(chkSumMd5Part1.Concat(chkSumMd5Part2).Take(32).ToArray());
                // unpack size
                rawGameFile.ulUnpackSize = BitConverter.ToUInt32(unpackSize, 0);
                // pack size
                rawGameFile.ulPackSize = BitConverter.ToUInt32(packSize, 0);

                // read compressed data
                byte[] compressedData = new byte[rawGameFile.ulPackSize];
                int len = Convert.ToInt32(rawGameFile.ulPackSize);
                read = src.Read(compressedData, 0, Convert.ToInt32(rawGameFile.ulPackSize)); // read the compressed (packed size data)
                Debug.Assert(read == Convert.ToInt32(rawGameFile.ulPackSize), "unable to read compressed data");

                // read uch chksum
                byte[] uchChksum = new byte[1];
                read = src.Read(uchChksum, 0, 1);
                Debug.Assert(read == 1, "unable to read uch chksum");

                // uch chksum
                rawGameFile.uchChksum = uchChksum[0];

                byte uchVal = calculateUchVal(compressedData);
                Debug.Assert(rawGameFile.uchChksum == uchVal, "uch checksum validation failed");

                // decompress the compressed data using zlib
                byte[] uncompressedData = new byte[rawGameFile.ulUnpackSize];
                using (var deflatedStream = Zlib.Deflate(compressedData))
                {
                    read = deflatedStream.Read(uncompressedData, 0, Convert.ToInt32(rawGameFile.ulUnpackSize));
                    Debug.Assert(read == Convert.ToInt32(rawGameFile.ulUnpackSize), "unable to unpack deflated stream");
                }

                // compute md5 of uncompressed data
                string computedHash = "";
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    computedHash = string.Join("", md5.ComputeHash(new MemoryStream(uncompressedData)).Select(s => s.ToString("x2")));
                }
                Debug.Assert(computedHash == rawGameFile.strChksumMd5, "uncompressed data md5 checksum validate failed");

                rawGameFile.uncompressedData = uncompressedData;

                return rawGameFile;
            }
        }

        private static byte calculateUchVal(byte[] compressedData)
        {
            byte chkSumResult = 0x00;
            for (int i = 0; i < compressedData.Length - 1; i++)
            {
                chkSumResult += compressedData[i];
            }
            chkSumResult = (byte)(0b100000000 - chkSumResult);
            return chkSumResult;
        }
    }
}
