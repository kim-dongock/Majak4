////////////////////////////////////////////////////////////////////////////////
//
// Hangame Crypto class (C++ to C# conversion)
// Original: Crypto.cpp - Hangame Encryption/Decryption Implementation
// Converted: 2025.10.20
//
////////////////////////////////////////////////////////////////////////////////

using System;
using System.Text;

namespace MajakServer.Utils
{
    /// <summary>
    /// Hangame Crypto - Main encryption/decryption class
    /// Behaves identically to C++ Hangame::Crypto class
    /// </summary>
    public class HangameCrypto : CryptoData
    {
        // Constants from C++ Crypto.h
        public const int MAX_BLOCK = 32;
        public const int MAX_LINE = 16384;

        private byte[] m_InitiateVector;

        /// <summary>
        /// C++ Crypto::Crypto constructor
        /// Default IV: "!@EF@#%@$%@$^%"
        /// </summary>
        public HangameCrypto(string? initiateVector = null) : base()
        {
            m_InitiateVector = new byte[MAX_BLOCK + 1];

            if (!string.IsNullOrEmpty(initiateVector))
            {
                byte[] ivBytes = Encoding.ASCII.GetBytes(initiateVector);
                int copyLength = Math.Min(ivBytes.Length, MAX_BLOCK);
                Array.Copy(ivBytes, 0, m_InitiateVector, 0, copyLength);
                
                // Fill remaining part with 0x00
                for (int i = copyLength; i < MAX_BLOCK; i++)
                {
                    m_InitiateVector[i] = 0x00;
                }
            }
            else
            {
                // Default IV: "!@EF@#%@$%@$^%"
                string defaultIV = "!@EF@#%@$%@$^%";
                byte[] ivBytes = Encoding.ASCII.GetBytes(defaultIV);
                int copyLength = Math.Min(ivBytes.Length, MAX_BLOCK);
                Array.Copy(ivBytes, 0, m_InitiateVector, 0, copyLength);
                
                for (int i = copyLength; i < MAX_BLOCK; i++)
                {
                    m_InitiateVector[i] = 0x00;
                }
            }
        }

        /// <summary>
        /// Implementation identical to C++ Encrypt
        /// </summary>
        public byte[]? Encrypt(byte[] buffer)
        {
            if (!m_isKeyInitialized || buffer == null || buffer.Length != 16)
                return null;

            uint[] a = new uint[8];
            uint[] b = new uint[8];

            // Pack input into 32-bit words
            for (int i = 0, j = 0; i < m_BlockCount; ++i, j += 4)
            {
                a[i] = Pack(buffer, j);
                a[i] ^= fKey[i]; // XOR with first round key
            }

            int k = (int)m_BlockCount;
            uint[] x = a;
            uint[] y = b;

            // Apply rounds 1 to Nr-1
            for (int round = 1; round < (int)m_RoundCount; ++round)
            {
                for (int j = 0, m = 0; j < m_BlockCount; ++j, m += 3)
                {
                    y[j] = fKey[k++] ^
                           ftable[x[j] & 0xFF] ^
                           ROTL8(ftable[(x[fi[m]] >> 8) & 0xFF]) ^
                           ROTL16(ftable[(x[fi[m + 1]] >> 16) & 0xFF]) ^
                           ROTL24(ftable[x[fi[m + 2]] >> 24]);
                }
                // Swap pointers
                uint[] t = x; x = y; y = t;
            }

            // Last round uses fbsub
            for (int j = 0, m = 0; j < m_BlockCount; ++j, m += 3)
            {
                y[j] = fKey[k++] ^
                       (uint)fbsub[x[j] & 0xFF] ^
                       ROTL8((uint)fbsub[(x[fi[m]] >> 8) & 0xFF]) ^
                       ROTL16((uint)fbsub[(x[fi[m + 1]] >> 16) & 0xFF]) ^
                       ROTL24((uint)fbsub[x[fi[m + 2]] >> 24]);
            }

            // Unpack result
            byte[] result = new byte[16];
            for (int i = 0, j = 0; i < m_BlockCount; ++i, j += 4)
            {
                UnPack(y[i], result, j);
                x[i] = y[i] = 0; // clean up
            }

            return result;
        }

