using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TFCGameFilesDecryption.decrypt_utils;
using TFCGameFilesDecryption.utils;

namespace TFCGameFilesDecryption
{
    struct DPDPalette {
        public const int STRUCT_SIZE_BYTES = LPSZ_ID_LENGTH + LP_SPRITE_PAL_LENGTH;
        public const int LPSZ_ID_LENGTH = 64;
        public const int LP_SPRITE_PAL_LENGTH = 256 * 3;

        public string lpszID; // len 64
        public byte[] lpSpritePal; // len 256 * 3
    }

    class TFCPaletteManager
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private const int XOR_DECRYPTION_KEY = 0x66;

        private const string PALETTE_FILE = "V2ColorI.dpd";
        private string gamefilePath;

        private V2GameFile paletteGameFile;
        private DPDPalette[] loadedPalette;

        public TFCPaletteManager(string _gamefilePath) {
            this.gamefilePath = _gamefilePath;
        }

        public void DecryptPalette()
        {
            this.loadGamefile();
            using (var uncompressedDataStream = new MemoryStream(this.paletteGameFile.uncompressedData))
            {
                this.loadedPalette = new DPDPalette[this.paletteGameFile.indicesCount];
                for (var i = 0; i < this.paletteGameFile.indicesCount; i++)
                {
                    DPDPalette palette = new DPDPalette();
                    byte[] paletteBuf = StreamUtils.readBytes(uncompressedDataStream, 0, DPDPalette.STRUCT_SIZE_BYTES);
                    using (var paletteStream = new MemoryStream(paletteBuf)) {
                        palette.lpszID = StreamUtils.convertToASCIIString(StreamUtils.readBytes(paletteStream, 0, DPDPalette.LPSZ_ID_LENGTH));
                        palette.lpSpritePal = StreamUtils.readBytes(paletteStream, 0, DPDPalette.LP_SPRITE_PAL_LENGTH);
                    }
                    this.loadedPalette[i] = palette;
                }
            }
        }

        public DPDPalette? GetPal(string id, int palIdx) {
            DPDPalette? bestPalette = null;
            for (var i = this.paletteGameFile.indicesCount - 1; i >= 0; i--) {
                if (palIdx == 1)
                {
                    if (this.loadedPalette[i].lpszID.ToLower().Contains(id.ToLower()))
                    {
                        bestPalette = this.loadedPalette[i];
                    }
                }
                else
                {
                    if (palIdx < 10)
                    {
                        if (this.loadedPalette[i].lpszID[this.loadedPalette[i].lpszID.Length - 1] == (char)palIdx)
                        {
                            if (this.loadedPalette[i].lpszID.ToLower().Contains(id.ToLower()))
                            {
                                bestPalette = this.loadedPalette[i];
                            }
                        }
                    }
                    else
                    {
                        string lpszId = this.loadedPalette[i].lpszID;
                        if (Int32.Parse(lpszId.Substring(lpszId.Length - 2, lpszId.Length)) == palIdx)
                        {
                            if (this.loadedPalette[i].lpszID.ToLower().Contains(id.ToLower()))
                            {
                                bestPalette = this.loadedPalette[i];
                            }
                        }
                    }
                }
            }

            return bestPalette;
        }

        private void LoadPalette(byte[] paletteData)
        {
        
        }

        private void loadGamefile() {
            if (!File.Exists(Path.Combine(this.gamefilePath, PALETTE_FILE))) { throw new Exception("Index data file does not exists."); }

            Logger.Debug($"Loading dpd file index header {Path.Combine(this.gamefilePath, PALETTE_FILE)}");
            paletteGameFile = V2GamefilesDecryptionUtils.DecryptV2Gamefile(Path.Combine(this.gamefilePath, PALETTE_FILE));
            paletteGameFile.uncompressedData = paletteGameFile.uncompressedData.Select(b => (byte)(b ^ XOR_DECRYPTION_KEY)).ToArray(); // apply XOR key for index data

            paletteGameFile.indicesCount = Convert.ToInt32(paletteGameFile.ulUnpackSize) / DPDPalette.STRUCT_SIZE_BYTES;
        }
    }
}
