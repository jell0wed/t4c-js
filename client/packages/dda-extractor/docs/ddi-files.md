# DDI Files

DDI files are Dialsoft indexes files for sprites. The payload is compressed using zlib.

## File structure
Bytes are encoded in little-endian.

Bytes ordering: 
- [0..16] (2 bytes) : first part of md5 checksum
- [16..20] (1 byte) : expected unpack size (after decompression)
- [20..24] (1 bytes) : compressed size (before decompression) `compressedSize`
- [24..40] (2 bytes) : second part of md5 checksum
- [40..40+compressedSize] (`compressedSize` bytes) : ZLib compressed payload
- [40+compressedSize..40+compressedSize+1] (1 byte) : checksum used pre-decompression
