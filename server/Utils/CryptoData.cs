////////////////////////////////////////////////////////////////////////////////
//
// Crypto Data class (C++ to C# conversion)
// Original: CryptoData.cpp - Hangame Crypto Implementation
// Converted: 2025.10.20
//
////////////////////////////////////////////////////////////////////////////////

using System;

namespace MajakServer.Utils
{
    /// <summary>
    /// Hangame Crypto Data - Rijndael encryption implementation
    /// Behaves identically to C++ original code
    /// </summary>
    public class CryptoData
    {
        // Private member variables (C++ private)
        private byte[] InCo = new byte[4];       // Inverse Coefficients
        private uint[] rco = new uint[30];       // round constants
        private byte[] ptab = new byte[256];     // power table
        private byte[] ltab = new byte[256];     // log table

        // Public member variables (C++ public - accessed by HangameCrypto)
        public byte[] fbsub = new byte[256];     // forward S-box
        public byte[] rbsub = new byte[256];     // reverse S-box
        public uint[] ftable = new uint[256];    // forward table
        public uint[] rtable = new uint[256];    // reverse table
        
        public byte[] fi = new byte[24];         // forward increments (24 in C++)
        public byte[] ri = new byte[24];         // reverse increments (24 in C++)
        public uint[] fKey = new uint[120];      // forward key (120 in C++)
        public uint[] rKey = new uint[120];      // reverse key (120 in C++)

        // Protected member variables
        protected uint m_KeyCount = 4;
        protected uint m_BlockCount = 4;
        protected uint m_RoundCount = 0;
        protected bool m_isKeyInitialized = false;

        // Constructor
        public CryptoData()
        {
            m_isKeyInitialized = false;

            InCo[0] = 0x0b; // Inverse Coefficients
            InCo[1] = 0x0d;
            InCo[2] = 0x09;
            InCo[3] = 0x0e;

            m_KeyCount = 4;
            m_BlockCount = 4;
            m_RoundCount = 0;

            InitializeTable();
        }

        // Private inline functions from C++

        private byte XTime(byte n)
        {
            byte temp = (byte)((n & 0x80) != 0 ? 0x1b : 0);
            n <<= 1;
            n ^= temp;
            return n;
        }

        private byte Mul(byte a, byte b)
        {
            // x.y = AntiLog(Log(x) + Log(y))
            if (a != 0 && b != 0)
            {
                return ptab[(ltab[a] + ltab[b]) % 255];
            }
            return 0;
        }

        private byte Product(uint a, uint b)
        {
            // dot product of two 4-byte arrays
            byte[] arA = new byte[4];
            byte[] arB = new byte[4];

            UnPack(a, arA);
            UnPack(b, arB);

            return (byte)(Mul(arA[0], arB[0]) ^ Mul(arA[1], arB[1]) ^ Mul(arA[2], arB[2]) ^ Mul(arA[3], arB[3]));
        }

        private uint InvMixCol(uint n)
        {
            // matrix Multiplication
            uint m;
            byte[] temp = new byte[4];

            m = Pack(InCo);
            temp[3] = Product(m, n);

            m = ROTL24(m);
            temp[2] = Product(m, n);

            m = ROTL24(m);
            temp[1] = Product(m, n);

            m = ROTL24(m);
            temp[0] = Product(m, n);

            return Pack(temp);
        }

        private uint SubByte(uint n)
        {
            byte[] temp = new byte[4];

            UnPack(n, temp);
            temp[0] = fbsub[temp[0]];
            temp[1] = fbsub[temp[1]];
            temp[2] = fbsub[temp[2]];
            temp[3] = fbsub[temp[3]];

            return Pack(temp);
        }

        private byte ByteSub(byte n)
        {
            byte a = ptab[255 - ltab[n]];  // multiplicative inverse
            byte b = a;

            a = b;
            a = ROTL(a);
            b ^= a;
            a = ROTL(a);
            b ^= a;
            a = ROTL(a);
            b ^= a;
            a = ROTL(a);
            b ^= a;
            b ^= 0x63;

            return b;
        }

        // Rotation operations
        private byte ROTL(byte x)
        {
            return (byte)((x << 1) | (x >> 7));
        }

        protected uint ROTL8(uint x) => (x << 8) | (x >> 24);
        protected uint ROTL16(uint x) => (x << 16) | (x >> 16);
        protected uint ROTL24(uint x) => (x << 24) | (x >> 8);

        // Pack/Unpack operations (Little Endian)
        protected uint Pack(byte[] bytes, int offset = 0)
        {
            if (bytes == null || offset + 3 >= bytes.Length) return 0;
            return (uint)(bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24));
        }

