using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TFCGameFilesDecryption
{
    
    class RandomTable
    {
        const uint MAX_SHORT = 65535;

        private int size;
        private uint Seed;
        private uint MinValue;
        private uint MaxValue;
        private uint SeedID;
        private uint Multiplier;
        public ushort[] Values { get; private set; }

        public RandomTable(int size, uint newMinValue = 0, uint newMaxVaue = MAX_SHORT, uint initSeed = 0, uint seedNumber = 0, uint newMultiplier = 7563921) {
            this.size = size;
            MinValue = newMinValue;
            MaxValue = newMaxVaue + 1;
            Seed = initSeed;
            SeedID = seedNumber;
            Multiplier = newMultiplier;
            this.Values = new ushort[size];

            this.createTable();
        }

        public void CreateRandom(uint NewMinValue = 0, uint NewMaxValue = MAX_SHORT, uint InitSeed = 0, uint newMultiplier = 0) {
            MinValue = NewMinValue;
            MaxValue = NewMaxValue;
            Seed = InitSeed;
            if (newMultiplier > 0)
                Multiplier = newMultiplier;

            createTable();
        }

        private void createTable() {
            for (var i = 0; i < this.size; i++) {
                Seed = Seed * Multiplier + 1;
                Values[i] = (ushort)((Seed % (MaxValue - MinValue)) + MinValue);
            }
        }

        public uint Randomize() {
            return 0;
        }

    }
}
