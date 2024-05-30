using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SMTPNET.Sender.Models.Base;


namespace SMTPNET.Sender
{
    public static class CurrentDkimKeys
    {


        public static string HashBody(string body)
        {
            return Convert.ToBase64String(theRSA.SignData(Encoding.UTF8.GetBytes(body),
                   HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
        }

        /// <summary>
        /// First 
        /// </summary>
        /// <param name="smtpHeaders"></param>
        /// <returns>Item1 = Headers' names, Item2 = Headers with values</returns>
        public static SmtpHeadersRolledForDKIN HashedHeaders(MailMessage message)
        {
            string[] sw = new string[2];
            string key;
            for (int i = 0; i < message.Headers.Count; i++)
            {
                key = message.Headers.GetKey(i) ?? "";
                Console.WriteLine($"Lowered Key: {key}");
                sw[0] += $"{key}:";
                sw[1] += $"{key}: {message.Headers[i]}\n";
            }
            sw[0] += "from:";
            sw[1] += $"From: {message.From}\n";
            sw[0] += "to:";
            sw[1] += $"To: {message.To}\n";
            sw[0] += "subject:";
            sw[1] += $"Subject: {message.Subject}\n";


            ReadOnlySpan<char> headersColonDelimited = sw[0].ToString();
            return new SmtpHeadersRolledForDKIN()
            {
                SignatureColonDelimited = headersColonDelimited[0..(headersColonDelimited.Length - 1)],
                CanonicalizationedSMTPHeaders = sw[1].ToString()

            };
        }

        public static string HashHeadersAndBody(string canonicalizedHeaders, string body)
        {
            string combined = canonicalizedHeaders + "\r\n" + body;
            using (SHA256 sha256 = SHA256.Create())
            {

                byte[] hashedBytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(combined));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        public static string Sign(string data)
        {
            return Convert.ToBase64String(theRSA.SignData(Encoding.ASCII.GetBytes(data),
                   HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
        }

        public static string dkimKeysPath = Path.Combine(AppContext.BaseDirectory, "dkims.json");

        public static RSA theRSA { get; set; } = LoadRSAOfKeys(GetCurrentOrNewDkimKeys());


        /// <summary>
        /// This will give the DkimKeys or generate new ones on failed attempt.
        /// </summary>
        /// <returns></returns>
        public static DkimKeys GetCurrentOrNewDkimKeys()
        {
            DkimKeys? keys = null;
            try
            {
                keys = GetCurrentDkimKeys();
            }
            catch { }
            if (keys is null)
            {
                return GenerateRsaParameters();
            }
            else
            { return keys; }
        }

        public static DkimKeys? GetCurrentDkimKeys()
        {
            DkimKeys? keys = null;
            try
            {
                using (FileStream fs = File.OpenRead(dkimKeysPath))
                {
                    keys = JsonSerializer.Deserialize<DkimKeys>(fs);
                }
            }
            catch { }
            return keys;
        }


        public static RSA LoadRSAOfKeys(DkimKeys keys)
        {
            RSA rsa = RSA.Create();
            int importCount = 0;
            rsa.ImportRSAPublicKey(keys.publicKey, out importCount);
            if (importCount > 0)
            {
                Console.WriteLine($"Imported public key of: {importCount} bytes");
            }
            rsa.ImportRSAPrivateKey(keys.privateKey, out importCount);
            if (importCount > 0)
            {
                Console.WriteLine($"Imported private key of: {importCount} bytes");
            }
            return rsa;
        }




        public static DkimKeys GenerateRsaParameters(int keySize = 2048)
        {
            theRSA = RSA.Create(keySize);
            DkimKeys keys = new(theRSA.ExportRSAPublicKey(), theRSA.ExportRSAPrivateKey());
            try
            {
                using (FileStream fs = File.Create(dkimKeysPath))
                {
                    JsonSerializer.Serialize(fs, keys);
                }
            }
            catch { }

            return keys;
        }
    }
}


