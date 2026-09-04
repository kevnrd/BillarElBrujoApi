using System.Text;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowDesktopApp", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<Db>();
builder.Services.AddSingleton<SheetsReporter>();

var app = builder.Build();

app.UseCors("AllowDesktopApp");

app.MapGet("/", () => Results.Ok(new
{
    app = "BILLAR EL BRUJO API",
    status = "online",
    message = "API funcionando correctamente"
}));

app.MapGet("/health", async (Db db, SheetsReporter sheets) =>
{
    try
    {
        await using var con = await db.OpenAsync();
        await using var cmd = new MySqlCommand("SELECT DATABASE();", con);
        var database = Convert.ToString(await cmd.ExecuteScalarAsync());

        return Results.Ok(new
        {
            ok = true,
            database,
            mysql = "conectado",
            googleSheets = sheets.IsConfigured ? "configurado" : "faltan variables GOOGLE_SHEET_ID y GOOGLE_CREDENTIALS_JSON"
        });
    }
    catch (Exception ex)
    {
        return Results.Problem("No se pudo conectar a MySQL: " + ex.Message);
    }
});

app.MapGet("/api/sheets/status", (SheetsReporter sheets) =>
{
    return Results.Ok(new
    {
        configured = sheets.IsConfigured,
        spreadsheetId = sheets.SpreadsheetId,
        message = sheets.IsConfigured
            ? "Google Sheets configurado en Railway"
            : "Faltan GOOGLE_SHEET_ID y GOOGLE_CREDENTIALS_JSON en Variables de Railway"
    });
});

app.MapPost("/api/sheets/sync", async (Db db, SheetsReporter sheets) =>
{
    if (!sheets.IsConfigured)
        return Results.BadRequest(new { ok = false, message = "Faltan GOOGLE_SHEET_ID y GOOGLE_CREDENTIALS_JSON en Railway." });

    try
    {
        var result = await sheets.SyncFromDatabaseAsync(db);
        return Results.Ok(new { ok = true, message = result });
    }
    catch (Exception ex)
    {
        return Results.Problem("No se pudo actualizar Google Sheets: " + ex.Message);
    }
});

app.MapGet("/api/sheets/sync", async (Db db, SheetsReporter sheets) =>
{
    if (!sheets.IsConfigured)
        return Results.BadRequest(new { ok = false, message = "Faltan GOOGLE_SHEET_ID y GOOGLE_CREDENTIALS_JSON en Railway." });

    try
    {
        var result = await sheets.SyncFromDatabaseAsync(db);
        return Results.Ok(new { ok = true, message = result });
    }
    catch (Exception ex)
    {
        return Results.Problem("No se pudo actualizar Google Sheets: " + ex.Message);
    }
});

app.MapPost("/api/login", async (Db db, LoginRequest req) =>
{
    await using var con = await db.OpenAsync();

    const string sql = """
        SELECT u.id, u.usuario, u.rol, u.estado, s.nombre AS sucursal
        FROM usuarios u
        LEFT JOIN sucursales s ON s.id = u.sucursal_id
        WHERE u.usuario = @usuario AND u.clave = @clave AND u.estado = 'ACTIVO'
        LIMIT 1;
    """;

    await using var cmd = new MySqlCommand(sql, con);
    cmd.Parameters.AddWithValue("@usuario", req.Usuario);
    cmd.Parameters.AddWithValue("@clave", req.Clave);

    await using var rd = await cmd.ExecuteReaderAsync();
    if (!await rd.ReadAsync())
        return Results.Unauthorized();

    return Results.Ok(new
    {
        id = rd.GetInt32("id"),
        usuario = rd.GetString("usuario"),
        rol = rd.GetString("rol"),
        sucursal = rd.IsDBNull(rd.GetOrdinal("sucursal")) ? "TODAS" : rd.GetString("sucursal")
    });
});