        /// <summary>
        /// Implementation identical to C++ Decrypt
        /// </summary>
        public byte[]? Decrypt(byte[] buffer)
        {
            if (!m_isKeyInitialized || buffer == null || buffer.Length != 16)
                return null;

            uint[] a = new uint[8];
            uint[] b = new uint[8];

            // Pack input into 32-bit words
            for (int i = 0, j = 0; i < m_BlockCount; ++i, j += 4)
            {
                a[i] = Pack(buffer, j);
                a[i] ^= rKey[i]; // XOR with first round key
            }

            int k = (int)m_BlockCount;
            uint[] x = a;
            uint[] y = b;

            // Apply rounds 1 to Nr-1
            for (int round = 1; round < (int)m_RoundCount; ++round)
            {
                for (int j = 0, m = 0; j < m_BlockCount; ++j, m += 3)
                {
                    y[j] = rKey[k++] ^
                           rtable[x[j] & 0xFF] ^
                           ROTL8(rtable[(x[ri[m]] >> 8) & 0xFF]) ^
                           ROTL16(rtable[(x[ri[m + 1]] >> 16) & 0xFF]) ^
                           ROTL24(rtable[x[ri[m + 2]] >> 24]);
                }
                // Swap pointers
                uint[] t = x; x = y; y = t;
            }

            // Last round uses rbsub
            for (int j = 0, m = 0; j < m_BlockCount; ++j, m += 3)
            {
                y[j] = rKey[k++] ^
                       (uint)rbsub[x[j] & 0xFF] ^
                       ROTL8((uint)rbsub[(x[ri[m]] >> 8) & 0xFF]) ^
                       ROTL16((uint)rbsub[(x[ri[m + 1]] >> 16) & 0xFF]) ^
                       ROTL24((uint)rbsub[x[ri[m + 2]] >> 24]);
            }

            // Unpack result
            byte[] result = new byte[16];
            for (int i = 0, j = 0; i < m_BlockCount; ++i, j += 4)
            {
                UnPack(y[i], result, j);
                x[i] = y[i] = 0; // clean up
            }

            return result;
        }

        /// <summary>
        /// Identical to C++ CHgCrypt::InitKey
        /// Generate key using machine name + "xpfldntm" string
        /// 
        /// C++ logic:
        /// - if( m_Table.isKeyInitialized() == false ) - initialize only once
        /// </summary>
        public bool InitKey(string keyString)
        {
            // C++ only checks for NULL, empty strings are allowed
            if (keyString == null)
                return false;

            // C++: Skip if already initialized
            if (m_isKeyInitialized)
                return true;

            // Calculate dynamic key count
            int nKeyCount = (keyString.Length * 8) / 32;
            if ((keyString.Length * 8) % 32 != 0)
            {
                nKeyCount += 1;
            }
            if (nKeyCount < 4)
            {
                nKeyCount = 4;
            }

            int nKeyLen = nKeyCount * 4;
            byte[] szVerifyKey = new byte[nKeyLen];

            // Padding with 0x20 (space character)
            for (int i = 0; i < nKeyLen; i++)
            {
                szVerifyKey[i] = 0x20;
            }

            // Copy actual key data
            byte[] keyBytes = Encoding.ASCII.GetBytes(keyString);
            int nCopyLen = Math.Min(keyString.Length, nKeyLen);
            Array.Copy(keyBytes, 0, szVerifyKey, 0, nCopyLen);

            // Call InitializeKey
            InitializeKey(4, nKeyCount, szVerifyKey);

            return true;
        }

        /// <summary>
        /// C++ Crypto::Encrypt (with HMAC + CBC) - high-level encryption
        /// </summary>
        public byte[]? EncryptWithHMAC(byte[] source)
        {
            if (source == null || source.Length == 0)
                return null;

            const int MAX_LINE = 1024;
            byte[] buffer = new byte[MAX_LINE];

            // Generate HMAC (MD5)
            byte[] hmac = MakeHMAC(source);
            Array.Copy(hmac, 0, buffer, 0, 16);

            // Copy original data
            Array.Copy(source, 0, buffer, 16, source.Length);

            // CBC encryption
            byte[]? result = CBCEncrypt(buffer, source.Length + 16);

            // Clear buffer
            Array.Clear(buffer, 0, source.Length + 16);

            return result;
        }

