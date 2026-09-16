// =============================================================================
//  SettingsVault.cs - AES-256 + HMAC locked settings backup (.rpas).
// =============================================================================
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace RbaCore
{
    public static class SettingsVault
    {
        public const string FileFilter =
            "نسخة إعدادات مقفلة (*.rpas)|*.rpas|نسخة JSON قديمة (*.json)|*.json|كل الملفات (*.*)|*.*";

        // Same admin pair as SettingsLock — used to seal EXE + encrypt live config.
        public static string DefaultPassphrase()
        {
            return "wilzyadmin\nwilzy@1234";
        }

        public static string ReadUtf8Auto(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) { return ""; }
            if (LooksLocked(path))
            {
                return ReadLocked(path, DefaultPassphrase());
            }
            string raw = File.ReadAllText(path, Encoding.UTF8);
            if (!string.IsNullOrEmpty(raw) && raw[0] == '\ufeff') { raw = raw.Substring(1); }
            return raw;
        }

        public static void WriteUtf8Locked(string path, string utf8)
        {
            WriteLocked(path, utf8 ?? "", DefaultPassphrase());
        }

        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("RPAS");
        private const int Version = 1;
        private const int SaltLen = 16;
        private const int IvLen = 16;
        private const int KeyLen = 32;
        private const int MacLen = 32;
        private const int Iterations = 80000;

        public static bool LooksLocked(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) { return false; }
                byte[] head = new byte[4];
                using (FileStream fs = File.OpenRead(path))
                {
                    if (fs.Read(head, 0, 4) != 4) { return false; }
                }
                return head[0] == Magic[0] && head[1] == Magic[1] &&
                       head[2] == Magic[2] && head[3] == Magic[3];
            }
            catch
            {
                return false;
            }
        }

        public static void WriteLocked(string path, string utf8Json, string passphrase)
        {
            if (string.IsNullOrEmpty(path)) { throw new ArgumentException("path"); }
            if (utf8Json == null) { utf8Json = ""; }
            if (string.IsNullOrEmpty(passphrase)) { throw new InvalidOperationException("missing lock passphrase"); }

            byte[] plain = Encoding.UTF8.GetBytes(utf8Json);
            byte[] salt = new byte[SaltLen];
            byte[] iv = new byte[IvLen];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt);
                rng.GetBytes(iv);
            }

            byte[] encKey;
            byte[] macKey;
            DeriveKeys(passphrase, salt, out encKey, out macKey);
            byte[] cipher;
            try
            {
                cipher = Transform(plain, encKey, iv, true);
                byte[] mac = ComputeMac(macKey, salt, iv, cipher);

                using (FileStream fs = File.Create(path))
                using (BinaryWriter w = new BinaryWriter(fs))
                {
                    w.Write(Magic);
                    w.Write((byte)Version);
                    w.Write(salt);
                    w.Write(iv);
                    w.Write(mac);
                    w.Write(cipher);
                }
            }
            finally
            {
                Clear(encKey);
                Clear(macKey);
                Clear(plain);
            }
        }

        public static string ReadLocked(string path, string passphrase)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                throw new FileNotFoundException("locked settings file not found", path);
            }
            if (string.IsNullOrEmpty(passphrase))
            {
                throw new InvalidOperationException("missing lock passphrase");
            }

            byte[] blob = File.ReadAllBytes(path);
            int min = 4 + 1 + SaltLen + IvLen + MacLen;
            if (blob.Length < min)
            {
                throw new InvalidDataException("locked settings file is truncated");
            }
            if (blob[0] != Magic[0] || blob[1] != Magic[1] || blob[2] != Magic[2] || blob[3] != Magic[3])
            {
                throw new InvalidDataException("not a locked settings file");
            }
            if (blob[4] != Version)
            {
                throw new InvalidDataException("unsupported locked settings version");
            }

            byte[] salt = Slice(blob, 5, SaltLen);
            byte[] iv = Slice(blob, 5 + SaltLen, IvLen);
            byte[] mac = Slice(blob, 5 + SaltLen + IvLen, MacLen);
            byte[] cipher = Slice(blob, min, blob.Length - min);

            byte[] encKey;
            byte[] macKey;
            DeriveKeys(passphrase, salt, out encKey, out macKey);
            try
            {
                byte[] expect = ComputeMac(macKey, salt, iv, cipher);
                if (!FixedEquals(mac, expect))
                {
                    throw new CryptographicException("wrong password or file was tampered");
                }
                byte[] plain = Transform(cipher, encKey, iv, false);
                return Encoding.UTF8.GetString(plain);
            }
            finally
            {
                Clear(encKey);
                Clear(macKey);
            }
        }

        private static void DeriveKeys(string passphrase, byte[] salt, out byte[] encKey, out byte[] macKey)
        {
            using (Rfc2898DeriveBytes kdf = new Rfc2898DeriveBytes(passphrase, salt, Iterations))
            {
                byte[] material = kdf.GetBytes(KeyLen + KeyLen);
                encKey = new byte[KeyLen];
                macKey = new byte[KeyLen];
                Buffer.BlockCopy(material, 0, encKey, 0, KeyLen);
                Buffer.BlockCopy(material, KeyLen, macKey, 0, KeyLen);
                Clear(material);
            }
        }

        private static byte[] Transform(byte[] data, byte[] key, byte[] iv, bool encrypt)
        {
            using (AesCryptoServiceProvider aes = new AesCryptoServiceProvider())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;
                aes.IV = iv;
                using (ICryptoTransform t = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor())
                {
                    return t.TransformFinalBlock(data, 0, data.Length);
                }
            }
        }

        private static byte[] ComputeMac(byte[] macKey, byte[] salt, byte[] iv, byte[] cipher)
        {
            using (HMACSHA256 h = new HMACSHA256(macKey))
            {
                h.TransformBlock(salt, 0, salt.Length, salt, 0);
                h.TransformBlock(iv, 0, iv.Length, iv, 0);
                h.TransformFinalBlock(cipher, 0, cipher.Length);
                return h.Hash;
            }
        }

        private static byte[] Slice(byte[] src, int offset, int len)
        {
            byte[] d = new byte[len];
            Buffer.BlockCopy(src, offset, d, 0, len);
            return d;
        }

        private static bool FixedEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) { return false; }
            int diff = 0;
            for (int i = 0; i < a.Length; i++) { diff |= a[i] ^ b[i]; }
            return diff == 0;
        }

        private static void Clear(byte[] b)
        {
            if (b == null) { return; }
            for (int i = 0; i < b.Length; i++) { b[i] = 0; }
        }
    }
}
