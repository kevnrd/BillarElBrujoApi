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
            version = "V13_REPORTES_EXCEL_FINAL",
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
        SELECT u.id, u.usuario, u.rol, u.estado, CASE WHEN s.id = 2 THEN 'SEGUNDA SUCURSAL' ELSE 'PRIMERA SUCURSAL' END AS sucursal
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
        SELECT m.id, m.sucursal_id, CASE WHEN s.id = 2 THEN 'SEGUNDA SUCURSAL' ELSE 'PRIMERA SUCURSAL' END AS sucursal, m.nombre, m.precio_hora, m.estado
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
        SELECT p.id, p.sucursal_id, CASE WHEN s.id = 2 THEN 'SEGUNDA SUCURSAL' ELSE 'PRIMERA SUCURSAL' END AS sucursal, p.nombre, p.categoria,
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
            long productoId = d.ProductoId <= 0 ? Math.Abs((d.Producto ?? "PRODUCTO").GetHashCode()) : d.ProductoId;
            long presentacionId = d.PresentacionId <= 0 ? Math.Abs(((d.Producto ?? "") + "-" + (d.Presentacion ?? "")).GetHashCode()) : d.PresentacionId;

            // Garantiza que el producto exista en Railway antes de insertar el detalle.
            // Esto evita fallas por llaves foráneas cuando la PC local tiene productos
            // pero MySQL Railway fue limpiado para la entrega.
            await using (var prodCmd = new MySqlCommand("""
                INSERT INTO productos (id, sucursal_id, nombre, categoria, unidad_base, stock_actual, stock_minimo, estado)
                VALUES (@id, @sucursal_id, @nombre, 'General', 'UNIDAD', 0, 0, 'ACTIVO')
                ON DUPLICATE KEY UPDATE
                    nombre = VALUES(nombre),
                    sucursal_id = VALUES(sucursal_id),
                    estado = 'ACTIVO';
            """, con, tx))
            {
                prodCmd.Parameters.AddWithValue("@id", productoId);
                prodCmd.Parameters.AddWithValue("@sucursal_id", venta.SucursalId);
                prodCmd.Parameters.AddWithValue("@nombre", string.IsNullOrWhiteSpace(d.Producto) ? "Producto" : d.Producto);
                await prodCmd.ExecuteNonQueryAsync();
            }

            await using (var presCmd = new MySqlCommand("""
                INSERT INTO presentaciones (id, producto_id, nombre, cantidad_base, precio_venta, estado)
                VALUES (@id, @producto_id, @nombre, @cantidad_base, @precio_venta, 'ACTIVO')
                ON DUPLICATE KEY UPDATE
                    producto_id = VALUES(producto_id),
                    nombre = VALUES(nombre),
                    cantidad_base = VALUES(cantidad_base),
                    precio_venta = VALUES(precio_venta),
                    estado = 'ACTIVO';
            """, con, tx))
            {
                presCmd.Parameters.AddWithValue("@id", presentacionId);
                presCmd.Parameters.AddWithValue("@producto_id", productoId);
                presCmd.Parameters.AddWithValue("@nombre", string.IsNullOrWhiteSpace(d.Presentacion) ? "Unidad" : d.Presentacion);
                presCmd.Parameters.AddWithValue("@cantidad_base", d.CantidadBase <= 0 ? d.Cantidad : d.CantidadBase);
                presCmd.Parameters.AddWithValue("@precio_venta", d.PrecioUnitario);
                await presCmd.ExecuteNonQueryAsync();
            }

            const string detalleSql = """
                INSERT INTO detalle_ventas
                (venta_id, producto_id, presentacion_id, producto, presentacion, cantidad, precio_unitario, subtotal)
                VALUES
                (@venta_id, @producto_id, @presentacion_id, @producto, @presentacion, @cantidad, @precio_unitario, @subtotal);
            """;

            await using var detCmd = new MySqlCommand(detalleSql, con, tx);
            detCmd.Parameters.AddWithValue("@venta_id", ventaId);
            detCmd.Parameters.AddWithValue("@producto_id", productoId);
            detCmd.Parameters.AddWithValue("@presentacion_id", presentacionId);
            detCmd.Parameters.AddWithValue("@producto", d.Producto);
            detCmd.Parameters.AddWithValue("@presentacion", d.Presentacion);
            detCmd.Parameters.AddWithValue("@cantidad", d.Cantidad);
            detCmd.Parameters.AddWithValue("@precio_unitario", d.PrecioUnitario);
            detCmd.Parameters.AddWithValue("@subtotal", d.Subtotal);
            await detCmd.ExecuteNonQueryAsync();

            await using var stockCmd = new MySqlCommand("""
                UPDATE productos
                SET stock_actual = GREATEST(stock_actual - @cantidad_base, 0)
                WHERE id = @producto_id;
            """, con, tx);
            stockCmd.Parameters.AddWithValue("@cantidad_base", d.CantidadBase <= 0 ? d.Cantidad : d.CantidadBase);
            stockCmd.Parameters.AddWithValue("@producto_id", productoId);
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
        SELECT v.id, v.sucursal_id, CASE WHEN s.id = 2 THEN 'SEGUNDA SUCURSAL' ELSE 'PRIMERA SUCURSAL' END AS sucursal, v.cajero, v.fecha,
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



