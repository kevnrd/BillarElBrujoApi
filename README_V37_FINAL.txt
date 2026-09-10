BILLAR EL BRUJO - API V37_TARIFA_MESAS (FINAL PARA V98)

USAR CON:
- Programa de escritorio: V98_ARQUEO_DEFINITIVO
- App Mesera: V13_CORTESIA_COBRADA

FUNCIONES IMPORTANTES:
- Cierres de turno/arques completos enviados al Administrador.
- Arqueos remotos inmutables por sync_key.
- Tarifa única de mesa sincronizada por Railway.
- GET /api/config/tarifa-mesas
- POST /api/admin/tarifa-mesas?clave=ENTREGAR_LIMPIO_2026
- Tarifa inicial Bs 30.00/h.
- Mesas en vivo conservan la tarifa capturada al inicio.
- Pedidos/cortesías de mesera y stock siguen compatibles.

DESPLIEGUE:
1. Subir TODO el contenido de esta carpeta al repositorio conectado con Railway.
2. Esperar el deploy.
3. Abrir /health.
4. Debe indicar version = V37_TARIFA_MESAS.

La API crea/ajusta la configuración automáticamente. MIGRACION_V37_TARIFA_MESAS.sql queda como respaldo.
