# Casos de uso

Este archivo conserva una versión técnica trazable. Para el informe final se trasladará al formato
institucional sin cambiar el comportamiento aquí definido.

## UC-SEG-001 — Iniciar sesión

- **Actor:** usuario interno.
- **Objetivo:** acceder a las funciones autorizadas mediante correo y contraseña.
- **Precondiciones:** cuenta activa, creada por un administrador.
- **Disparador:** el actor abre una ruta protegida o la pantalla de acceso.

### Flujo principal

1. El sistema muestra el formulario sin ofrecer registro público ni la opción «recordarme».
2. El actor ingresa correo y contraseña.
3. El sistema valida formato y obligatoriedad.
4. Identity valida credenciales, estado activo y bloqueo.
5. El sistema crea una cookie de sesión no persistente y redirige a una ruta interna válida o al inicio.

### Alternativas

- Datos inválidos: se muestran mensajes asociados a los campos.
- Credenciales incorrectas, usuario inactivo o bloqueado: se muestra un mensaje genérico.
- Cinco intentos fallidos: Identity bloquea la cuenta durante 15 minutos.
- Ruta de retorno externa: se ignora para prevenir redirecciones abiertas.

- **Postcondición exitosa:** sesión autenticada con expiración deslizante después de 30 minutos de
  inactividad.
- **Postcondición fallida:** no se crea sesión y, cuando corresponde, aumenta el contador de fallos.
- **Requisitos:** RF-SEG-001, RF-SEG-004, RN-SEG-002, RNF-SEG-001, RNF-SEG-002.

## UC-SEG-002 — Cerrar sesión

- **Actor:** usuario autenticado.
- **Precondición:** existe una sesión activa.

### Flujo principal

1. El actor selecciona «Cerrar sesión».
2. El navegador envía un POST con token antifalsificación.
3. El sistema elimina la sesión y registra el evento técnico.
4. El sistema redirige al inicio de sesión.

- **Alternativa:** GET o POST sin token no ejecuta el cierre.
- **Postcondición:** la cookie anterior deja de autorizar solicitudes.
- **Requisito:** RF-SEG-002.

## UC-SEG-003 — Cambiar contraseña

- **Actor:** usuario autenticado y activo.
- **Precondición:** usuario autenticado. Cuando la sesión proviene de una contraseña temporal,
  esta ya fue verificada durante el inicio de sesión.

### Flujo principal

1. El actor con sesión ordinaria ingresa contraseña actual, contraseña nueva y confirmación.
2. El actor con contraseña temporal ingresa únicamente contraseña nueva y confirmación.
3. El sistema verifica coincidencia y política mínima.
4. Identity valida la contraseña actual en la sesión ordinaria o utiliza el estado temporal ya
   autenticado para sustituir el hash.
5. El sistema renueva la sesión y redirige al inicio.

### Alternativas

- Contraseña actual incorrecta: no se modifica la cuenta.
- Nueva contraseña débil o confirmación diferente: se muestran validaciones.
- Usuario inexistente o inactivo: se cierra la sesión.

- **Postcondición:** la nueva contraseña se utiliza en accesos posteriores.
- **Requisito:** RF-SEG-003.

## UC-SEG-004 — Inicializar seguridad

- **Actor:** responsable técnico autorizado.
- **Objetivo:** crear roles fijos y el primer administrador sin credenciales predeterminadas.
- **Precondiciones:** migración aplicada y secretos locales completos.

### Flujo principal

1. El responsable ejecuta la aplicación con `--bootstrap-admin`.
2. El sistema crea, si no existen, los cuatro roles aprobados.
3. Si ya existe un administrador activo, el comando termina sin elevar otra cuenta.
4. Si no existe, valida que nombre, correo y contraseña provengan de configuración protegida.
5. El sistema crea el administrador mediante `UserManager`; nunca eleva una cuenta preexistente.
6. El sistema asigna el rol Administrador sin duplicarlo y termina sin iniciar el servidor web.
7. El responsable elimina los valores temporales de bootstrap.

