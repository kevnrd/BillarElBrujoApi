using System.Data;
using System.Text.Json;
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

var app = builder.Build();

app.UseCors("AllowDesktopApp");

app.MapGet("/", () => Results.Ok(new
{
    app = "BILLAR EL BRUJO API",
    status = "online",
    message = "API funcionando correctamente"
}));

app.MapGet("/health", async (Db db) =>
{
    try
    {
        await using var con = await db.OpenAsync();
        await using var cmd = new MySqlCommand("SELECT DATABASE();", con);
        var database = Convert.ToString(await cmd.ExecuteScalarAsync());
        return Results.Ok(new { ok = true, database });
    }
    catch (Exception ex)
    {
        return Results.Problem("No se pudo conectar a MySQL: " + ex.Message);
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
        sucursal = rd.IsDBNull("sucursal") ? "TODAS" : rd.GetString("sucursal")
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
    string sql = """
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
    string sql = """
        SELECT p.id, p.sucursal_id, s.nombre AS sucursal, p.nombre, p.categoria, p.unidad_base,
               p.stock_actual, p.stock_minimo, p.estado
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

app.MapPost("/api/productos", async (Db db, ProductoRequest p) =>
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
    return Results.Ok(new { ok = true, id });
});

app.MapPost("/api/ventas", async (Db db, VentaRequest venta) =>
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

        const string deleteDetalle = "DELETE FROM detalle_ventas WHERE venta_id = @venta_id;";
        await using (var del = new MySqlCommand(deleteDetalle, con, tx))
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

            const string stockSql = """
                UPDATE productos
                SET stock_actual = stock_actual - @cantidad_base
                WHERE id = @producto_id;
            """;

            await using var stockCmd = new MySqlCommand(stockSql, con, tx);
            stockCmd.Parameters.AddWithValue("@cantidad_base", d.CantidadBase);
            stockCmd.Parameters.AddWithValue("@producto_id", d.ProductoId);
            await stockCmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
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
    string sql = """
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

app.MapPost("/api/reservas", async (Db db, ReservaRequest r) =>
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
    cmd.Parameters.AddWithValue("@celular", r.Celular);
    cmd.Parameters.AddWithValue("@fecha_reserva", r.FechaReserva);
    cmd.Parameters.AddWithValue("@minutos", r.Minutos);
    cmd.Parameters.AddWithValue("@estado", r.Estado);
    cmd.Parameters.AddWithValue("@cajero", r.Cajero);
    cmd.Parameters.AddWithValue("@sync_key", syncKey);

    await cmd.ExecuteNonQueryAsync();
    return Results.Ok(new { ok = true, syncKey });
});

app.MapPost("/api/propinas", async (Db db, PropinaRequest p) =>
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
    cmd.Parameters.AddWithValue("@mesa_id", p.MesaId);
    cmd.Parameters.AddWithValue("@mesera", p.Mesera);
    cmd.Parameters.AddWithValue("@cajero", p.Cajero);
    cmd.Parameters.AddWithValue("@fecha", p.Fecha);
    cmd.Parameters.AddWithValue("@monto", p.Monto);
    cmd.Parameters.AddWithValue("@sync_key", syncKey);

    await cmd.ExecuteNonQueryAsync();
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
                cmd.Parameters.AddWithValue(p.Key, p.Value ?? DBNull.Value);
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
        string? full = Environment.GetEnvironmentVariable("MYSQL_URL");

        if (!string.IsNullOrWhiteSpace(full))
        {
            // Railway MYSQL_URL normalmente viene como:
            // mysql://root:password@mysql.railway.internal:3306/railway
            var uri = new Uri(full);
            string[] userInfo = uri.UserInfo.Split(':', 2);
            string user = Uri.UnescapeDataString(userInfo[0]);
            string password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
            string database = uri.AbsolutePath.TrimStart('/');

            return $"Server={uri.Host};Port={uri.Port};Database={database};Uid={user};Pwd={password};SslMode=Preferred;";
        }

        string host = Environment.GetEnvironmentVariable("MYSQLHOST")
            ?? configuration["MYSQLHOST"]
            ?? "localhost";

        string port = Environment.GetEnvironmentVariable("MYSQLPORT")
            ?? configuration["MYSQLPORT"]
            ?? "3306";

        string databaseName = Environment.GetEnvironmentVariable("MYSQLDATABASE")
            ?? Environment.GetEnvironmentVariable("MYSQL_DATABASE")
            ?? configuration["MYSQLDATABASE"]
            ?? configuration["MYSQL_DATABASE"]
            ?? "railway";

        string user = Environment.GetEnvironmentVariable("MYSQLUSER")
            ?? configuration["MYSQLUSER"]
            ?? "root";

        string password = Environment.GetEnvironmentVariable("MYSQLPASSWORD")
            ?? configuration["MYSQLPASSWORD"]
            ?? "";

        return $"Server={host};Port={port};Database={databaseName};Uid={user};Pwd={password};SslMode=Preferred;";
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
