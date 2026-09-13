-- BILLAR EL BRUJO - V42 DEFENSA CONTABLE / ANTI DUPLICACION
-- HACER BACKUP DE RAILWAY ANTES DE EJECUTAR.
-- No borra ventas. Solo agrega defensas cuando la base ya está limpia de duplicados de sync_key.

-- 1) VENTAS: una sync_key = una venta.
ALTER TABLE ventas
  ADD UNIQUE KEY uk_ventas_sync_key_v42 (sync_key);

-- 2) COBROS DE MESA: una sync_key = un cobro.
ALTER TABLE cobros_mesa
  ADD UNIQUE KEY uk_cobros_mesa_sync_key_v42 (sync_key);

-- 3) ESTADO DE MESAS: Mesa 1 de sucursal 1 y Mesa 1 de sucursal 2 son registros distintos.
ALTER TABLE mesa_estados
  ADD UNIQUE KEY uk_mesa_estado_sucursal_mesa_v42 (sucursal_id, mesa_id);

-- 4) Cada clave de sincronización de estado de mesa también debe ser única.
ALTER TABLE mesa_estados
  ADD UNIQUE KEY uk_mesa_estado_sync_key_v42 (sync_key);

-- DIAGNOSTICO: debe devolver 0 filas en cada consulta.
SELECT sync_key, COUNT(*) cantidad FROM ventas GROUP BY sync_key HAVING COUNT(*) > 1;
SELECT sync_key, COUNT(*) cantidad FROM cobros_mesa GROUP BY sync_key HAVING COUNT(*) > 1;
SELECT sucursal_id, mesa_id, COUNT(*) cantidad FROM mesa_estados GROUP BY sucursal_id, mesa_id HAVING COUNT(*) > 1;
