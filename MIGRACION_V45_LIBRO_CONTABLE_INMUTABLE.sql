-- BILLAR EL BRUJO - V45
-- DEFENSA CONTABLE: libro de caja + auditoría + conciliación.
-- IMPORTANTE: hacer BACKUP de Railway/MySQL antes de ejecutar.
-- Este script NO elimina ventas ni productos.

CREATE TABLE IF NOT EXISTS libro_caja (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    venta_id BIGINT NOT NULL,
    sucursal_id INT NOT NULL,
    cajero VARCHAR(100) NOT NULL,
    fecha DATETIME NOT NULL,
    tipo VARCHAR(60) NOT NULL,
    metodo_pago VARCHAR(30) NOT NULL,
    efectivo DECIMAL(12,2) NOT NULL DEFAULT 0,
    qr DECIMAL(12,2) NOT NULL DEFAULT 0,
    total DECIMAL(12,2) NOT NULL,
    sync_key VARCHAR(220) NOT NULL,
    operation_key VARCHAR(220) NULL,
    estado VARCHAR(20) NOT NULL DEFAULT 'CONFIRMADA',
    creado_en TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uk_libro_venta (venta_id),
    UNIQUE KEY uk_libro_sync (sync_key),
    UNIQUE KEY uk_libro_operation (operation_key),
    KEY ix_libro_fecha_sucursal (fecha, sucursal_id),
    KEY ix_libro_cajero_fecha (cajero, fecha)
);

CREATE TABLE IF NOT EXISTS auditoria_contable (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    fecha DATETIME NOT NULL,
    usuario VARCHAR(100) NOT NULL,
    sucursal_id INT NOT NULL,
    accion VARCHAR(80) NOT NULL,
    entidad VARCHAR(80) NOT NULL,
    entidad_id BIGINT NOT NULL,
    detalle TEXT NULL,
    KEY ix_auditoria_fecha (fecha),
    KEY ix_auditoria_entidad (entidad, entidad_id)
);

-- Diagnóstico: estos resultados deben ser revisados, NO borrados automáticamente.
SELECT sync_key, COUNT(*) AS repeticiones
FROM ventas
GROUP BY sync_key
HAVING COUNT(*) > 1;

SELECT operation_key, COUNT(*) AS repeticiones
FROM ventas
WHERE operation_key IS NOT NULL AND TRIM(operation_key) <> ''
GROUP BY operation_key
HAVING COUNT(*) > 1;

SELECT id, sucursal_id, cajero, fecha, metodo_pago, efectivo, qr, total,
       ROUND((COALESCE(efectivo,0)+COALESCE(qr,0))-total,2) AS diferencia
FROM ventas
WHERE ABS((COALESCE(efectivo,0)+COALESCE(qr,0))-total) > 0.01;

-- No se hace backfill masivo del libro_caja para evitar convertir duplicados históricos
-- en movimientos contables válidos. La API V45 irá registrando/confirmando de forma segura
-- las operaciones que lleguen desde Caja V127.
