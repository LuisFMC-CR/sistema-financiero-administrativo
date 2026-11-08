# ADR-0055: Desactivación lógica de cuentas contables y movimientos históricos

- **Fecha:** 2026-11-07
- **Estado:** Aceptado
- **Complementa:** [ADR-0012](ADR-0012-catalogo-de-cuentas-contables.md) y
  [ADR-0017](ADR-0017-catalogo-base-protegido-cuentas-contables.md)

## Contexto

Las cuentas de sistema se identifican por `SystemRole` y los asientos automáticos las buscan por ese
rol. La regla anterior prohibía inactivarlas incondicionalmente, mientras que las cuentas ordinarias
con movimientos quedaban bloqueadas por el servicio y ni siquiera mostraban la acción en pantalla.
Se necesita una baja lógica consistente que preserve el libro mayor y permita reconocer claramente por
qué una cuenta no se puede desactivar.

## Decisión

- El botón para cambiar el estado se muestra para las cuentas de movimiento editables, incluidas las
  cuentas de sistema, aunque tengan movimientos.
- El servicio rechaza la desactivación cuando `finanzas.AsientoLineas` contiene cualquier movimiento
  para la cuenta e informa que existen movimientos asociados. No elimina ni modifica asientos.
- Si no hay movimientos, la cuenta puede desactivarse; las opciones manuales continúan incluyendo solo
  cuentas activas.
- Los asientos automáticos buscan el rol de sistema y además exigen que la cuenta esté activa. Si está
  inactiva, la operación se rechaza con un mensaje que solicita reactivarla. Las reversiones históricas
  conservan sus cuentas originales.
- Se mantienen las restricciones de cuentas de grupo protegidas, cuentas hijas activas y concurrencia.

## Consecuencias

- Una cuenta con movimientos históricos permanece activa para conservar la integridad del libro mayor.
- Una cuenta de sistema sin movimientos puede inactivarse; las operaciones que dependan de su rol no
  podrán registrarse hasta reactivarla.
- No se requiere migración de base de datos.
- La regla vigente se refleja en `CLAUDE.md` y en la trazabilidad RN-CTA-004.
