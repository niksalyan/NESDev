using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text;

namespace NTOSDev.Libs
{
    internal class RegistryHelper
    {
        private const string AppRegistryPath = @"Software\NTOSDev";

        /// <summary>
        /// Writes a value to the registry under the application's key.
        /// </summary>
        /// <param name="keyName">The name of the registry key.</param>
        /// <param name="value">The value to write.</param>
        public static void WriteRegistry(string keyName, string value)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(AppRegistryPath))
                {
                    if (key == null)
                    {
                        throw new InvalidOperationException("Failed to create or open registry key.");
                    }

                    key.SetValue(keyName, value, RegistryValueKind.String);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to registry: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads a value from the registry under the application's key.
        /// </summary>
        /// <param name="keyName">The name of the registry key.</param>
        /// <returns>The value as a string, or null if not found.</returns>
        public static string ReadRegistry(string keyName, string defaultValue = "")
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(AppRegistryPath))
                {
                    if (key == null)
                    {
                        return defaultValue; // Key does not exist
                    }

                    return key.GetValue(keyName) as string;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading from registry: {ex.Message}");
                return defaultValue;
            }
        }

        /// <summary>
        /// Writes a value to the registry under the application's key.
        /// </summary>
        /// <param name="keyName">The name of the registry key.</param>
        /// <param name="value">The value to write.</param>
        public static void WritePRegistry(string keyName, string value)
        {
            WriteRegistry(keyName, Protect(value));
        }

        /// <summary>
        /// Reads a value from the registry under the application's key.
        /// </summary>
        /// <param name="keyName">The name of the registry key.</param>
        /// <returns>The value as a string, or null if not found.</returns>
        public static string ReadPRegistry(string keyName, string defaultValue = "")
        {
            return Unprotect(ReadRegistry(keyName, null)) ?? defaultValue;
        }

        /// <summary>
        /// Deletes a value from the registry under the application's key.
        /// </summary>
        /// <param name="keyName">The name of the registry key to delete.</param>
        public static void DeleteRegistry(string keyName)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(AppRegistryPath, writable: true))
                {
                    if (key != null)
                    {
                        key.DeleteValue(keyName, throwOnMissingValue: false);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting from registry: {ex.Message}");
            }
        }

        public static string Protect(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            byte[] data = Encoding.UTF8.GetBytes(plainText);
            byte[] encrypted = ProtectedData.Protect(
                data,
                null,
                DataProtectionScope.CurrentUser
            );
            return Convert.ToBase64String(encrypted);
        }

        public static string Unprotect(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return string.Empty;

            try
            {
                byte[] encryptedData = Convert.FromBase64String(encryptedText);
                byte[] decrypted = ProtectedData.Unprotect(
                    encryptedData,
                    null,
                    DataProtectionScope.CurrentUser
                );
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                // Return empty if corrupted or invalid
                return string.Empty;
            }
        }
    }
}
