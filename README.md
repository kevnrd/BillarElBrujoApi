# BillarElBrujoApi

API ASP.NET Core para conectar el sistema BILLAR EL BRUJO con MySQL en Railway.

## Prueba

Cuando esté desplegada en Railway, abrir:

`/health`

Debe responder:

```json
{
  "ok": true,
  "database": "railway",
  "mysql": "conectado"
}
```

## Variables necesarias en Railway

- MYSQL_URL
- MYSQLHOST
- MYSQLPORT
- MYSQLDATABASE
- MYSQLUSER
- MYSQLPASSWORD
