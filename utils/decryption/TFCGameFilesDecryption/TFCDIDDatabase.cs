using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TFCGameFilesDecryption
{
    struct DIDIndexFileHeader {
        public string strChksumMd5; // len 33
        public ulong ulUnpackSize;
        public ulong ulPackSize;
        public byte uchChksum;
        public int indicesCount;

        public byte[] uncompressedData;
    }

    
    struct DIDIndexHeader {
        public const int NAME_LENGTH = 64;
        public const int PATH_LENGTH = 256;

        public const int STRUCT_SIZE_BYTES = NAME_LENGTH + PATH_LENGTH + 4 + 4 + 4;

        public string name;
        public string path;
        public uint dwFileOffset;
        public uint dwDataFileIndex;
        public uint dwThisPosIndex;
    }

    class TFCDIDDatabase
    {
        private const int XOR_DECRYPTION_KEY = 0x99;
        private readonly string _indexDatabasePath;
        private readonly string _databaseDataPrefixPath;

        private DIDIndexHeader[] indexDatabase;

        public TFCDIDDatabase(string _indexDatabasePath, string _databaseDataPrefix) 
        {
            this._indexDatabasePath = _indexDatabasePath;
            this._databaseDataPrefixPath = _databaseDataPrefix;
        }

        public void Decrypt() {
            DIDIndexFileHeader indexHeader = new DIDIndexFileHeader();
            byte[] uncompressedData = this.loadIndex(ref indexHeader);

            int read = 0;
            using (var uncompressedDataStream = new MemoryStream(uncompressedData))
            {
                this.indexDatabase = new DIDIndexHeader[indexHeader.indicesCount];
                for (var i = 0; i < indexHeader.indicesCount; i++)
                {
                    // create new header
                    DIDIndexHeader currentIndexHeader = new DIDIndexHeader();

                    byte[] indexBuf = new byte[DIDIndexHeader.STRUCT_SIZE_BYTES];
                    read = uncompressedDataStream.Read(indexBuf, 0, DIDIndexHeader.STRUCT_SIZE_BYTES);
                    Debug.Assert(read == DIDIndexHeader.STRUCT_SIZE_BYTES, "unable to read index stream for index = " + i);

                    using (var indexStream = new MemoryStream(indexBuf)) {
                        byte[] nameBuf = new byte[DIDIndexHeader.NAME_LENGTH];
                        read = indexStream.Read(nameBuf, 0, DIDIndexHeader.NAME_LENGTH);
                        currentIndexHeader.name = System.Text.Encoding.ASCII.GetString(nameBuf.Where(s => s > 0x0).ToArray());
                        Debug.Assert(read == DIDIndexHeader.NAME_LENGTH, "unable to extract name field");

                        byte[] pathBuf = new byte[DIDIndexHeader.PATH_LENGTH];
                        read = indexStream.Read(pathBuf, 0, DIDIndexHeader.PATH_LENGTH);
                        currentIndexHeader.path = System.Text.Encoding.ASCII.GetString(pathBuf.Where(s => s > 0x0).ToArray());
                        Debug.Assert(read == DIDIndexHeader.PATH_LENGTH, "unable to extract path field");

                        byte[] offsetBuf = new byte[4];
                        read = indexStream.Read(offsetBuf, 0, 4);
                        currentIndexHeader.dwFileOffset = BitConverter.ToUInt32(offsetBuf, 0);
                        Debug.Assert(read == 4, "unable to extract current offset field");

                        byte[] fileIndexBuf = new byte[4];
                        read = indexStream.Read(fileIndexBuf, 0, 4);
                        currentIndexHeader.dwDataFileIndex = BitConverter.ToUInt32(fileIndexBuf, 0);
                        Debug.Assert(read == 4, "unable to extract current fileIndex field");

                        byte[] thisPosBuf = new byte[4];
                        read = indexStream.Read(thisPosBuf, 0, 4);
                        currentIndexHeader.dwThisPosIndex = BitConverter.ToUInt32(thisPosBuf, 0);
                        Debug.Assert(read == 4, "unable to extract current thisPosIndex field");

                        // append database
                        this.indexDatabase[i] = currentIndexHeader;
                    }
                }
            }
        }

        private byte[] loadIndex(ref DIDIndexFileHeader loadHeader) {
            if (!File.Exists(this._indexDatabasePath)) { throw new Exception("Index data file does not exists."); }

            using (Stream src = File.OpenRead(this._indexDatabasePath)) {
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
                loadHeader.strChksumMd5 = System.Text.Encoding.ASCII.GetString(chkSumMd5Part1.Concat(chkSumMd5Part2).Take(32).ToArray());
                // unpack size
                loadHeader.ulUnpackSize = BitConverter.ToUInt32(unpackSize, 0);
                // pack size
                loadHeader.ulPackSize = BitConverter.ToUInt32(packSize, 0);
                
                // read compressed data
                byte[] compressedData = new byte[loadHeader.ulPackSize];
                int len = Convert.ToInt32(loadHeader.ulPackSize);
                read = src.Read(compressedData, 0, Convert.ToInt32(loadHeader.ulPackSize)); // read the compressed (packed size data)
                Debug.Assert(read == Convert.ToInt32(loadHeader.ulPackSize), "unable to read compressed data");

                // read uch chksum
                byte[] uchChksum = new byte[1];
                read = src.Read(uchChksum, 0, 1);
                Debug.Assert(read == 1, "unable to read uch chksum");

                // uch chksum
                loadHeader.uchChksum = uchChksum[0];

                byte uchVal = this.calculateUchVal(compressedData);
                Debug.Assert(loadHeader.uchChksum == uchVal, "uch checksum validation failed");

                // decompress the compressed data using zlib
                byte[] uncompressedData = new byte[loadHeader.ulUnpackSize];
                using (var deflatedStream = Zlib.Deflate(compressedData)) {
                    read = deflatedStream.Read(uncompressedData, 0, Convert.ToInt32(loadHeader.ulUnpackSize));
                    Debug.Assert(read == Convert.ToInt32(loadHeader.ulUnpackSize), "unable to unpack deflated stream");
                }

                // compute md5 of uncompressed data
                string computedHash = "";
                using (var md5 = System.Security.Cryptography.MD5.Create()) {
                    computedHash = string.Join("", md5.ComputeHash(new MemoryStream(uncompressedData)).Select(s => s.ToString("x2")));
                }
                Debug.Assert(computedHash == loadHeader.strChksumMd5, "uncompressed data md5 checksum validate failed");
                uncompressedData = uncompressedData.Select(b => (byte)(b ^ XOR_DECRYPTION_KEY)).ToArray(); // apply XOR key for index data

                loadHeader.indicesCount = Convert.ToInt32(loadHeader.ulUnpackSize) / DIDIndexHeader.STRUCT_SIZE_BYTES;
                return uncompressedData;
            }
        }

        private byte calculateUchVal(byte[] compressedData)
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