app.MapGet("/api/sucursales", async (Db db) =>
{
    await using var con = await db.OpenAsync();
    var rows = await db.QueryAsync(con, "SELECT id, nombre, direccion, estado FROM sucursales ORDER BY id;");
    return Results.Ok(rows);
});

app.MapGet("/api/mesas", async (Db db, int? sucursalId) =>
{
    await using var con = await db.OpenAsync();

    const string sql = """
        SELECT m.id, m.sucursal_id, s.nombre AS sucursal, m.nombre, m.precio_hora, m.estado
        FROM mesas m
        INNER JOIN sucursales s ON s.id = m.sucursal_id
        WHERE (@sucursalId IS NULL OR m.sucursal_id = @sucursalId)
        ORDER BY m.sucursal_id, m.id;
    """;

    var rows = await db.QueryAsync(con, sql, new Dictionary<string, object?>
    {
        ["@sucursalId"] = sucursalId
    });

    return Results.Ok(rows);
});

app.MapGet("/api/productos", async (Db db, int? sucursalId) =>
{
    await using var con = await db.OpenAsync();

    const string sql = """
        SELECT p.id, p.sucursal_id, s.nombre AS sucursal, p.nombre, p.categoria,
               p.unidad_base, p.stock_actual, p.stock_minimo, p.estado
        FROM productos p
        INNER JOIN sucursales s ON s.id = p.sucursal_id
        WHERE (@sucursalId IS NULL OR p.sucursal_id = @sucursalId)
        ORDER BY p.sucursal_id, p.nombre;
    """;

    var rows = await db.QueryAsync(con, sql, new Dictionary<string, object?>
    {
        ["@sucursalId"] = sucursalId
    });

    return Results.Ok(rows);
});

app.MapPost("/api/productos", async (Db db, SheetsReporter sheets, ProductoRequest p) =>
{
    await using var con = await db.OpenAsync();

    const string sql = """
        INSERT INTO productos (sucursal_id, nombre, categoria, unidad_base, stock_actual, stock_minimo, estado)
        VALUES (@sucursal_id, @nombre, @categoria, @unidad_base, @stock_actual, @stock_minimo, 'ACTIVO');
        SELECT LAST_INSERT_ID();
    """;

    await using var cmd = new MySqlCommand(sql, con);
    cmd.Parameters.AddWithValue("@sucursal_id", p.SucursalId);
    cmd.Parameters.AddWithValue("@nombre", p.Nombre);
    cmd.Parameters.AddWithValue("@categoria", p.Categoria);
    cmd.Parameters.AddWithValue("@unidad_base", p.UnidadBase);
    cmd.Parameters.AddWithValue("@stock_actual", p.StockActual);
    cmd.Parameters.AddWithValue("@stock_minimo", p.StockMinimo);

    var id = Convert.ToInt64(await cmd.ExecuteScalarAsync());

    await TrySyncSheets(db, sheets);

    return Results.Ok(new { ok = true, id });
});

