using System;
using System.Security.Cryptography;
using System.Text;

namespace MajakServer.Utils
{
    /// <summary>
    /// Encryption helper class
    /// Performs encryption/decryption using Hangame Crypto library
    /// </summary>
    public static class CryptoHelper
    {
        /// <summary>
        /// Encrypt string (using Hangame Crypto)
        /// </summary>
        /// <param name="plaintext">Plain text to encrypt</param>
        /// <param name="key">Encryption key (not used - machine name-based key is auto-generated)</param>
        /// <returns>Base64 encoded encrypted string</returns>
        public static string Encrypt(string plaintext, string key)
        {
            if (string.IsNullOrEmpty(plaintext))
                return string.Empty;

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plaintext);
                byte[]? encrypted = HangameCryptographic.Encrypt(plainBytes, useDynamicKey: true);
                
                if (encrypted == null)
                    return string.Empty;

                return Encoding.ASCII.GetString(encrypted);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Decrypt string (using Hangame Crypto)
        /// </summary>
        /// <param name="encrypted">Base64 encoded encrypted string</param>
        /// <param name="key">Decryption key (not used - machine name-based key is auto-generated)</param>
        /// <returns>Decrypted plain text</returns>
        public static string Decrypt(string encrypted, string key)
        {
            if (string.IsNullOrEmpty(encrypted))
                return string.Empty;

            try
            {
                byte[] encryptedBytes = Encoding.ASCII.GetBytes(encrypted);
                byte[]? decrypted = HangameCryptographic.Decrypt(encryptedBytes, useDynamicKey: false);
                
                if (decrypted == null)
                {
                    Console.WriteLine($"[CryptoHelper.Decrypt] Decryption returned null for input: {encrypted.Substring(0, Math.Min(50, encrypted.Length))}");
                    return string.Empty;
                }

                var result = Encoding.UTF8.GetString(decrypted);
                Console.WriteLine($"[CryptoHelper.Decrypt] Successfully decrypted: input length={encrypted.Length}, output length={result.Length}, output='{result}'");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CryptoHelper.Decrypt] Exception: {ex.GetType().Name} - {ex.Message}");
                Console.WriteLine($"[CryptoHelper.Decrypt] Input: {encrypted.Substring(0, Math.Min(100, encrypted.Length))}");
                Console.WriteLine($"[CryptoHelper.Decrypt] StackTrace: {ex.StackTrace}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Decrypt AWS Parameter Store value
        /// Decrypt Base64 encoded Hangame Crypto encrypted data
        /// </summary>
        /// <param name="encryptedBase64">Encrypted Base64 string retrieved from AWS Parameter Store</param>
        /// <returns>Decrypted plain text</returns>
        public static string DecryptParameterStoreValue(string encryptedBase64)
        {
            if (string.IsNullOrEmpty(encryptedBase64))
                return string.Empty;

            try
            {
                // Convert Base64 string to byte array
                byte[] encryptedBytes = Encoding.ASCII.GetBytes(encryptedBase64);
                
                // Use HangameCryptographic.Decrypt (Base64 decoding + HMAC + CBC decryption)
                byte[]? decrypted = HangameCryptographic.Decrypt(encryptedBytes, useDynamicKey: false);
                
                if (decrypted == null)
                    return string.Empty;

                return Encoding.UTF8.GetString(decrypted);
            }
            catch (Exception ex)
            {
                // Retain exception info for logging
                Console.WriteLine($"DecryptParameterStoreValue failed: {ex.Message}");
                return string.Empty;
            }
        }
    }
}