app.MapGet("/api/detalle-ventas", async (Db db, int? sucursalId) =>
{
    await using var con = await db.OpenAsync();

    string where = sucursalId.HasValue ? "WHERE v.sucursal_id = @sucursal_id" : "";

    string sql = $"""
        SELECT d.venta_id AS id_venta,
               CASE WHEN s.id = 2 THEN 'SEGUNDA SUCURSAL' ELSE 'PRIMERA SUCURSAL' END AS sucursal,
               v.cajero,
               d.producto,
               d.presentacion,
               d.cantidad,
               d.precio_unitario AS precio,
               d.subtotal
        FROM detalle_ventas d
        INNER JOIN ventas v ON v.id = d.venta_id
        INNER JOIN sucursales s ON s.id = v.sucursal_id
        {where}
        ORDER BY d.venta_id DESC, d.id DESC;
    """;

    Dictionary<string, object?>? parameters = sucursalId.HasValue
        ? new Dictionary<string, object?> { ["@sucursal_id"] = sucursalId.Value }
        : null;

    return Results.Ok(await db.QueryAsync(con, sql, parameters));
});

app.MapPost("/api/cobros-mesa", async (Db db, SheetsReporter sheets, CobroMesaRequest c) =>
{
    await using var con = await db.OpenAsync();

    await using (var create = new MySqlCommand("""
        CREATE TABLE IF NOT EXISTS cobros_mesa (
            id BIGINT AUTO_INCREMENT PRIMARY KEY,
            sucursal_id INT NOT NULL,
            session_id INT NULL,
            mesa_id INT NULL,
            mesa VARCHAR(100) NOT NULL,
            cajero VARCHAR(100) NOT NULL,
            mesera VARCHAR(150) NULL,
            fecha DATETIME NOT NULL,
            tiempo VARCHAR(50) NULL,
            total_mesa DECIMAL(10,2) NOT NULL DEFAULT 0,
            total_consumo DECIMAL(10,2) NOT NULL DEFAULT 0,
            total_cobrado DECIMAL(10,2) NOT NULL DEFAULT 0,
            metodo_pago VARCHAR(50) NOT NULL,
            sync_key VARCHAR(180) NOT NULL UNIQUE
        );
    """, con))
    {
        await create.ExecuteNonQueryAsync();
    }

    string syncKey = string.IsNullOrWhiteSpace(c.SyncKey) ? Guid.NewGuid().ToString("N") : c.SyncKey;

    const string sql = """
        INSERT INTO cobros_mesa
        (sucursal_id, session_id, mesa_id, mesa, cajero, mesera, fecha, tiempo, total_mesa, total_consumo, total_cobrado, metodo_pago, sync_key)
        VALUES
        (@sucursal_id, @session_id, @mesa_id, @mesa, @cajero, @mesera, @fecha, @tiempo, @total_mesa, @total_consumo, @total_cobrado, @metodo_pago, @sync_key)
        ON DUPLICATE KEY UPDATE
            mesa = VALUES(mesa),
            cajero = VALUES(cajero),
            mesera = VALUES(mesera),
            fecha = VALUES(fecha),
            tiempo = VALUES(tiempo),
            total_mesa = VALUES(total_mesa),
            total_consumo = VALUES(total_consumo),
            total_cobrado = VALUES(total_cobrado),
            metodo_pago = VALUES(metodo_pago);
    """;

    await using var cmd = new MySqlCommand(sql, con);
    cmd.Parameters.AddWithValue("@sucursal_id", c.SucursalId);
    cmd.Parameters.AddWithValue("@session_id", c.SessionId.HasValue ? c.SessionId.Value : DBNull.Value);
    cmd.Parameters.AddWithValue("@mesa_id", c.MesaId.HasValue ? c.MesaId.Value : DBNull.Value);
    cmd.Parameters.AddWithValue("@mesa", c.Mesa ?? "");
    cmd.Parameters.AddWithValue("@cajero", c.Cajero ?? "");
    cmd.Parameters.AddWithValue("@mesera", c.Mesera ?? "");
    cmd.Parameters.AddWithValue("@fecha", c.Fecha);
    cmd.Parameters.AddWithValue("@tiempo", c.Tiempo ?? "");
    cmd.Parameters.AddWithValue("@total_mesa", c.TotalMesa);
    cmd.Parameters.AddWithValue("@total_consumo", c.TotalConsumo);
    cmd.Parameters.AddWithValue("@total_cobrado", c.TotalCobrado);
    cmd.Parameters.AddWithValue("@metodo_pago", c.MetodoPago ?? "");
    cmd.Parameters.AddWithValue("@sync_key", syncKey);

    await cmd.ExecuteNonQueryAsync();

    await TrySyncSheets(db, sheets);

    return Results.Ok(new { ok = true, syncKey });
});