app.MapPost("/api/ventas", async (Db db, SheetsReporter sheets, VentaRequest venta) =>
{
    await using var con = await db.OpenAsync();
    await using var tx = await con.BeginTransactionAsync();

    try
    {
        string syncKey = string.IsNullOrWhiteSpace(venta.SyncKey)
            ? Guid.NewGuid().ToString("N")
            : venta.SyncKey;

        const string ventaSql = """
            INSERT INTO ventas (sucursal_id, cajero, fecha, tipo, metodo_pago, total, sync_key)
            VALUES (@sucursal_id, @cajero, @fecha, @tipo, @metodo_pago, @total, @sync_key)
            ON DUPLICATE KEY UPDATE
                total = VALUES(total),
                metodo_pago = VALUES(metodo_pago);
            SELECT id FROM ventas WHERE sync_key = @sync_key LIMIT 1;
        """;

        await using var ventaCmd = new MySqlCommand(ventaSql, con, tx);
        ventaCmd.Parameters.AddWithValue("@sucursal_id", venta.SucursalId);
        ventaCmd.Parameters.AddWithValue("@cajero", venta.Cajero);
        ventaCmd.Parameters.AddWithValue("@fecha", venta.Fecha);
        ventaCmd.Parameters.AddWithValue("@tipo", venta.Tipo);
        ventaCmd.Parameters.AddWithValue("@metodo_pago", venta.MetodoPago);
        ventaCmd.Parameters.AddWithValue("@total", venta.Total);
        ventaCmd.Parameters.AddWithValue("@sync_key", syncKey);

        var ventaId = Convert.ToInt64(await ventaCmd.ExecuteScalarAsync());

        await using (var del = new MySqlCommand("DELETE FROM detalle_ventas WHERE venta_id = @venta_id;", con, tx))
        {
            del.Parameters.AddWithValue("@venta_id", ventaId);
            await del.ExecuteNonQueryAsync();
        }

        foreach (var d in venta.Detalle)
        {
            const string detalleSql = """
                INSERT INTO detalle_ventas
                (venta_id, producto_id, presentacion_id, producto, presentacion, cantidad, precio_unitario, subtotal)
                VALUES
                (@venta_id, @producto_id, @presentacion_id, @producto, @presentacion, @cantidad, @precio_unitario, @subtotal);
            """;

            await using var detCmd = new MySqlCommand(detalleSql, con, tx);
            detCmd.Parameters.AddWithValue("@venta_id", ventaId);
            detCmd.Parameters.AddWithValue("@producto_id", d.ProductoId);
            detCmd.Parameters.AddWithValue("@presentacion_id", d.PresentacionId);
            detCmd.Parameters.AddWithValue("@producto", d.Producto);
            detCmd.Parameters.AddWithValue("@presentacion", d.Presentacion);
            detCmd.Parameters.AddWithValue("@cantidad", d.Cantidad);
            detCmd.Parameters.AddWithValue("@precio_unitario", d.PrecioUnitario);
            detCmd.Parameters.AddWithValue("@subtotal", d.Subtotal);
            await detCmd.ExecuteNonQueryAsync();

            await using var stockCmd = new MySqlCommand("""
                UPDATE productos
                SET stock_actual = stock_actual - @cantidad_base
                WHERE id = @producto_id;
            """, con, tx);
            stockCmd.Parameters.AddWithValue("@cantidad_base", d.CantidadBase);
            stockCmd.Parameters.AddWithValue("@producto_id", d.ProductoId);
            await stockCmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();

        await TrySyncSheets(db, sheets);

        return Results.Ok(new { ok = true, id = ventaId, syncKey });
    }
    catch (Exception ex)
    {
        await tx.RollbackAsync();
        return Results.Problem("Error al guardar venta: " + ex.Message);
    }
});

app.MapGet("/api/ventas", async (Db db, int? sucursalId) =>
{
    await using var con = await db.OpenAsync();

    const string sql = """
        SELECT v.id, v.sucursal_id, s.nombre AS sucursal, v.cajero, v.fecha,
               v.tipo, v.metodo_pago, v.total, v.sync_key
        FROM ventas v
        INNER JOIN sucursales s ON s.id = v.sucursal_id
        WHERE (@sucursalId IS NULL OR v.sucursal_id = @sucursalId)
        ORDER BY v.fecha DESC, v.id DESC
        LIMIT 500;
    """;

    var rows = await db.QueryAsync(con, sql, new Dictionary<string, object?>
    {
        ["@sucursalId"] = sucursalId
    });

    return Results.Ok(rows);
});

app.MapPost("/api/reservas", async (Db db, SheetsReporter sheets, ReservaRequest r) =>
{
    await using var con = await db.OpenAsync();
    string syncKey = string.IsNullOrWhiteSpace(r.SyncKey) ? Guid.NewGuid().ToString("N") : r.SyncKey;

    const string sql = """
        INSERT INTO reservas
        (sucursal_id, mesa_id, cliente, celular, fecha_reserva, minutos, estado, cajero, sync_key)
        VALUES
        (@sucursal_id, @mesa_id, @cliente, @celular, @fecha_reserva, @minutos, @estado, @cajero, @sync_key)
        ON DUPLICATE KEY UPDATE
            cliente = VALUES(cliente),
            celular = VALUES(celular),
            fecha_reserva = VALUES(fecha_reserva),
            minutos = VALUES(minutos),
            estado = VALUES(estado);
    """;

    await using var cmd = new MySqlCommand(sql, con);
    cmd.Parameters.AddWithValue("@sucursal_id", r.SucursalId);
    cmd.Parameters.AddWithValue("@mesa_id", r.MesaId);
    cmd.Parameters.AddWithValue("@cliente", r.Cliente);
    cmd.Parameters.AddWithValue("@celular", r.Celular ?? "");
    cmd.Parameters.AddWithValue("@fecha_reserva", r.FechaReserva);
    cmd.Parameters.AddWithValue("@minutos", r.Minutos);
    cmd.Parameters.AddWithValue("@estado", r.Estado);
    cmd.Parameters.AddWithValue("@cajero", r.Cajero ?? "");
    cmd.Parameters.AddWithValue("@sync_key", syncKey);

    await cmd.ExecuteNonQueryAsync();

    await TrySyncSheets(db, sheets);

    return Results.Ok(new { ok = true, syncKey });
});

app.MapPost("/api/propinas", async (Db db, SheetsReporter sheets, PropinaRequest p) =>
{
    await using var con = await db.OpenAsync();
    string syncKey = string.IsNullOrWhiteSpace(p.SyncKey) ? Guid.NewGuid().ToString("N") : p.SyncKey;

    const string sql = """
        INSERT INTO propinas
        (sucursal_id, mesa_id, mesera, cajero, fecha, monto, sync_key)
        VALUES
        (@sucursal_id, @mesa_id, @mesera, @cajero, @fecha, @monto, @sync_key)
        ON DUPLICATE KEY UPDATE
            monto = VALUES(monto);
    """;

    await using var cmd = new MySqlCommand(sql, con);
    cmd.Parameters.AddWithValue("@sucursal_id", p.SucursalId);
    cmd.Parameters.AddWithValue("@mesa_id", p.MesaId.HasValue ? p.MesaId.Value : DBNull.Value);
    cmd.Parameters.AddWithValue("@mesera", p.Mesera);
    cmd.Parameters.AddWithValue("@cajero", p.Cajero);
    cmd.Parameters.AddWithValue("@fecha", p.Fecha);
    cmd.Parameters.AddWithValue("@monto", p.Monto);
    cmd.Parameters.AddWithValue("@sync_key", syncKey);

    await cmd.ExecuteNonQueryAsync();

    await TrySyncSheets(db, sheets);

    return Results.Ok(new { ok = true, syncKey });
});

app.MapGet("/api/reportes/resumen", async (Db db) =>
{
    await using var con = await db.OpenAsync();

    const string sql = """
        SELECT
            s.nombre AS sucursal,
            COALESCE(SUM(v.total), 0) AS total_ventas,
            COUNT(v.id) AS cantidad_ventas
        FROM sucursales s
        LEFT JOIN ventas v ON v.sucursal_id = s.id
        GROUP BY s.id, s.nombre
        ORDER BY s.id;
    """;

    var porSucursal = await db.QueryAsync(con, sql);

    const string totalSql = """
        SELECT
            COALESCE(SUM(total), 0) AS total_general,
            COUNT(id) AS cantidad_ventas
        FROM ventas;
    """;

    var total = await db.QueryAsync(con, totalSql);

    return Results.Ok(new { porSucursal, total });
});

app.Run();

static async Task TrySyncSheets(Db db, SheetsReporter sheets)
{
    if (!sheets.IsConfigured) return;

    try
    {
        await sheets.SyncFromDatabaseAsync(db);
    }
    catch
    {
        // No se debe perder la venta si Google Sheets falla.
        // La venta ya queda guardada en MySQL y luego se puede forzar /api/sheets/sync.
    }
}

public sealed class Db
{
    private readonly string _connectionString;

    public Db(IConfiguration configuration)
    {
        _connectionString = BuildConnectionString(configuration);
    }

    public async Task<MySqlConnection> OpenAsync()
    {
        var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();
        return con;
    }

    public async Task<List<Dictionary<string, object?>>> QueryAsync(
        MySqlConnection con,
        string sql,
        Dictionary<string, object?>? parameters = null)
    {
        await using var cmd = new MySqlCommand(sql, con);

        if (parameters != null)
        {
            foreach (var p in parameters)
            {
                cmd.Parameters.AddWithValue(p.Key, p.Value ?? DBNull.Value);
            }
        }

        var rows = new List<Dictionary<string, object?>>();
        await using var rd = await cmd.ExecuteReaderAsync();

        while (await rd.ReadAsync())
        {
            var item = new Dictionary<string, object?>();
            for (int i = 0; i < rd.FieldCount; i++)
            {
                item[rd.GetName(i)] = rd.IsDBNull(i) ? null : rd.GetValue(i);
            }
            rows.Add(item);
        }

        return rows;
    }

    private static string BuildConnectionString(IConfiguration configuration)
    {
        string? fullUrl = Environment.GetEnvironmentVariable("MYSQL_URL");

        if (!string.IsNullOrWhiteSpace(fullUrl))
        {
            var uri = new Uri(fullUrl);
            string[] mysqlUserInfo = uri.UserInfo.Split(':', 2);
            string mysqlUserFromUrl = Uri.UnescapeDataString(mysqlUserInfo[0]);
            string mysqlPasswordFromUrl = mysqlUserInfo.Length > 1 ? Uri.UnescapeDataString(mysqlUserInfo[1]) : "";
            string mysqlDatabaseFromUrl = uri.AbsolutePath.TrimStart('/');

            return $"Server={uri.Host};Port={uri.Port};Database={mysqlDatabaseFromUrl};Uid={mysqlUserFromUrl};Pwd={mysqlPasswordFromUrl};SslMode=Preferred;";
        }

        string mysqlHost = Environment.GetEnvironmentVariable("MYSQLHOST")
            ?? configuration["MYSQLHOST"]
            ?? "localhost";

        string mysqlPort = Environment.GetEnvironmentVariable("MYSQLPORT")
            ?? configuration["MYSQLPORT"]
            ?? "3306";

        string mysqlDatabaseName = Environment.GetEnvironmentVariable("MYSQLDATABASE")
            ?? Environment.GetEnvironmentVariable("MYSQL_DATABASE")
            ?? configuration["MYSQLDATABASE"]
            ?? configuration["MYSQL_DATABASE"]
            ?? "railway";

        string mysqlUserName = Environment.GetEnvironmentVariable("MYSQLUSER")
            ?? configuration["MYSQLUSER"]
            ?? "root";

        string mysqlPasswordValue = Environment.GetEnvironmentVariable("MYSQLPASSWORD")
            ?? configuration["MYSQLPASSWORD"]
            ?? "";

        return $"Server={mysqlHost};Port={mysqlPort};Database={mysqlDatabaseName};Uid={mysqlUserName};Pwd={mysqlPasswordValue};SslMode=Preferred;";
    }
}

public sealed class SheetsReporter
{
    private readonly string _sheetId;
    private readonly string _credentialsJson;

    public SheetsReporter()
    {
        _sheetId = Environment.GetEnvironmentVariable("GOOGLE_SHEET_ID") ?? "";
        _credentialsJson = Environment.GetEnvironmentVariable("GOOGLE_CREDENTIALS_JSON") ?? "";
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_sheetId) &&
        !string.IsNullOrWhiteSpace(_credentialsJson);

    public string SpreadsheetId => string.IsNullOrWhiteSpace(_sheetId) ? "(sin configurar)" : _sheetId;

    public async Task<string> SyncFromDatabaseAsync(Db db)
    {
        if (!IsConfigured)
            return "Google Sheets no configurado.";

        SheetsService service = CreateService();

        await EnsureSheetsAsync(service, new[]
        {
            "Ventas",
            "Detalle_Ventas",
            "Cobros_Mesa",
            "Stock",
            "Reservas",
            "Propinas",
            "Resumen_Diario"
        });

        await using var con = await db.OpenAsync();

        List<List<object>> ventas = new()
        {
            new() { "id_venta", "fecha", "hora", "sucursal", "cajero", "tipo", "metodo_pago", "total", "sincronizado" }
        };
        ventas.AddRange((await db.QueryAsync(con, """
            SELECT v.id, DATE(v.fecha) AS fecha, TIME(v.fecha) AS hora, s.nombre AS sucursal,
                   v.cajero, v.tipo, v.metodo_pago, v.total
            FROM ventas v
            INNER JOIN sucursales s ON s.id = v.sucursal_id
            ORDER BY v.fecha, v.id;
        """)).Select(r => new List<object>
        {
            Val(r, "id"), DateOnlyText(r, "fecha"), Text(r, "hora"), Text(r, "sucursal"),
            Text(r, "cajero"), Text(r, "tipo"), Text(r, "metodo_pago"), Val(r, "total"), "SI"
        }));

        List<List<object>> detalle = new()
        {
            new() { "id_venta", "sucursal", "cajero", "producto", "presentacion", "cantidad", "precio", "subtotal" }
        };
        detalle.AddRange((await db.QueryAsync(con, """
            SELECT d.venta_id, s.nombre AS sucursal, v.cajero, d.producto, d.presentacion,
                   d.cantidad, d.precio_unitario, d.subtotal
            FROM detalle_ventas d
            INNER JOIN ventas v ON v.id = d.venta_id
            INNER JOIN sucursales s ON s.id = v.sucursal_id
            ORDER BY d.venta_id, d.id;
        """)).Select(r => new List<object>
        {
            Val(r, "venta_id"), Text(r, "sucursal"), Text(r, "cajero"), Text(r, "producto"),
            Text(r, "presentacion"), Val(r, "cantidad"), Val(r, "precio_unitario"), Val(r, "subtotal")
        }));

        List<List<object>> cobrosMesa = new()
        {
            new() { "id_sesion", "fecha", "hora", "sucursal", "mesa", "cajero", "mesera", "tiempo", "total_mesa", "total_consumo", "total_cobrado", "metodo_pago" }
        };

        List<List<object>> stock = new()
        {
            new() { "id_producto", "sucursal", "producto", "categoria", "stock_actual", "stock_minimo", "unidad_base", "alerta" }
        };
        stock.AddRange((await db.QueryAsync(con, """
            SELECT p.id, s.nombre AS sucursal, p.nombre, p.categoria, p.stock_actual,
                   p.stock_minimo, p.unidad_base,
                   CASE WHEN p.stock_actual <= p.stock_minimo THEN 'BAJO' ELSE 'OK' END AS alerta
            FROM productos p
            INNER JOIN sucursales s ON s.id = p.sucursal_id
            ORDER BY s.id, p.nombre;
        """)).Select(r => new List<object>
        {
            Val(r, "id"), Text(r, "sucursal"), Text(r, "nombre"), Text(r, "categoria"),
            Val(r, "stock_actual"), Val(r, "stock_minimo"), Text(r, "unidad_base"), Text(r, "alerta")
        }));

        List<List<object>> reservas = new()
        {
            new() { "id_reserva", "fecha", "hora", "sucursal", "mesa", "cliente", "celular", "minutos", "estado" }
        };
        reservas.AddRange((await db.QueryAsync(con, """
            SELECT r.id, DATE(r.fecha_reserva) AS fecha, TIME(r.fecha_reserva) AS hora,
                   s.nombre AS sucursal, m.nombre AS mesa, r.cliente, r.celular, r.minutos, r.estado
            FROM reservas r
            INNER JOIN sucursales s ON s.id = r.sucursal_id
            INNER JOIN mesas m ON m.id = r.mesa_id
            ORDER BY r.fecha_reserva, r.id;
        """)).Select(r => new List<object>
        {
            Val(r, "id"), DateOnlyText(r, "fecha"), Text(r, "hora"), Text(r, "sucursal"),
            Text(r, "mesa"), Text(r, "cliente"), Text(r, "celular"), Val(r, "minutos"), Text(r, "estado")
        }));

        List<List<object>> propinas = new()
        {
            new() { "fecha", "hora", "sucursal", "mesa", "mesera", "monto", "cajero" }
        };
        propinas.AddRange((await db.QueryAsync(con, """
            SELECT DATE(p.fecha) AS fecha, TIME(p.fecha) AS hora, s.nombre AS sucursal,
                   COALESCE(m.nombre, '') AS mesa, p.mesera, p.monto, p.cajero
            FROM propinas p
            INNER JOIN sucursales s ON s.id = p.sucursal_id
            LEFT JOIN mesas m ON m.id = p.mesa_id
            ORDER BY p.fecha, p.id;
        """)).Select(r => new List<object>
        {
            DateOnlyText(r, "fecha"), Text(r, "hora"), Text(r, "sucursal"),
            Text(r, "mesa"), Text(r, "mesera"), Val(r, "monto"), Text(r, "cajero")
        }));

        List<List<object>> resumen = new()
        {
            new() { "fecha", "sucursal", "ventas_productos", "cobro_mesas", "total_ingreso", "propinas", "cajero" }
        };
        resumen.AddRange((await db.QueryAsync(con, """
            SELECT
                x.fecha_dia,
                x.sucursal,
                x.ventas_productos,
                x.cobro_mesas,
                x.total_ingreso,
                COALESCE(SUM(p.monto), 0) AS propinas,
                x.cajero
            FROM (
                SELECT
                    DATE(v.fecha) AS fecha_dia,
                    v.sucursal_id,
                    s.nombre AS sucursal,
                    v.cajero,
                    SUM(v.total) AS ventas_productos,
                    0 AS cobro_mesas,
                    SUM(v.total) AS total_ingreso
                FROM ventas v
                INNER JOIN sucursales s ON s.id = v.sucursal_id
                GROUP BY DATE(v.fecha), v.sucursal_id, s.nombre, v.cajero
            ) x
            LEFT JOIN propinas p
                ON p.sucursal_id = x.sucursal_id
               AND DATE(p.fecha) = x.fecha_dia
            GROUP BY x.fecha_dia, x.sucursal, x.ventas_productos, x.cobro_mesas, x.total_ingreso, x.cajero
            ORDER BY x.fecha_dia, x.sucursal, x.cajero;
        """)).Select(r => new List<object>
        {
            DateOnlyText(r, "fecha_dia"), Text(r, "sucursal"), Val(r, "ventas_productos"),
            Val(r, "cobro_mesas"), Val(r, "total_ingreso"), Val(r, "propinas"), Text(r, "cajero")
        }));

        await ReplaceSheetAsync(service, "Ventas", ventas);
        await ReplaceSheetAsync(service, "Detalle_Ventas", detalle);
        await ReplaceSheetAsync(service, "Cobros_Mesa", cobrosMesa);
        await ReplaceSheetAsync(service, "Stock", stock);
        await ReplaceSheetAsync(service, "Reservas", reservas);
        await ReplaceSheetAsync(service, "Propinas", propinas);
        await ReplaceSheetAsync(service, "Resumen_Diario", resumen);

        return "Google Sheets actualizado desde MySQL Railway.";
    }

    private SheetsService CreateService()
    {
        GoogleCredential credential = GoogleCredential
            .FromJson(_credentialsJson)
            .CreateScoped(SheetsService.Scope.Spreadsheets);

        return new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Billar El Brujo API"
        });
    }

    private async Task EnsureSheetsAsync(SheetsService service, IEnumerable<string> names)
    {
        var spreadsheet = await service.Spreadsheets.Get(_sheetId).ExecuteAsync();
        var existing = spreadsheet.Sheets
            .Select(s => s.Properties.Title)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var requests = new List<Google.Apis.Sheets.v4.Data.Request>();

        foreach (string name in names)
        {
            if (!existing.Contains(name))
            {
                requests.Add(new Google.Apis.Sheets.v4.Data.Request
                {
                    AddSheet = new Google.Apis.Sheets.v4.Data.AddSheetRequest
                    {
                        Properties = new Google.Apis.Sheets.v4.Data.SheetProperties
                        {
                            Title = name
                        }
                    }
                });
            }
        }

        if (requests.Count == 0) return;

        var batch = new Google.Apis.Sheets.v4.Data.BatchUpdateSpreadsheetRequest
        {
            Requests = requests
        };

        await service.Spreadsheets.BatchUpdate(batch, _sheetId).ExecuteAsync();
    }

    private async Task ReplaceSheetAsync(SheetsService service, string sheetName, List<List<object>> values)
    {
        string range = "'" + sheetName.Replace("'", "''") + "'!A1:Z2000";

        await service.Spreadsheets.Values.Clear(
            new Google.Apis.Sheets.v4.Data.ClearValuesRequest(),
            _sheetId,
            range
        ).ExecuteAsync();

        var valueRange = new Google.Apis.Sheets.v4.Data.ValueRange
        {
            Values = values.Select(r => (IList<object>)r).ToList()
        };

        var update = service.Spreadsheets.Values.Update(valueRange, _sheetId, "'" + sheetName.Replace("'", "''") + "'!A1");
        update.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
        await update.ExecuteAsync();
    }

    private static object Val(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out object? value) || value == null) return "";
        return value;
    }

    private static string Text(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out object? value) || value == null) return "";
        return Convert.ToString(value) ?? "";
    }

    private static string DateOnlyText(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out object? value) || value == null) return "";
        if (value is DateTime dt) return dt.ToString("yyyy-MM-dd");
        return Convert.ToString(value) ?? "";
    }
}

public record LoginRequest(string Usuario, string Clave);

public record ProductoRequest(
    int SucursalId,
    string Nombre,
    string Categoria,
    string UnidadBase,
    decimal StockActual,
    decimal StockMinimo
);

public record VentaDetalleRequest(
    int ProductoId,
    int PresentacionId,
    string Producto,
    string Presentacion,
    decimal Cantidad,
    decimal CantidadBase,
    decimal PrecioUnitario,
    decimal Subtotal
);

public record VentaRequest(
    int SucursalId,
    string Cajero,
    DateTime Fecha,
    string Tipo,
    string MetodoPago,
    decimal Total,
    string? SyncKey,
    List<VentaDetalleRequest> Detalle
);

public record ReservaRequest(
    int SucursalId,
    int MesaId,
    string Cliente,
    string? Celular,
    DateTime FechaReserva,
    int Minutos,
    string Estado,
    string? Cajero,
    string? SyncKey
);

public record PropinaRequest(
    int SucursalId,
    int? MesaId,
    string Mesera,
    string Cajero,
    DateTime Fecha,
    decimal Monto,
    string? SyncKey
);
