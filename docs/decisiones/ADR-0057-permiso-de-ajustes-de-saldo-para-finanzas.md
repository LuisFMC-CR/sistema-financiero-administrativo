# ADR-0057: Permiso de ajustes de saldo para Finanzas

- **Fecha:** 2026-11-07
- **Estado:** Aceptado
- **Actualiza:** ADR-0037, solo respecto a los perfiles autorizados.

## Contexto

La política `Movimientos.Ajustar` permitía los ajustes de saldo únicamente a Gerencia. Sin embargo,
la matriz de perfiles vigente asigna los ajustes a Finanzas y Gerencia, y el usuario confirmó que
Finanzas necesita registrar estos ajustes operativos.

## Decisión

- Finanzas y Gerencia pueden ajustar el saldo de cuentas por cobrar y por pagar.
- Asistente y Administrador no reciben este permiso.
- Se conserva la autorización del lado del servidor mediante `SystemPolicies.AdjustFinancialBalances`.
- No cambian las reglas de dominio, la validación del motivo, el historial inmutable, ni las
  validaciones de período y saldo.

## Consecuencias

- `AuthorizationExtensions` asocia `Movimientos.Ajustar` con los roles Finanzas y Gerencia.
- Los botones de ajuste ya reflejan automáticamente el permiso mediante la misma política.
- Las pruebas verifican que Finanzas y Gerencia son admitidos y que Asistente no puede ajustar.
- La trazabilidad se actualiza en RF-AJU-001.
