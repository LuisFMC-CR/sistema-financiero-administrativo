# ADR-0060: Inicio del Administrador con los últimos registros de seguridad

- **Fecha:** 2026-11-16
- **Estado:** Aceptado
- **Relacionado:** ADR-0059 (congelamiento del sistema), ADR-0049 (menú lateral).

## Contexto

El Administrador no tiene acceso financiero, así que su inicio (Dashboard) solo mostraba el encabezado
"Control financiero contable", sin contenido. El usuario pidió un inicio más propio del perfil. Con el
sistema congelado (ADR-0059), se descartó quitar el menú lateral, porque RF-MEN-001 y ADR-0049 lo
describen. El contenido del inicio del Administrador no lo describe ningún documento: RF-CON-001,
RF-42 y RF-59 solo definen los indicadores financieros, que el Administrador no ve.

## Decisión

1. Si el usuario cumple `ManageUsers` (solo Administrador), `HomeController.Index` consulta
   `ISecurityAuditService.SearchAsync` (página 1, de la más reciente a la más antigua) y pasa los
   primeros cinco registros en `HomeIndicatorsViewModel.RecentSecurityEvents`.
2. La vista titula el inicio "Administración del sistema" y muestra la tabla "Últimos registros de
   seguridad" con las columnas de la bitácora y el enlace "Ver bitácora completa"; si la bitácora está
   vacía, muestra un mensaje.
3. Los demás perfiles conservan el título y los indicadores de siempre.

## Consecuencias

- Es compatible con el congelamiento: no cambia el menú, las rutas, los campos, las reglas, los
  permisos (el Administrador ya veía la bitácora completa), los reportes ni el modelo de datos.
- No hay contrato nuevo: se reutiliza la consulta de `SecurityAuditController`.
- Si el documento del TFG incluye una captura del inicio del Administrador, conviene actualizarla.
- Prueba: `AdministratorHomeTests`, con una bitácora en memoria.
