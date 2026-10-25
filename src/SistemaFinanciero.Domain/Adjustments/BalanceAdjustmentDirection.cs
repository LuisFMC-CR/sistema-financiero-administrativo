namespace SistemaFinanciero.Domain.Adjustments;

/// <summary>Sentido de un ajuste manual de saldo (regla de negocio 4).</summary>
public enum BalanceAdjustmentDirection
{
    /// <summary>El ajuste reduce el saldo.</summary>
    Decrease = 1,

    /// <summary>El ajuste aumenta el saldo.</summary>
    Increase = 2,
}
