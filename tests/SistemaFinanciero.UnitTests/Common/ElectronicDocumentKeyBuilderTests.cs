using System.Text.RegularExpressions;
using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.UnitTests.Common;

public sealed class ElectronicDocumentKeyBuilderTests
{
    [Theory]
    [InlineData(ElectronicDocumentKeyBuilder.InvoiceDocumentTypeCode, "001", "00001", 1L,
        "01" + "001" + "00001" + "0000000001")]
    [InlineData(ElectronicDocumentKeyBuilder.CreditNoteDocumentTypeCode, "002", "00010", 9_999_999_999L,
        "03" + "002" + "00010" + "9999999999")]
    public void BuildConsecutivo_ConcatenatesSegmentsInOrder(
        string documentTypeCode, string branchCode, string terminalCode, long sequentialNumber, string expected)
    {
        string consecutivo = ElectronicDocumentKeyBuilder.BuildConsecutivo(
            documentTypeCode, branchCode, terminalCode, sequentialNumber);

        Assert.Equal(20, consecutivo.Length);
        Assert.Equal(expected, consecutivo);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("123")]
    [InlineData("ab")]
    public void BuildConsecutivo_RejectsADocumentTypeCodeThatIsNotTwoDigits(string invalidCode)
    {
        Assert.Throws<ArgumentException>(() =>
            ElectronicDocumentKeyBuilder.BuildConsecutivo(invalidCode, "001", "00001", 1L));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1234")]
    public void BuildConsecutivo_RejectsABranchCodeThatIsNotThreeDigits(string invalidCode)
    {
        Assert.Throws<ArgumentException>(() =>
            ElectronicDocumentKeyBuilder.BuildConsecutivo(
                ElectronicDocumentKeyBuilder.InvoiceDocumentTypeCode, invalidCode, "00001", 1L));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("123456")]
    public void BuildConsecutivo_RejectsATerminalCodeThatIsNotFiveDigits(string invalidCode)
    {
        Assert.Throws<ArgumentException>(() =>
            ElectronicDocumentKeyBuilder.BuildConsecutivo(
                ElectronicDocumentKeyBuilder.InvoiceDocumentTypeCode, "001", invalidCode, 1L));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(10_000_000_000L)]
    public void BuildConsecutivo_RejectsASequentialNumberOutsideTheTenDigitRange(long invalidSequential)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ElectronicDocumentKeyBuilder.BuildConsecutivo(
                ElectronicDocumentKeyBuilder.InvoiceDocumentTypeCode, "001", "00001", invalidSequential));
    }

    [Fact]
    public void BuildKey_ConcatenatesSegmentsInOrder()
    {
        string consecutivo = ElectronicDocumentKeyBuilder.BuildConsecutivo(
            ElectronicDocumentKeyBuilder.InvoiceDocumentTypeCode, "001", "00001", 42L);
        string issuerId = ElectronicDocumentKeyBuilder.PadIssuerId("3101365118");

        string key = ElectronicDocumentKeyBuilder.BuildKey(
            new DateOnly(2026, 3, 15), issuerId, consecutivo, "12345678");

        Assert.Equal(50, key.Length);
        Assert.Equal("506", key[..3]); // País.
        Assert.Equal("150326", key[3..9]); // dd MM yy.
        Assert.Equal("003101365118", key[9..21]); // Cédula del emisor, ya rellenada a 12 dígitos.
        Assert.Equal(consecutivo, key[21..41]); // Consecutivo de 20 dígitos.
        Assert.Equal("1", key[41..42]); // Situación: siempre Normal.
        Assert.Equal("12345678", key[42..50]); // Código de seguridad.
    }

    [Theory]
    [InlineData("123")]
    [InlineData("abc")]
    public void BuildKey_RejectsAnIssuerIdThatIsNotTwelveDigits(string invalidIssuerId)
    {
        string consecutivo = ElectronicDocumentKeyBuilder.BuildConsecutivo(
            ElectronicDocumentKeyBuilder.InvoiceDocumentTypeCode, "001", "00001", 1L);

        Assert.Throws<ArgumentException>(() =>
            ElectronicDocumentKeyBuilder.BuildKey(new DateOnly(2026, 1, 1), invalidIssuerId, consecutivo, "12345678"));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("abc")]
    public void BuildKey_RejectsAConsecutivoThatIsNotTwentyDigits(string invalidConsecutivo)
    {
        string issuerId = ElectronicDocumentKeyBuilder.PadIssuerId("3101365118");

        Assert.Throws<ArgumentException>(() =>
            ElectronicDocumentKeyBuilder.BuildKey(new DateOnly(2026, 1, 1), issuerId, invalidConsecutivo, "12345678"));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("abcdefgh")]
    public void BuildKey_RejectsASecurityCodeThatIsNotEightDigits(string invalidSecurityCode)
    {
        string consecutivo = ElectronicDocumentKeyBuilder.BuildConsecutivo(
            ElectronicDocumentKeyBuilder.InvoiceDocumentTypeCode, "001", "00001", 1L);
        string issuerId = ElectronicDocumentKeyBuilder.PadIssuerId("3101365118");

        Assert.Throws<ArgumentException>(() =>
            ElectronicDocumentKeyBuilder.BuildKey(new DateOnly(2026, 1, 1), issuerId, consecutivo, invalidSecurityCode));
    }

    [Fact]
    public void PadIssuerId_PrependsTwoZerosToATenDigitLegalEntityId()
    {
        string padded = ElectronicDocumentKeyBuilder.PadIssuerId("3101365118");

        Assert.Equal("003101365118", padded);
        Assert.Equal(12, padded.Length);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("12345678901")]
    [InlineData("abcdefghij")]
    public void PadIssuerId_RejectsAnIdThatIsNotExactlyTenDigits(string invalidId)
    {
        Assert.Throws<ArgumentException>(() => ElectronicDocumentKeyBuilder.PadIssuerId(invalidId));
    }

    [Fact]
    public void GenerateSecurityCode_AlwaysProducesExactlyEightDigits()
    {
        for (int iteration = 0; iteration < 200; iteration++)
        {
            string code = ElectronicDocumentKeyBuilder.GenerateSecurityCode();

            Assert.Matches(new Regex(@"^\d{8}$"), code);
        }
    }
}