### Alternativas

- Configuración incompleta: el comando falla sin crear una contraseña predeterminada.
- Usuario existente inactivo: el comando no reactiva ni modifica la cuenta.
- Repetición del comando: no duplica roles, usuario ni asignación.

- **Requisitos:** RF-SEG-006, RN-SEG-001.

## UC-CAT-001 — Consultar catálogos

- **Actores:** Gerencia, Finanzas y Asistente.
- **Objetivo:** localizar registros maestros autorizados sin modificar información.
- **Precondición:** sesión activa con un rol financiero autorizado.

### Flujo principal

1. El actor selecciona clientes, proveedores, productos/servicios, categorías, cuentas o tipos de
   cambio.
2. El sistema presenta la primera página en un orden estable y muestra únicamente activos por defecto.
3. El actor ingresa un criterio de búsqueda, cambia el filtro de estado o solicita otra página.
4. El sistema aplica los filtros en el servidor y devuelve la página correspondiente.
5. El actor consulta el detalle del registro seleccionado.

### Alternativas

- Sin coincidencias: el sistema presenta una lista vacía, no un error.
- Filtro de inactivos: se muestran registros conservados por trazabilidad y se identifica su estado.
- Rol no autorizado: el servidor rechaza el acceso sin revelar el contenido del catálogo.

- **Postcondición:** no se modifica información.
- **Requisitos:** RF-CAT-001, RF-MON-001, RN-SEG-003, RNF-CAT-001.

## UC-CAT-002 — Gestionar clientes

- **Actores:** Finanzas y Asistente.
- **Objetivo:** mantener la información administrativa utilizada posteriormente en facturación y
  cuentas por cobrar.
- **Precondición:** sesión activa con permiso de mantenimiento de clientes y proveedores.

### Flujo principal

1. El actor abre el catálogo de clientes y elige crear o modificar.
2. El sistema presenta un ViewModel con los campos editables y, al modificar, el token de concurrencia.
3. El actor completa los datos y confirma mediante POST protegido contra CSRF.
4. El sistema valida autorización, formato, longitudes y reglas de dominio.
5. El sistema guarda el cliente y registra usuario e instante UTC.
6. El sistema confirma la operación sin exponer información técnica.

### Alternativas

- Datos inválidos: no se guarda y se muestran errores asociados a los campos.
- Desactivar: el registro se conserva, deja de ofrecerse en operaciones nuevas y registra auditoría.
- Reactivar: se vuelven a validar todas las reglas antes de habilitarlo.
- Conflicto concurrente: no se sobrescriben cambios ajenos; se solicita recargar la versión vigente.
- Rol distinto de Finanzas o Asistente: el servidor responde acceso denegado.

- **Postcondición:** cliente creado o actualizado, o estado lógico cambiado con trazabilidad.
- **Requisitos:** RF-CAT-002, RN-CAT-001, RN-CAT-002, RN-CAT-008, RN-SEG-004,
  RNF-DAT-003, RNF-AUD-001.

## UC-CAT-003 — Gestionar proveedores

- **Actores:** Finanzas y Asistente.
- **Objetivo:** mantener la información administrativa utilizada posteriormente en gastos y cuentas
  por pagar.
- **Precondición:** sesión activa con permiso de mantenimiento de clientes y proveedores.

### Flujo principal

1. El actor abre el catálogo de proveedores y elige crear o modificar.
2. El sistema presenta únicamente los campos permitidos y el token de concurrencia cuando corresponda.
3. El actor completa los datos y confirma mediante POST protegido contra CSRF.
4. El sistema valida autorización, entrada y reglas de dominio.
5. El sistema guarda el proveedor con usuario e instante UTC de auditoría.
6. El sistema confirma la operación.

### Alternativas

- Datos inválidos: no se modifica información.
- Desactivar o reactivar: se cambia el estado lógico sin eliminar el proveedor.
- Conflicto concurrente: se conserva la versión almacenada y se solicita recargar.
- Rol distinto de Finanzas o Asistente: el servidor rechaza la escritura.

