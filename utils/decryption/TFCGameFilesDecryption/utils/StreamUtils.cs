using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TFCGameFilesDecryption.utils
{
    class StreamUtils
    {
        public static byte[] readBytes(Stream s, int offset, int bufSize)
        {
            byte[] resultBuf = new byte[bufSize];
            int read = s.Read(resultBuf, offset, bufSize);
            Debug.Assert(read == bufSize, "unable to read up to bufSize");

            return resultBuf;
        }

        public static string convertToASCIIString(byte[] buf)
        { 
            return System.Text.Encoding.ASCII.GetString(buf.Where(s => s > 0x0).ToArray());
        }
    }
}
