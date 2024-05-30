namespace SMTPNET.Sender.Models.Base
{
    public ref struct SmtpHeadersRolledForDKIN
    {
        public readonly ReadOnlySpan<char> SignatureColonDelimited { get; init; }
        public readonly string CanonicalizationedSMTPHeaders { get; init; }
    }
    public record DkimKeys(byte[] publicKey, byte[] privateKey);
}