- **Postcondición:** proveedor creado o actualizado, o estado lógico cambiado con trazabilidad.
- **Requisitos:** RF-CAT-003, RN-CAT-001, RN-CAT-002, RN-CAT-008, RN-SEG-004,
  RNF-DAT-003, RNF-AUD-001.

## UC-CAT-004 — Gestionar productos y servicios

- **Actor:** Finanzas.
- **Objetivo:** mantener un catálogo facturable único con precios de referencia CRC/USD.
- **Precondición:** sesión activa con permiso financiero de mantenimiento.

### Flujo principal

1. Finanzas elige crear o modificar un artículo.
2. Indica código, nombre, tipo Producto/Servicio y, opcionalmente, precio CRC, precio USD y categoría de
   ingreso predeterminada.
3. El sistema valida que cualquier precio informado sea positivo y que la categoría seleccionada sea
   de ingreso.
4. El sistema guarda el artículo con auditoría y versión de concurrencia.
5. El sistema confirma la operación.

### Alternativas

- Ambos precios vacíos: se permite un artículo de precio variable.
- Precio cero o negativo: el sistema rechaza la entrada.
- Categoría inexistente, inactiva o de gasto: no se guarda como categoría predeterminada.
- Desactivar o reactivar: se conserva el registro y se aplican las mismas reglas de trazabilidad.
- Conflicto concurrente o rol no autorizado: no se sobrescribe ni modifica el artículo.

- **Postcondición:** artículo y precios de referencia guardados sin crear existencias de inventario.
- **Requisitos:** RF-CAT-004, RN-CAT-002 a RN-CAT-005, RN-CAT-008, RN-SEG-004,
  RNF-DAT-003, RNF-AUD-001.

## UC-CAT-005 — Gestionar categorías financieras

- **Actor:** Finanzas.
- **Objetivo:** mantener una clasificación jerárquica de ingresos y gastos.
- **Precondición:** sesión activa con permiso financiero de mantenimiento.

### Flujo principal

1. Finanzas crea o modifica una categoría e indica código, nombre y tipo Ingreso/Gasto.
2. Opcionalmente selecciona una categoría padre del mismo tipo.
3. Selecciona la cuenta contable de la categoría, que es obligatoria: una cuenta de tipo Ingreso para las
   categorías de ingreso y una de tipo Gasto para las de gasto.
4. El sistema comprueba unicidad, existencia del padre y ausencia de ciclos, y que la cuenta exista, esté
   activa y sea del tipo que corresponde.
5. El sistema guarda la categoría con auditoría y versión de concurrencia.

### Alternativas

- La propia categoría o una descendiente se selecciona como padre: el sistema rechaza el ciclo.
- El padre pertenece al otro tipo: el sistema rechaza la relación.
- La cuenta contable no existe, está inactiva o es de otro tipo: el sistema rechaza la entrada.
- Reactivar una categoría cuya cuenta contable está inactiva: el sistema exige elegir o reactivar una
  cuenta activa.
- Desactivar una categoría con hijas activas o artículos activos que la usan: el sistema bloquea la
  operación; no aplica una cascada lógica.
- Reactivar una categoría hija cuyo padre está inactivo: el sistema exige reactivar primero al padre.
- Conflicto concurrente o rol no autorizado: no se modifica la jerarquía.

- **Postcondición:** jerarquía válida y trazable.
- **Requisitos:** RF-CAT-005, RF-CTA-002, RN-CAT-002, RN-CAT-006, RN-CAT-008, RN-CAT-009, RN-CTA-004,
  RN-SEG-004, RNF-DAT-003, RNF-AUD-001.

## UC-CAT-006 — Gestionar cuentas contables

- **Actor de consulta:** Gerencia, Finanzas y Asistente.
- **Actor de escritura:** Finanzas.
- **Objetivo:** mantener el catálogo jerárquico de cuentas contables, que clasifica ingresos y gastos y
  reúne las cajas y los bancos que recibirán movimientos futuros.
- **Precondición:** sesión activa con la capacidad correspondiente.

