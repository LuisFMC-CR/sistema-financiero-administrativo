using System.Globalization;

namespace SistemaFinanciero.Domain.Common;

/// <summary>
/// Construye el consecutivo (20 dígitos) y la clave (50 dígitos) del comprobante electrónico de
/// Hacienda, a partir de datos que quien llama ya resolvió (secuencia SQL, parámetros de la empresa,
/// fecha de emisión). El sistema nunca se conecta a Hacienda, no firma ni transmite nada: por eso la
/// situación del comprobante siempre es "1" (Normal).
/// </summary>
/// <remarks>
/// Conexiones: la comparten <c>Invoice.Confirm</c> y el constructor de <c>CreditNote</c>, los únicos
/// dos documentos tributarios del sistema; ninguno de los dos depende del otro. Es pública (a
/// diferencia de <see cref="DomainRules"/>) porque <c>InvoiceService</c> y <c>CreditNoteService</c>
/// (Infrastructure) deben poder llamarla y no hay <c>InternalsVisibleTo</c> en la solución.
/// </remarks>
public static class ElectronicDocumentKeyBuilder
{
    /// <summary>Longitud exacta del consecutivo del comprobante electrónico.</summary>
    public const int ConsecutivoLength = 20;

    /// <summary>Longitud exacta de la clave del comprobante electrónico.</summary>
    public const int ClaveLength = 50;

    private const int DocumentTypeCodeLength = 2;
    private const int BranchCodeLength = 3;
    private const int TerminalCodeLength = 5;
    private const int SequentialNumberLength = 10;
    private const int IssuerIdLength = 12;
    private const int LegalEntityIdLength = 10;
    private const int SecurityCodeLength = 8;
    private const long MinSequentialNumber = 1L;
    private const long MaxSequentialNumber = 9_999_999_999L;

    private const string CountryCode = "506";

    /// <summary>Situación del comprobante siempre "Normal": el sistema nunca transmite a Hacienda.</summary>
    private const string NormalSituationCode = "1";

    /// <summary>
    /// Código oficial de Hacienda para factura electrónica. Sin relación con
    /// <c>JournalEntrySourceType.Invoice</c> (ese es un enum interno de asientos contables, una
    /// numeración totalmente distinta).
    /// </summary>
    public const string InvoiceDocumentTypeCode = "01";

    /// <summary>
    /// Código oficial de Hacienda para nota de crédito electrónica. Sin relación con
    /// <c>JournalEntrySourceType.CreditNote = 7</c> (ese es un enum interno de asientos contables, una
    /// numeración totalmente distinta).
    /// </summary>
    public const string CreditNoteDocumentTypeCode = "03";

    /// <summary>
    /// Construye el consecutivo de 20 dígitos: tipo de documento (2) + sucursal (3) + terminal (5) +
    /// secuencial (10), tomado de una secuencia de SQL Server distinta por tipo de documento.
    /// </summary>
    public static string BuildConsecutivo(
        string documentTypeCode, string branchCode, string terminalCode, long sequentialNumber)
    {
        string validDocumentTypeCode = DomainRules.RequiredDigits(
            documentTypeCode, DocumentTypeCodeLength, nameof(documentTypeCode));
        string validBranchCode = DomainRules.RequiredDigits(branchCode, BranchCodeLength, nameof(branchCode));
        string validTerminalCode = DomainRules.RequiredDigits(terminalCode, TerminalCodeLength, nameof(terminalCode));

        if (sequentialNumber < MinSequentialNumber || sequentialNumber > MaxSequentialNumber)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequentialNumber),
                sequentialNumber,
                $"El secuencial del consecutivo debe estar entre {MinSequentialNumber} y {MaxSequentialNumber}.");
        }

        string consecutivo = validDocumentTypeCode + validBranchCode + validTerminalCode
            + sequentialNumber.ToString("D" + SequentialNumberLength, CultureInfo.InvariantCulture);

        return DomainRules.RequiredDigits(consecutivo, ConsecutivoLength, nameof(consecutivo));
    }

    /// <summary>
    /// Construye la clave de 50 dígitos: país (3, "506") + fecha de emisión (6, ddMMyy) + cédula del
    /// emisor ya rellenada a 12 dígitos + consecutivo (20) + situación (1, siempre "Normal") + código
    /// de seguridad (8).
    /// </summary>
    public static string BuildKey(
        DateOnly issueDate, string paddedIssuerId, string consecutivo, string securityCode)
    {
        string validIssuerId = DomainRules.RequiredDigits(paddedIssuerId, IssuerIdLength, nameof(paddedIssuerId));
        string validConsecutivo = DomainRules.RequiredDigits(consecutivo, ConsecutivoLength, nameof(consecutivo));
        string validSecurityCode = DomainRules.RequiredDigits(securityCode, SecurityCodeLength, nameof(securityCode));

        string issueDateDigits = issueDate.Day.ToString("D2", CultureInfo.InvariantCulture)
            + issueDate.Month.ToString("D2", CultureInfo.InvariantCulture)
            + (issueDate.Year % 100).ToString("D2", CultureInfo.InvariantCulture);

        string clave = CountryCode + issueDateDigits + validIssuerId + validConsecutivo
            + NormalSituationCode + validSecurityCode;

        return DomainRules.RequiredDigits(clave, ClaveLength, nameof(clave));
    }

    /// <summary>
    /// Rellena una cédula jurídica de 10 dígitos (<c>IdentificationType.LegalEntity</c>) a los 12
    /// dígitos que exige el campo del emisor en la clave de Hacienda.
    /// </summary>
    public static string PadIssuerId(string legalEntityId)
    {
        string validLegalEntityId = DomainRules.RequiredDigits(
            legalEntityId, LegalEntityIdLength, nameof(legalEntityId));

        return validLegalEntityId.PadLeft(IssuerIdLength, '0');
    }

    /// <summary>Genera un código de seguridad aleatorio de 8 dígitos, distinto en cada documento.</summary>
    public static string GenerateSecurityCode() =>
        Random.Shared.Next(0, 100_000_000).ToString("D" + SecurityCodeLength, CultureInfo.InvariantCulture);
}
