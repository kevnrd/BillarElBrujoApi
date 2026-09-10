API V35 - BILLAR EL BRUJO - ARQUEO / CIERRE ADMIN

Compatible con:
- Programa de escritorio V90.
- App Mesera V13.

NUEVO
- POST /api/cierres-turno
  Guarda el arqueo completo e inmutable del cajero.
- GET /api/admin/cierres-turno?clave=ENTREGAR_LIMPIO_2026
  Devuelve todos los arqueos para Administración.
- Tabla MySQL cierres_turno con detalle_json completo.
- El sync_key es único: volver a enviar el mismo cierre NO lo duplica ni lo reemplaza.

/health debe mostrar:
V36_CORTESIA_COBRADA