### Flujo principal

1. El actor consulta, busca y filtra las cuentas por estado.
2. Finanzas crea una cuenta e indica código, nombre y tipo (Activo, Pasivo, Patrimonio, Ingreso o Gasto).
3. Opcionalmente selecciona una cuenta superior del mismo tipo.
4. Si la cuenta es de tipo Activo, puede marcarla «de efectivo» e indicar su subtipo (Caja o Cuenta
   bancaria) y su moneda CRC/USD.
5. El sistema valida valores permitidos, unicidad del código, existencia y estado de la cuenta superior,
   y ausencia de ciclos.
6. El sistema guarda la cuenta sin solicitar ni calcular saldo, con auditoría y versión de concurrencia.
7. En modificaciones posteriores solo permite cambiar código, nombre, cuenta superior, referencia y
   descripción, o el estado lógico; tipo, subtipo de efectivo y moneda permanecen fijos.

### Alternativas

- Código duplicado o valor no permitido: el sistema rechaza la entrada.
- Una cuenta que no es Activo se marca de efectivo, una de efectivo no indica moneda, o una que no es de
  efectivo indica moneda: el sistema rechaza la entrada.
- La cuenta superior es de otro tipo, es de efectivo, está inactiva o produciría un ciclo: el sistema
  rechaza la relación.
- Intento de cambiar tipo, subtipo o moneda: no se aplica el cambio; se toman siempre del registro.
- Desactivar una cuenta con cuentas hijas activas o con categorías activas que la usan: el sistema
  bloquea la operación. Reactivar una hija cuya cuenta superior está inactiva exige reactivar primero a
  la superior.
- Conflicto concurrente o rol no autorizado: no se modifica la cuenta.

- **Postcondición:** cuenta identificada por un código único, con jerarquía válida y sin saldo editable.
- **Requisitos:** RF-CTA-001, RF-CTA-003, RN-CAT-002, RN-CAT-008, RN-CTA-001 a RN-CTA-006, RN-SEG-004,
  RNF-DAT-003, RNF-AUD-001.

## UC-CAT-007 — Gestionar tipos de impuesto

- **Actor de consulta:** Gerencia, Finanzas y Asistente.
- **Actor de escritura:** Finanzas.
- **Objetivo:** mantener los impuestos con tarifa configurable que las líneas de factura pueden aplicar.
- **Precondición:** sesión activa con la capacidad correspondiente.

### Flujo principal

1. El actor consulta, busca y filtra los tipos de impuesto por estado.
2. Finanzas crea un tipo e indica código, nombre, método de cálculo (porcentaje sobre el neto de la
   línea o monto fijo por unidad), tarifa y una descripción opcional.
3. El sistema normaliza el código, redondea la tarifa a cuatro decimales y valida las reglas: la tarifa
   no es negativa y un porcentaje no supera 100 %.
4. El sistema guarda el tipo con auditoría y control de concurrencia.
5. En modificaciones posteriores Finanzas puede cambiar código, nombre, tarifa y descripción, o
   desactivar y reactivar el tipo; el método de cálculo permanece fijo.

### Flujo alterno: cargar tarifas de referencia

1. Finanzas elige «Cargar tarifas de referencia» y confirma la advertencia.
2. El sistema crea, a nombre de quien lo solicita, las tarifas de IVA de referencia del Ministerio de
   Hacienda que aún no existan, identificadas por su código.
3. Los tipos que ya existen no se modifican ni se reactivan. El sistema informa cuántos creó y
   recuerda validar su uso con la asesoría contable.

### Alternativas

- Código duplicado, tarifa negativa o porcentaje superior a 100 %: el sistema rechaza la entrada.
- Intento de cambiar el método de cálculo: no se aplica; el método se toma siempre del registro.
- Conflicto concurrente: no se sobrescribe el cambio de otro usuario.
- Gerencia o Asistente intentan escribir, o se intenta cargar las referencias con una solicitud GET: el
  servidor rechaza la operación.
