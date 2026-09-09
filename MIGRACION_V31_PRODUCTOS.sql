-- BILLAR EL BRUJO - MIGRACION V31 PRODUCTOS
-- Normalmente NO hace falta ejecutarla manualmente: la API V31 intenta aplicar estos cambios sola.

ALTER TABLE productos ADD COLUMN IF NOT EXISTS tipo_entrada VARCHAR(60) NOT NULL DEFAULT 'PAQUETE';
ALTER TABLE productos ADD COLUMN IF NOT EXISTS unidades_por_entrada INT NOT NULL DEFAULT 1;
ALTER TABLE productos ADD COLUMN IF NOT EXISTS precio_compra DECIMAL(10,2) NOT NULL DEFAULT 0;
ALTER TABLE productos ADD COLUMN IF NOT EXISTS genera_comision TINYINT(1) NOT NULL DEFAULT 0;
ALTER TABLE productos ADD COLUMN IF NOT EXISTS tipo_comision VARCHAR(30) NOT NULL DEFAULT 'NINGUNA';
ALTER TABLE productos ADD COLUMN IF NOT EXISTS valor_comision DECIMAL(10,2) NOT NULL DEFAULT 0;
ALTER TABLE productos ADD COLUMN IF NOT EXISTS sin_limite_stock TINYINT(1) NOT NULL DEFAULT 0;

UPDATE productos SET categoria='Cocas' WHERE categoria='Coca machucada';
UPDATE productos SET categoria='Otros' WHERE categoria IN ('Varios','Otros / Extras');
UPDATE productos SET categoria='Accesorios' WHERE categoria='Vasos/Accesorios';
UPDATE productos SET categoria='Ceniceros'
WHERE UPPER(nombre) LIKE '%CENICER%' OR UPPER(nombre) LIKE '%CINCERO%';
UPDATE productos SET categoria='Accesorios'
WHERE UPPER(nombre) IN (
 'ENCENDEDOR','COPAS DE VINO','VASO TEQUILERO','VASOS CERVECEROS','VASOS DE SODA',
 'VASOS DE WISKI','VASOS DE WHISKY','VASO DE WISKI','VASO DE WISKIE','VASO DE WHISKY'
);