        /// <summary>
        /// C++ Crypto::Decrypt (with HMAC + CBC) - high-level decryption
        /// </summary>
        public byte[]? DecryptWithHMAC(byte[] source)
        {
            if (source == null || source.Length == 0)
                return null;

            // CBC decryption
            byte[]? decrypted = CBCDecrypt(source);
            if (decrypted == null || decrypted.Length < 17)
                return null;

            // Verify HMAC
            byte[] receivedHMAC = new byte[16];
            Array.Copy(decrypted, 0, receivedHMAC, 0, 16);

            int dataLength = decrypted.Length - 16;
            byte[] data = new byte[dataLength];
            Array.Copy(decrypted, 16, data, 0, dataLength);

            byte[] calculatedHMAC = MakeHMAC(data);

            // Compare HMAC
            for (int i = 0; i < 16; i++)
            {
                if (receivedHMAC[i] != calculatedHMAC[i])
                {
                    return null; // MAC auth fail
                }
            }

            return data;
        }

        /// <summary>
        /// C++ MakeHMAC - Generate MD5 hash
        /// </summary>
        private byte[] MakeHMAC(byte[] source)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                return md5.ComputeHash(source);
            }
        }

        /// <summary>
        /// C++ CBCEncrypt - Cipher Block Chaining encryption
        /// </summary>
        private byte[]? CBCEncrypt(byte[] source, int sourceLength)
        {
            if (source == null || sourceLength == 0)
                return null;

            byte[] block = new byte[MAX_BLOCK + 1];
            byte[] prevCipherText = new byte[MAX_BLOCK + 1];

            int blockBase = (int)m_BlockCount * 4; // 16 bytes
            int blockSize = (sourceLength + blockBase - 1) / blockBase;

            // Create result buffer (4 bytes length + encrypted data)
            byte[] result = new byte[sizeof(uint) + blockSize * blockBase];

            // Store original length in first 4 bytes (Little Endian)
            BitConverter.GetBytes((uint)sourceLength).CopyTo(result, 0);

            byte[] buffer = new byte[blockSize * blockBase];

            // Copy IV (use m_InitiateVector)
            Array.Copy(m_InitiateVector, 0, prevCipherText, 0, Math.Min(MAX_BLOCK, m_InitiateVector.Length));

            for (int m = 0; m < blockSize; ++m)
            {
                Array.Clear(block, 0, block.Length);

                for (int i = 0; i < blockBase; ++i)
                {
                    int index = m * blockBase + i;
                    if (index < sourceLength)
                    {
                        block[i] = source[index];
                    }

                    block[i] ^= prevCipherText[i]; // XOR with previous cipher text
                }

                // Encrypt single block (pass exactly 16 bytes)
                byte[] blockToEncrypt = new byte[16];
                Array.Copy(block, 0, blockToEncrypt, 0, 16);
                byte[]? encryptedBlock = Encrypt(blockToEncrypt);
                if (encryptedBlock == null)
                    return null;

                for (int i = 0; i < blockBase; ++i)
                {
                    int index = m * blockBase + i;
                    buffer[index] = encryptedBlock[i];
                }

                Array.Copy(encryptedBlock, 0, prevCipherText, 0, blockBase);
            }

            // Copy result
            Array.Copy(buffer, 0, result, sizeof(uint), buffer.Length);

            return result;
        }

        /// <summary>
        /// C++ CBCDecrypt - Cipher Block Chaining decryption
        /// </summary>
        public byte[]? CBCDecrypt(byte[] source)
        {
            if (source == null || source.Length < sizeof(uint))
                return null;

            byte[] block = new byte[MAX_BLOCK + 1];
            byte[] prevCipherText = new byte[MAX_BLOCK + 1];
            byte[] prevCipher = new byte[MAX_BLOCK + 1];

            // Read original length from first 4 bytes (Little Endian)
            uint packetLength = BitConverter.ToUInt32(source, 0);
            int resultLength = source.Length - sizeof(uint);

            if (packetLength == 0 || packetLength > resultLength)
                return null;

            int blockBase = (int)m_BlockCount * 4; // 16 bytes
            int blockSize = (resultLength + blockBase - 1) / blockBase;

            byte[] result = new byte[resultLength];
            byte[] buffer = new byte[source.Length - sizeof(uint)];
            Array.Copy(source, sizeof(uint), buffer, 0, buffer.Length);

            // Copy IV (use m_InitiateVector)
            Array.Copy(m_InitiateVector, 0, prevCipherText, 0, Math.Min(MAX_BLOCK, m_InitiateVector.Length));

            for (int m = 0; m < blockSize; ++m)
            {
                Array.Clear(block, 0, block.Length);

                for (int i = 0; i < blockBase; ++i)
                {
                    int index = m * blockBase + i;
                    if (index < buffer.Length)
                    {
                        block[i] = buffer[index];
                    }
                }

                Array.Copy(block, 0, prevCipher, 0, blockBase);

                // Decrypt single block (pass exactly 16 bytes)
                byte[] blockToDecrypt = new byte[16];
                Array.Copy(block, 0, blockToDecrypt, 0, 16);
                byte[]? decryptedBlock = Decrypt(blockToDecrypt);
                if (decryptedBlock == null)
                    return null;

                for (int i = 0; i < blockBase; ++i)
                {
                    decryptedBlock[i] ^= prevCipherText[i]; // XOR with previous cipher text

                    int index = m * blockBase + i;
                    if (index < result.Length)
                    {
                        result[index] = decryptedBlock[i];
                    }
                }

                Array.Copy(prevCipher, 0, prevCipherText, 0, blockBase);
            }

            // Return only packet length bytes
            byte[] finalResult = new byte[packetLength];
            Array.Copy(result, 0, finalResult, 0, (int)packetLength);

            return finalResult;
        }

        /// <summary>
        /// C++ GetKeyWithComputerName - Generate key based on machine name
        /// </summary>
        public static string GetKeyWithComputerName()
        {
            string computerName = Environment.MachineName.ToUpper();

            // secret string: "xpfldntm"
            char[] secretString = new char[8];
            secretString[6] = 't';
            secretString[0] = 'x';
            secretString[7] = 'm';
            secretString[5] = 'n';
            secretString[1] = 'p';
            secretString[3] = 'l';
            secretString[2] = 'f';
            secretString[4] = 'd';

            return computerName + new string(secretString);
        }
    }

    /// <summary>
    /// C++ Hangame::CRYPTOGRAPHIC namespace
    /// High-level encryption/decryption functions with Base64 encoding
    /// </summary>
    public static class HangameCryptographic
    {
        private const int ENCRYPTION_BUFFER_SIZE = 1024;
        private const int BASE64_BUFFER_SIZE = ENCRYPTION_BUFFER_SIZE + (ENCRYPTION_BUFFER_SIZE / 2);
        private const int PLAIN_BUFFER_SIZE = ENCRYPTION_BUFFER_SIZE;

        /// <summary>
        /// C++ CRYPTOGRAPHIC::Encrypt
        /// Encrypts data with optional dynamic key (machine name based)
        /// Returns Base64 encoded result
        /// </summary>
        public static byte[]? Encrypt(byte[] source, bool useDynamicKey = true)
        {
            if (source == null || source.Length == 0)
                return null;

            try
            {
                var crypto = new HangameCrypto();

                // Create and initialize key
                string key = CreateInitializeKey(useDynamicKey);
                if (!crypto.InitKey(key))
                    return null;

                // Encrypt with HMAC
                byte[]? encrypted = crypto.EncryptWithHMAC(source);
                if (encrypted == null || encrypted.Length == 0)
                    return null;

                // Base64 encode
                string base64String = Convert.ToBase64String(encrypted);
                return Encoding.ASCII.GetBytes(base64String);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// C++ CRYPTOGRAPHIC::Decrypt
        /// Decrypts Base64 encoded data with optional dynamic key
        /// </summary>
        public static byte[]? Decrypt(byte[] source, bool useDynamicKey = false)
        {
            if (source == null || source.Length == 0)
                return null;

            try
            {
                // Base64 decode
                string base64String = Encoding.ASCII.GetString(source);
                byte[] encrypted = Convert.FromBase64String(base64String);

                var crypto = new HangameCrypto();

                // Create and initialize key
                string key = CreateInitializeKey(useDynamicKey);
                if (!crypto.InitKey(key))
                    return null;

                // Decrypt with HMAC verification
                byte[]? decrypted = crypto.DecryptWithHMAC(encrypted);
                return decrypted;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// C++ CreateInitializeKey
        /// Creates key based on useDynamicKey flag
        /// 
        /// C++ logic:
        /// - useDynamicKey=false: key = "\0\0\0\0"
        /// - strlen(key) = 0 (stops at first null)
        /// - Fill verifyKey with 16 bytes of spaces (0x20)
        /// - memcpy(verifyKey, key, 0) - nothing is copied
        /// - Result: entire 16 bytes are spaces
        /// </summary>
        private static string CreateInitializeKey(bool useDynamicKey)
        {
            if (!useDynamicKey)
            {
                // Static key: C++ strlen("\0\0\0\0") = 0
                // -> Treated as empty string, filled with all spaces
                return ""; // Empty string
            }

            return HangameCrypto.GetKeyWithComputerName();
        }
    }
}