- Modificar o desactivar un tipo ya usado: las facturas existentes conservan la fotografía de sus
  impuestos y no cambian.

- **Postcondición:** el catálogo contiene tipos de impuesto únicos por código, que las facturas nuevas
  pueden seleccionar mientras estén activos.
- **Requisitos:** RF-IMP-001, RF-IMP-002, RN-IMP-001 a RN-IMP-004, RN-CAT-002, RN-SEG-004, RNF-DAT-003,
  RNF-AUD-001.

## UC-CAT-008 — Gestionar tipos de retención

- **Actor de consulta:** Gerencia, Finanzas y Asistente.
- **Actor de escritura:** Finanzas.
- **Objetivo:** mantener los tipos de retención con tarifa porcentual que se aplican al registrar un
  abono o un pago.
- **Precondición:** sesión activa con la capacidad correspondiente.

### Flujo principal

1. El actor consulta, busca y filtra los tipos de retención por estado.
2. Finanzas crea un tipo e indica código, nombre, un porcentaje entre 0 y 100 y una descripción opcional.
3. El sistema normaliza el código, redondea la tarifa a cuatro decimales y valida que esté en ese rango.
4. El sistema guarda el tipo con auditoría y control de concurrencia.
5. En modificaciones posteriores Finanzas puede cambiar código, nombre, tarifa y descripción, o
   desactivar y reactivar el tipo.

### Alternativas

- Código duplicado o porcentaje fuera de 0 a 100: el sistema rechaza la entrada.
- Conflicto concurrente: no se sobrescribe el cambio de otro usuario.
- Gerencia o Asistente intentan escribir: el servidor rechaza la operación.
- Modificar o desactivar un tipo ya usado: los abonos y pagos existentes conservarán la fotografía de su
  retención y no cambian, una vez que exista ese registro.

- **Postcondición:** el catálogo contiene tipos de retención únicos por código, que los abonos y pagos
  nuevos podrán seleccionar mientras estén activos.
- **Requisitos:** RF-RET-001, RN-RET-001, RN-RET-002, RN-CAT-002, RN-SEG-004, RNF-DAT-003, RNF-AUD-001.

## UC-CFG-001 — Definir parámetros del sistema

- **Actor de consulta:** Gerencia y Finanzas.
- **Actor de escritura:** Gerencia.
- **Objetivo:** configurar el límite de autorización de abonos y pagos (regla de negocio 7) y los días
  de alerta de vencimiento próximo (regla de negocio 9).
- **Precondición:** sesión activa con la capacidad correspondiente.

### Flujo principal

1. El actor consulta los parámetros vigentes; si nadie los ha configurado todavía, el sistema lo indica.
2. Gerencia indica el límite de autorización en CRC y los días de alerta.
3. El sistema valida que el límite no sea negativo y que los días sean mayores que cero, y guarda la
   configuración con auditoría y control de concurrencia.
4. El sistema conserva un registro del cambio con el valor anterior y el nuevo de cada campo.

### Alternativas

- Límite negativo o días de alerta en cero o negativos: el sistema rechaza la entrada.
- Conflicto concurrente: no se sobrescribe el cambio de otro usuario; se informa el valor vigente.
- Finanzas, Asistente o Administrador intentan escribir: el servidor rechaza la operación.

- **Postcondición:** existe como máximo una configuración vigente, y su historial completo queda
  disponible para revisión futura.
- **Requisitos:** RF-CFG-001, RF-CFG-002, RN-CFG-001 a RN-CFG-003, RNF-AUD-001.

## UC-MON-001 — Gestionar tipos de cambio diarios

- **Actor de consulta:** Gerencia, Finanzas y Asistente.
- **Actor de escritura:** Finanzas.
- **Objetivo:** conservar una tasa manual CRC por USD para cada fecha de negocio.
- **Precondición:** sesión activa con la capacidad correspondiente.

### Flujo principal

