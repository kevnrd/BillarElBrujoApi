# BillarElBrujoApi V7

API ASP.NET Core para Railway + MySQL + Google Sheets.

## Rutas principales

- `/health`
- `/api/sheets/status`
- `/api/sheets/sync`

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

No subir credenciales a GitHub.


## V6
Corrige el error MySQL ONLY_FULL_GROUP_BY en `/api/sheets/sync`.


## V7
`/health` muestra `version: V7_GOOGLE_SHEETS_FIX_SIMPLE` para confirmar que Railway usa el código nuevo.
