-- BILLAR EL BRUJO - V43 DEFENSA DE OPERACION UNICA
-- OBJETIVO: impedir que el MISMO cobro se registre dos veces aunque dos PCs usen sync_key distintas.
-- IMPORTANTE: HACER BACKUP DE RAILWAY / MYSQL ANTES DE EJECUTAR.
-- Esta migracion NO borra ventas.

SET @db := DATABASE();

-- 1) Agregar operation_key si todavia no existe.
SET @sql := (
  SELECT IF(COUNT(*) = 0,
    'ALTER TABLE ventas ADD COLUMN operation_key VARCHAR(220) NULL AFTER sync_key',
    'SELECT ''operation_key ya existe'' AS info')
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'ventas' AND COLUMN_NAME = 'operation_key'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 2) Indice UNIQUE. MySQL permite varios NULL, por lo que las ventas antiguas siguen intactas.
SET @sql := (
  SELECT IF(COUNT(*) = 0,
    'ALTER TABLE ventas ADD UNIQUE KEY uk_ventas_operation_key (operation_key)',
    'SELECT ''uk_ventas_operation_key ya existe'' AS info')
  FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'ventas' AND INDEX_NAME = 'uk_ventas_operation_key'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 3) Mantener la defensa anterior por sync_key.
SET @sql := (
  SELECT IF(COUNT(*) = 0,
    'ALTER TABLE ventas ADD UNIQUE KEY uk_ventas_sync_key (sync_key)',
    'SELECT ''uk_ventas_sync_key ya existe'' AS info')
  FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'ventas' AND INDEX_NAME = 'uk_ventas_sync_key'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 4) DIAGNOSTICOS CONTABLES. Estas consultas NO modifican datos.

-- Duplicados por sync_key: debe devolver 0 filas.
SELECT sync_key, COUNT(*) cantidad
FROM ventas
WHERE sync_key IS NOT NULL AND TRIM(sync_key) <> ''
GROUP BY sync_key
HAVING COUNT(*) > 1;

-- Duplicados por operation_key de V43: debe devolver 0 filas.
SELECT operation_key, COUNT(*) cantidad
FROM ventas
WHERE operation_key IS NOT NULL AND TRIM(operation_key) <> ''
GROUP BY operation_key
HAVING COUNT(*) > 1;

-- Pagos cuyo efectivo + QR no cuadra con el total: debe devolver 0 filas.
SELECT id, sucursal_id, cajero, fecha, tipo, metodo_pago, efectivo, qr, total,
       ROUND(COALESCE(efectivo,0) + COALESCE(qr,0) - total, 2) AS diferencia
FROM ventas
WHERE ABS(ROUND(COALESCE(efectivo,0) + COALESCE(qr,0) - total, 2)) > 0.01;

-- Resumen de control por dia y sucursal.
SELECT DATE(fecha) fecha, sucursal_id,
       COUNT(*) cantidad_cobros,
       ROUND(SUM(total),2) total_cobrado,
       ROUND(SUM(COALESCE(efectivo,0)),2) efectivo,
       ROUND(SUM(COALESCE(qr,0)),2) qr,
       ROUND(SUM(COALESCE(efectivo,0) + COALESCE(qr,0)),2) total_medios_pago
FROM ventas
GROUP BY DATE(fecha), sucursal_id
ORDER BY fecha DESC, sucursal_id;
