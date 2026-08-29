# ADR-0006: Clientes y proveedores separados

- **Fecha:** 2026-08-22
- **Estado:** Aceptado

## Contexto

Una persona física o jurídica podría relacionarse con la empresa como cliente y proveedor. Se evaluó
representarla mediante una entidad genérica de tercero con roles, lo que reduciría datos repetidos,
pero introduciría reglas compartidas y asociaciones indirectas en todos los documentos.

El proyecto requiere una lógica administrativa comprensible y los flujos de venta y gasto utilizan
responsabilidades diferentes.

## Decisión

Cliente y proveedor se modelan como entidades y catálogos independientes. Las facturas futuras
referenciarán clientes y los gastos futuros referenciarán proveedores, sin una tabla genérica de
terceros ni banderas de rol.

Si una misma persona cumple ambas funciones, se conserva un registro en cada catálogo. Su estado,
contacto y trazabilidad se administran de forma independiente.

## Consecuencias

- Los casos de uso y permisos se expresan directamente con términos del negocio.
- No es posible asociar por error un proveedor donde se requiere un cliente, o viceversa.
- Puede existir duplicación deliberada de nombre, identificación o contacto entre ambos catálogos.
- Una modificación en un catálogo no se replica silenciosamente en el otro.
- Las validaciones comunes solo se reutilizan cuando no mezclan el ciclo de vida de ambas entidades.
- Una unificación futura requeriría un nuevo ADR y una migración explícita de datos.
