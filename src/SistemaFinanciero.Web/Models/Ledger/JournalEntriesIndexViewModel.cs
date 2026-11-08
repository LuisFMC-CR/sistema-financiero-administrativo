using SistemaFinanciero.Application.Ledger;

namespace SistemaFinanciero.Web.Models.Ledger;

/// <summary>
/// Modelo de la consulta general de asientos contables: el mes consultado, el estado filtrado y el
/// resultado (conteo, total de movimientos y detalle de cada asiento).
/// </summary>
public sealed record JournalEntriesIndexViewModel(
    int PeriodYear,
    int PeriodMonth,
    JournalEntryStateFilter State,
    long? EntryNumber,
    JournalEntryListResult Result);
