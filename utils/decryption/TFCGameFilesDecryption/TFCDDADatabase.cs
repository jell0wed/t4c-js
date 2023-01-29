using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
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

    struct DDASpriteHeader {
        public const int STRUCT_SIZE_BYTES = 10 * 2 + 2 * 4;

        public ushort dwCompType;
        public ushort flag1;
        public ushort dwWidth;
        public ushort dwHeight;
        public short shOffX1;
        public short shOffY1;
        public short shOffX2;
        public short shOffY2;
        public ushort ushTransparency;
        public ushort ushTransColor;
        public ulong dwDataUnpack;
        public ulong dwDataPack;
    }

    struct DDALoadedSprite {
        public DIDIndexHeader indexHeader;
        public DDASpriteHeader spriteHeader;
        public byte[] loadedChunk;
    }

    class TFCDDADatabase
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private const string DID_FILE = "V2DataI.did";
        private const string DDA_FILE = "V2Data";

        private const int COMP_DD = 1;
        private const int COMP_NCK = 2;
        private const int COMP_NULL = 3;
        private const int COMP_ZIP = 9;

        private const int XOR_DECRYPTION_KEY = 0x99;
        private const int DDA_FILES_COUNT = 20;

        private RandomTable XOR;
        private readonly string gamefilePath;

        private DIDIndexHeader[] indexDatabase;
        private Dictionary<string, DIDIndexHeader> indicesMap;
        private Dictionary<string, byte[]> loadedDDAs;

        public TFCDDADatabase(string _gamefilePath) 
        {
            this.gamefilePath = _gamefilePath;
            this.XOR = new RandomTable(4096);
            this.XOR.CreateRandom(0, 255, 666666);

            Logger.Info($"Initialized TFCDDADatabase on gamefile path {_gamefilePath}");
        }

        public void Decrypt() {
            // first load indices from the ddi file
            this.loadIndices();

            // load ddas
            this.loadDDAs();
        }

        public void LoadSprite(string index) {
            if (this.indicesMap == null) {
                Logger.Error("Indices map arent loaded yet. Make sure to call decrypt first.");
                throw new Exception("Indices map arent loaded yet. Make sure to call decrypt first.");
            }

            if (!this.indicesMap.ContainsKey(index)) {
                throw new Exception($"{index} not found");
            }

            DIDIndexHeader indexHeader = this.indicesMap[index];
            DDALoadedSprite loadedSprite = internalLoadSprite(indexHeader);
            exportSprite(loadedSprite);
        }

        private DDALoadedSprite internalLoadSprite(DIDIndexHeader header) {
            string ddaFile = GenerateDDAFileFromIndex(Convert.ToInt32(header.dwDataFileIndex));
            Debug.Assert(this.loadedDDAs.ContainsKey(ddaFile), $"cannot load dda file from index {Convert.ToInt32(header.dwDataFileIndex)}");

            // retrieve sprite header from dda
            byte[] ddaSegmentBuf = getDDAFileSegment(this.loadedDDAs[ddaFile], Convert.ToInt32(header.dwFileOffset), DDASpriteHeader.STRUCT_SIZE_BYTES, false); 
            int read = 0;
            {
                var ddaSegmentStream = new MemoryStream(ddaSegmentBuf);

                // load sprite header
                DDASpriteHeader spriteHeader = new DDASpriteHeader();
                spriteHeader.dwCompType = (ushort)(BitConverter.ToUInt16(readBytes(ddaSegmentStream, 0, 2), 0) ^ 0xAAAA);
                spriteHeader.flag1 = (ushort)(BitConverter.ToUInt16(readBytes(ddaSegmentStream, 0, 2), 0) ^ 0x1458);
                spriteHeader.dwWidth = (ushort)(BitConverter.ToUInt16(readBytes(ddaSegmentStream, 0, 2), 0) ^ 0x1234);
                spriteHeader.dwHeight = (ushort)(BitConverter.ToUInt16(readBytes(ddaSegmentStream, 0, 2), 0) ^ 0x6242);
                spriteHeader.shOffX1 = (short)(BitConverter.ToInt16(readBytes(ddaSegmentStream, 0, 2), 0) ^ 0x2355);
                spriteHeader.shOffY1 = (short)(BitConverter.ToInt16(readBytes(ddaSegmentStream, 0, 2), 0) ^ 0xF6C3);
                spriteHeader.shOffX2 = (short)(BitConverter.ToInt16(readBytes(ddaSegmentStream, 0, 2), 0) ^ 0xAAF3);
                spriteHeader.shOffY2 = (short)(BitConverter.ToInt16(readBytes(ddaSegmentStream, 0, 2), 0) ^ 0xAAAA);
                spriteHeader.ushTransparency = (ushort)(BitConverter.ToUInt16(readBytes(ddaSegmentStream, 0, 2), 0) ^ 0x4321);
                spriteHeader.ushTransColor = (ushort)(BitConverter.ToUInt16(readBytes(ddaSegmentStream, 0, 2), 0) ^ 0x1234);
                spriteHeader.dwDataUnpack = (ulong)(BitConverter.ToUInt32(readBytes(ddaSegmentStream, 0, 4), 0) ^ 0xDDCCBBAA);
                spriteHeader.dwDataPack = (ulong)(BitConverter.ToUInt32(readBytes(ddaSegmentStream, 0, 4), 0) ^ 0xAABBCCDD);

                DDALoadedSprite loadedSprite = new DDALoadedSprite;
                switch (spriteHeader.dwCompType) {
                    case COMP_DD:
                        loadedSprite = this.loadSprite_Raw(header, spriteHeader);
                        break;
                    case COMP_NCK:
                        this.loadSprite_NoColorKey(header, spriteHeader);
                        break;
                    case COMP_ZIP:
                        this.loadSprite_Zip(header, spriteHeader);
                        break;
                    case COMP_NULL:
                        this.loadSprite_Null(header, spriteHeader);
                        break;
                }

                return loadedSprite;
            }
        }

        private void exportSprite(DDALoadedSprite loadedSprite) {
            // export the sprite into a bitmap file
            Bitmap bmp = new Bitmap(loadedSprite.spriteHeader.dwWidth, loadedSprite.spriteHeader.dwHeight, PixelFormat.Format32bppRgb);
            int cIndex = 0;
            for (var i = 0; i < loadedSprite.spriteHeader.dwWidth; i++) {
                for (var j = 0; j < loadedSprite.spriteHeader.dwHeight; j++) {
                    byte red = (byte)(loadedSprite.loadedChunk[cIndex + j] * 3);
                    byte green = (byte)(loadedSprite.loadedChunk[cIndex + j + 1] * 3);
                    byte blue = (byte)(loadedSprite.loadedChunk[cIndex + j + 2] * 3);


                }
                cIndex += loadedSprite.spriteHeader.dwWidth;
            }
        }

        private DDALoadedSprite loadSprite_Raw(DIDIndexHeader indexHeader, DDASpriteHeader spriteHeader)
        {
            DDALoadedSprite loadedSprite = new DDALoadedSprite();
            loadedSprite.indexHeader = indexHeader;
            loadedSprite.spriteHeader = spriteHeader;

            string ddaFile = GenerateDDAFileFromIndex(Convert.ToInt32(indexHeader.dwDataFileIndex));
            // read from dda from file offset + sizeof(sprite header)
            byte[] ddaSegmentBuf = getDDAFileSegment(this.loadedDDAs[ddaFile], Convert.ToInt32(indexHeader.dwFileOffset) + DDASpriteHeader.STRUCT_SIZE_BYTES, Convert.ToInt32(spriteHeader.dwDataPack));
            byte[] uncompressedChunkBuf = new byte[spriteHeader.dwDataUnpack];
            if (spriteHeader.dwWidth > 180 || spriteHeader.dwHeight > 180)
            {
                // sprite is compressed
                byte[] compressedChunkBuf = readBytes(new MemoryStream(ddaSegmentBuf), 0, Convert.ToInt32(spriteHeader.dwDataPack));
                uncompressedChunkBuf = readBytes(Zlib.Deflate(compressedChunkBuf), 0, Convert.ToInt32(spriteHeader.dwDataUnpack)); // never hit??
            }
            else
            {
                uncompressedChunkBuf = readBytes(new MemoryStream(ddaSegmentBuf), 0, Convert.ToInt32(spriteHeader.dwDataPack)); // shouldnt this be data unpack?? 
            }

            loadedSprite.loadedChunk = uncompressedChunkBuf;
            return loadedSprite;
        }

        private void loadSprite_NoColorKey(DIDIndexHeader indexHeader, DDASpriteHeader spriteHeader) {  }
        private void loadSprite_Zip(DIDIndexHeader indexHeader, DDASpriteHeader spriteHeader) { }
        private void loadSprite_Null(DIDIndexHeader indexHeader, DDASpriteHeader spriteHeader) {  }

        private byte[] getDDAFileSegment(byte[] ddaBuf, int offset, int len, bool mXor = false) {
            if (!mXor) {
                offset += 4;
            }
            using (var ddaStream = new MemoryStream(ddaBuf))
            {
                byte[] segmentBuf = new byte[len];
                ddaStream.Seek(offset, SeekOrigin.Begin);
                int read = ddaStream.Read(segmentBuf, 0, len);
                Debug.Assert(read == len);

                if (mXor) {
                    for (var i = 0; i < len; i++)
                    {
                        segmentBuf[i] ^= (byte)(XOR.Values[(offset + i) / 4096]);
                    }
                }

                return segmentBuf;
            }
        }

        private static byte[] readBytes(Stream s, int offset, int bufSize) {
            byte[] resultBuf = new byte[bufSize];
            int read = s.Read(resultBuf, offset, bufSize);
            Debug.Assert(read == bufSize, "unable to read up to bufSize");

            return resultBuf;
        }

        private void loadDDAs() {
            this.loadedDDAs = new Dictionary<string, byte[]>();

            Logger.Info("Loading DDAs into memory ...");
            for (var i = 0; i < DDA_FILES_COUNT; i++) {
                string ddaFile = GenerateDDAFileFromIndex(i);
                string ddaPath = Path.Combine(this.gamefilePath, ddaFile);
                if (File.Exists(ddaPath)) {
                    Logger.Info($"Loading DDA file {ddaFile}");
                    // load dda data in memory
                    byte[] ddaData = File.ReadAllBytes(ddaPath);
                    this.loadedDDAs[ddaFile] = ddaData;
                }
            }
        }

        private static string GenerateDDAFileFromIndex(int i) {
            return string.Format("{0}{1:D2}.dda", DDA_FILE, i); ;
        }

        private void loadIndices() {
            this.indicesMap = new Dictionary<string, DIDIndexHeader>();

            DIDIndexFileHeader indexHeader = new DIDIndexFileHeader();
            byte[] uncompressedData = this.loadIndexHeader(ref indexHeader);
            Logger.Info($"Loading {indexHeader.indicesCount} indices...");

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

                    using (var indexStream = new MemoryStream(indexBuf))
                    {
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
                        this.indicesMap[currentIndexHeader.name] = this.indexDatabase[i];
                    }
                }
            }
        }

        private byte[] loadIndexHeader(ref DIDIndexFileHeader loadHeader) {
            if (!File.Exists(Path.Combine(this.gamefilePath, DID_FILE))) { throw new Exception("Index data file does not exists."); }

            Logger.Debug($"Loading ddi file index header {Path.Combine(this.gamefilePath, DID_FILE)}");
            using (Stream src = File.OpenRead(Path.Combine(this.gamefilePath, DID_FILE)))
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
                using (var deflatedStream = Zlib.Deflate(compressedData))
                {
                    read = deflatedStream.Read(uncompressedData, 0, Convert.ToInt32(loadHeader.ulUnpackSize));
                    Debug.Assert(read == Convert.ToInt32(loadHeader.ulUnpackSize), "unable to unpack deflated stream");
                }

                // compute md5 of uncompressed data
                string computedHash = "";
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
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
