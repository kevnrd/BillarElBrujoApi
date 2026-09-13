-- BILLAR EL BRUJO API V41 - PROTECCION ANTI DUPLICADOS
-- EJECUTAR UNA SOLA VEZ EN MYSQL/RAILWAY.
-- Antes de borrar duplicados crea respaldos de las filas que se consolidaran.

-- 1) Toda venta antigua sin sync_key recibe una clave unica.
UPDATE ventas
SET sync_key = CONCAT('LEGACY-VENTA-', id)
WHERE sync_key IS NULL OR TRIM(sync_key) = '';

-- 2) Respaldar ventas duplicadas (se conserva el ID mas bajo de cada sync_key).
CREATE TABLE IF NOT EXISTS backup_ventas_duplicadas_v41 LIKE ventas;
INSERT INTO backup_ventas_duplicadas_v41
SELECT v.*
FROM ventas v
JOIN (
  SELECT sync_key, MIN(id) keep_id
  FROM ventas
  GROUP BY sync_key
  HAVING COUNT(*) > 1
) d ON d.sync_key = v.sync_key
WHERE v.id <> d.keep_id;

-- 3) Respaldar detalle asociado a las copias duplicadas.
CREATE TABLE IF NOT EXISTS backup_detalle_ventas_duplicadas_v41 LIKE detalle_ventas;
INSERT INTO backup_detalle_ventas_duplicadas_v41
SELECT dv.*
FROM detalle_ventas dv
JOIN backup_ventas_duplicadas_v41 bv ON bv.id = dv.venta_id;

-- 4) Eliminar detalle y cabeceras DUPLICADAS solamente.
DELETE dv
FROM detalle_ventas dv
JOIN backup_ventas_duplicadas_v41 bv ON bv.id = dv.venta_id;

DELETE v
FROM ventas v
JOIN backup_ventas_duplicadas_v41 bv ON bv.id = v.id;

-- 5) Blindaje definitivo: Railway rechazara un segundo INSERT con el mismo sync_key.
ALTER TABLE ventas
ADD UNIQUE KEY uk_ventas_sync_key (sync_key);

-- Comprobacion: debe devolver cero filas.
SELECT sync_key, COUNT(*) cantidad
FROM ventas
GROUP BY sync_key
HAVING COUNT(*) > 1;
