-- BILLAR EL BRUJO - API V44
-- DEFENSA EXTRA PARA CAJAS ANTIGUAS SIN operation_key
-- NO BORRA VENTAS, PRODUCTOS NI STOCK.
-- Recomendado: hacer backup de Railway/MySQL antes de ejecutar.

ALTER TABLE ventas
    ADD COLUMN legacy_fingerprint VARCHAR(64) NULL AFTER operation_key;

ALTER TABLE ventas
    ADD UNIQUE KEY uk_ventas_legacy_fingerprint (legacy_fingerprint);

-- La API V44 llena esta huella solo para solicitudes antiguas que no envían operation_key.
-- Las ventas actuales V125/V126 usan operation_key como identidad principal.

-- Verificación de índices de protección:
SHOW INDEX FROM ventas
WHERE Key_name IN ('uk_ventas_sync_key','uk_ventas_operation_key','uk_ventas_legacy_fingerprint');
