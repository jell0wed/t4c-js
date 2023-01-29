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
            TFCDIDDatabase didDatabase = new TFCDIDDatabase("..\\..\\..\\..\\..\\gamefiles\\v2datai.did", "");
            didDatabase.Decrypt();
        }
    }
}
