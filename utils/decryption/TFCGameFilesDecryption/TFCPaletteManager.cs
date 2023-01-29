using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TFCGameFilesDecryption.decrypt_utils;

namespace TFCGameFilesDecryption
{
    struct DPDIndexHeader {
        public const int STRUCT_SIZE_BYTES = 0;
    }

    class TFCPaletteManager
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private const int XOR_DECRYPTION_KEY = 0x66;

        private const string PALETTE_FILE = "V2ColorI.dpd";
        private string gamefilePath;

        private V2GameFile paletteGameFile;

        public TFCPaletteManager(string _gamefilePath) {
            this.gamefilePath = _gamefilePath;
        }

        public void DecryptPalette()
        {
            this.loadGamefile();
        }

        private void loadGamefile() {
            if (!File.Exists(Path.Combine(this.gamefilePath, PALETTE_FILE))) { throw new Exception("Index data file does not exists."); }

            Logger.Debug($"Loading dpd file index header {Path.Combine(this.gamefilePath, PALETTE_FILE)}");
            paletteGameFile = V2GamefilesDecryptionUtils.DecryptV2Gamefile(Path.Combine(this.gamefilePath, PALETTE_FILE));
            paletteGameFile.uncompressedData = paletteGameFile.uncompressedData.Select(b => (byte)(b ^ XOR_DECRYPTION_KEY)).ToArray(); // apply XOR key for index data

            paletteGameFile.indicesCount = Convert.ToInt32(paletteGameFile.ulUnpackSize) / DPDIndexHeader.STRUCT_SIZE_BYTES;
        }
    }
}
