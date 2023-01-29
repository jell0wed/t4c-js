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
            TFCDDADatabase didDatabase = new TFCDDADatabase("..\\..\\..\\..\\..\\gamefiles\\");
            didDatabase.Decrypt();

            didDatabase.LoadSprite("NormalSword");
        }
    }
}
