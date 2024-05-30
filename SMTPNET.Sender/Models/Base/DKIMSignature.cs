using System.Text;

namespace SMTPNET.Sender.Models.Base
{
    public ref struct DKIMSignature
    {

        public DKIMSignature(string domainName, ReadOnlySpan<char> headersNamesColonDelimited, string signingAlgorithm = "rsa-sha256")
        {
            DomainName = domainName;
            SigningAlgorithm = signingAlgorithm;
            Headers = $"h={headersNamesColonDelimited}";
            Selector = "dkim." + domainName;
        }

        /// <summary>
        /// Version as no
        /// </summary>
        public readonly uint Version { get; init; } = 1;
        /// <summary>
        /// Signing algorithm default: (rsa-sha256)
        /// </summary>
        public readonly ReadOnlySpan<char> SigningAlgorithm { get; init; }
        /// <summary>
        /// Domain of sender / host
        /// </summary>
        public readonly string DomainName { get; init; }
        /// <summary>
        /// DNS Selector 
        /// </summary>
        public readonly ReadOnlySpan<char> Selector { get; init; }
        /// <summary>
        /// Canonicalization algorithm that’s used for both the header and body.
        /// </summary>
        public readonly ReadOnlySpan<char> Canonicalization { get; init; } = "relaxed/relaxed";
        /// <summary>
        /// Query method
        /// </summary>
        public readonly ReadOnlySpan<char> QueryMethod { get; init; } = "dns/txt";
        /// <summary>
        ///  A timestamp of when the message was signed.
        /// </summary>
        public ReadOnlySpan<char> IDTimeStamp { get; set; }
        /// <summary>
        /// List of headers, separated by colons(:).
        /// </summary>
        public readonly ReadOnlySpan<char> Headers { get; init; }
        /// <summary>
        /// The hashed message body, after being canonicalized with the method from “c” tag and then run through the hash function from “a” tag.
        /// </summary>
        public ReadOnlySpan<char> BodyHash { get; set; }
        /// <summary>
        ///  This is the digital signature of both headers and body, hashed with the very same function.
        /// </summary>
        public ReadOnlySpan<char> MessageDigitalSignature { get; set; }

        public ReadOnlySpan<char> CreateDkimHeaderValue()
        {
            return $"v={Version}; a={SigningAlgorithm}; d={DomainName}; s={Selector}; " +
                  $"c={Canonicalization}; \r\nh={Headers}; \r\nbh={BodyHash}; \r\nb={MessageDigitalSignature}\r\n";
        }

        public override string ToString()
        {
            StringBuilder sb = new();
            sb.Append($"v={Version}; a={SigningAlgorithm}; d={DomainName}; s={Selector}; ");
            sb.AppendLine($"c={Canonicalization};");
            sb.AppendLine($"h={Headers};");
            sb.AppendLine($"bh={BodyHash}");
            sb.AppendLine($"b={MessageDigitalSignature}");
            sb.Append("\r\n");
            return sb.ToString();
        }
    }
}
