# BillarElBrujoApi V35 - Arqueo de caja / cierre remoto

API ASP.NET Core para BILLAR EL BRUJO, Railway + MySQL + Google Sheets.

## Versión esperada
Abrir `/health` y verificar:
`V35_ARQUEO_CIERRE_ADMIN`

## Nuevo en V35
- `POST /api/cierres-turno`: recibe un arqueo completo al cerrar caja.
- `GET /api/admin/cierres-turno?clave=ENTREGAR_LIMPIO_2026`: permite al Administrador recuperar cierres desde otra PC.
- Tabla `cierres_turno` con resumen + `detalle_json` multipágina.
- `sync_key` único para impedir duplicados y proteger cierres ya realizados.

## Compatibilidad
- Escritorio: V90.
- App Mesera: V12 (no requiere cambios para este módulo).

## Variables Railway necesarias
MySQL:
- MYSQL_URL
- MYSQLHOST
- MYSQLPORT
- MYSQLDATABASE
- MYSQLUSER
- MYSQLPASSWORD

Google Sheets:
- GOOGLE_SHEET_ID
- GOOGLE_CREDENTIALS_JSON

No subir credenciales privadas a GitHub.