        protected void UnPack(uint value, byte[] bytes, int offset = 0)
        {
            if (bytes == null || offset + 3 >= bytes.Length) return;
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        // Initialize Rijndael tables
        private void InitializeTable()
        {
            byte c;
            byte[] tB = new byte[4];

            // use 3 as primitive root to generate power and log tables
            ltab[0] = 0;
            ptab[0] = 1;
            ltab[1] = 0;
            ptab[1] = 3;
            ltab[3] = 1;

            for (int i = 2; i < 256; ++i)
            {
                ptab[i] = (byte)(ptab[i - 1] ^ XTime(ptab[i - 1]));
                ltab[ptab[i]] = (byte)i;
            }

            // affine transformation:- each bit is xored with itself shifted one bit
            fbsub[0] = 0x63;
            rbsub[0x63] = 0;

            for (int i = 1; i < 256; ++i)
            {
                c = ByteSub((byte)i);
                fbsub[i] = c;
                rbsub[c] = (byte)i;
            }

            for (int i = 0; i < 30; ++i)
            {
                c = (byte)(i == 0 ? 1 : XTime((byte)rco[i - 1]));
                rco[i] = c;
            }

            // calculate forward and reverse tables
            for (int i = 0; i < 256; ++i)
            {
                c = fbsub[i];
                tB[3] = (byte)(c ^ XTime(c));
                tB[2] = c;
                tB[1] = c;
                tB[0] = XTime(c);
                ftable[i] = Pack(tB);

                c = rbsub[i];
                tB[3] = Mul(InCo[0], c);
                tB[2] = Mul(InCo[1], c);
                tB[1] = Mul(InCo[2], c);
                tB[0] = Mul(InCo[3], c);
                rtable[i] = Pack(tB);
            }
        }

        // Initialize encryption key
        public void InitializeKey(int blockCount, int keyCount, byte[] key)
        {
            if (key == null)
            {
                return;
            }

            // blocksize=32*nb bits. Key=32*nk bits
            // currently nb,bk = 4, 6 or 8
            // key comes as 4*Nk bytes
            // Key Scheduler. Create expanded encryption key
            int N, K;
            uint C1, C2, C3;

            m_BlockCount = (uint)blockCount;
            m_KeyCount = (uint)keyCount;

            // Nr is number of rounds
            m_RoundCount = (uint)((blockCount >= keyCount) ? (6 + blockCount) : (6 + keyCount));

            C1 = 1;
            if (blockCount < 8)
            {
                C2 = 2;
                C3 = 3;
            }
            else
            {
                C2 = 3;
                C3 = 4;
            }

            // pre-calculate forward and reverse increments
            for (int i = 0, m = 0; i < blockCount; ++i, m += 3)
            {
                fi[m] = (byte)((i + C1) % blockCount);
                fi[m + 1] = (byte)((i + C2) % blockCount);
                fi[m + 2] = (byte)((i + C3) % blockCount);
                ri[m] = (byte)((blockCount + i - C1) % blockCount);
                ri[m + 1] = (byte)((blockCount + i - C2) % blockCount);
                ri[m + 2] = (byte)((blockCount + i - C3) % blockCount);
            }

            for (int i = 0, m = 0; i < keyCount; ++i, m += 4)
            {
                if (m + 3 < key.Length)
                {
                    fKey[i] = Pack(key, m);
                }
            }

            N = (int)(blockCount * (m_RoundCount + 1));
            for (int i = keyCount, m = 0; i < N; i += keyCount, ++m)
            {
                fKey[i] = fKey[i - keyCount] ^ SubByte(ROTL24(fKey[i - 1])) ^ rco[m];
                
                if (keyCount <= 6)
                {
                    for (int x = 1; x < keyCount && (x + i) < N; ++x)
                    {
                        fKey[x + i] = fKey[x + i - keyCount] ^ fKey[x + i - 1];
                    }
                }
                else
                {
                    for (int x = 1; x < 4 && (x + i) < N; ++x)
                    {
                        fKey[x + i] = fKey[x + i - keyCount] ^ fKey[x + i - 1];
                    }
                    if ((i + 4) < N)
                    {
                        fKey[i + 4] = fKey[i + 4 - keyCount] ^ SubByte(fKey[i + 3]);
                    }
                    for (int x = 5; x < keyCount && (x + i) < N; ++x)
                    {
                        fKey[x + i] = fKey[x + i - keyCount] ^ fKey[x + i - 1];
                    }
                }
            }

            // now for the expanded decrypt key in reverse order
            for (int i = 0; i < blockCount; ++i)
            {
                rKey[i + N - blockCount] = fKey[i];
            }

            for (int i = blockCount; i < N - blockCount; i += blockCount)
            {
                K = N - blockCount - i;
                for (int x = 0; x < blockCount; ++x)
                {
                    rKey[K + x] = InvMixCol(fKey[i + x]);
                }
            }

            for (int i = N - blockCount; i < N; ++i)
            {
                rKey[i - N + blockCount] = fKey[i];
            }

            m_isKeyInitialized = true;
        }

        // Public methods from C++ CryptoData.h

        /// <summary>
        /// C++ bool isKeyInitialized(void)
        /// </summary>
        public bool IsKeyInitialized => m_isKeyInitialized;

        /// <summary>
        /// C++ unsigned int GetBlockCount(void)
        /// </summary>
        public uint GetBlockCount() => m_BlockCount;

        /// <summary>
        /// C++ unsigned int GetRoundCount(void)
        /// </summary>
        public uint GetRoundCount() => m_RoundCount;
    }
}

