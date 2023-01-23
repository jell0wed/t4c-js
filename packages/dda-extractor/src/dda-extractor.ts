import {  readFileSync } from "fs";
import { Buffer } from "buffer";
import { deflateSync } from "zlib";
import { createHash } from "crypto";

const DDI_FILE = "./gamefiles/v2datai.did";

export class DDAExtractor {
    public constructor() {
        this.loadIndexes();
    }

    private loadIndexes(): void {
        const indexesBuf = readFileSync(DDI_FILE, { flag: "r" });
        const checkSumBuf = indexesBuf.subarray(0, 16);
        const checkSumPart2Buf = indexesBuf.subarray(16 + 4 + 4, 16 + 4 + 4 + 16 + 1);
        const unpackSizeBuf = indexesBuf.subarray(16, 16 + 4); // ULONG, little endian
        const packSizeBuf = indexesBuf.subarray(16 + 4, 16 + 4 + 4); // ULONG, little endian

        const packSize = packSizeBuf.readUInt32LE();
        const unpackSize = unpackSizeBuf.readUInt32LE();

        const compressedBuf = indexesBuf.subarray(16 + 4 + 4 + 16 + 1, packSize + 16 + 4 + 4 + 16 + 1);
        const uchChecksumBuf = indexesBuf.subarray(packSize + 16 + 4 + 4 + 16 + 1, packSize + 16 + 4 + 4 + 16 + 1 + 1);
        
        const uchChecksum = uchChecksumBuf.readUInt8();
        const uchVal = DDAExtractor.calculateChecksum(compressedBuf, packSize);
        if (uchVal != uchChecksum) {
            throw new Error(`Invalid inner checksum; expected checksum ${uchChecksum}, calculated checksum ${uchVal}`);
        }
        
        const decompressedBuf = deflateSync(compressedBuf);
        const md5Checksum = createHash("md5").update(decompressedBuf).digest();

        console.log("end");
    }

    private uncompress(packBuf: Buffer, packSize: number) {

    }

    static calculateChecksum(packBuf: Buffer, size: number): number { // 1 byte
        if (!packBuf || size <= 0) {
            throw new Error("no data to read");
        }

        const chkSum = Buffer.alloc(1, 0x0);
        for (let i = 0; i < size - 1; i++) {
            const cChkSum = chkSum.readUInt8();
            const dat = Buffer.alloc(1, packBuf.at(i)).readUInt8();
            const res = (cChkSum + dat);

            chkSum.writeUInt8(res & 0b11111111); // mask it back to 1 unsigned byte
        }

        return 256 - chkSum.readUInt8()
    }
}


const ddaExtract = new DDAExtractor();