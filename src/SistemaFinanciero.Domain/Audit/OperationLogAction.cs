namespace SistemaFinanciero.Domain.Audit;

/// <summary>Acción financiera registrada en la bitácora de operaciones (regla 4/16).</summary>
public enum OperationLogAction
{
    /// <summary>El documento pasó de borrador a confirmado.</summary>
    Confirmed = 1,

    /// <summary>El documento, o el movimiento generado a partir de él, se anuló.</summary>
    Cancelled = 2,

    /// <summary>El saldo de la cuenta por cobrar o por pagar se ajustó manualmente (regla 4).</summary>
    Adjusted = 3,
}