1. El actor consulta las tasas ordenadas por fecha.
2. Finanzas selecciona una fecha sin tasa o elige corregir la existente.
3. Ingresa una tasa positiva y una fuente obligatoria.
4. El sistema normaliza la tasa a seis decimales y comprueba la unicidad de la fecha.
5. El sistema guarda la tasa o su corrección con auditoría y control de concurrencia.

### Alternativas

- Tasa cero, negativa o fuente vacía: el sistema rechaza la entrada.
- Ya existe una tasa para una fecha nueva: el sistema informa el conflicto y no crea otra fila.
- Conflicto concurrente: no se sobrescribe la corrección de otro usuario.
- Gerencia o Asistente intentan escribir: el servidor rechaza la operación.

- **Postcondición:** existe como máximo una tasa manual y trazable para la fecha.
- **Requisitos:** RF-MON-001, RF-MON-002, RN-MON-001 a RN-MON-003, RN-SEG-004,
  RNF-DAT-003, RNF-AUD-001.

## UC-USR-001 — Administrar usuarios internos

- **Actor:** Administrador.
- **Objetivo:** crear y mantener las cuentas internas y su único rol aprobado.
- **Precondición:** sesión activa con la política `Usuarios.Administrar`.

### Flujo principal

1. El Administrador consulta usuarios por nombre, correo, estado o rol.
2. Para un alta indica nombre, correo, rol y una contraseña temporal conforme a la política.
3. Identity normaliza y valida el correo, genera únicamente el hash y crea la cuenta activa.
4. El sistema asigna exactamente un rol y marca pendiente el cambio de contraseña.
5. En una edición posterior, el Administrador puede modificar nombre, correo o rol con la versión
   consultada.
6. El sistema confirma todos los cambios en una transacción, invalida sesiones cuando corresponde y
   agrega eventos a la bitácora de seguridad.

### Alternativas

- Correo duplicado, rol desconocido o contraseña débil: no se crea ni modifica parcialmente la cuenta.
- Versión obsoleta: se conserva el cambio vigente y se solicita revisar la cuenta actual.
- Desactivar o reactivar: cambia únicamente el estado lógico; el bloqueo temporal se mantiene separado.
- Automodificación peligrosa: el actor no puede desactivarse ni cambiar su propio rol.
- Último Administrador activo: no se permite desactivarlo ni asignarle otro rol.
- Rol no autorizado: el servidor rechaza lectura y escritura aunque se invoque la ruta directamente.

- **Postcondición:** cuenta y rol coherentes, sin borrado físico y con evento de seguridad.
- **Requisitos:** RF-USR-001 a RF-USR-004, RN-USR-001 a RN-USR-006, RNF-USR-001 a
  RNF-USR-003.

## UC-USR-002 — Restablecer acceso de un usuario

- **Actor:** Administrador.
- **Objetivo:** recuperar de forma interna una cuenta bloqueada o cuya contraseña debe sustituirse.
- **Precondición:** cuenta existente y versión de concurrencia vigente.

### Flujo principal de restablecimiento

1. El Administrador ingresa y confirma una contraseña temporal que cumple la política de Identity.
2. El sistema genera internamente un token de restablecimiento y cambia el hash; ni el token ni la
   contraseña se registran o incorporan a la URL.
3. Se eliminan el bloqueo y los intentos fallidos, se invalidan sesiones anteriores y se obliga al
   propietario a cambiar la contraseña al iniciar sesión.
4. El sistema registra el evento y confirma la operación sin volver a mostrar la credencial.

### Flujo alterno de desbloqueo

1. El Administrador selecciona desbloquear sin modificar la contraseña.
2. El sistema limpia `LockoutEnd` y el contador de fallos, e incorpora el evento correspondiente.

### Alternativas

- El actor selecciona su propia cuenta: debe utilizar el cambio de contraseña personal.
- Contraseña débil, cuenta inexistente o versión obsoleta: no se confirma ningún cambio parcial.

- **Postcondición:** acceso restablecido de forma controlada o cuenta sin cambios.
- **Requisitos:** RF-USR-005, RF-USR-006, RN-USR-004 a RN-USR-006, RNF-USR-001 a
  RNF-USR-003.
