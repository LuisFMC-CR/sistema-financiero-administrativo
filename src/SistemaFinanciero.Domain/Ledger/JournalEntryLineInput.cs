namespace SistemaFinanciero.Domain.Ledger;

/// <summary>
/// Línea propuesta para un asiento, antes de validarse y numerarse. La construye
/// <c>JournalPostingService</c> (Infrastructure/Ledger) a partir de las reglas de contabilización
/// (RF-78); <c>JournalEntry</c> omite las que queden en cero (por ejemplo, el IVA de una tarifa
/// exenta) y valida que el resto balancee.
/// </summary>
public readonly record struct JournalEntryLineInput(Guid AccountId, decimal Debit, decimal Credit);
