using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TFCGameFilesDecryption
{
    class Program
    {
        static void Main(string[] args)
        {
            TFCPaletteManager paletteManager = new TFCPaletteManager("..\\..\\..\\..\\..\\gamefiles\\");
            paletteManager.DecryptPalette();

            TFCDDADatabase didDatabase = new TFCDDADatabase(paletteManager, "..\\..\\..\\..\\..\\gamefiles\\");
            didDatabase.Decrypt();

            didDatabase.LoadSprite("NetWorkAccess");

            
        }
    }
}