app.MapGet("/api/cobros-mesa", async (Db db, int? sucursalId) =>
{
    await using var con = await db.OpenAsync();

    await using (var create = new MySqlCommand("""
        CREATE TABLE IF NOT EXISTS cobros_mesa (
            id BIGINT AUTO_INCREMENT PRIMARY KEY,
            sucursal_id INT NOT NULL,
            session_id INT NULL,
            mesa_id INT NULL,
            mesa VARCHAR(100) NOT NULL,
            cajero VARCHAR(100) NOT NULL,
            mesera VARCHAR(150) NULL,
            fecha DATETIME NOT NULL,
            tiempo VARCHAR(50) NULL,
            total_mesa DECIMAL(10,2) NOT NULL DEFAULT 0,
            total_consumo DECIMAL(10,2) NOT NULL DEFAULT 0,
            total_cobrado DECIMAL(10,2) NOT NULL DEFAULT 0,
            metodo_pago VARCHAR(50) NOT NULL,
            sync_key VARCHAR(180) NOT NULL UNIQUE
        );
    """, con))
    {
        await create.ExecuteNonQueryAsync();
    }

    string where = sucursalId.HasValue ? "WHERE c.sucursal_id = @sucursal_id" : "";

    string sql = $"""
        SELECT c.id, c.session_id, DATE(c.fecha) AS fecha, TIME(c.fecha) AS hora,
               CASE WHEN s.id = 2 THEN 'SEGUNDA SUCURSAL' ELSE 'PRIMERA SUCURSAL' END AS sucursal,
               c.mesa, c.cajero, c.mesera, c.tiempo,
               c.total_mesa, c.total_consumo, c.total_cobrado, c.metodo_pago
        FROM cobros_mesa c
        INNER JOIN sucursales s ON s.id = c.sucursal_id
        {where}
        ORDER BY c.fecha DESC, c.id DESC;
    """;

    Dictionary<string, object?>? parameters = sucursalId.HasValue
        ? new Dictionary<string, object?> { ["@sucursal_id"] = sucursalId.Value }
        : null;

    return Results.Ok(await db.QueryAsync(con, sql, parameters));
});


