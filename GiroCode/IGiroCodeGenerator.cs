namespace GiroCode;

public interface IGiroCodeGenerator
{
    /// <summary>
    /// Generates the giro code.
    /// </summary>
    /// <param name="beneficiary">The beneficiary.</param>
    /// <param name="iban">The iban.</param>
    /// <param name="remittance">The remittance.</param>
    /// <param name="amount">The amount.</param>
    /// <param name="bic">The bic.</param>
    /// <param name="charSet">The character set.</param>
    /// <returns>giro code as byte array</returns>
    byte[] GenerateGiroCode(
        string beneficiary,
        string iban,
        string remittance,
        decimal amount,
        string bic,
        CharSet charSet = CharSet.Utf8);
}