-- BILLAR EL BRUJO - V37
-- Tarifa única para todas las mesas + detalle amplio del tiempo cobrado.

CREATE TABLE IF NOT EXISTS configuracion_sistema (
    clave VARCHAR(100) PRIMARY KEY,
    valor_decimal DECIMAL(12,2) NULL,
    actualizado DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

INSERT INTO configuracion_sistema (clave, valor_decimal, actualizado)
VALUES ('TARIFA_MESA_HORA', 30.00, NOW())
ON DUPLICATE KEY UPDATE
    valor_decimal = IF(valor_decimal IS NULL OR valor_decimal <= 0, 30.00, valor_decimal),
    actualizado = NOW();

UPDATE mesas
SET precio_hora = (
    SELECT valor_decimal
    FROM configuracion_sistema
    WHERE clave = 'TARIFA_MESA_HORA'
    LIMIT 1
);

ALTER TABLE cobros_mesa MODIFY COLUMN tiempo VARCHAR(220) NULL;

-- Conserva en cada mesa abierta la tarifa con la que se inició la sesión.
-- Si la columna ya existe, este ALTER puede mostrar "Duplicate column" y se puede ignorar.
ALTER TABLE mesa_estados ADD COLUMN tarifa_hora DECIMAL(10,2) NOT NULL DEFAULT 30.00 AFTER minutos;