app.MapPost("/api/mesas/estado", async (Db db, SheetsReporter sheets, MesaEstadoRequest m) =>
{
    await using var con = await db.OpenAsync();

    await EnsureMesasEnVivoTables(con);

    string syncKey = string.IsNullOrWhiteSpace(m.SyncKey)
        ? $"MESA-{m.SucursalId}-{m.MesaId}"
        : m.SyncKey;

    const string sql = """
        INSERT INTO mesa_estados
        (sucursal_id, mesa_id, mesa, estado, cajero, inicio, fin_programado, minutos, total_mesa, total_consumo, total_general, cliente_reserva, actualizado, sync_key)
        VALUES
        (@sucursal_id, @mesa_id, @mesa, @estado, @cajero, @inicio, @fin_programado, @minutos, @total_mesa, @total_consumo, @total_general, @cliente_reserva, NOW(), @sync_key)
        ON DUPLICATE KEY UPDATE
            mesa = VALUES(mesa),
            estado = VALUES(estado),
            cajero = VALUES(cajero),
            inicio = VALUES(inicio),
            fin_programado = VALUES(fin_programado),
            minutos = VALUES(minutos),
            total_mesa = VALUES(total_mesa),
            total_consumo = VALUES(total_consumo),
            total_general = VALUES(total_general),
            cliente_reserva = VALUES(cliente_reserva),
            actualizado = NOW();
    """;

    await using var cmd = new MySqlCommand(sql, con);
    cmd.Parameters.AddWithValue("@sucursal_id", m.SucursalId);
    cmd.Parameters.AddWithValue("@mesa_id", m.MesaId);
    cmd.Parameters.AddWithValue("@mesa", m.Mesa ?? ("Mesa " + m.MesaId));
    cmd.Parameters.AddWithValue("@estado", m.Estado ?? "LIBRE");
    cmd.Parameters.AddWithValue("@cajero", m.Cajero ?? "");
    cmd.Parameters.AddWithValue("@inicio", m.Inicio.HasValue ? m.Inicio.Value : DBNull.Value);
    cmd.Parameters.AddWithValue("@fin_programado", m.FinProgramado.HasValue ? m.FinProgramado.Value : DBNull.Value);
    cmd.Parameters.AddWithValue("@minutos", m.Minutos);
    cmd.Parameters.AddWithValue("@total_mesa", m.TotalMesa);
    cmd.Parameters.AddWithValue("@total_consumo", m.TotalConsumo);
    cmd.Parameters.AddWithValue("@total_general", m.TotalGeneral);
    cmd.Parameters.AddWithValue("@cliente_reserva", m.ClienteReserva ?? "");
    cmd.Parameters.AddWithValue("@sync_key", syncKey);
    await cmd.ExecuteNonQueryAsync();

    await using (var del = new MySqlCommand("DELETE FROM mesa_consumos_vivos WHERE sucursal_id = @sucursal_id AND mesa_id = @mesa_id;", con))
    {
        del.Parameters.AddWithValue("@sucursal_id", m.SucursalId);
        del.Parameters.AddWithValue("@mesa_id", m.MesaId);
        await del.ExecuteNonQueryAsync();
    }

    foreach (var d in m.Detalle ?? new List<MesaConsumoVivoRequest>())
    {
        await using var det = new MySqlCommand("""
            INSERT INTO mesa_consumos_vivos
            (sucursal_id, mesa_id, producto, presentacion, cantidad, precio_unitario, subtotal, actualizado)
            VALUES
            (@sucursal_id, @mesa_id, @producto, @presentacion, @cantidad, @precio_unitario, @subtotal, NOW());
        """, con);
        det.Parameters.AddWithValue("@sucursal_id", m.SucursalId);
        det.Parameters.AddWithValue("@mesa_id", m.MesaId);
        det.Parameters.AddWithValue("@producto", d.Producto ?? "");
        det.Parameters.AddWithValue("@presentacion", d.Presentacion ?? "");
        det.Parameters.AddWithValue("@cantidad", d.Cantidad);
        det.Parameters.AddWithValue("@precio_unitario", d.PrecioUnitario);
        det.Parameters.AddWithValue("@subtotal", d.Subtotal);
        await det.ExecuteNonQueryAsync();
    }

    return Results.Ok(new { ok = true, syncKey });
});

app.MapGet("/api/mesas/estado", async (Db db, int? sucursalId) =>
{
    await using var con = await db.OpenAsync();
    await EnsureMesasEnVivoTables(con);

    string where = sucursalId.HasValue ? "WHERE e.sucursal_id = @sucursal_id" : "";

    string sql = $"""
        SELECT e.sucursal_id,
               CASE WHEN e.sucursal_id = 2 THEN 'SEGUNDA SUCURSAL' ELSE 'PRIMERA SUCURSAL' END AS sucursal,
               e.mesa_id, e.mesa, e.estado, e.cajero, e.inicio, e.fin_programado,
               e.minutos, e.total_mesa, e.total_consumo, e.total_general,
               e.cliente_reserva, e.actualizado
        FROM mesa_estados e
        {where}
        ORDER BY e.sucursal_id, e.mesa_id;
    """;

    Dictionary<string, object?>? parameters = sucursalId.HasValue
        ? new Dictionary<string, object?> { ["@sucursal_id"] = sucursalId.Value }
        : null;

    return Results.Ok(await db.QueryAsync(con, sql, parameters));
});

app.MapGet("/api/mesas/consumos-vivos", async (Db db, int? sucursalId) =>
{
    await using var con = await db.OpenAsync();
    await EnsureMesasEnVivoTables(con);

    string where = sucursalId.HasValue ? "WHERE c.sucursal_id = @sucursal_id" : "";

    string sql = $"""
        SELECT c.sucursal_id,
               CASE WHEN c.sucursal_id = 2 THEN 'SEGUNDA SUCURSAL' ELSE 'PRIMERA SUCURSAL' END AS sucursal,
               c.mesa_id, c.producto, c.presentacion, c.cantidad, c.precio_unitario, c.subtotal
        FROM mesa_consumos_vivos c
        {where}
        ORDER BY c.sucursal_id, c.mesa_id, c.id;
    """;

    Dictionary<string, object?>? parameters = sucursalId.HasValue
        ? new Dictionary<string, object?> { ["@sucursal_id"] = sucursalId.Value }
        : null;

    return Results.Ok(await db.QueryAsync(con, sql, parameters));
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
            CASE WHEN s.id = 2 THEN 'SEGUNDA SUCURSAL' ELSE 'PRIMERA SUCURSAL' END AS sucursal,
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


static async Task EnsureMesasEnVivoTables(MySqlConnection con)
{
    await using (var cmd = new MySqlCommand("""
        CREATE TABLE IF NOT EXISTS mesa_estados (
            id BIGINT AUTO_INCREMENT PRIMARY KEY,
            sucursal_id INT NOT NULL,
            mesa_id INT NOT NULL,
            mesa VARCHAR(100) NOT NULL,
            estado VARCHAR(50) NOT NULL,
            cajero VARCHAR(100) NULL,
            inicio DATETIME NULL,
            fin_programado DATETIME NULL,
            minutos INT NOT NULL DEFAULT 0,
            total_mesa DECIMAL(10,2) NOT NULL DEFAULT 0,
            total_consumo DECIMAL(10,2) NOT NULL DEFAULT 0,
            total_general DECIMAL(10,2) NOT NULL DEFAULT 0,
            cliente_reserva VARCHAR(150) NULL,
            actualizado DATETIME NOT NULL,
            sync_key VARCHAR(180) NOT NULL,
            UNIQUE KEY uk_mesa_estado (sucursal_id, mesa_id)
        );
    """, con))
    {
        await cmd.ExecuteNonQueryAsync();
    }

    await using (var cmd = new MySqlCommand("""
        CREATE TABLE IF NOT EXISTS mesa_consumos_vivos (
            id BIGINT AUTO_INCREMENT PRIMARY KEY,
            sucursal_id INT NOT NULL,
            mesa_id INT NOT NULL,
            producto VARCHAR(180) NOT NULL,
            presentacion VARCHAR(120) NULL,
            cantidad DECIMAL(10,2) NOT NULL DEFAULT 0,
            precio_unitario DECIMAL(10,2) NOT NULL DEFAULT 0,
            subtotal DECIMAL(10,2) NOT NULL DEFAULT 0,
            actualizado DATETIME NOT NULL,
            INDEX idx_mesa_consumos_vivos (sucursal_id, mesa_id)
        );
    """, con))
    {
        await cmd.ExecuteNonQueryAsync();
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
            "Resumen_Diario",
            "Resumen_Turno",
            "Dinero_Turno",
            "Ventas_Cada_Cajero",
            "Como_Pagaron_Clientes",
            "Ganancia_Negocio",
            "Empleados_Turnos",
            "Ayuda_Comida_Empleados",
            "Ingreso_Mercaderia",
            "Productos_Usados_Sin_Venta",
            "Productos_Perdidos_Danados",
            "Historial_Productos"
        });

        await using var con = await db.OpenAsync();

        List<List<object>> ventas = new()
        {
            new() { "id_venta", "fecha", "hora", "sucursal", "cajero", "tipo", "metodo_pago", "total", "sincronizado" }
        };
        ventas.AddRange((await db.QueryAsync(con, """
            SELECT v.id, DATE(v.fecha) AS fecha, TIME(v.fecha) AS hora, CASE WHEN s.id = 2 THEN 'SEGUNDA SUCURSAL' ELSE 'PRIMERA SUCURSAL' END AS sucursal,
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
            SELECT d.venta_id, CASE WHEN s.id = 2 THEN 'SEGUNDA SUCURSAL' ELSE 'PRIMERA SUCURSAL' END AS sucursal, v.cajero, d.producto, d.presentacion,
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
        cobrosMesa.AddRange((await db.QueryAsync(con, """
            SELECT c.session_id, DATE(c.fecha) AS fecha, TIME(c.fecha) AS hora,
                   CASE WHEN s.id = 2 THEN 'SEGUNDA SUCURSAL' ELSE 'PRIMERA SUCURSAL' END AS sucursal,
                   c.mesa, c.cajero, c.mesera, c.tiempo, c.total_mesa, c.total_consumo, c.total_cobrado, c.metodo_pago
            FROM cobros_mesa c
            INNER JOIN sucursales s ON s.id = c.sucursal_id
            ORDER BY c.fecha, c.id;
        """)).Select(r => new List<object>
        {
            Val(r, "session_id"), DateOnlyText(r, "fecha"), Text(r, "hora"), Text(r, "sucursal"),
            Text(r, "mesa"), Text(r, "cajero"), Text(r, "mesera"), Text(r, "tiempo"),
            Val(r, "total_mesa"), Val(r, "total_consumo"), Val(r, "total_cobrado"), Text(r, "metodo_pago")
        }));

        List<List<object>> stock = new()
        {
            new() { "id_producto", "sucursal", "producto", "categoria", "stock_actual", "stock_minimo", "unidad_base", "alerta" }
        };
        stock.AddRange((await db.QueryAsync(con, """
            SELECT p.id, CASE WHEN s.id = 2 THEN 'SEGUNDA SUCURSAL' ELSE 'PRIMERA SUCURSAL' END AS sucursal, p.nombre, p.categoria, GREATEST(p.stock_actual, 0) AS stock_actual,
                   p.stock_minimo, p.unidad_base,
                   CASE WHEN GREATEST(p.stock_actual, 0) <= p.stock_minimo THEN 'BAJO' ELSE 'OK' END AS alerta
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
                   CASE WHEN s.id = 2 THEN 'SEGUNDA SUCURSAL' ELSE 'PRIMERA SUCURSAL' END AS sucursal, m.nombre AS mesa, r.cliente, r.celular, r.minutos, r.estado
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
            SELECT DATE(p.fecha) AS fecha, TIME(p.fecha) AS hora, CASE WHEN s.id = 2 THEN 'SEGUNDA SUCURSAL' ELSE 'PRIMERA SUCURSAL' END AS sucursal,
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
                SUM(x.productos) AS ventas_productos,
                SUM(x.mesa) AS cobro_mesas,
                SUM(x.total) AS total_ingreso,
                COALESCE((
                    SELECT SUM(p.monto)
                    FROM propinas p
                    INNER JOIN sucursales sp ON sp.id = p.sucursal_id
                    WHERE DATE(p.fecha) = x.fecha_dia
                      AND p.cajero = x.cajero
                      AND (CASE WHEN sp.id = 2 THEN 'SEGUNDA SUCURSAL' ELSE 'PRIMERA SUCURSAL' END) = x.sucursal
                ), 0) AS propinas,
                x.cajero
            FROM (
                SELECT
                    DATE(v.fecha) AS fecha_dia,
                    CASE WHEN s.id = 2 THEN 'SEGUNDA SUCURSAL' ELSE 'PRIMERA SUCURSAL' END AS sucursal,
                    v.cajero,
                    v.tipo,
                    v.total,
                    COALESCE((SELECT SUM(d.subtotal) FROM detalle_ventas d WHERE d.venta_id = v.id), 0) AS productos,
                    CASE
                        WHEN v.tipo = 'MESA'
                        THEN GREATEST(v.total - COALESCE((SELECT SUM(d2.subtotal) FROM detalle_ventas d2 WHERE d2.venta_id = v.id), 0), 0)
                        ELSE 0
                    END AS mesa
                FROM ventas v
                INNER JOIN sucursales s ON s.id = v.sucursal_id
            ) x
            GROUP BY x.fecha_dia, x.sucursal, x.cajero
            ORDER BY x.fecha_dia, x.sucursal, x.cajero;
        """)).Select(r => new List<object>
        {
            DateOnlyText(r, "fecha_dia"), Text(r, "sucursal"), Val(r, "ventas_productos"),
            Val(r, "cobro_mesas"), Val(r, "total_ingreso"), Val(r, "propinas"), Text(r, "cajero")
        }));

        List<List<object>> resumenTurno = new()
        {
            new() { "fecha", "turno", "sucursal", "cajero", "dinero_vendido", "efectivo", "qr", "tarjeta", "transferencia", "productos_vendidos", "uso_y_cobro_mesas", "propinas", "ayuda_comida_empleados", "productos_usados_sin_venta", "productos_perdidos_o_danados", "dinero_neto_para_revisar", "observaciones" }
        };
        resumenTurno.AddRange((await db.QueryAsync(con, """
            SELECT
                x.fecha_dia,
                x.turno,
                x.sucursal,
                x.cajero,
                SUM(x.total) AS dinero_vendido,
                SUM(CASE WHEN UPPER(x.metodo_pago) = 'EFECTIVO' THEN x.total ELSE 0 END) AS efectivo,
                SUM(CASE WHEN UPPER(x.metodo_pago) = 'QR' THEN x.total ELSE 0 END) AS qr,
                SUM(CASE WHEN UPPER(x.metodo_pago) = 'TARJETA' THEN x.total ELSE 0 END) AS tarjeta,
                SUM(CASE WHEN UPPER(x.metodo_pago) = 'TRANSFERENCIA' THEN x.total ELSE 0 END) AS transferencia,
                SUM(x.productos) AS productos_vendidos,
                SUM(x.mesa) AS uso_y_cobro_mesas,
                COALESCE((
                    SELECT SUM(p.monto)
                    FROM propinas p
                    INNER JOIN sucursales sp ON sp.id = p.sucursal_id
                    WHERE DATE(p.fecha) = x.fecha_dia
                      AND (CASE WHEN HOUR(p.fecha) < 16 THEN 'MAÑANA' ELSE 'NOCHE' END) = x.turno
                      AND (CASE WHEN sp.id = 2 THEN 'SEGUNDA SUCURSAL' ELSE 'PRIMERA SUCURSAL' END) = x.sucursal
                      AND p.cajero = x.cajero
                ), 0) AS propinas
            FROM (
                SELECT
                    DATE(v.fecha) AS fecha_dia,
                    CASE WHEN HOUR(v.fecha) < 16 THEN 'MAÑANA' ELSE 'NOCHE' END AS turno,
                    CASE WHEN s.id = 2 THEN 'SEGUNDA SUCURSAL' ELSE 'PRIMERA SUCURSAL' END AS sucursal,
                    v.cajero,
                    v.metodo_pago,
                    v.tipo,
                    v.total,
                    COALESCE((SELECT SUM(d.subtotal) FROM detalle_ventas d WHERE d.venta_id = v.id), 0) AS productos,
                    CASE
                        WHEN v.tipo = 'MESA'
                        THEN GREATEST(v.total - COALESCE((SELECT SUM(d2.subtotal) FROM detalle_ventas d2 WHERE d2.venta_id = v.id), 0), 0)
                        ELSE 0
                    END AS mesa
                FROM ventas v
                INNER JOIN sucursales s ON s.id = v.sucursal_id
            ) x
            GROUP BY x.fecha_dia, x.turno, x.sucursal, x.cajero
            ORDER BY x.fecha_dia, x.turno, x.sucursal, x.cajero;
        """)).Select(r => new List<object>
        {
            DateOnlyText(r, "fecha_dia"), Text(r, "turno"), Text(r, "sucursal"), Text(r, "cajero"),
            Val(r, "dinero_vendido"), Val(r, "efectivo"), Val(r, "qr"), Val(r, "tarjeta"), Val(r, "transferencia"),
            Val(r, "productos_vendidos"), Val(r, "uso_y_cobro_mesas"), Val(r, "propinas"),
            0, 0, 0, Val(r, "dinero_vendido"), ""
        }));

        List<List<object>> dineroTurno = new()
        {
            new() { "fecha", "turno", "sucursal", "cajero", "efectivo", "qr", "tarjeta", "transferencia", "total_vendido" }
        };
        dineroTurno.AddRange(resumenTurno.Skip(1).Select(r => new List<object> { r[0], r[1], r[2], r[3], r[5], r[6], r[7], r[8], r[4] }));

        List<List<object>> ventasCadaCajero = new()
        {
            new() { "fecha", "sucursal", "cajero", "cantidad_operaciones", "total_vendido" }
        };
        ventasCadaCajero.AddRange((await db.QueryAsync(con, """
            SELECT DATE(v.fecha) AS fecha_dia,
                   CASE WHEN s.id = 2 THEN 'SEGUNDA SUCURSAL' ELSE 'PRIMERA SUCURSAL' END AS sucursal,
                   v.cajero,
                   COUNT(*) AS cantidad_operaciones,
                   SUM(v.total) AS total_vendido
            FROM ventas v
            INNER JOIN sucursales s ON s.id = v.sucursal_id
            GROUP BY DATE(v.fecha), s.id, v.cajero
            ORDER BY DATE(v.fecha), s.id, v.cajero;
        """)).Select(r => new List<object>
        {
            DateOnlyText(r, "fecha_dia"), Text(r, "sucursal"), Text(r, "cajero"),
            Val(r, "cantidad_operaciones"), Val(r, "total_vendido")
        }));

        List<List<object>> comoPagaron = new()
        {
            new() { "fecha", "sucursal", "metodo_pago", "cantidad", "total" }
        };
        comoPagaron.AddRange((await db.QueryAsync(con, """
            SELECT DATE(v.fecha) AS fecha_dia,
                   CASE WHEN s.id = 2 THEN 'SEGUNDA SUCURSAL' ELSE 'PRIMERA SUCURSAL' END AS sucursal,
                   v.metodo_pago,
                   COUNT(*) AS cantidad,
                   SUM(v.total) AS total
            FROM ventas v
            INNER JOIN sucursales s ON s.id = v.sucursal_id
            GROUP BY DATE(v.fecha), s.id, v.metodo_pago
            ORDER BY DATE(v.fecha), s.id, v.metodo_pago;
        """)).Select(r => new List<object>
        {
            DateOnlyText(r, "fecha_dia"), Text(r, "sucursal"), Text(r, "metodo_pago"),
            Val(r, "cantidad"), Val(r, "total")
        }));

        List<List<object>> gananciaNegocio = new()
        {
            new() { "fecha", "sucursal", "productos_vendidos", "uso_y_cobro_mesas", "total_ingreso", "ayuda_comida", "uso_interno", "perdidas", "neto_para_revisar" }
        };
        gananciaNegocio.AddRange(resumen.Select((r, idx) => idx == 0 ? null : new List<object> { r[0], r[1], r[2], r[3], r[4], 0, 0, 0, r[4] }).Where(r => r != null)!);

        await ReplaceSheetAsync(service, "Ventas", ventas);
        await ReplaceSheetAsync(service, "Detalle_Ventas", detalle);
        await ReplaceSheetAsync(service, "Cobros_Mesa", cobrosMesa);
        await ReplaceSheetAsync(service, "Stock", stock);
        await ReplaceSheetAsync(service, "Reservas", reservas);
        await ReplaceSheetAsync(service, "Propinas", propinas);
        await ReplaceSheetAsync(service, "Resumen_Diario", resumen);
        await ReplaceSheetAsync(service, "Resumen_Turno", resumenTurno);
        await ReplaceSheetAsync(service, "Dinero_Turno", dineroTurno);
        await ReplaceSheetAsync(service, "Ventas_Cada_Cajero", ventasCadaCajero);
        await ReplaceSheetAsync(service, "Como_Pagaron_Clientes", comoPagaron);
        await ReplaceSheetAsync(service, "Ganancia_Negocio", gananciaNegocio);

        await InitSheetIfEmptyAsync(service, "Empleados_Turnos", new List<object> { "empleado", "oficio", "sucursal", "turno", "hora_entrada", "hora_salida", "ayuda_comida", "estado", "observacion" });
        await InitSheetIfEmptyAsync(service, "Ayuda_Comida_Empleados", new List<object> { "fecha", "turno", "empleado", "oficio", "monto_comida", "autorizado_por", "observacion" });
        await InitSheetIfEmptyAsync(service, "Ingreso_Mercaderia", new List<object> { "fecha", "producto", "cantidad_que_entro", "unidad", "precio_compra", "total_compra", "registrado_por", "observacion" });
        await InitSheetIfEmptyAsync(service, "Productos_Usados_Sin_Venta", new List<object> { "fecha", "hora", "turno", "producto", "cantidad", "motivo", "para_quien_fue", "costo_aproximado", "autorizado_por", "observacion" });
        await InitSheetIfEmptyAsync(service, "Productos_Perdidos_Danados", new List<object> { "fecha", "hora", "producto", "cantidad", "que_paso", "costo_perdido", "registrado_por", "observacion" });
        await InitSheetIfEmptyAsync(service, "Historial_Productos", new List<object> { "fecha", "hora", "producto", "tipo_movimiento", "cantidad", "responsable", "observacion" });

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


public record MesaConsumoVivoRequest(
    string? Producto,
    string? Presentacion,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal Subtotal
);

public record MesaEstadoRequest(
    int SucursalId,
    int MesaId,
    string? Mesa,
    string? Estado,
    string? Cajero,
    DateTime? Inicio,
    DateTime? FinProgramado,
    int Minutos,
    decimal TotalMesa,
    decimal TotalConsumo,
    decimal TotalGeneral,
    string? ClienteReserva,
    string? SyncKey,
    List<MesaConsumoVivoRequest>? Detalle
);

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

public record CobroMesaRequest(
    int SucursalId,
    int? SessionId,
    int? MesaId,
    string? Mesa,
    string? Cajero,
    string? Mesera,
    DateTime Fecha,
    string? Tiempo,
    decimal TotalMesa,
    decimal TotalConsumo,
    decimal TotalCobrado,
    string? MetodoPago,
    string? SyncKey
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
