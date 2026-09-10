using System.Text;
using System.Security.Cryptography;
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
            version = "V35_ARQUEO_CIERRE_ADMIN",
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

app.MapPost("/api/admin/limpiar-pruebas", async (Db db, SheetsReporter sheets, string clave, bool? syncSheets) =>
{
    const string cleanKey = "ENTREGAR_LIMPIO_2026";

    if (clave != cleanKey)
        return Results.Unauthorized();

    string[] tables =
    {
        "detalle_ventas",
        "ventas",
        "cobros_mesa",
        "propinas",
        "reservas",
        "mesa_consumos_vivos",
        "mesa_estados",
        "detalle_pedidos_movil",
        "pedidos_movil",
        "comisiones_meseras",
        "reportes_productos_movil",
        "cierres_turno"
    };

    List<string> cleaned = new();
    List<string> warnings = new();

    await using var con = await db.OpenAsync();

    await using (var fkOff = new MySqlCommand("SET FOREIGN_KEY_CHECKS = 0;", con))
        await fkOff.ExecuteNonQueryAsync();

    foreach (string table in tables)
    {
        try
        {
            await using var cmd = new MySqlCommand("TRUNCATE TABLE " + table + ";", con);
            await cmd.ExecuteNonQueryAsync();
            cleaned.Add(table);
        }
        catch (Exception ex)
        {
            warnings.Add(table + ": " + ex.Message);
        }
    }

    await using (var fkOn = new MySqlCommand("SET FOREIGN_KEY_CHECKS = 1;", con))
        await fkOn.ExecuteNonQueryAsync();

    string sheetsMessage = "Google Sheets no sincronizado.";
    if (syncSheets == true && sheets.IsConfigured)
    {
        try
        {
            sheetsMessage = await sheets.SyncFromDatabaseAsync(db);
        }
        catch (Exception ex)
        {
            sheetsMessage = "Railway quedó limpio, pero Google Sheets no se pudo actualizar ahora: " + ex.Message;
        }
    }

    return Results.Ok(new
    {
        ok = true,
        message = "Datos de prueba limpiados para entregar al cliente.",
        cleaned,
        warnings,
        googleSheets = sheetsMessage,
        note = "No se borraron usuarios, sucursales, mesas ni productos."
    });
});

app.MapGet("/api/admin/limpiar-pruebas", async (Db db, SheetsReporter sheets, string clave, bool? syncSheets) =>
{
    const string cleanKey = "ENTREGAR_LIMPIO_2026";

    if (clave != cleanKey)
        return Results.Unauthorized();

    string[] tables =
    {
        "detalle_ventas",
        "ventas",
        "cobros_mesa",
        "propinas",
        "reservas",
        "mesa_consumos_vivos",
        "mesa_estados",
        "detalle_pedidos_movil",
        "pedidos_movil",
        "comisiones_meseras",
        "reportes_productos_movil",
        "cierres_turno"
    };

    List<string> cleaned = new();
    List<string> warnings = new();

    await using var con = await db.OpenAsync();

    await using (var fkOff = new MySqlCommand("SET FOREIGN_KEY_CHECKS = 0;", con))
        await fkOff.ExecuteNonQueryAsync();

    foreach (string table in tables)
    {
        try
        {
            await using var cmd = new MySqlCommand("TRUNCATE TABLE " + table + ";", con);
            await cmd.ExecuteNonQueryAsync();
            cleaned.Add(table);
        }
        catch (Exception ex)
        {
            warnings.Add(table + ": " + ex.Message);
        }
    }

    await using (var fkOn = new MySqlCommand("SET FOREIGN_KEY_CHECKS = 1;", con))
        await fkOn.ExecuteNonQueryAsync();

    string sheetsMessage = "Google Sheets no sincronizado.";
    if (syncSheets == true && sheets.IsConfigured)
    {
        try
        {
            sheetsMessage = await sheets.SyncFromDatabaseAsync(db);
        }
        catch (Exception ex)
        {
            sheetsMessage = "Railway quedó limpio, pero Google Sheets no se pudo actualizar ahora: " + ex.Message;
        }
    }

    return Results.Ok(new
    {
        ok = true,
        message = "Datos de prueba limpiados para entregar al cliente.",
        cleaned,
        warnings,
        googleSheets = sheetsMessage,
        note = "No se borraron usuarios, sucursales, mesas ni productos."
    });
});

app.MapPost("/api/login", async (Db db, LoginRequest req) =>
{
    await using var con = await db.OpenAsync();
    await EnsureUserManagementTables(con);

    string usuario = (req.Usuario ?? "").Trim().ToLowerInvariant();
    string claveIngresada = req.Clave ?? "";

    int id = 0;
    string usuarioDb = "";
    string rol = "";
    int sucursalId = 1;
    string sucursal = "PRIMERA SUCURSAL";
    string nombre = "";
    string caja = "";
    string turno = "MAÑANA";
    string claveGuardada = "";

    const string sql = """
        SELECT u.id, u.usuario, u.clave, u.rol, u.estado, u.sucursal_id,
               COALESCE(u.nombre_completo, u.usuario) AS nombre_completo,
               COALESCE(u.caja_nombre, '') AS caja_nombre,
               COALESCE(u.turno, 'MAÑANA') AS turno,
               CASE WHEN s.id = 2 THEN 'SEGUNDA SUCURSAL' ELSE 'PRIMERA SUCURSAL' END AS sucursal
        FROM usuarios u
        LEFT JOIN sucursales s ON s.id = u.sucursal_id
        WHERE u.usuario = @usuario AND u.estado = 'ACTIVO'
        LIMIT 1;
    """;

    await using (var cmd = new MySqlCommand(sql, con))
    {
        cmd.Parameters.AddWithValue("@usuario", usuario);
        await using var rd = await cmd.ExecuteReaderAsync();
        if (!await rd.ReadAsync())
            return Results.Unauthorized();

        id = rd.GetInt32("id");
        usuarioDb = rd.GetString("usuario");
        claveGuardada = rd.GetString("clave");
        rol = rd.GetString("rol");
        sucursalId = rd.IsDBNull(rd.GetOrdinal("sucursal_id")) ? 1 : rd.GetInt32("sucursal_id");
        sucursal = rd.IsDBNull(rd.GetOrdinal("sucursal")) ? "TODAS" : rd.GetString("sucursal");
        nombre = rd.IsDBNull(rd.GetOrdinal("nombre_completo")) ? usuarioDb : rd.GetString("nombre_completo");
        caja = rd.IsDBNull(rd.GetOrdinal("caja_nombre")) ? "" : rd.GetString("caja_nombre");
        turno = rd.IsDBNull(rd.GetOrdinal("turno")) ? "MAÑANA" : rd.GetString("turno");
    }

    if (!PasswordHasher.Verify(claveIngresada, claveGuardada))
        return Results.Unauthorized();

    if (!PasswordHasher.IsHashed(claveGuardada))
        await UpdateUserPasswordHash(con, id, claveIngresada);

    return Results.Ok(new
    {
        id,
        usuario = usuarioDb,
        rol,
        sucursal,
        nombre,
        caja,
        turno,
        sucursal_id = sucursalId
    });
});

app.MapGet("/api/admin/usuarios", async (Db db, string clave) =>
{
    const string cleanKey = "ENTREGAR_LIMPIO_2026";
    if (clave != cleanKey) return Results.Unauthorized();

    await using var con = await db.OpenAsync();
    await EnsureUserManagementTables(con);

    const string sql = """
        SELECT u.id,
               u.usuario,
               COALESCE(u.nombre_completo, u.usuario) AS nombre_completo,
               u.rol,
               u.sucursal_id,
               CASE WHEN s.id = 2 THEN 'SEGUNDA SUCURSAL' ELSE 'PRIMERA SUCURSAL' END AS sucursal,
               COALESCE(u.caja_nombre, '') AS caja_nombre,
               COALESCE(u.turno, 'MAÑANA') AS turno,
               u.estado
        FROM usuarios u
        LEFT JOIN sucursales s ON s.id = u.sucursal_id
        ORDER BY u.rol, u.sucursal_id, u.usuario;
    """;

    return Results.Ok(await db.QueryAsync(con, sql, new Dictionary<string, object?>()));
});

app.MapPost("/api/admin/usuarios", async (Db db, string clave, AdminUserRequest req) =>
{
    const string cleanKey = "ENTREGAR_LIMPIO_2026";
    if (clave != cleanKey) return Results.Unauthorized();

    await using var con = await db.OpenAsync();
    await EnsureUserManagementTables(con);

    string usuario = (req.Usuario ?? "").Trim().ToLowerInvariant();
    string pass = (req.Clave ?? "").Trim();
    string rol = NormalizarRol(req.Rol);
    int sucursalId = req.SucursalId <= 0 ? 1 : req.SucursalId;
    string estado = string.IsNullOrWhiteSpace(req.Estado) ? "ACTIVO" : req.Estado.Trim().ToUpperInvariant();
    string turno = NormalizarTurno(req.Turno);

    if (string.IsNullOrWhiteSpace(usuario))
        return Results.BadRequest(new { ok = false, message = "Usuario requerido." });

    string passHash;
    if (string.IsNullOrWhiteSpace(pass))
    {
        object? actual = null;
        await using (var getPass = new MySqlCommand("SELECT clave FROM usuarios WHERE usuario = @usuario LIMIT 1;", con))
        {
            getPass.Parameters.AddWithValue("@usuario", usuario);
            actual = await getPass.ExecuteScalarAsync();
        }

        passHash = actual == null ? PasswordHasher.Hash("123456") : Convert.ToString(actual) ?? PasswordHasher.Hash("123456");
        if (!PasswordHasher.IsHashed(passHash))
            passHash = PasswordHasher.Hash(passHash);
    }
    else
    {
        passHash = PasswordHasher.Hash(pass);
    }

    await using var cmd = new MySqlCommand("""
        INSERT INTO usuarios
            (usuario, clave, rol, sucursal_id, estado, nombre_completo, caja_nombre, turno)
        VALUES
            (@usuario, @clave, @rol, @sucursal_id, @estado, @nombre_completo, @caja_nombre, @turno)
        ON DUPLICATE KEY UPDATE
            clave = VALUES(clave),
            rol = VALUES(rol),
            sucursal_id = VALUES(sucursal_id),
            estado = VALUES(estado),
            nombre_completo = VALUES(nombre_completo),
            caja_nombre = VALUES(caja_nombre),
            turno = VALUES(turno);
    """, con);

    cmd.Parameters.AddWithValue("@usuario", usuario);
    cmd.Parameters.AddWithValue("@clave", passHash);
    cmd.Parameters.AddWithValue("@rol", rol);
    cmd.Parameters.AddWithValue("@sucursal_id", sucursalId);
    cmd.Parameters.AddWithValue("@estado", estado);
    cmd.Parameters.AddWithValue("@nombre_completo", string.IsNullOrWhiteSpace(req.NombreCompleto) ? usuario : req.NombreCompleto.Trim());
    cmd.Parameters.AddWithValue("@caja_nombre", req.CajaNombre ?? "");
    cmd.Parameters.AddWithValue("@turno", turno);
    await cmd.ExecuteNonQueryAsync();

    return Results.Ok(new
    {
        ok = true,
        usuario,
        rol,
        sucursal_id = sucursalId,
        turno,
        estado,
        message = "Usuario guardado."
    });
});

app.MapPost("/api/admin/usuarios/{id:int}/estado", async (Db db, string clave, int id, UserEstadoRequest req) =>
{
    const string cleanKey = "ENTREGAR_LIMPIO_2026";
    if (clave != cleanKey) return Results.Unauthorized();

    await using var con = await db.OpenAsync();
    await EnsureUserManagementTables(con);

    string estado = string.IsNullOrWhiteSpace(req.Estado) ? "INACTIVO" : req.Estado.Trim().ToUpperInvariant();
    if (estado != "ACTIVO" && estado != "INACTIVO")
        return Results.BadRequest(new { ok = false, message = "Estado inválido." });

    await using var cmd = new MySqlCommand("UPDATE usuarios SET estado = @estado WHERE id = @id;", con);
    cmd.Parameters.AddWithValue("@estado", estado);
    cmd.Parameters.AddWithValue("@id", id);
    int rows = await cmd.ExecuteNonQueryAsync();

    return Results.Ok(new { ok = rows > 0, id, estado });
});



app.MapPost("/api/admin/productos/comision", async (Db db, string clave, ProductCommissionRequest req) =>
{
    const string cleanKey = "ENTREGAR_LIMPIO_2026";
    if (clave != cleanKey) return Results.Unauthorized();

    await using var con = await db.OpenAsync();
    await EnsureAppMeseraTables(con);

    string nombre = (req.Nombre ?? "").Trim();
    string tipo = (req.TipoComision ?? "NINGUNA").Trim().ToUpperInvariant();
    bool genera = req.GeneraComision && req.ValorComision > 0;

    if (string.IsNullOrWhiteSpace(nombre))
        return Results.BadRequest(new { ok = false, message = "Nombre del producto requerido." });

    if (!genera)
    {
        tipo = "NINGUNA";
    }
    else if (tipo != "PORCENTAJE" && tipo != "MONTO")
    {
        return Results.BadRequest(new { ok = false, message = "Tipo de comisión inválido. Use PORCENTAJE o MONTO." });
    }

    decimal valor = genera ? req.ValorComision : 0;

    const string sql = """
        UPDATE productos
        SET genera_comision = @genera_comision,
            tipo_comision = @tipo_comision,
            valor_comision = @valor_comision
        WHERE sucursal_id = @sucursal_id
          AND LOWER(nombre) = LOWER(@nombre);
    """;

    await using var cmd = new MySqlCommand(sql, con);
    cmd.Parameters.AddWithValue("@genera_comision", genera ? 1 : 0);
    cmd.Parameters.AddWithValue("@tipo_comision", tipo);
    cmd.Parameters.AddWithValue("@valor_comision", valor);
    cmd.Parameters.AddWithValue("@sucursal_id", req.SucursalId <= 0 ? 1 : req.SucursalId);
    cmd.Parameters.AddWithValue("@nombre", nombre);

    int rows = await cmd.ExecuteNonQueryAsync();
    if (rows <= 0)
        return Results.NotFound(new { ok = false, message = "Producto no encontrado en esa sucursal." });

    return Results.Ok(new
    {
        ok = true,
        producto = nombre,
        sucursal_id = req.SucursalId <= 0 ? 1 : req.SucursalId,
        genera_comision = genera,
        tipo_comision = tipo,
        valor_comision = valor
    });
});

app.MapGet("/api/admin/productos/comision", async (Db db, string clave, int sucursalId) =>
{
    const string cleanKey = "ENTREGAR_LIMPIO_2026";
    if (clave != cleanKey) return Results.Unauthorized();

    await using var con = await db.OpenAsync();
    await EnsureAppMeseraTables(con);

    const string sql = """
        SELECT id, sucursal_id, nombre, categoria,
               COALESCE(genera_comision, 0) AS genera_comision,
               COALESCE(tipo_comision, 'NINGUNA') AS tipo_comision,
               COALESCE(valor_comision, 0) AS valor_comision
        FROM productos
        WHERE sucursal_id = @sucursal_id
          AND estado = 'ACTIVO'
        ORDER BY categoria, nombre;
    """;

    return Results.Ok(await db.QueryAsync(con, sql, new Dictionary<string, object?>
    {
        ["@sucursal_id"] = sucursalId <= 0 ? 1 : sucursalId
    }));
});


app.MapPost("/api/admin/productos/guardar", async (Db db, SheetsReporter sheets, string clave, AdminProductRequest req) =>
{
    const string cleanKey = "ENTREGAR_LIMPIO_2026";
    if (clave != cleanKey) return Results.Unauthorized();

    int sucursalId = req.SucursalId == 2 ? 2 : 1;
    string nombre = (req.Nombre ?? "").Trim();
    if (string.IsNullOrWhiteSpace(nombre))
        return Results.BadRequest(new { ok = false, message = "Nombre del producto requerido." });

    string categoria = NormalizarCategoriaProducto(req.Categoria, nombre);
    string unidadBase = string.IsNullOrWhiteSpace(req.UnidadBase) ? "UNIDAD" : req.UnidadBase.Trim();
    string tipoEntrada = string.IsNullOrWhiteSpace(req.TipoEntrada) ? "PAQUETE" : req.TipoEntrada.Trim();
    int unidadesPorEntrada = req.UnidadesPorEntrada <= 0 ? 1 : req.UnidadesPorEntrada;
    decimal stockActual = Math.Max(0, req.StockActual);
    decimal stockMinimo = Math.Max(0, req.StockMinimo);
    decimal precioCompra = Math.Max(0, req.PrecioCompra);
    string estado = string.Equals(req.Estado, "INACTIVO", StringComparison.OrdinalIgnoreCase) ? "INACTIVO" : "ACTIVO";
    string tipoComision = req.GeneraComision ? (req.TipoComision ?? "PORCENTAJE").Trim().ToUpperInvariant() : "NINGUNA";
    decimal valorComision = req.GeneraComision ? Math.Max(0, req.ValorComision) : 0;

    await using var con = await db.OpenAsync();
    await EnsureAppMeseraTables(con);
    await using var tx = await con.BeginTransactionAsync();

    try
    {
        long productoId = 0;

        // Si la PC ya conoce el ID online, se usa primero. Esto permite renombrar
        // un producto sin crear un duplicado en Railway.
        if (req.ProductoId.GetValueOrDefault() > 0)
        {
            await using var buscarId = new MySqlCommand("SELECT id FROM productos WHERE id = @id AND sucursal_id = @sucursal_id LIMIT 1;", con, tx);
            buscarId.Parameters.AddWithValue("@id", req.ProductoId!.Value);
            buscarId.Parameters.AddWithValue("@sucursal_id", sucursalId);
            object? foundId = await buscarId.ExecuteScalarAsync();
            if (foundId != null) productoId = Convert.ToInt64(foundId);
        }

        if (productoId <= 0)
        {
            await using var buscar = new MySqlCommand("SELECT id FROM productos WHERE sucursal_id = @sucursal_id AND LOWER(TRIM(nombre)) = LOWER(TRIM(@nombre)) LIMIT 1;", con, tx);
            buscar.Parameters.AddWithValue("@sucursal_id", sucursalId);
            buscar.Parameters.AddWithValue("@nombre", nombre);
            object? found = await buscar.ExecuteScalarAsync();
            if (found != null) productoId = Convert.ToInt64(found);
        }

        if (productoId <= 0)
        {
            await using var cmd = new MySqlCommand("""
                INSERT INTO productos
                    (sucursal_id, nombre, categoria, unidad_base, stock_actual, stock_minimo, estado,
                     genera_comision, tipo_comision, valor_comision, sin_limite_stock,
                     tipo_entrada, unidades_por_entrada, precio_compra)
                VALUES
                    (@sucursal_id, @nombre, @categoria, @unidad_base, @stock_actual, @stock_minimo, @estado,
                     @genera_comision, @tipo_comision, @valor_comision, @sin_limite_stock,
                     @tipo_entrada, @unidades_por_entrada, @precio_compra);
                SELECT LAST_INSERT_ID();
            """, con, tx);
            cmd.Parameters.AddWithValue("@sucursal_id", sucursalId);
            cmd.Parameters.AddWithValue("@nombre", nombre);
            cmd.Parameters.AddWithValue("@categoria", categoria);
            cmd.Parameters.AddWithValue("@unidad_base", unidadBase);
            cmd.Parameters.AddWithValue("@stock_actual", stockActual);
            cmd.Parameters.AddWithValue("@stock_minimo", stockMinimo);
            cmd.Parameters.AddWithValue("@estado", estado);
            cmd.Parameters.AddWithValue("@genera_comision", req.GeneraComision ? 1 : 0);
            cmd.Parameters.AddWithValue("@tipo_comision", tipoComision);
            cmd.Parameters.AddWithValue("@valor_comision", valorComision);
            cmd.Parameters.AddWithValue("@sin_limite_stock", req.SinLimiteStock ? 1 : 0);
            cmd.Parameters.AddWithValue("@tipo_entrada", tipoEntrada);
            cmd.Parameters.AddWithValue("@unidades_por_entrada", unidadesPorEntrada);
            cmd.Parameters.AddWithValue("@precio_compra", precioCompra);
            productoId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }
        else
        {
            await using var cmd = new MySqlCommand("""
                UPDATE productos
                SET nombre = @nombre,
                    categoria = @categoria,
                    unidad_base = @unidad_base,
                    stock_actual = @stock_actual,
                    stock_minimo = @stock_minimo,
                    estado = @estado,
                    genera_comision = @genera_comision,
                    tipo_comision = @tipo_comision,
                    valor_comision = @valor_comision,
                    sin_limite_stock = @sin_limite_stock,
                    tipo_entrada = @tipo_entrada,
                    unidades_por_entrada = @unidades_por_entrada,
                    precio_compra = @precio_compra
                WHERE id = @id;
            """, con, tx);
            cmd.Parameters.AddWithValue("@nombre", nombre);
            cmd.Parameters.AddWithValue("@categoria", categoria);
            cmd.Parameters.AddWithValue("@unidad_base", unidadBase);
            cmd.Parameters.AddWithValue("@stock_actual", stockActual);
            cmd.Parameters.AddWithValue("@stock_minimo", stockMinimo);
            cmd.Parameters.AddWithValue("@estado", estado);
            cmd.Parameters.AddWithValue("@genera_comision", req.GeneraComision ? 1 : 0);
            cmd.Parameters.AddWithValue("@tipo_comision", tipoComision);
            cmd.Parameters.AddWithValue("@valor_comision", valorComision);
            cmd.Parameters.AddWithValue("@sin_limite_stock", req.SinLimiteStock ? 1 : 0);
            cmd.Parameters.AddWithValue("@tipo_entrada", tipoEntrada);
            cmd.Parameters.AddWithValue("@unidades_por_entrada", unidadesPorEntrada);
            cmd.Parameters.AddWithValue("@precio_compra", precioCompra);
            cmd.Parameters.AddWithValue("@id", productoId);
            await cmd.ExecuteNonQueryAsync();
        }

        var presentacionesGuardadas = new List<object>();

        foreach (var pres in req.Presentaciones ?? new List<AdminProductPresentationRequest>())
        {
            string presNombre = string.IsNullOrWhiteSpace(pres.Nombre) ? unidadBase : pres.Nombre.Trim();
            int cantidadBase = pres.CantidadBase <= 0 ? 1 : pres.CantidadBase;
            decimal precioVenta = Math.Max(0, pres.PrecioVenta);
            string presEstado = string.Equals(pres.Estado, "INACTIVO", StringComparison.OrdinalIgnoreCase) ? "INACTIVO" : "ACTIVO";
            long presId = 0;

            if (pres.PresentacionId.GetValueOrDefault() > 0)
            {
                await using var buscarPresId = new MySqlCommand("SELECT id FROM presentaciones WHERE id = @id AND producto_id = @producto_id LIMIT 1;", con, tx);
                buscarPresId.Parameters.AddWithValue("@id", pres.PresentacionId!.Value);
                buscarPresId.Parameters.AddWithValue("@producto_id", productoId);
                object? foundPresId = await buscarPresId.ExecuteScalarAsync();
                if (foundPresId != null) presId = Convert.ToInt64(foundPresId);
            }

            if (presId <= 0)
            {
                await using var buscarPres = new MySqlCommand("SELECT id FROM presentaciones WHERE producto_id = @producto_id AND LOWER(TRIM(nombre)) = LOWER(TRIM(@nombre)) LIMIT 1;", con, tx);
                buscarPres.Parameters.AddWithValue("@producto_id", productoId);
                buscarPres.Parameters.AddWithValue("@nombre", presNombre);
                object? foundPres = await buscarPres.ExecuteScalarAsync();
                if (foundPres != null) presId = Convert.ToInt64(foundPres);
            }

            if (presId <= 0)
            {
                await using var cmd = new MySqlCommand("""
                    INSERT INTO presentaciones (producto_id, nombre, cantidad_base, precio_venta, estado)
                    VALUES (@producto_id, @nombre, @cantidad_base, @precio_venta, @estado);
                """, con, tx);
                cmd.Parameters.AddWithValue("@producto_id", productoId);
                cmd.Parameters.AddWithValue("@nombre", presNombre);
                cmd.Parameters.AddWithValue("@cantidad_base", cantidadBase);
                cmd.Parameters.AddWithValue("@precio_venta", precioVenta);
                cmd.Parameters.AddWithValue("@estado", presEstado);
                await cmd.ExecuteNonQueryAsync();
                presId = cmd.LastInsertedId;
            }
            else
            {
                await using var cmd = new MySqlCommand("""
                    UPDATE presentaciones SET nombre = @nombre, cantidad_base = @cantidad_base, precio_venta = @precio_venta, estado = @estado WHERE id = @id;
                """, con, tx);
                cmd.Parameters.AddWithValue("@nombre", presNombre);
                cmd.Parameters.AddWithValue("@cantidad_base", cantidadBase);
                cmd.Parameters.AddWithValue("@precio_venta", precioVenta);
                cmd.Parameters.AddWithValue("@estado", presEstado);
                cmd.Parameters.AddWithValue("@id", presId);
                await cmd.ExecuteNonQueryAsync();
            }

            presentacionesGuardadas.Add(new { id = presId, nombre = presNombre });
        }

        await tx.CommitAsync();
        await TrySyncSheets(db, sheets);
        return Results.Ok(new { ok = true, id = productoId, sucursalId, nombre, categoria, presentaciones = presentacionesGuardadas, message = "Producto guardado y sincronizado." });
    }
    catch (Exception ex)
    {
        await tx.RollbackAsync();
        return Results.Problem("No se pudo guardar el producto: " + ex.Message);
    }
});

app.MapGet("/api/admin/productos/detalle", async (Db db, string clave, int sucursalId) =>
{
    const string cleanKey = "ENTREGAR_LIMPIO_2026";
    if (clave != cleanKey) return Results.Unauthorized();

    await using var con = await db.OpenAsync();
    await EnsureAppMeseraTables(con);

    const string sql = """
        SELECT p.id AS producto_id, p.sucursal_id, p.nombre, p.categoria, p.unidad_base,
               p.stock_actual, p.stock_minimo, COALESCE(p.sin_limite_stock,0) AS sin_limite_stock,
               COALESCE(p.tipo_entrada,'PAQUETE') AS tipo_entrada,
               COALESCE(p.unidades_por_entrada,1) AS unidades_por_entrada,
               COALESCE(p.precio_compra,0) AS precio_compra,
               COALESCE(p.genera_comision,0) AS genera_comision,
               COALESCE(p.tipo_comision,'NINGUNA') AS tipo_comision,
               COALESCE(p.valor_comision,0) AS valor_comision,
               p.estado,
               pr.id AS presentacion_id, pr.nombre AS presentacion,
               pr.cantidad_base, pr.precio_venta, pr.estado AS presentacion_estado
        FROM productos p
        LEFT JOIN presentaciones pr ON pr.producto_id = p.id
        WHERE p.sucursal_id = @sucursal_id
        ORDER BY p.categoria, p.nombre, pr.cantidad_base;
    """;

    return Results.Ok(await db.QueryAsync(con, sql, new Dictionary<string, object?>
    {
        ["@sucursal_id"] = sucursalId == 2 ? 2 : 1
    }));
});

app.MapPost("/api/app-mesera/login", async (Db db, LoginRequest req) =>
{
    await using var con = await db.OpenAsync();
    await EnsureUserManagementTables(con);
    await EnsureAppMeseraTables(con);

    string usuario = (req.Usuario ?? "").Trim().ToLowerInvariant();
    string claveIngresada = req.Clave ?? "";

    int id = 0;
    int sucursalId = 1;
    string usuarioDb = "";
    string rol = "";
    string sucursal = "PRIMERA SUCURSAL";
    string nombre = "";
    string turno = "MAÑANA";
    string claveGuardada = "";

    const string sql = """
        SELECT u.id, u.usuario, u.clave, u.rol, u.estado, u.sucursal_id,
               COALESCE(u.nombre_completo, u.usuario) AS nombre_completo,
               COALESCE(u.caja_nombre, '') AS caja_nombre,
               COALESCE(u.turno, 'MAÑANA') AS turno,
               CASE WHEN s.id = 2 THEN 'SEGUNDA SUCURSAL' ELSE 'PRIMERA SUCURSAL' END AS sucursal
        FROM usuarios u
        LEFT JOIN sucursales s ON s.id = u.sucursal_id
        WHERE u.usuario = @usuario AND u.estado = 'ACTIVO'
        LIMIT 1;
    """;

    await using (var cmd = new MySqlCommand(sql, con))
    {
        cmd.Parameters.AddWithValue("@usuario", usuario);
        await using var rd = await cmd.ExecuteReaderAsync();
        if (!await rd.ReadAsync())
            return Results.Unauthorized();

        id = rd.GetInt32("id");
        usuarioDb = rd.GetString("usuario");
        claveGuardada = rd.GetString("clave");
        rol = rd.GetString("rol");
        sucursalId = rd.IsDBNull(rd.GetOrdinal("sucursal_id")) ? 1 : rd.GetInt32("sucursal_id");
        sucursal = rd.IsDBNull(rd.GetOrdinal("sucursal")) ? "PRIMERA SUCURSAL" : rd.GetString("sucursal");
        nombre = rd.IsDBNull(rd.GetOrdinal("nombre_completo")) ? usuarioDb : rd.GetString("nombre_completo");
        turno = rd.IsDBNull(rd.GetOrdinal("turno")) ? "MAÑANA" : rd.GetString("turno");
    }

    if (!PasswordHasher.Verify(claveIngresada, claveGuardada))
        return Results.Unauthorized();

    if (!rol.Contains("MESERA", StringComparison.OrdinalIgnoreCase) &&
        !rol.Contains("MESERO", StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new { ok = false, message = "Este usuario no tiene rol de mesera." });
    }

    if (!PasswordHasher.IsHashed(claveGuardada))
        await UpdateUserPasswordHash(con, id, claveIngresada);

    return Results.Ok(new
    {
        ok = true,
        id,
        usuario = usuarioDb,
        nombre,
        rol,
        turno,
        sucursal_id = sucursalId,
        sucursal
    });
});

app.MapGet("/api/app-mesera/mesas", async (Db db, int sucursalId) =>
{
    await using var con = await db.OpenAsync();
    await EnsureAppMeseraTables(con);
    await EnsureMesasEnVivoTables(con);

    const string sql = """
        SELECT m.id AS mesa_id,
               m.nombre AS mesa,
               COALESCE(me.estado, m.estado, 'LIBRE') AS estado,
               COALESCE(me.total_consumo, 0) AS total_consumo,
               me.fin_programado,
               me.cajero
        FROM mesas m
        LEFT JOIN mesa_estados me
            ON me.sucursal_id = m.sucursal_id AND me.mesa_id = m.id
        WHERE m.sucursal_id = @sucursalId
          AND m.estado <> 'INACTIVA'
        ORDER BY m.id;
    """;

    var rows = await db.QueryAsync(con, sql, new Dictionary<string, object?>
    {
        ["@sucursalId"] = sucursalId
    });

    return Results.Ok(rows);
});

app.MapGet("/api/app-mesera/productos", async (Db db, int sucursalId) =>
{
    await using var con = await db.OpenAsync();
    await EnsureAppMeseraTables(con);

    const string sql = """
        SELECT p.id AS producto_id,
               p.nombre AS producto,
               p.categoria,
               p.stock_actual,
               pr.id AS presentacion_id,
               COALESCE(pr.nombre, 'UNIDAD') AS presentacion,
               COALESCE(pr.precio_venta, 0) AS precio,
               COALESCE(p.genera_comision, 0) AS genera_comision,
               COALESCE(p.tipo_comision, 'NINGUNA') AS tipo_comision,
               COALESCE(p.valor_comision, 0) AS valor_comision
        FROM productos p
        LEFT JOIN presentaciones pr ON pr.producto_id = p.id AND pr.estado = 'ACTIVO'
        WHERE p.sucursal_id = @sucursalId
          AND p.estado = 'ACTIVO'
        ORDER BY
            CASE
                WHEN p.categoria = 'Agua' THEN 1
                WHEN p.categoria = 'Energizantes' THEN 2
                WHEN p.categoria = 'Sodas' THEN 3
                WHEN p.categoria IN ('Cocas', 'Coca machucada') THEN 4
                WHEN p.categoria = 'Cervezas' THEN 5
                WHEN p.categoria = 'Tragos / Botellas' THEN 6
                WHEN p.categoria = 'Servidos en vaso' THEN 7
                WHEN p.categoria = 'Cigarros' THEN 8
                WHEN p.categoria = 'Snacks y piqueos' THEN 9
                WHEN p.categoria = 'Dulces y golosinas' THEN 10
                WHEN p.categoria = 'Combos / Promos' THEN 11
                WHEN p.categoria = 'Accesorios' THEN 12
                WHEN p.categoria = 'Ceniceros' THEN 13
                WHEN p.categoria IN ('Otros', 'Otros / Extras', 'Varios') THEN 14
                ELSE 99
            END,
            p.nombre, pr.nombre;
    """;

    var rows = await db.QueryAsync(con, sql, new Dictionary<string, object?>
    {
        ["@sucursalId"] = sucursalId
    });

    return Results.Ok(rows);
});

app.MapGet("/api/app-mesera/test", async (Db db, int sucursalId) =>
{
    await using var con = await db.OpenAsync();
    await EnsureAppMeseraTables(con);
    await EnsureMesasEnVivoTables(con);

    long productos = Convert.ToInt64(await new MySqlCommand("SELECT COUNT(*) FROM productos WHERE sucursal_id = " + sucursalId + " AND estado = 'ACTIVO';", con).ExecuteScalarAsync() ?? 0);
    long presentaciones = Convert.ToInt64(await new MySqlCommand("SELECT COUNT(*) FROM presentaciones pr INNER JOIN productos p ON p.id = pr.producto_id WHERE p.sucursal_id = " + sucursalId + " AND pr.estado = 'ACTIVO';", con).ExecuteScalarAsync() ?? 0);
    long mesasVivas = Convert.ToInt64(await new MySqlCommand("SELECT COUNT(*) FROM mesa_estados WHERE sucursal_id = " + sucursalId + ";", con).ExecuteScalarAsync() ?? 0);
    long pedidosPendientes = Convert.ToInt64(await new MySqlCommand("SELECT COUNT(*) FROM pedidos_movil WHERE sucursal_id = " + sucursalId + " AND estado = 'PENDIENTE';", con).ExecuteScalarAsync() ?? 0);

    return Results.Ok(new
    {
        ok = true,
        version = "V34_CORTESIA_MESERA",
        sucursalId,
        productos,
        presentaciones,
        mesasVivas,
        pedidosPendientes
    });
});

app.MapPost("/api/app-mesera/pedidos", async (Db db, AppPedidoMovilRequest req) =>
{
    await using var con = await db.OpenAsync();
    await EnsureAppMeseraTables(con);

    bool esCortesia = req.MesaId <= 0 && (req.Mesa ?? "").Contains("CORTES", StringComparison.OrdinalIgnoreCase);

    if (req.Cantidad <= 0)
        return Results.BadRequest(new { ok = false, message = "Cantidad inválida." });

    if (esCortesia)
    {
        await using var validar = new MySqlCommand("SELECT categoria FROM productos WHERE id = @id AND sucursal_id = @sucursal_id AND estado = 'ACTIVO' LIMIT 1;", con);
        validar.Parameters.AddWithValue("@id", req.ProductoId);
        validar.Parameters.AddWithValue("@sucursal_id", req.SucursalId);
        string categoria = Convert.ToString(await validar.ExecuteScalarAsync()) ?? "";
        string cat = categoria.Trim().ToUpperInvariant();
        bool permitido = cat.Contains("TRAGO") || cat.Contains("BOTELLA") || cat.Contains("SERVIDOS EN VASO");
        if (!permitido)
            return Results.BadRequest(new { ok = false, message = "La cortesía solo permite tragos en vaso o botella." });
    }

    decimal precioAplicado = esCortesia ? 0M : req.PrecioUnitario;
    decimal subtotal = req.Cantidad * precioAplicado;
    bool generaComisionAplicada = esCortesia || req.GeneraComision;
    string tipoComisionAplicada = esCortesia ? "MONTO" : (req.TipoComision ?? "NINGUNA");
    decimal valorComisionAplicada = esCortesia ? 5M : req.ValorComision;
    string syncKey = string.IsNullOrWhiteSpace(req.SyncKey) ? Guid.NewGuid().ToString("N") : req.SyncKey;

    await using var tx = await con.BeginTransactionAsync();

    try
    {
        const string pedidoSql = """
            INSERT INTO pedidos_movil
                (sucursal_id, mesa_id, mesa, mesera_usuario, mesera_nombre, fecha, estado, total, observacion, sync_key)
            VALUES
                (@sucursal_id, @mesa_id, @mesa, @mesera_usuario, @mesera_nombre, NOW(), 'PENDIENTE', @total, @observacion, @sync_key)
            ON DUPLICATE KEY UPDATE
                total = VALUES(total),
                observacion = VALUES(observacion);
            SELECT id FROM pedidos_movil WHERE sync_key = @sync_key LIMIT 1;
        """;

        await using var pedidoCmd = new MySqlCommand(pedidoSql, con, tx);
        pedidoCmd.Parameters.AddWithValue("@sucursal_id", req.SucursalId);
        pedidoCmd.Parameters.AddWithValue("@mesa_id", req.MesaId);
        pedidoCmd.Parameters.AddWithValue("@mesa", req.Mesa);
        pedidoCmd.Parameters.AddWithValue("@mesera_usuario", req.MeseraUsuario);
        pedidoCmd.Parameters.AddWithValue("@mesera_nombre", req.MeseraNombre);
        pedidoCmd.Parameters.AddWithValue("@total", subtotal);
        pedidoCmd.Parameters.AddWithValue("@observacion", req.Observacion ?? "");
        pedidoCmd.Parameters.AddWithValue("@sync_key", syncKey);

        long pedidoId = Convert.ToInt64(await pedidoCmd.ExecuteScalarAsync());

        await using (var del = new MySqlCommand("DELETE FROM detalle_pedidos_movil WHERE pedido_id = @pedido_id;", con, tx))
        {
            del.Parameters.AddWithValue("@pedido_id", pedidoId);
            await del.ExecuteNonQueryAsync();
        }

        const string detSql = """
            INSERT INTO detalle_pedidos_movil
                (pedido_id, producto_id, presentacion_id, producto, presentacion, cantidad, precio_unitario, subtotal,
                 genera_comision, tipo_comision, valor_comision, comision_calculada)
            VALUES
                (@pedido_id, @producto_id, @presentacion_id, @producto, @presentacion, @cantidad, @precio_unitario, @subtotal,
                 @genera_comision, @tipo_comision, @valor_comision, @comision_calculada);
        """;

        decimal comision = generaComisionAplicada
            ? (tipoComisionAplicada.Equals("PORCENTAJE", StringComparison.OrdinalIgnoreCase)
                ? subtotal * (valorComisionAplicada / 100M)
                : valorComisionAplicada * req.Cantidad)
            : 0M;

        await using var detCmd = new MySqlCommand(detSql, con, tx);
        detCmd.Parameters.AddWithValue("@pedido_id", pedidoId);
        detCmd.Parameters.AddWithValue("@producto_id", req.ProductoId);
        detCmd.Parameters.AddWithValue("@presentacion_id", req.PresentacionId);
        detCmd.Parameters.AddWithValue("@producto", req.Producto);
        detCmd.Parameters.AddWithValue("@presentacion", req.Presentacion);
        detCmd.Parameters.AddWithValue("@cantidad", req.Cantidad);
        detCmd.Parameters.AddWithValue("@precio_unitario", precioAplicado);
        detCmd.Parameters.AddWithValue("@subtotal", subtotal);
        detCmd.Parameters.AddWithValue("@genera_comision", generaComisionAplicada);
        detCmd.Parameters.AddWithValue("@tipo_comision", tipoComisionAplicada);
        detCmd.Parameters.AddWithValue("@valor_comision", valorComisionAplicada);
        detCmd.Parameters.AddWithValue("@comision_calculada", comision);
        await detCmd.ExecuteNonQueryAsync();

        await tx.CommitAsync();

        return Results.Ok(new
        {
            ok = true,
            pedido_id = pedidoId,
            estado = "PENDIENTE",
            total = subtotal,
            comision_calculada = comision,
            message = esCortesia ? "Cortesía enviada a caja." : "Pedido enviado a caja."
        });
    }
    catch (Exception ex)
    {
        await tx.RollbackAsync();
        return Results.Problem("No se pudo registrar el pedido móvil: " + ex.Message);
    }
});


app.MapPost("/api/app-mesera/reportes-producto", async (Db db, ProductReportRequest req) =>
{
    await using var con = await db.OpenAsync();
    await EnsureAppMeseraTables(con);

    string motivo = string.IsNullOrWhiteSpace(req.Motivo) ? "DAÑADO/PERDIDO" : req.Motivo.Trim().ToUpperInvariant();
    string syncKey = string.IsNullOrWhiteSpace(req.SyncKey) ? Guid.NewGuid().ToString("N") : req.SyncKey;
    decimal costo = req.Cantidad * req.PrecioUnitario;

    const string sql = """
        INSERT INTO reportes_productos_movil
            (sucursal_id, turno, usuario, nombre, fecha, producto_id, presentacion_id,
             producto, presentacion, cantidad, precio_unitario, costo_perdido, motivo, observacion, sync_key)
        VALUES
            (@sucursal_id, @turno, @usuario, @nombre, NOW(), @producto_id, @presentacion_id,
             @producto, @presentacion, @cantidad, @precio_unitario, @costo_perdido, @motivo, @observacion, @sync_key)
        ON DUPLICATE KEY UPDATE
            cantidad = VALUES(cantidad),
            precio_unitario = VALUES(precio_unitario),
            costo_perdido = VALUES(costo_perdido),
            motivo = VALUES(motivo),
            observacion = VALUES(observacion);
    """;

    await using var cmd = new MySqlCommand(sql, con);
    cmd.Parameters.AddWithValue("@sucursal_id", req.SucursalId <= 0 ? 1 : req.SucursalId);
    cmd.Parameters.AddWithValue("@turno", string.IsNullOrWhiteSpace(req.Turno) ? "MAÑANA" : req.Turno.Trim().ToUpperInvariant());
    cmd.Parameters.AddWithValue("@usuario", req.Usuario ?? "");
    cmd.Parameters.AddWithValue("@nombre", req.Nombre ?? "");
    cmd.Parameters.AddWithValue("@producto_id", req.ProductoId);
    cmd.Parameters.AddWithValue("@presentacion_id", req.PresentacionId);
    cmd.Parameters.AddWithValue("@producto", req.Producto ?? "");
    cmd.Parameters.AddWithValue("@presentacion", req.Presentacion ?? "");
    cmd.Parameters.AddWithValue("@cantidad", req.Cantidad);
    cmd.Parameters.AddWithValue("@precio_unitario", req.PrecioUnitario);
    cmd.Parameters.AddWithValue("@costo_perdido", costo);
    cmd.Parameters.AddWithValue("@motivo", motivo);
    cmd.Parameters.AddWithValue("@observacion", req.Observacion ?? "");
    cmd.Parameters.AddWithValue("@sync_key", syncKey);
    await cmd.ExecuteNonQueryAsync();

    return Results.Ok(new { ok = true, costo_perdido = costo, message = "Reporte de producto registrado." });
});

app.MapGet("/api/admin/productos-reportados", async (Db db, string clave) =>
{
    const string cleanKey = "ENTREGAR_LIMPIO_2026";
    if (clave != cleanKey) return Results.Unauthorized();

    await using var con = await db.OpenAsync();
    await EnsureAppMeseraTables(con);

    const string sql = """
        SELECT r.id, r.sucursal_id,
               CASE WHEN s.id = 2 THEN 'SEGUNDA SUCURSAL' ELSE 'PRIMERA SUCURSAL' END AS sucursal,
               r.turno, r.usuario, r.nombre, r.fecha,
               r.producto, r.presentacion, r.cantidad, r.precio_unitario,
               r.costo_perdido, r.motivo, r.observacion
        FROM reportes_productos_movil r
        LEFT JOIN sucursales s ON s.id = r.sucursal_id
        ORDER BY r.fecha DESC;
    """;

    return Results.Ok(await db.QueryAsync(con, sql));
});


app.MapGet("/api/app-mesera/pedidos-pendientes", async (Db db, int sucursalId) =>
{
    await using var con = await db.OpenAsync();
    await EnsureAppMeseraTables(con);

    const string sql = """
        SELECT p.id, p.sucursal_id, p.mesa_id, p.mesa, p.mesera_usuario, p.mesera_nombre,
               p.fecha, p.estado, p.total, p.observacion,
               d.producto_id, d.presentacion_id, d.producto, d.presentacion, d.cantidad,
               d.precio_unitario, d.subtotal, d.genera_comision, d.tipo_comision, d.valor_comision,
               d.comision_calculada
        FROM pedidos_movil p
        INNER JOIN detalle_pedidos_movil d ON d.pedido_id = p.id
        WHERE p.sucursal_id = @sucursalId
          AND p.estado = 'PENDIENTE'
        ORDER BY p.fecha;
    """;

    return Results.Ok(await db.QueryAsync(con, sql, new Dictionary<string, object?>
    {
        ["@sucursalId"] = sucursalId
    }));
});

app.MapPost("/api/app-mesera/pedidos/{id:long}/estado", async (Db db, SheetsReporter sheets, long id, PedidoEstadoRequest req) =>
{
    await using var con = await db.OpenAsync();
    await EnsureAppMeseraTables(con);

    string estado = (req.Estado ?? "").Trim().ToUpperInvariant();
    if (estado != "ACEPTADO" && estado != "RECHAZADO" && estado != "ENTREGADO")
        return Results.BadRequest(new { ok = false, message = "Estado inválido." });

    await using var tx = await con.BeginTransactionAsync();

    try
    {
        const string updateSql = """
            UPDATE pedidos_movil
            SET estado = @estado,
                cajero_usuario = @cajero,
                fecha_respuesta = NOW()
            WHERE id = @id;
        """;

        await using var cmd = new MySqlCommand(updateSql, con, tx);
        cmd.Parameters.AddWithValue("@estado", estado);
        cmd.Parameters.AddWithValue("@cajero", req.CajeroUsuario ?? "");
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();

        if (estado == "ACEPTADO" || estado == "ENTREGADO")
        {
            const string comSql = """
                INSERT INTO comisiones_meseras
                    (pedido_id, sucursal_id, mesa_id, mesera_usuario, mesera_nombre, fecha,
                     producto, cantidad, venta_total, comision_total, estado)
                SELECT p.id, p.sucursal_id, p.mesa_id, p.mesera_usuario, p.mesera_nombre, NOW(),
                       d.producto, d.cantidad, d.subtotal, d.comision_calculada, 'PENDIENTE_PAGO'
                FROM pedidos_movil p
                INNER JOIN detalle_pedidos_movil d ON d.pedido_id = p.id
                WHERE p.id = @id AND d.comision_calculada > 0
                ON DUPLICATE KEY UPDATE
                    venta_total = VALUES(venta_total),
                    comision_total = VALUES(comision_total),
                    estado = VALUES(estado);
            """;

            await using var comCmd = new MySqlCommand(comSql, con, tx);
            comCmd.Parameters.AddWithValue("@id", id);
            await comCmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
        await TrySyncSheets(db, sheets);

        return Results.Ok(new { ok = true, pedido_id = id, estado });
    }
    catch (Exception ex)
    {
        await tx.RollbackAsync();
        return Results.Problem("No se pudo cambiar estado del pedido: " + ex.Message);
    }
});

app.MapGet("/api/app-mesera/comisiones", async (Db db, int sucursalId, string? meseraUsuario) =>
{
    await using var con = await db.OpenAsync();
    await EnsureAppMeseraTables(con);

    const string sql = """
        SELECT sucursal_id, mesera_usuario, mesera_nombre, DATE(fecha) AS fecha,
               SUM(venta_total) AS total_vendido,
               SUM(comision_total) AS total_comision
        FROM comisiones_meseras
        WHERE sucursal_id = @sucursalId
          AND (@meseraUsuario IS NULL OR mesera_usuario = @meseraUsuario)
        GROUP BY sucursal_id, mesera_usuario, mesera_nombre, DATE(fecha)
        ORDER BY fecha DESC, mesera_nombre;
    """;

    return Results.Ok(await db.QueryAsync(con, sql, new Dictionary<string, object?>
    {
        ["@sucursalId"] = sucursalId,
        ["@meseraUsuario"] = meseraUsuario
    }));
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
    await EnsureAppMeseraTables(con);

    const string sql = """
        SELECT p.id, p.sucursal_id, CASE WHEN s.id = 2 THEN 'SEGUNDA SUCURSAL' ELSE 'PRIMERA SUCURSAL' END AS sucursal, p.nombre, p.categoria,
               p.unidad_base, p.stock_actual, p.stock_minimo, COALESCE(p.sin_limite_stock, 0) AS sin_limite_stock, p.estado
        FROM productos p
        INNER JOIN sucursales s ON s.id = p.sucursal_id
        WHERE (@sucursalId IS NULL OR p.sucursal_id = @sucursalId)
        ORDER BY p.sucursal_id,
                 CASE
                    WHEN p.categoria = 'Agua' THEN 1
                    WHEN p.categoria = 'Energizantes' THEN 2
                    WHEN p.categoria = 'Sodas' THEN 3
                    WHEN p.categoria IN ('Cocas', 'Coca machucada') THEN 4
                    WHEN p.categoria = 'Cervezas' THEN 5
                    WHEN p.categoria = 'Tragos / Botellas' THEN 6
                    WHEN p.categoria = 'Servidos en vaso' THEN 7
                    WHEN p.categoria = 'Cigarros' THEN 8
                    WHEN p.categoria = 'Snacks y piqueos' THEN 9
                    WHEN p.categoria = 'Dulces y golosinas' THEN 10
                    WHEN p.categoria = 'Combos / Promos' THEN 11
                    WHEN p.categoria = 'Accesorios' THEN 12
                    WHEN p.categoria = 'Ceniceros' THEN 13
                    WHEN p.categoria IN ('Otros', 'Otros / Extras', 'Varios') THEN 14
                    ELSE 99
                 END,
                 p.nombre;
    """;

    var rows = await db.QueryAsync(con, sql, new Dictionary<string, object?>
    {
        ["@sucursalId"] = sucursalId
    });

    return Results.Ok(rows);
});

app.MapPost("/api/admin/cargar-catalogo-local", async (Db db, string clave) =>
{
    const string cleanKey = "ENTREGAR_LIMPIO_2026";
    if (clave != cleanKey) return Results.Unauthorized();

    await using var con = await db.OpenAsync();
    await EnsureAppMeseraTables(con);

    int insertados = 0;
    int actualizados = 0;

    foreach (int sucursalId in new[] { 1, 2 })
    {
        foreach (var item in CatalogoProductosLocalV19())
        {
            bool updated = await UpsertCatalogoProductoLocal(con, sucursalId, item.nombre, item.categoria, item.precio, "Unidad", 5);
            if (updated) actualizados++; else insertados++;
        }

        foreach (var item in CatalogoCombosPromosLocalV19())
        {
            bool updated = await UpsertCatalogoProductoLocal(con, sucursalId, item.nombre, item.categoria, item.precio, item.detalle, 2);
            if (updated) actualizados++; else insertados++;
        }
    }

    return Results.Ok(new
    {
        ok = true,
        version = "V19_CATALOGO_LOCAL_DULCES",
        message = "Catálogo local cargado en Railway: productos, dulces, combos y promociones.",
        insertados,
        actualizados,
        nota = "No se cargó PRUEBA porque parece dato de prueba."
    });
});

app.MapGet("/api/admin/cargar-catalogo-local", async (Db db, string clave) =>
{
    const string cleanKey = "ENTREGAR_LIMPIO_2026";
    if (clave != cleanKey) return Results.Unauthorized();

    await using var con = await db.OpenAsync();
    await EnsureAppMeseraTables(con);

    int insertados = 0;
    int actualizados = 0;

    foreach (int sucursalId in new[] { 1, 2 })
    {
        foreach (var item in CatalogoProductosLocalV19())
        {
            bool updated = await UpsertCatalogoProductoLocal(con, sucursalId, item.nombre, item.categoria, item.precio, "Unidad", 5);
            if (updated) actualizados++; else insertados++;
        }

        foreach (var item in CatalogoCombosPromosLocalV19())
        {
            bool updated = await UpsertCatalogoProductoLocal(con, sucursalId, item.nombre, item.categoria, item.precio, item.detalle, 2);
            if (updated) actualizados++; else insertados++;
        }
    }

    return Results.Ok(new
    {
        ok = true,
        version = "V19_CATALOGO_LOCAL_DULCES",
        message = "Catálogo local cargado en Railway: productos, dulces, combos y promociones.",
        insertados,
        actualizados,
        nota = "No se cargó PRUEBA porque parece dato de prueba."
    });
});

app.MapPost("/api/admin/cargar-catalogo-final", async (Db db, SheetsReporter sheets, string clave) =>
{
    const string cleanKey = "ENTREGAR_LIMPIO_2026";
    if (clave != cleanKey) return Results.Unauthorized();

    await using var con = await db.OpenAsync();
    await EnsureAppMeseraTables(con);

    int insertados = 0;
    int actualizados = 0;

    foreach (int sucursalId in new[] { 1, 2 })
    {
        foreach (var item in CatalogoFinalV29())
        {
            bool updated = await UpsertCatalogoFinalV29(con, sucursalId, item.nombre, item.categoria, item.cantidad, item.precio, item.sinLimiteStock);
            if (updated) actualizados++; else insertados++;
        }
    }

    await TrySyncSheets(db, sheets);

    return Results.Ok(new
    {
        ok = true,
        version = "V29_CATALOGO_FINAL_STOCK",
        message = "Catálogo final cargado: cantidades, precios y productos en vaso sin límite de stock.",
        productos = CatalogoFinalV29().Length,
        sucursales = 2,
        insertados,
        actualizados,
        nota = "Los productos de categoría Servidos en vaso quedan con sin_limite_stock = 1 y no descuentan inventario."
    });
});

app.MapGet("/api/admin/cargar-catalogo-final", async (Db db, SheetsReporter sheets, string clave) =>
{
    const string cleanKey = "ENTREGAR_LIMPIO_2026";
    if (clave != cleanKey) return Results.Unauthorized();

    await using var con = await db.OpenAsync();
    await EnsureAppMeseraTables(con);

    int insertados = 0;
    int actualizados = 0;

    foreach (int sucursalId in new[] { 1, 2 })
    {
        foreach (var item in CatalogoFinalV29())
        {
            bool updated = await UpsertCatalogoFinalV29(con, sucursalId, item.nombre, item.categoria, item.cantidad, item.precio, item.sinLimiteStock);
            if (updated) actualizados++; else insertados++;
        }
    }

    await TrySyncSheets(db, sheets);

    return Results.Ok(new
    {
        ok = true,
        version = "V29_CATALOGO_FINAL_STOCK",
        message = "Catálogo final cargado: cantidades, precios y productos en vaso sin límite de stock.",
        productos = CatalogoFinalV29().Length,
        sucursales = 2,
        insertados,
        actualizados,
        nota = "Los productos de categoría Servidos en vaso quedan con sin_limite_stock = 1 y no descuentan inventario."
    });
});



app.MapPost("/api/admin/aplicar-stock-inicial", async (Db db, SheetsReporter sheets, string clave, int sucursalId = 1) =>
{
    return await AplicarStockTxtPaquetesV33(db, sheets, clave, sucursalId);
});

app.MapGet("/api/admin/aplicar-stock-inicial", async (Db db, SheetsReporter sheets, string clave, int sucursalId = 1) =>
{
    return await AplicarStockTxtPaquetesV33(db, sheets, clave, sucursalId);
});

app.MapPost("/api/admin/aplicar-stock-txt", async (Db db, SheetsReporter sheets, string clave, int sucursalId = 1) =>
{
    return await AplicarStockTxtPaquetesV33(db, sheets, clave, sucursalId);
});

app.MapGet("/api/admin/aplicar-stock-txt", async (Db db, SheetsReporter sheets, string clave, int sucursalId = 1) =>
{
    return await AplicarStockTxtPaquetesV33(db, sheets, clave, sucursalId);
});

app.MapPost("/api/productos", async (Db db, SheetsReporter sheets, string clave, ProductoRequest p) =>
{
    // Endpoint legado protegido: el alta normal de productos se realiza desde
    // /api/admin/productos/guardar, que conserva toda la estructura del producto.
    const string cleanKey = "ENTREGAR_LIMPIO_2026";
    if (clave != cleanKey) return Results.Unauthorized();
    if (p.SucursalId != 1 && p.SucursalId != 2)
        return Results.BadRequest(new { ok = false, message = "Sucursal inválida." });
    if (string.IsNullOrWhiteSpace(p.Nombre))
        return Results.BadRequest(new { ok = false, message = "El nombre del producto es obligatorio." });

    await using var con = await db.OpenAsync();
    await EnsureAppMeseraTables(con);

    string nombre = p.Nombre.Trim();
    string categoria = NormalizarCategoriaProducto(p.Categoria, nombre);
    string unidadBase = string.IsNullOrWhiteSpace(p.UnidadBase) ? "UNIDAD" : p.UnidadBase.Trim().ToUpperInvariant();
    long id = 0;

    await using (var buscar = new MySqlCommand("SELECT id FROM productos WHERE sucursal_id=@sucursal_id AND LOWER(TRIM(nombre))=LOWER(TRIM(@nombre)) LIMIT 1;", con))
    {
        buscar.Parameters.AddWithValue("@sucursal_id", p.SucursalId);
        buscar.Parameters.AddWithValue("@nombre", nombre);
        object? found = await buscar.ExecuteScalarAsync();
        if (found != null) id = Convert.ToInt64(found);
    }

    if (id <= 0)
    {
        await using var cmd = new MySqlCommand("""
            INSERT INTO productos (sucursal_id, nombre, categoria, unidad_base, stock_actual, stock_minimo, estado)
            VALUES (@sucursal_id, @nombre, @categoria, @unidad_base, @stock_actual, @stock_minimo, 'ACTIVO');
            SELECT LAST_INSERT_ID();
        """, con);
        cmd.Parameters.AddWithValue("@sucursal_id", p.SucursalId);
        cmd.Parameters.AddWithValue("@nombre", nombre);
        cmd.Parameters.AddWithValue("@categoria", categoria);
        cmd.Parameters.AddWithValue("@unidad_base", unidadBase);
        cmd.Parameters.AddWithValue("@stock_actual", Math.Max(0, p.StockActual));
        cmd.Parameters.AddWithValue("@stock_minimo", Math.Max(0, p.StockMinimo));
        id = Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }
    else
    {
        await using var cmd = new MySqlCommand("""
            UPDATE productos
            SET categoria=@categoria, unidad_base=@unidad_base,
                stock_actual=@stock_actual, stock_minimo=@stock_minimo, estado='ACTIVO'
            WHERE id=@id;
        """, con);
        cmd.Parameters.AddWithValue("@categoria", categoria);
        cmd.Parameters.AddWithValue("@unidad_base", unidadBase);
        cmd.Parameters.AddWithValue("@stock_actual", Math.Max(0, p.StockActual));
        cmd.Parameters.AddWithValue("@stock_minimo", Math.Max(0, p.StockMinimo));
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    await TrySyncSheets(db, sheets);
    return Results.Ok(new { ok = true, id, categoria });
});

app.MapPost("/api/ventas", async (Db db, SheetsReporter sheets, VentaRequest venta) =>
{
    await using var con = await db.OpenAsync();
    await EnsureAppMeseraTables(con);
    await using var tx = await con.BeginTransactionAsync();

    try
    {
        string syncKey = string.IsNullOrWhiteSpace(venta.SyncKey)
            ? Guid.NewGuid().ToString("N")
            : venta.SyncKey;

        bool ventaYaExistia;
        await using (var existeCmd = new MySqlCommand("SELECT COUNT(*) FROM ventas WHERE sync_key = @sync_key;", con, tx))
        {
            existeCmd.Parameters.AddWithValue("@sync_key", syncKey);
            ventaYaExistia = Convert.ToInt32(await existeCmd.ExecuteScalarAsync() ?? 0) > 0;
        }

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
            string nombreProducto = string.IsNullOrWhiteSpace(d.Producto) ? "Producto" : d.Producto.Trim();
            string nombrePresentacion = string.IsNullOrWhiteSpace(d.Presentacion) ? "Unidad" : d.Presentacion.Trim();
            decimal cantidadBase = d.CantidadBase <= 0 ? d.Cantidad : d.CantidadBase;

            // Los ID de SQLite/JSON de la PC no se reutilizan como ID de MySQL.
            // Railway resuelve producto por sucursal + nombre y presentación por producto + nombre.
            // Así no se crean duplicados ni se pisa otro producto cuando los ID locales difieren.
            long productoId;
            await using (var findProd = new MySqlCommand("""
                SELECT id
                FROM productos
                WHERE sucursal_id = @sucursal_id
                  AND LOWER(TRIM(nombre)) = LOWER(TRIM(@nombre))
                ORDER BY id
                LIMIT 1;
            """, con, tx))
            {
                findProd.Parameters.AddWithValue("@sucursal_id", venta.SucursalId);
                findProd.Parameters.AddWithValue("@nombre", nombreProducto);
                var found = await findProd.ExecuteScalarAsync();
                if (found != null)
                {
                    productoId = Convert.ToInt64(found);
                    await using var activar = new MySqlCommand("UPDATE productos SET estado = 'ACTIVO' WHERE id = @id;", con, tx);
                    activar.Parameters.AddWithValue("@id", productoId);
                    await activar.ExecuteNonQueryAsync();
                }
                else
                {
                    string categoriaNueva = NormalizarCategoriaProducto(null, nombreProducto);
                    await using var insertProd = new MySqlCommand("""
                        INSERT INTO productos
                        (sucursal_id, nombre, categoria, tipo_entrada, unidad_base, unidades_por_entrada,
                         precio_compra, stock_actual, stock_minimo, estado)
                        VALUES
                        (@sucursal_id, @nombre, @categoria, 'UNIDAD', 'UNIDAD', 1, 0, 0, 0, 'ACTIVO');
                        SELECT LAST_INSERT_ID();
                    """, con, tx);
                    insertProd.Parameters.AddWithValue("@sucursal_id", venta.SucursalId);
                    insertProd.Parameters.AddWithValue("@nombre", nombreProducto);
                    insertProd.Parameters.AddWithValue("@categoria", categoriaNueva);
                    productoId = Convert.ToInt64(await insertProd.ExecuteScalarAsync());
                }
            }

            long presentacionId;
            await using (var findPres = new MySqlCommand("""
                SELECT id
                FROM presentaciones
                WHERE producto_id = @producto_id
                  AND LOWER(TRIM(nombre)) = LOWER(TRIM(@nombre))
                ORDER BY id
                LIMIT 1;
            """, con, tx))
            {
                findPres.Parameters.AddWithValue("@producto_id", productoId);
                findPres.Parameters.AddWithValue("@nombre", nombrePresentacion);
                var found = await findPres.ExecuteScalarAsync();
                if (found != null)
                {
                    presentacionId = Convert.ToInt64(found);
                    await using var updatePres = new MySqlCommand("""
                        UPDATE presentaciones
                        SET cantidad_base = @cantidad_base,
                            precio_venta = @precio_venta,
                            estado = 'ACTIVO'
                        WHERE id = @id;
                    """, con, tx);
                    updatePres.Parameters.AddWithValue("@cantidad_base", cantidadBase);
                    updatePres.Parameters.AddWithValue("@precio_venta", d.PrecioUnitario);
                    updatePres.Parameters.AddWithValue("@id", presentacionId);
                    await updatePres.ExecuteNonQueryAsync();
                }
                else
                {
                    await using var insertPres = new MySqlCommand("""
                        INSERT INTO presentaciones (producto_id, nombre, cantidad_base, precio_venta, estado)
                        VALUES (@producto_id, @nombre, @cantidad_base, @precio_venta, 'ACTIVO');
                        SELECT LAST_INSERT_ID();
                    """, con, tx);
                    insertPres.Parameters.AddWithValue("@producto_id", productoId);
                    insertPres.Parameters.AddWithValue("@nombre", nombrePresentacion);
                    insertPres.Parameters.AddWithValue("@cantidad_base", cantidadBase);
                    insertPres.Parameters.AddWithValue("@precio_venta", d.PrecioUnitario);
                    presentacionId = Convert.ToInt64(await insertPres.ExecuteScalarAsync());
                }
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

            // Una misma venta se reenvía durante la sincronización automática. El stock solo
            // debe descontarse la primera vez que llega ese sync_key.
            if (!ventaYaExistia)
            {
                await using var stockCmd = new MySqlCommand("""
                    UPDATE productos
                    SET stock_actual = CASE
                        WHEN COALESCE(sin_limite_stock, 0) = 1 OR categoria = 'Servidos en vaso'
                        THEN stock_actual
                        ELSE GREATEST(stock_actual - @cantidad_base, 0)
                    END
                    WHERE id = @producto_id;
                """, con, tx);
                stockCmd.Parameters.AddWithValue("@cantidad_base", cantidadBase);
                stockCmd.Parameters.AddWithValue("@producto_id", productoId);
                await stockCmd.ExecuteNonQueryAsync();
            }
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


// V35: cierre de turno/arqueo inmutable.
// La caja envía una fotografía completa del turno; el Administrador la consulta después.
app.MapPost("/api/cierres-turno", async (Db db, SheetsReporter sheets, ShiftCloseRequest r) =>
{
    await using var con = await db.OpenAsync();
    await EnsureShiftCloseTables(con);

    string syncKey = string.IsNullOrWhiteSpace(r.SyncKey)
        ? "CIERRE-" + r.SucursalId + "-" + (r.CajeroUsuario ?? "") + "-" + r.Inicio.Ticks
        : r.SyncKey.Trim();

    const string sql = """
        INSERT INTO cierres_turno
        (
            sucursal_id, sucursal, cajero_usuario, cajero_nombre, caja, turno,
            inicio, fin, hora_entrada, fecha_cierre,
            transacciones_total, transacciones_efectivo, transacciones_qr,
            transacciones_tarjeta, transacciones_transferencia,
            efectivo, qr, tarjeta, transferencia, sin_metodo,
            productos_total, mesas_total, minutos_jugados, propinas_total,
            cortesias_valor, comisiones_total, gastos_total, perdidas_total,
            total_generado, neto_turno, observaciones, detalle_json, sync_key
        )
        VALUES
        (
            @sucursal_id, @sucursal, @cajero_usuario, @cajero_nombre, @caja, @turno,
            @inicio, @fin, @hora_entrada, @fecha_cierre,
            @transacciones_total, @transacciones_efectivo, @transacciones_qr,
            @transacciones_tarjeta, @transacciones_transferencia,
            @efectivo, @qr, @tarjeta, @transferencia, @sin_metodo,
            @productos_total, @mesas_total, @minutos_jugados, @propinas_total,
            @cortesias_valor, @comisiones_total, @gastos_total, @perdidas_total,
            @total_generado, @neto_turno, @observaciones, @detalle_json, @sync_key
        )
        ON DUPLICATE KEY UPDATE sync_key = VALUES(sync_key);
    """;

    await using (var cmd = new MySqlCommand(sql, con))
    {
        cmd.Parameters.AddWithValue("@sucursal_id", r.SucursalId);
        cmd.Parameters.AddWithValue("@sucursal", string.IsNullOrWhiteSpace(r.Sucursal) ? (r.SucursalId == 2 ? "SEGUNDA SUCURSAL" : "PRIMERA SUCURSAL") : r.Sucursal.Trim());
        cmd.Parameters.AddWithValue("@cajero_usuario", r.CajeroUsuario ?? "");
        cmd.Parameters.AddWithValue("@cajero_nombre", r.CajeroNombre ?? "");
        cmd.Parameters.AddWithValue("@caja", r.Caja ?? "");
        cmd.Parameters.AddWithValue("@turno", NormalizarTurno(r.Turno));
        cmd.Parameters.AddWithValue("@inicio", r.Inicio);
        cmd.Parameters.AddWithValue("@fin", r.Fin);
        cmd.Parameters.AddWithValue("@hora_entrada", r.HoraEntrada == DateTime.MinValue ? r.Inicio : r.HoraEntrada);
        cmd.Parameters.AddWithValue("@fecha_cierre", r.FechaCierre);
        cmd.Parameters.AddWithValue("@transacciones_total", Math.Max(0, r.TransaccionesTotal));
        cmd.Parameters.AddWithValue("@transacciones_efectivo", Math.Max(0, r.TransaccionesEfectivo));
        cmd.Parameters.AddWithValue("@transacciones_qr", Math.Max(0, r.TransaccionesQr));
        cmd.Parameters.AddWithValue("@transacciones_tarjeta", Math.Max(0, r.TransaccionesTarjeta));
        cmd.Parameters.AddWithValue("@transacciones_transferencia", Math.Max(0, r.TransaccionesTransferencia));
        cmd.Parameters.AddWithValue("@efectivo", Math.Max(0, r.Efectivo));
        cmd.Parameters.AddWithValue("@qr", Math.Max(0, r.Qr));
        cmd.Parameters.AddWithValue("@tarjeta", Math.Max(0, r.Tarjeta));
        cmd.Parameters.AddWithValue("@transferencia", Math.Max(0, r.Transferencia));
        cmd.Parameters.AddWithValue("@sin_metodo", Math.Max(0, r.SinMetodo));
        cmd.Parameters.AddWithValue("@productos_total", Math.Max(0, r.ProductosTotal));
        cmd.Parameters.AddWithValue("@mesas_total", Math.Max(0, r.MesasTotal));
        cmd.Parameters.AddWithValue("@minutos_jugados", Math.Max(0, r.MinutosJugados));
        cmd.Parameters.AddWithValue("@propinas_total", Math.Max(0, r.PropinasTotal));
        cmd.Parameters.AddWithValue("@cortesias_valor", Math.Max(0, r.CortesiasValor));
        cmd.Parameters.AddWithValue("@comisiones_total", Math.Max(0, r.ComisionesTotal));
        cmd.Parameters.AddWithValue("@gastos_total", Math.Max(0, r.GastosTotal));
        cmd.Parameters.AddWithValue("@perdidas_total", Math.Max(0, r.PerdidasTotal));
        cmd.Parameters.AddWithValue("@total_generado", Math.Max(0, r.TotalGenerado));
        cmd.Parameters.AddWithValue("@neto_turno", r.NetoTurno);
        cmd.Parameters.AddWithValue("@observaciones", r.Observaciones ?? "");
        cmd.Parameters.AddWithValue("@detalle_json", string.IsNullOrWhiteSpace(r.DetalleJson) ? "[]" : r.DetalleJson);
        cmd.Parameters.AddWithValue("@sync_key", syncKey);
        await cmd.ExecuteNonQueryAsync();
    }

    long id;
    await using (var idCmd = new MySqlCommand("SELECT id FROM cierres_turno WHERE sync_key=@sync_key LIMIT 1;", con))
    {
        idCmd.Parameters.AddWithValue("@sync_key", syncKey);
        id = Convert.ToInt64(await idCmd.ExecuteScalarAsync() ?? 0L);
    }

    await TrySyncSheets(db, sheets);
    return Results.Ok(new { ok = true, id, syncKey, message = "Arqueo guardado para Administración." });
});

app.MapGet("/api/admin/cierres-turno", async (Db db, string clave, int? sucursalId) =>
{
    const string adminKey = "ENTREGAR_LIMPIO_2026";
    if (clave != adminKey) return Results.Unauthorized();

    await using var con = await db.OpenAsync();
    await EnsureShiftCloseTables(con);

    string sql = """
        SELECT
            id,
            sync_key,
            sucursal_id,
            sucursal,
            cajero_usuario,
            cajero_nombre,
            caja,
            turno,
            inicio,
            fin,
            hora_entrada,
            fecha_cierre,
            transacciones_total,
            transacciones_efectivo,
            transacciones_qr,
            transacciones_tarjeta,
            transacciones_transferencia,
            efectivo,
            qr,
            tarjeta,
            transferencia,
            sin_metodo,
            productos_total,
            mesas_total,
            minutos_jugados,
            propinas_total,
            cortesias_valor,
            comisiones_total,
            gastos_total,
            perdidas_total,
            total_generado,
            neto_turno,
            observaciones,
            detalle_json
        FROM cierres_turno
    """;

    if (sucursalId.HasValue && sucursalId.Value > 0)
        sql += " WHERE sucursal_id = @sucursal_id";

    sql += " ORDER BY fecha_cierre DESC, id DESC;";

    await using var cmd = new MySqlCommand(sql, con);
    if (sucursalId.HasValue && sucursalId.Value > 0)
        cmd.Parameters.AddWithValue("@sucursal_id", sucursalId.Value);

    List<Dictionary<string, object?>> rows = new();
    await using var rd = await cmd.ExecuteReaderAsync();
    while (await rd.ReadAsync())
    {
        Dictionary<string, object?> row = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < rd.FieldCount; i++)
            row[rd.GetName(i)] = rd.IsDBNull(i) ? null : rd.GetValue(i);
        rows.Add(row);
    }

    return Results.Ok(rows);
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


static async Task<IResult> AplicarStockTxtPaquetesV33(Db db, SheetsReporter sheets, string clave, int sucursalId)
{
    const string cleanKey = "ENTREGAR_LIMPIO_2026";
    if (clave != cleanKey) return Results.Unauthorized();
    if (sucursalId != 1 && sucursalId != 2)
        return Results.BadRequest(new { ok = false, message = "sucursalId debe ser 1 o 2." });

    await using var con = await db.OpenAsync();
    await EnsureAppMeseraTables(con);

    int productosActualizados = 0;
    int productosSinLimite = 0;
    var faltantes = new List<string>();
    var revisarUnidades = new List<string>();

    foreach (var item in StockSoloTxtV30())
    {
        long productoId = 0;
        int unidadesPorEntrada = 1;
        bool sinLimite = false;
        string nombreEncontrado = item.aliases[0];

        foreach (string alias in item.aliases)
        {
            await using var buscar = new MySqlCommand("""
                SELECT id,
                       GREATEST(COALESCE(unidades_por_entrada, 1), 1) AS unidades_por_entrada,
                       COALESCE(sin_limite_stock, 0) AS sin_limite_stock,
                       nombre
                FROM productos
                WHERE sucursal_id = @sucursal_id
                  AND UPPER(TRIM(nombre)) = UPPER(TRIM(@nombre))
                  AND estado = 'ACTIVO'
                LIMIT 1;
            """, con);
            buscar.Parameters.AddWithValue("@sucursal_id", sucursalId);
            buscar.Parameters.AddWithValue("@nombre", alias);

            await using var reader = await buscar.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                productoId = reader.GetInt64("id");
                unidadesPorEntrada = reader.GetInt32("unidades_por_entrada");
                sinLimite = reader.GetInt32("sin_limite_stock") == 1;
                nombreEncontrado = reader.GetString("nombre");
                break;
            }
        }

        if (productoId <= 0)
        {
            faltantes.Add(item.aliases[0]);
            continue;
        }

        if (sinLimite)
        {
            productosSinLimite++;
            continue;
        }

        if (unidadesPorEntrada == 1 && item.cantidad > 0)
            revisarUnidades.Add(nombreEncontrado);

        decimal stockTotal = Math.Max(0, item.cantidad * unidadesPorEntrada);

        await using var actualizar = new MySqlCommand("""
            UPDATE productos
            SET stock_actual = @stock_actual,
                tipo_entrada = CASE WHEN @unidades_por_entrada > 1 THEN 'PAQUETE' ELSE tipo_entrada END
            WHERE id = @id;
        """, con);
        actualizar.Parameters.AddWithValue("@stock_actual", stockTotal);
        actualizar.Parameters.AddWithValue("@unidades_por_entrada", unidadesPorEntrada);
        actualizar.Parameters.AddWithValue("@id", productoId);
        await actualizar.ExecuteNonQueryAsync();
        productosActualizados++;
    }

    await TrySyncSheets(db, sheets);

    return Results.Ok(new
    {
        ok = true,
        version = "V34_CORTESIA_MESERA",
        message = "Stock calculado desde el TXT como cantidad de paquetes/entradas por unidades_por_entrada.",
        formula = "stock_actual = cantidad_TXT × unidades_por_entrada",
        sucursalId,
        productosActualizados,
        productosSinLimite,
        faltantes,
        revisarUnidades,
        nota = "Los productos con unidades_por_entrada = 1 se calculan 1 a 1. Si realmente llegan en paquetes de 6, 12, 24, etc., configure primero ese valor desde Productos / Stock."
    });
}

static async Task<IResult> AplicarStockInicialReferenciaV32(Db db, SheetsReporter sheets, string clave, int sucursalId)
{
    const string cleanKey = "ENTREGAR_LIMPIO_2026";
    if (clave != cleanKey) return Results.Unauthorized();
    if (sucursalId != 1 && sucursalId != 2)
        return Results.BadRequest(new { ok = false, message = "sucursalId debe ser 1 o 2." });

    await using var con = await db.OpenAsync();
    await EnsureAppMeseraTables(con);

    int productosActualizados = 0;
    var faltantes = new List<string>();

    foreach (var item in StockInicialReferenciaV32())
    {
        long productoId = 0;

        foreach (string alias in item.aliases)
        {
            await using var buscar = new MySqlCommand("""
                SELECT id
                FROM productos
                WHERE sucursal_id = @sucursal_id
                  AND UPPER(TRIM(nombre)) = UPPER(TRIM(@nombre))
                  AND estado = 'ACTIVO'
                LIMIT 1;
            """, con);
            buscar.Parameters.AddWithValue("@sucursal_id", sucursalId);
            buscar.Parameters.AddWithValue("@nombre", alias);

            object? found = await buscar.ExecuteScalarAsync();
            if (found != null)
            {
                productoId = Convert.ToInt64(found);
                break;
            }
        }

        if (productoId <= 0)
        {
            faltantes.Add(item.aliases[0]);
            continue;
        }

        await using var actualizar = new MySqlCommand("""
            UPDATE productos
            SET stock_actual = @stock_actual
            WHERE id = @id;
        """, con);
        actualizar.Parameters.AddWithValue("@stock_actual", item.cantidad);
        actualizar.Parameters.AddWithValue("@id", productoId);
        await actualizar.ExecuteNonQueryAsync();
        productosActualizados++;
    }

    await TrySyncSheets(db, sheets);

    return Results.Ok(new
    {
        ok = true,
        version = "V34_CORTESIA_MESERA",
        message = "Stock inicial cargado con las cantidades visibles en las capturas del inventario.",
        sucursalId,
        productosActualizados,
        faltantes,
        nota = "Solo se modifica stock_actual. No se cambian precios, categorías, presentaciones ni comisiones."
    });
}

static (decimal cantidad, string[] aliases)[] StockInicialReferenciaV32() => new (decimal cantidad, string[] aliases)[]
{
                (2m, new[] { "RON ABUELO", "ABUELO" }),
                (13m, new[] { "AGUA 2 LITROS", "AGUA 2L" }),
                (12m, new[] { "AGUA PERSONAL CON GAS", "AGUA CON GAS" }),
                (17m, new[] { "AGUA PERSONAL SIN GAS", "AGUA SIN GAS" }),
                (11m, new[] { "AGUA TONICA" }),
                (5m, new[] { "ALIKAL" }),
                (0m, new[] { "BELDEN" }),
                (44m, new[] { "BICO SABORES" }),
                (13m, new[] { "BLACK" }),
                (22m, new[] { "CERVEZA AMSTEL" }),
                (0m, new[] { "CERVEZA CONTI" }),
                (49m, new[] { "CERVEZA CORONA" }),
                (152m, new[] { "CERVEZA PACEÑA" }),
                (48m, new[] { "CERVEZA SKUL", "CERVEZA SKOL" }),
                (18m, new[] { "CICLON" }),
                (10m, new[] { "CIGARRO BOHEM DOUBLE GRANDE" }),
                (18m, new[] { "CIGARRO BOHEM UND" }),
                (0m, new[] { "CIGARRO BOHEN BLACK" }),
                (0m, new[] { "CIGARRO BOHEN SANDIA" }),
                (20m, new[] { "CIGARRO BOHEN UNIDAD" }),
                (5m, new[] { "CIGARRO BOHEN YOGOURT" }),
                (31m, new[] { "CIGARRO CAMEL ACTIVA UNID" }),
                (1m, new[] { "CIGARRO CAMEL ATIVO CHICO" }),
                (9m, new[] { "CIGARRO CAMEL GRANDE ACTIVA" }),
                (27m, new[] { "CIGARRO CAMEL SANDI UNIDAD" }),
                (11m, new[] { "CIGARRO CAMEL SANDIA CHICO" }),
                (11m, new[] { "CIGARRO CAMEL SANDIA GRANDE" }),
                (6m, new[] { "CIGARRO HILLS SANDI" }),
                (0m, new[] { "CIGARRO HILS" }),
                (16m, new[] { "CINCERO", "CENICERO" }),
                (89m, new[] { "CLORETS" }),
                (0m, new[] { "COCA EL BRUJO BICO STEVIA" }),
                (0m, new[] { "COCA EL BRUJO CHICLE", "COCA EL BRUJO CHILE" }),
                (3m, new[] { "COCA EL BRUJO MARACUYA" }),
                (3m, new[] { "COCA EL BRUJO MEDUSA" }),
                (2m, new[] { "COCA EL BRUJO RED BULL" }),
                (8m, new[] { "COCA EL BRUJO SANDIA RED BULL" }),
                (13m, new[] { "COCA EL BRUJO YOGOURT RED BULL" }),
                (0m, new[] { "COCA EL BRUJO YOGUBOLL" }),
                (6m, new[] { "COMBO FERNET" }),
                (10m, new[] { "COMBO FLOR DE CAÑA" }),
                (10m, new[] { "COMBO GIN" }),
                (10m, new[] { "COMBO HABANA" }),
                (15m, new[] { "COPAS DE VINO" }),
                (4m, new[] { "DOCILE MINTY" }),
                (5m, new[] { "ENCENDEDOR" }),
                (3m, new[] { "FERNET" }),
                (4m, new[] { "FLOW ACHACHAIRU" }),
                (0m, new[] { "FLOW CHUFLAY" }),
                (11m, new[] { "FLOW SIN AZUCAR" }),
                (19m, new[] { "FOUR LOCO" }),
                (2m, new[] { "GIN ROSADO" }),
                (63m, new[] { "GROSSO" }),
                (13m, new[] { "HALLS" }),
                (24m, new[] { "ICE 51" }),
                (8m, new[] { "NACHO NORMAL" }),
                (9m, new[] { "NACHOS PICANTES" }),
                (8m, new[] { "NOCHE ICE" }),
                (12m, new[] { "NACHO MAX QUESO" }),
                (10m, new[] { "PAPAS NORMALES" }),
                (0m, new[] { "PAPAS PICANTES" }),
                (28m, new[] { "PASTILLAS EUCALIPTO" }),
                (12m, new[] { "PASTILLAS MINT" }),
                (12m, new[] { "POWER CHICO" }),
                (6m, new[] { "POWER GRANDE" }),
                (20m, new[] { "PROMO AMSTEL" }),
                (30m, new[] { "PROMO AMSTEL X 3" }),
                (20m, new[] { "PROMO CONTI" }),
                (30m, new[] { "PROMO CONTI X 3" }),
                (20m, new[] { "PROMO CORONA" }),
                (50m, new[] { "PROMO PACEÑA" }),
                (17m, new[] { "PROMO VASO DE FERNET" }),
                (6m, new[] { "PAPA NAX" }),
                (8m, new[] { "PIZONES CHOCOLATE" }),
                (10m, new[] { "PIZONES PICANTES" }),
                (2m, new[] { "PLATANITO CHIPS" }),
                (1m, new[] { "QUISQUE BLACK LABEL", "WHIKY BLACK LABEL" }),
                (23m, new[] { "RED BULL" }),
                (1m, new[] { "RON DE COCO OLD" }),
                (6m, new[] { "RON FLOR DE CAÑA" }),
                (3m, new[] { "RON HABANA 7 AÑOS" }),
                (7m, new[] { "SANTE GRANDE" }),
                (7m, new[] { "SANTE PEQUEÑO" }),
                (10m, new[] { "SODA COCA COLA 2 LITROS" }),
                (7m, new[] { "SODA COCA COLA 3 LITROS" }),
                (13m, new[] { "SODA FANTA 2 LITROS" }),
                (13m, new[] { "SODA PEQUE COCA COLA VARIOS" }),
                (10m, new[] { "SODA SPRITE 2 LITROS" }),
                (7m, new[] { "TAKIS" }),
                (2m, new[] { "TEQUILA JOSE CUERVO" }),
                (51m, new[] { "VASO DE FERNET + COCA COLA 2L E 3L" }),
                (10m, new[] { "VASO DE RON" }),
                (10m, new[] { "VASO DE WISKIE", "VASO DE WHISKY" }),
                (4m, new[] { "VASO TEQUILERO" }),
                (37m, new[] { "VASOS CERVECEROS" }),
                (20m, new[] { "VASOS DE SODA" }),
                (4m, new[] { "VINO BLANCO" }),
                (17m, new[] { "VINO TINTO" }),
                (64m, new[] { "CHICLE" }),
                (15m, new[] { "CHICLE GRANDE" }),
                (30m, new[] { "CHICLE PEQUEÑO" }),
                (1m, new[] { "CHUPETE" }),
                (0m, new[] { "MABEL" }),
                (4m, new[] { "MIX NAX" }),
};


static (decimal cantidad, string[] aliases)[] StockSoloTxtV30() => new (decimal cantidad, string[] aliases)[]
{
    (9m, new[] { "AGUA 2 LITROS", "AGUA 2L" }),
    (11m, new[] { "AGUA PERSONAL CON GAS", "AGUA CON GAS" }),
    (21m, new[] { "AGUA PERSONAL SIN GAS", "AGUA SIN GAS" }),
    (11m, new[] { "AGUA TONICA" }),
    (13m, new[] { "SANTE GRANDE" }),
    (7m, new[] { "SANTE PEQUEÑO" }),
    (7m, new[] { "BLACK" }),
    (14m, new[] { "CICLON" }),
    (5m, new[] { "POWER CHICO" }),
    (4m, new[] { "POWER GRANDE" }),
    (22m, new[] { "RED BULL" }),
    (4m, new[] { "SODA COCA COLA 2 LITROS", "COCA COLA 2L" }),
    (5m, new[] { "SODA COCA COLA 3 LITROS", "COCA COLA 3L" }),
    (12m, new[] { "SODA FANTA 2 LITROS", "FANTA 2L" }),
    (36m, new[] { "SODA PEQUE COCA COLA VARIOS", "PEQUE" }),
    (15m, new[] { "SODA SPRITE 2 LITROS", "SPRITE 2L" }),
    (5m, new[] { "COCA AMAIRE N" }),
    (15m, new[] { "COCA EL BRUJO BICO STEVIA", "COCA BICO ESTEBIA" }),
    (9m, new[] { "COCA EL BRUJO MARACUYA", "COCA MARACUYA" }),
    (15m, new[] { "COCA EL BRUJO MEDUSA", "COCA MEDUSA" }),
    (28m, new[] { "COCA MEDUSA N" }),
    (8m, new[] { "COCA EL BRUJO RED BULL", "COCA REDBUL" }),
    (3m, new[] { "COCA EL BRUJO SANDIA RED BULL", "COCA SANDIA REDBUL" }),
    (8m, new[] { "COCA EL BRUJO YOGUBOLL", "COCA YOGUBOL" }),
    (4m, new[] { "COCA YOGUBOL N" }),
    (8m, new[] { "COCA EL BRUJO YOGOURT RED BULL", "COCA YOGURT REDBUL" }),
    (10m, new[] { "CIGARRO BOHEN BLACK", "BOHEM BLACK" }),
    (16m, new[] { "CIGARRO BOHEN SANDIA", "BOHEM SANDIA" }),
    (0m, new[] { "CIGARRO BOHEN YOGOURT", "BOHEM YOGURT" }),
    (9m, new[] { "CIGARRO CAMEL ACTIVA UNID", "CAMEL ACTIVA" }),
    (1m, new[] { "CIGARRO CAMEL ATIVO CHICO", "CAMEL CHICO ACTIVA" }),
    (43m, new[] { "CIGARRO CAMEL SANDIA CHICO", "CAMEL CHICO SANDIA" }),
    (13m, new[] { "CIGARRO CAMEL SANDIA GRANDE", "CAMEL SANDIA" }),
    (10m, new[] { "CIGARRO HILS", "HILLS" }),
    (10m, new[] { "CIGARRO HILLS SANDI", "HILLS SANDIA" }),
    (1m, new[] { "CERVEZA AMSTEL" }),
    (16m, new[] { "CERVEZA CORONA" }),
    (44m, new[] { "CERVEZA SKUL", "CERVEZA SKOL" }),
    (1m, new[] { "AMARULA" }),
    (2m, new[] { "FERNET" }),
    (15m, new[] { "RON FLOR DE CAÑA", "FLOR DE CAÑA" }),
    (22m, new[] { "FLOW ACHACHAIRU" }),
    (12m, new[] { "FLOW CHUFLAY" }),
    (12m, new[] { "FLOW NENE" }),
    (17m, new[] { "FOUR LOCO" }),
    (2m, new[] { "GIN ROSADO", "GIN" }),
    (3m, new[] { "RON HABANA 7 AÑOS", "HAVANA" }),
    (20m, new[] { "ICE 51" }),
    (1m, new[] { "NOCHE ICE" }),
    (0m, new[] { "RON DE COCO OLD", "OLD" }),
    (2m, new[] { "RON ABUELO", "ABUELO" }),
    (2m, new[] { "TEQUILA JOSE CUERVO", "TEQUILA" }),
    (4m, new[] { "VINO BLANCO" }),
    (14m, new[] { "VINO TINTO" }),
    (1m, new[] { "QUISQUE BLACK LABEL", "WHIKY BLACK LABEL" }),
    (2m, new[] { "COMBO FERNET" }),
    (3m, new[] { "COMBO FLOR DE CAÑA" }),
    (2m, new[] { "COMBO RON ABUELO" }),
    (10m, new[] { "VASO CHUFLAY" }),
    (10m, new[] { "VASO FERNET" }),
    (10m, new[] { "VASO FLOR DE CAÑA" }),
    (10m, new[] { "VASO RUM/RON ABUELO" }),
    (10m, new[] { "VASO TEQUILA" }),
    (10m, new[] { "VASO VINO" }),
    (10m, new[] { "VASO VINO tinto" }),
    (10m, new[] { "VASO WHISKY" }),
    (0m, new[] { "NACHO LIMON" }),
    (8m, new[] { "NACHO NORMAL" }),
    (7m, new[] { "NACHOS PICANTES", "NACHO PICANTE" }),
    (10m, new[] { "NACHO MAX QUESO", "NACHO SABOR QUESO" }),
    (13m, new[] { "PAPA CHURRAZCO" }),
    (4m, new[] { "PAPA NAX", "PAPA NAX NORMALES" }),
    (4m, new[] { "PIZONES CHOCOLATE", "PINZONES CHOCOLATE" }),
    (9m, new[] { "PIZONES PICANTES", "PIZONES PICANTE" }),
    (5m, new[] { "PLATANITO CHIPS", "PLATANITO" }),
    (0m, new[] { "TAKIS" }),
    (0m, new[] { "ARCOR" }),
    (0m, new[] { "BELDEN" }),
    (44m, new[] { "CHICLE" }),
    (11m, new[] { "CHICLE GRANDE" }),
    (10m, new[] { "CHICLE PEQUEÑO" }),
    (0m, new[] { "CHUPETE" }),
    (73m, new[] { "CLORETS" }),
    (14m, new[] { "COCA EL BRUJO CHICLE", "COCA CHICLE" }),
    (4m, new[] { "COCA CHICLE N" }),
    (12m, new[] { "PASTILLAS EUCALIPTO", "EUCALIPTO" }),
    (24m, new[] { "GROSSO", "GROSO" }),
    (10m, new[] { "HALLS" }),
    (0m, new[] { "MABEL" }),
    (6m, new[] { "PASTILLAS MINT", "MINT" }),
    (4m, new[] { "DOCILE MINTY", "MINTY" }),
    (2m, new[] { "ALIKAL" }),
    (15m, new[] { "COPAS DE VINO", "COPA" }),
    (14m, new[] { "MESAS" }),
    (30m, new[] { "SILLAS" }),
    (10m, new[] { "VASO DE WISKIE", "VASOS DE WISKI" }),
    (4m, new[] { "VASO TEQUILERO", "VASOS TEQUILERO" }),
    (86m, new[] { "VICO" }),
};

static (string nombre, string categoria, decimal cantidad, decimal precio, bool sinLimiteStock)[] CatalogoFinalV29() => new (string nombre, string categoria, decimal cantidad, decimal precio, bool sinLimiteStock)[]
{
    ("AGUA 2L", "Agua", 9m, 20.00m, false),
    ("AGUA CON GAS", "Agua", 11m, 10.00m, false),
    ("AGUA SIN GAS", "Agua", 21m, 10.00m, false),
    ("AGUA TONICA", "Agua", 11m, 20.00m, false),
    ("SANTE GRANDE", "Agua", 13m, 25.00m, false),
    ("SANTE PEQUEÑO", "Agua", 7m, 18.00m, false),
    ("BLACK", "Energizantes", 7m, 20.00m, false),
    ("CICLON", "Energizantes", 14m, 20.00m, false),
    ("POWER CHICO", "Energizantes", 5m, 15.00m, false),
    ("POWER GRANDE", "Energizantes", 4m, 25.00m, false),
    ("RED BULL", "Energizantes", 22m, 30.00m, false),
    ("COCA COLA 2L", "Sodas", 4m, 25.00m, false),
    ("COCA COLA 3L", "Sodas", 5m, 30.00m, false),
    ("FANTA 2L", "Sodas", 12m, 25.00m, false),
    ("PEQUE", "Sodas", 36m, 6.00m, false),
    ("SPRITE 2L", "Sodas", 15m, 25.00m, false),
    ("COCA AMAIRE N", "Coca machucada", 5m, 55.00m, false),
    ("COCA BICO ESTEBIA", "Coca machucada", 15m, 55.00m, false),
    ("COCA MARACUYA", "Coca machucada", 9m, 55.00m, false),
    ("COCA MEDUSA", "Coca machucada", 15m, 65.00m, false),
    ("COCA MEDUSA N", "Coca machucada", 28m, 65.00m, false),
    ("COCA REDBUL", "Coca machucada", 8m, 55.00m, false),
    ("COCA SANDIA REDBUL", "Coca machucada", 3m, 55.00m, false),
    ("COCA YOGUBOL", "Coca machucada", 8m, 55.00m, false),
    ("COCA YOGUBOL N", "Coca machucada", 4m, 55.00m, false),
    ("COCA YOGURT REDBUL", "Coca machucada", 8m, 55.00m, false),
    ("BOHEM BLACK", "Cigarros", 10m, 30.00m, false),
    ("BOHEM SANDIA", "Cigarros", 16m, 25.00m, false),
    ("BOHEM YOGURT", "Cigarros", 0m, 25.00m, false),
    ("CAMEL ACTIVA", "Cigarros", 9m, 2.00m, false),
    ("CAMEL CHICO ACTIVA", "Cigarros", 1m, 18.00m, false),
    ("CAMEL CHICO SANDIA", "Cigarros", 43m, 20.00m, false),
    ("CAMEL SANDIA", "Cigarros", 13m, 30.00m, false),
    ("HILLS", "Cigarros", 10m, 18.00m, false),
    ("HILLS SANDIA", "Cigarros", 10m, 18.00m, false),
    ("CERVEZA AMSTEL", "Cervezas", 1m, 22.00m, false),
    ("CERVEZA CORONA", "Cervezas", 16m, 25.00m, false),
    ("CERVEZA SKOL", "Cervezas", 44m, 10.00m, false),
    ("AMARULA", "Tragos / Botellas", 1m, 50.00m, false),
    ("FERNET", "Tragos / Botellas", 2m, 275.00m, false),
    ("FLOR DE CAÑA", "Tragos / Botellas", 15m, 275.00m, false),
    ("FLOW ACHACHAIRU", "Tragos / Botellas", 22m, 25.00m, false),
    ("FLOW CHUFLAY", "Tragos / Botellas", 12m, 25.00m, false),
    ("FLOW NENE", "Tragos / Botellas", 12m, 25.00m, false),
    ("FOUR LOCO", "Tragos / Botellas", 17m, 70.00m, false),
    ("GIN", "Tragos / Botellas", 2m, 275.00m, false),
    ("HAVANA", "Tragos / Botellas", 3m, 425.00m, false),
    ("ICE 51", "Tragos / Botellas", 20m, 30.00m, false),
    ("NOCHE ICE", "Tragos / Botellas", 1m, 25.00m, false),
    ("OLD", "Tragos / Botellas", 0m, 300.00m, false),
    ("RON ABUELO", "Tragos / Botellas", 2m, 300.00m, false),
    ("TEQUILA", "Tragos / Botellas", 2m, 200.00m, false),
    ("VINO BLANCO", "Tragos / Botellas", 4m, 50.00m, false),
    ("VINO TINTO", "Tragos / Botellas", 14m, 50.00m, false),
    ("WHIKY BLACK LABEL", "Tragos / Botellas", 1m, 800.00m, false),
    ("COMBO FERNET", "Combos / Promos", 2m, 320.00m, false),
    ("COMBO FLOR DE CAÑA", "Combos / Promos", 3m, 310.00m, false),
    ("COMBO RON ABUELO", "Combos / Promos", 2m, 340.00m, false),
    ("VASO CHUFLAY", "Servidos en vaso", 10m, 25.00m, true),
    ("VASO FERNET", "Servidos en vaso", 10m, 25.00m, true),
    ("VASO FLOR DE CAÑA", "Servidos en vaso", 10m, 25.00m, true),
    ("VASO RUM/RON ABUELO", "Servidos en vaso", 10m, 25.00m, true),
    ("VASO TEQUILA", "Servidos en vaso", 10m, 20.00m, true),
    ("VASO VINO", "Servidos en vaso", 10m, 15.00m, true),
    ("VASO VINO tinto", "Servidos en vaso", 10m, 15.00m, true),
    ("VASO WHISKY", "Servidos en vaso", 10m, 35.00m, true),
    ("NACHO LIMON", "Snacks y piqueos", 0m, 5.00m, false),
    ("NACHO NORMAL", "Snacks y piqueos", 8m, 5.00m, false),
    ("NACHO PICANTE", "Snacks y piqueos", 7m, 5.00m, false),
    ("NACHO SABOR QUESO", "Snacks y piqueos", 10m, 5.00m, false),
    ("PAPA CHURRAZCO", "Snacks y piqueos", 13m, 5.00m, false),
    ("PAPA NAX NORMALES", "Snacks y piqueos", 4m, 5.00m, false),
    ("PINZONES CHOCOLATE", "Snacks y piqueos", 4m, 5.00m, false),
    ("PIZONES PICANTE", "Snacks y piqueos", 9m, 5.00m, false),
    ("PLATANITO", "Snacks y piqueos", 5m, 5.00m, false),
    ("TAKIS", "Snacks y piqueos", 0m, 8.00m, false),
    ("ARCOR", "Dulces y golosinas", 0m, 1.00m, false),
    ("BELDEN", "Dulces y golosinas", 0m, 8.00m, false),
    ("CHICLE", "Dulces y golosinas", 44m, 1.00m, false),
    ("CHICLE GRANDE", "Dulces y golosinas", 11m, 4.00m, false),
    ("CHICLE PEQUEÑO", "Dulces y golosinas", 10m, 1.00m, false),
    ("CHUPETE", "Dulces y golosinas", 0m, 2.00m, false),
    ("CLORETS", "Dulces y golosinas", 73m, 1.00m, false),
    ("COCA CHICLE", "Dulces y golosinas", 14m, 55.00m, false),
    ("COCA CHICLE N", "Dulces y golosinas", 4m, 55.00m, false),
    ("EUCALIPTO", "Dulces y golosinas", 12m, 0.50m, false),
    ("GROSO", "Dulces y golosinas", 24m, 1.00m, false),
    ("HALLS", "Dulces y golosinas", 10m, 8.00m, false),
    ("MABEL", "Dulces y golosinas", 0m, 6.00m, false),
    ("MINT", "Dulces y golosinas", 6m, 0.50m, false),
    ("MINTY", "Dulces y golosinas", 4m, 5.00m, false),
    ("ALIKAL", "Otros / Extras", 2m, 10.00m, false),
    ("COPA", "Otros / Extras", 15m, 10.00m, false),
    ("MESAS", "Otros / Extras", 14m, 0.00m, false),
    ("SILLAS", "Otros / Extras", 30m, 0.00m, false),
    ("VASOS DE WISKI", "Otros / Extras", 10m, 10.00m, false),
    ("VASOS TEQUILERO", "Otros / Extras", 4m, 10.00m, false),
    ("VICO", "Otros / Extras", 86m, 1.00m, false)
};

static (string nombre, string categoria, decimal precio)[] CatalogoProductosLocalV19() => new (string nombre, string categoria, decimal precio)[]
{
    ("AGUA 2 LITROS", "Bebidas", 20.00m),
    ("AGUA PERSONAL CON GAS", "Bebidas", 10.00m),
    ("AGUA PERSONAL SIN GAS", "Bebidas", 10.00m),
    ("AGUA TONICA", "Bebidas", 20.00m),
    ("CICLON", "Bebidas", 20.00m),
    ("COCA EL BRUJO MARACUYA", "Bebidas", 25.00m),
    ("COCA EL BRUJO MEDUSA", "Bebidas", 35.00m),
    ("COCA EL BRUJO RED BULL", "Bebidas", 25.00m),
    ("COCA EL BRUJO SANDIA RED BULL", "Bebidas", 25.00m),
    ("COCA EL BRUJO YOGOURT RED BULL", "Bebidas", 25.00m),
    ("COCA EL BRUJO YOGUBOLL", "Bebidas", 25.00m),
    ("FLOW ACHACHAIRU", "Bebidas", 25.00m),
    ("FLOW CHUFLAY", "Bebidas", 25.00m),
    ("FLOW SIN AZUCAR", "Bebidas", 25.00m),
    ("POWER CHICO", "Bebidas", 18.00m),
    ("POWER GRANDE", "Bebidas", 25.00m),
    ("RED BULL", "Bebidas", 30.00m),
    ("SODA COCA COLA 2 LITROS", "Bebidas", 25.00m),
    ("SODA COCA COLA 3 LITROS", "Bebidas", 30.00m),
    ("SODA FANTA 2 LITROS", "Bebidas", 25.00m),
    ("SODA PEQUE COCA COLA VARIOS", "Bebidas", 6.00m),
    ("SODA SPRITE 2 LITROS", "Bebidas", 25.00m),
    ("VASOS DE SODA", "Bebidas", 10.00m),
    ("CERVEZA AMSTEL", "Cervezas", 22.00m),
    ("CERVEZA CONTI", "Cervezas", 20.00m),
    ("CERVEZA CORONA", "Cervezas", 25.00m),
    ("CERVEZA PACEÑA", "Cervezas", 30.00m),
    ("CERVEZA SKUL", "Cervezas", 10.00m),
    ("BLACK", "Botellas/Tragos", 20.00m),
    ("FERNET", "Botellas/Tragos", 275.00m),
    ("FOUR LOCO", "Botellas/Tragos", 70.00m),
    ("GIN ROSADO", "Botellas/Tragos", 275.00m),
    ("ICE 51", "Botellas/Tragos", 30.00m),
    ("NOCHE ICE", "Botellas/Tragos", 25.00m),
    ("QUISQUE BLACK LABEL", "Botellas/Tragos", 800.00m),
    ("RON ABUELO", "Botellas/Tragos", 300.00m),
    ("RON DE COCO OLD", "Botellas/Tragos", 300.00m),
    ("RON FLOR DE CAÑA", "Botellas/Tragos", 275.00m),
    ("RON HABANA 7 AÑOS", "Botellas/Tragos", 425.00m),
    ("TEQUILA JOSE CUERVO", "Botellas/Tragos", 200.00m),
    ("VASO DE FERNET + COCA COLA 2L E 3L", "Botellas/Tragos", 20.00m),
    ("VASO DE RON", "Botellas/Tragos", 20.00m),
    ("VASO DE WISKIE", "Botellas/Tragos", 10.00m),
    ("VINO BLANCO", "Botellas/Tragos", 50.00m),
    ("VINO TINTO", "Botellas/Tragos", 50.00m),
    ("CIGARRO BOHEM DOUBLE GRANDE", "Cigarros", 30.00m),
    ("CIGARRO BOHEM UND", "Cigarros", 2.00m),
    ("CIGARRO BOHEN BLACK", "Cigarros", 30.00m),
    ("CIGARRO BOHEN SANDIA", "Cigarros", 25.00m),
    ("CIGARRO BOHEN UNIDAD", "Cigarros", 2.00m),
    ("CIGARRO BOHEN YOGOURT", "Cigarros", 25.00m),
    ("CIGARRO CAMEL ACTIVA UNID", "Cigarros", 2.00m),
    ("CIGARRO CAMEL ATIVO CHICO", "Cigarros", 18.00m),
    ("CIGARRO CAMEL GRANDE ACTIVA", "Cigarros", 30.00m),
    ("CIGARRO CAMEL SANDI UNIDAD", "Cigarros", 2.00m),
    ("CIGARRO CAMEL SANDIA CHICO", "Cigarros", 20.00m),
    ("CIGARRO CAMEL SANDIA GRANDE", "Cigarros", 30.00m),
    ("CIGARRO HILLS SANDI", "Cigarros", 18.00m),
    ("CIGARRO HILS", "Cigarros", 18.00m),
    ("BICO SABORES", "Dulces", 5.00m),
    ("CHICLE", "Dulces", 1.00m),
    ("CHICLE GRANDE", "Dulces", 4.00m),
    ("CHICLE PEQUEÑO", "Dulces", 1.00m),
    ("CHUPETE", "Dulces", 2.00m),
    ("CLORETS", "Dulces", 1.00m),
    ("COCA EL BRUJO BICO STEVIA", "Dulces", 25.00m),
    ("COCA EL BRUJO CHICLE", "Dulces", 25.00m),
    ("DOCILE MINTY", "Dulces", 5.00m),
    ("GROSSO", "Dulces", 1.00m),
    ("HALLS", "Dulces", 8.00m),
    ("MABEL", "Dulces", 6.00m),
    ("PASTILLAS EUCALIPTO", "Dulces", 0.50m),
    ("PASTILLAS MINT", "Dulces", 0.50m),
    ("MIX NAX", "Snacks", 8.00m),
    ("NACHO MAX QUESO", "Snacks", 5.00m),
    ("NACHO NORMAL", "Snacks", 5.00m),
    ("NACHOS PICANTES", "Snacks", 5.00m),
    ("PAPA NAX", "Snacks", 5.00m),
    ("PAPAS NORMALES", "Snacks", 5.00m),
    ("PAPAS PICANTES", "Snacks", 5.00m),
    ("PIZONES CHOCOLATE", "Snacks", 5.00m),
    ("PIZONES PICANTES", "Snacks", 5.00m),
    ("PLATANITO CHIPS", "Snacks", 5.00m),
    ("SANTE GRANDE", "Snacks", 25.00m),
    ("SANTE PEQUEÑO", "Snacks", 18.00m),
    ("TAKIS", "Snacks", 8.00m),
    ("CINCERO", "Vasos/Accesorios", 10.00m),
    ("COPAS DE VINO", "Vasos/Accesorios", 10.00m),
    ("ENCENDEDOR", "Vasos/Accesorios", 3.00m),
    ("VASO TEQUILERO", "Vasos/Accesorios", 10.00m),
    ("VASOS CERVECEROS", "Vasos/Accesorios", 10.00m),
    ("ALIKAL", "Varios", 10.00m),
    ("BELDEN", "Varios", 8.00m)
};

static (string nombre, string categoria, decimal precio, string detalle)[] CatalogoCombosPromosLocalV19() => new (string nombre, string categoria, decimal precio, string detalle)[]
{
    ("COMBO FERNET", "Combo", 300.00m, "1x FERNET + 1x SODA COCA COLA 2 LITROS"),
    ("COMBO FLOR DE CAÑA", "Combo", 300.00m, "1x RON FLOR DE CAÑA + 1x SODA COCA COLA 2 LITROS"),
    ("COMBO GIN", "Combo", 300.00m, "1x SANTE GRANDE + 1x GIN ROSADO"),
    ("COMBO HABANA", "Combo", 450.00m, "1x RON HABANA 7 AÑOS + 1x SODA COCA COLA 2 LITROS"),
    ("PROMO AMSTEL", "Promoción", 100.00m, "5x CERVEZA AMSTEL"),
    ("PROMO AMSTEL X 3", "Promoción", 60.00m, "3x CERVEZA AMSTEL"),
    ("PROMO CONTI", "Promoción", 100.00m, "5x CERVEZA CONTI"),
    ("PROMO CONTI X 3", "Promoción", 60.00m, "3x CERVEZA CONTI"),
    ("PROMO CORONA", "Promoción", 110.00m, "5x CERVEZA CORONA"),
    ("PROMO PACEÑA", "Promoción", 120.00m, "5x CERVEZA PACEÑA"),
    ("PROMO VASO DE FERNET", "Promoción", 15.00m, "1x VASO DE FERNET")
};

static async Task<bool> UpsertCatalogoFinalV29(MySqlConnection con, int sucursalId, string nombre, string categoria, decimal cantidad, decimal precio, bool sinLimiteStock)
{
    categoria = NormalizarCategoriaProducto(categoria, nombre);
    long productoId = 0;

    await using (var buscar = new MySqlCommand("SELECT id FROM productos WHERE sucursal_id = @sucursal_id AND nombre = @nombre LIMIT 1;", con))
    {
        buscar.Parameters.AddWithValue("@sucursal_id", sucursalId);
        buscar.Parameters.AddWithValue("@nombre", nombre);
        object? found = await buscar.ExecuteScalarAsync();
        if (found != null) productoId = Convert.ToInt64(found);
    }

    bool existed = productoId > 0;
    decimal stockActual = sinLimiteStock ? 0 : cantidad;
    decimal stockMinimo = sinLimiteStock ? 0 : 30;
    string unidadBase = sinLimiteStock ? "SIN LÍMITE" : "UNIDAD";

    if (!existed)
    {
        await using var cmd = new MySqlCommand("""
            INSERT INTO productos
                (sucursal_id, nombre, categoria, unidad_base, stock_actual, stock_minimo, estado, genera_comision, tipo_comision, valor_comision, sin_limite_stock)
            VALUES
                (@sucursal_id, @nombre, @categoria, @unidad_base, @stock_actual, @stock_minimo, 'ACTIVO', @genera_comision, @tipo_comision, @valor_comision, @sin_limite_stock);
            SELECT LAST_INSERT_ID();
        """, con);
        cmd.Parameters.AddWithValue("@sucursal_id", sucursalId);
        cmd.Parameters.AddWithValue("@nombre", nombre);
        cmd.Parameters.AddWithValue("@categoria", categoria);
        cmd.Parameters.AddWithValue("@unidad_base", unidadBase);
        cmd.Parameters.AddWithValue("@stock_actual", stockActual);
        cmd.Parameters.AddWithValue("@stock_minimo", stockMinimo);
        cmd.Parameters.AddWithValue("@genera_comision", EsProductoConComision(nombre));
        cmd.Parameters.AddWithValue("@tipo_comision", EsProductoConComision(nombre) ? "PORCENTAJE" : "NINGUNA");
        cmd.Parameters.AddWithValue("@valor_comision", EsProductoConComision(nombre) ? 10 : 0);
        cmd.Parameters.AddWithValue("@sin_limite_stock", sinLimiteStock ? 1 : 0);
        productoId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }
    else
    {
        await using var cmd = new MySqlCommand("""
            UPDATE productos
            SET categoria = @categoria,
                unidad_base = @unidad_base,
                stock_actual = @stock_actual,
                stock_minimo = @stock_minimo,
                estado = 'ACTIVO',
                sin_limite_stock = @sin_limite_stock
            WHERE id = @id;
        """, con);
        cmd.Parameters.AddWithValue("@categoria", categoria);
        cmd.Parameters.AddWithValue("@unidad_base", unidadBase);
        cmd.Parameters.AddWithValue("@stock_actual", stockActual);
        cmd.Parameters.AddWithValue("@stock_minimo", stockMinimo);
        cmd.Parameters.AddWithValue("@sin_limite_stock", sinLimiteStock ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", productoId);
        await cmd.ExecuteNonQueryAsync();
    }

    long presId = 0;
    await using (var buscarPres = new MySqlCommand("SELECT id FROM presentaciones WHERE producto_id = @producto_id AND estado = 'ACTIVO' LIMIT 1;", con))
    {
        buscarPres.Parameters.AddWithValue("@producto_id", productoId);
        object? foundPres = await buscarPres.ExecuteScalarAsync();
        if (foundPres != null) presId = Convert.ToInt64(foundPres);
    }

    if (presId <= 0)
    {
        await using var cmd = new MySqlCommand("""
            INSERT INTO presentaciones (producto_id, nombre, cantidad_base, precio_venta, estado)
            VALUES (@producto_id, 'Unidad', 1, @precio_venta, 'ACTIVO');
        """, con);
        cmd.Parameters.AddWithValue("@producto_id", productoId);
        cmd.Parameters.AddWithValue("@precio_venta", precio);
        await cmd.ExecuteNonQueryAsync();
    }
    else
    {
        await using var cmd = new MySqlCommand("""
            UPDATE presentaciones
            SET nombre = 'Unidad',
                cantidad_base = 1,
                precio_venta = @precio_venta,
                estado = 'ACTIVO'
            WHERE id = @id;
        """, con);
        cmd.Parameters.AddWithValue("@precio_venta", precio);
        cmd.Parameters.AddWithValue("@id", presId);
        await cmd.ExecuteNonQueryAsync();
    }

    return existed;
}

static async Task<bool> UpsertCatalogoProductoLocal(MySqlConnection con, int sucursalId, string nombre, string categoria, decimal precio, string presentacion, int minimo)
{
    categoria = NormalizarCategoriaProducto(categoria, nombre);
    long productoId = 0;

    await using (var buscar = new MySqlCommand("SELECT id FROM productos WHERE sucursal_id = @sucursal_id AND nombre = @nombre LIMIT 1;", con))
    {
        buscar.Parameters.AddWithValue("@sucursal_id", sucursalId);
        buscar.Parameters.AddWithValue("@nombre", nombre);
        object? found = await buscar.ExecuteScalarAsync();
        if (found != null) productoId = Convert.ToInt64(found);
    }

    bool existed = productoId > 0;

    if (!existed)
    {
        await using var cmd = new MySqlCommand("""
            INSERT INTO productos
                (sucursal_id, nombre, categoria, unidad_base, stock_actual, stock_minimo, estado, genera_comision, tipo_comision, valor_comision)
            VALUES
                (@sucursal_id, @nombre, @categoria, 'UNIDAD', 0, @minimo, 'ACTIVO', @genera_comision, @tipo_comision, @valor_comision);
            SELECT LAST_INSERT_ID();
        """, con);
        cmd.Parameters.AddWithValue("@sucursal_id", sucursalId);
        cmd.Parameters.AddWithValue("@nombre", nombre);
        cmd.Parameters.AddWithValue("@categoria", categoria);
        cmd.Parameters.AddWithValue("@minimo", minimo);
        cmd.Parameters.AddWithValue("@genera_comision", EsProductoConComision(nombre));
        cmd.Parameters.AddWithValue("@tipo_comision", EsProductoConComision(nombre) ? "PORCENTAJE" : "NINGUNA");
        cmd.Parameters.AddWithValue("@valor_comision", EsProductoConComision(nombre) ? 10 : 0);
        productoId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }
    else
    {
        await using var cmd = new MySqlCommand("""
            UPDATE productos
            SET categoria = @categoria,
                unidad_base = 'UNIDAD',
                stock_minimo = @minimo,
                estado = 'ACTIVO',
                genera_comision = @genera_comision,
                tipo_comision = @tipo_comision,
                valor_comision = @valor_comision
            WHERE id = @id;
        """, con);
        cmd.Parameters.AddWithValue("@categoria", categoria);
        cmd.Parameters.AddWithValue("@minimo", minimo);
        cmd.Parameters.AddWithValue("@genera_comision", EsProductoConComision(nombre));
        cmd.Parameters.AddWithValue("@tipo_comision", EsProductoConComision(nombre) ? "PORCENTAJE" : "NINGUNA");
        cmd.Parameters.AddWithValue("@valor_comision", EsProductoConComision(nombre) ? 10 : 0);
        cmd.Parameters.AddWithValue("@id", productoId);
        await cmd.ExecuteNonQueryAsync();
    }

    long presId = 0;
    await using (var buscarPres = new MySqlCommand("SELECT id FROM presentaciones WHERE producto_id = @producto_id AND estado = 'ACTIVO' LIMIT 1;", con))
    {
        buscarPres.Parameters.AddWithValue("@producto_id", productoId);
        object? foundPres = await buscarPres.ExecuteScalarAsync();
        if (foundPres != null) presId = Convert.ToInt64(foundPres);
    }

    if (presId <= 0)
    {
        await using var cmd = new MySqlCommand("""
            INSERT INTO presentaciones (producto_id, nombre, cantidad_base, precio_venta, estado)
            VALUES (@producto_id, @nombre, 1, @precio_venta, 'ACTIVO');
        """, con);
        cmd.Parameters.AddWithValue("@producto_id", productoId);
        cmd.Parameters.AddWithValue("@nombre", presentacion);
        cmd.Parameters.AddWithValue("@precio_venta", precio);
        await cmd.ExecuteNonQueryAsync();
    }
    else
    {
        await using var cmd = new MySqlCommand("""
            UPDATE presentaciones
            SET nombre = @nombre,
                cantidad_base = 1,
                precio_venta = @precio_venta,
                estado = 'ACTIVO'
            WHERE id = @id;
        """, con);
        cmd.Parameters.AddWithValue("@nombre", presentacion);
        cmd.Parameters.AddWithValue("@precio_venta", precio);
        cmd.Parameters.AddWithValue("@id", presId);
        await cmd.ExecuteNonQueryAsync();
    }

    return existed;
}

static bool EsProductoConComision(string nombre)
{
    string n = (nombre ?? "").ToUpperInvariant();
    return n.Contains("RON") || n.Contains("TEQUILA") || n.Contains("GIN") || n.Contains("FERNET") || n.Contains("WHISK") || n.Contains("WISKIE") || n.Contains("ABUELO") || n.Contains("HABANA") || n.Contains("BLACK LABEL");
}


static async Task UpdateUserPasswordHash(MySqlConnection con, int userId, string plainPassword)
{
    await using var cmd = new MySqlCommand("UPDATE usuarios SET clave = @clave WHERE id = @id;", con);
    cmd.Parameters.AddWithValue("@clave", PasswordHasher.Hash(plainPassword));
    cmd.Parameters.AddWithValue("@id", userId);
    await cmd.ExecuteNonQueryAsync();
}

static async Task HashPlainUserPasswords(MySqlConnection con)
{
    var pendientes = new List<(int id, string clave)>();

    await using (var cmd = new MySqlCommand("SELECT id, clave FROM usuarios;", con))
    await using (var rd = await cmd.ExecuteReaderAsync())
    {
        while (await rd.ReadAsync())
        {
            string clave = rd.IsDBNull(rd.GetOrdinal("clave")) ? "" : rd.GetString("clave");
            if (!PasswordHasher.IsHashed(clave))
                pendientes.Add((rd.GetInt32("id"), clave));
        }
    }

    foreach (var item in pendientes)
    {
        await using var update = new MySqlCommand("UPDATE usuarios SET clave = @clave WHERE id = @id;", con);
        update.Parameters.AddWithValue("@clave", PasswordHasher.Hash(item.clave));
        update.Parameters.AddWithValue("@id", item.id);
        await update.ExecuteNonQueryAsync();
    }
}

static async Task EnsureUserManagementTables(MySqlConnection con)
{
    await using (var alterClave = new MySqlCommand("ALTER TABLE usuarios MODIFY COLUMN clave VARCHAR(255) NOT NULL;", con))
    {
        try { await alterClave.ExecuteNonQueryAsync(); } catch { }
    }

    await using (var cmd = new MySqlCommand("ALTER TABLE usuarios ADD COLUMN nombre_completo VARCHAR(180) NULL;", con))
    {
        try { await cmd.ExecuteNonQueryAsync(); } catch { }
    }

    await using (var cmd = new MySqlCommand("ALTER TABLE usuarios ADD COLUMN caja_nombre VARCHAR(60) NULL;", con))
    {
        try { await cmd.ExecuteNonQueryAsync(); } catch { }
    }

    await using (var cmd = new MySqlCommand("ALTER TABLE usuarios ADD COLUMN turno VARCHAR(20) NOT NULL DEFAULT 'MAÑANA';", con))
    {
        try { await cmd.ExecuteNonQueryAsync(); } catch { }
    }

    await using (var seed = new MySqlCommand("""
        INSERT INTO usuarios (usuario, clave, rol, sucursal_id, estado, nombre_completo, caja_nombre, turno)
        SELECT 'admin', 'ElBrujo2026SI', 'ADMINISTRADOR', 1, 'ACTIVO', 'Administrador', 'ADMIN', 'MAÑANA'
        WHERE NOT EXISTS (SELECT 1 FROM usuarios WHERE usuario = 'admin');

        INSERT INTO usuarios (usuario, clave, rol, sucursal_id, estado, nombre_completo, caja_nombre, turno)
        SELECT 'caja1', 'BrujoPremiu2026', 'CAJERO', 1, 'ACTIVO', 'Caja Sucursal 1', 'CAJA 1', 'MAÑANA'
        WHERE NOT EXISTS (SELECT 1 FROM usuarios WHERE usuario = 'caja1');

        INSERT INTO usuarios (usuario, clave, rol, sucursal_id, estado, nombre_completo, caja_nombre, turno)
        SELECT 'caja2', 'BrujoPRO2026', 'CAJERO', 2, 'ACTIVO', 'Caja Sucursal 2', 'CAJA 1', 'MAÑANA'
        WHERE NOT EXISTS (SELECT 1 FROM usuarios WHERE usuario = 'caja2');

        INSERT INTO usuarios (usuario, clave, rol, sucursal_id, estado, nombre_completo, caja_nombre, turno)
        SELECT 'caja2_2', 'Caja2Sucursal2', 'CAJERO', 2, 'ACTIVO', 'Caja 2 Sucursal 2', 'CAJA 2', 'NOCHE'
        WHERE NOT EXISTS (SELECT 1 FROM usuarios WHERE usuario = 'caja2_2');

        INSERT INTO usuarios (usuario, clave, rol, sucursal_id, estado, nombre_completo, caja_nombre, turno)
        SELECT 'ana_mesera', 'mesera123', 'MESERA', 1, 'ACTIVO', 'Ana Mesera', '', 'MAÑANA'
        WHERE NOT EXISTS (SELECT 1 FROM usuarios WHERE usuario = 'ana_mesera');

        INSERT INTO usuarios (usuario, clave, rol, sucursal_id, estado, nombre_completo, caja_nombre, turno)
        SELECT 'rosa_mesera', 'mesera123', 'MESERA', 2, 'ACTIVO', 'Rosa Mesera', '', 'NOCHE'
        WHERE NOT EXISTS (SELECT 1 FROM usuarios WHERE usuario = 'rosa_mesera');
    """, con))
    {
        try { await seed.ExecuteNonQueryAsync(); } catch { }
    }

    await HashPlainUserPasswords(con);
}

static string NormalizarRol(string? rol)
{
    string r = (rol ?? "").Trim().ToUpperInvariant();
    if (r.Contains("ADMIN")) return "ADMINISTRADOR";
    if (r.Contains("CAJ")) return "CAJERO";
    if (r.Contains("MESER")) return "MESERA";
    return string.IsNullOrWhiteSpace(r) ? "CAJERO" : r;
}

static string NormalizarTurno(string? turno)
{
    string t = (turno ?? "").Trim().ToUpperInvariant();
    if (t.Contains("NOCHE")) return "NOCHE";
    return "MAÑANA";
}

static string NormalizarCategoriaProducto(string? categoria, string? nombre)
{
    string c = (categoria ?? "").Trim().ToUpperInvariant();
    string n = (nombre ?? "").Trim().ToUpperInvariant();

    if (c.Contains("CENICER") || c.Contains("CINCER") || n.Contains("CENICER") || n.Contains("CINCERO")) return "Ceniceros";
    if (c.Contains("ACCESORIO") || n == "ENCENDEDOR" || n == "COPAS DE VINO" || n == "VASO TEQUILERO" || n == "VASOS CERVECEROS" || n == "VASOS DE SODA" || n == "VASOS DE WISKI" || n == "VASOS DE WHISKY" || n == "VASO DE WISKI" || n == "VASO DE WISKIE" || n == "VASO DE WHISKY") return "Accesorios";
    if (c.Contains("SERVIDOS EN VASO") || c.Contains("LO QUE SE SIRVE EN VASO") || (n.StartsWith("VASO ") && n != "VASO TEQUILERO")) return "Servidos en vaso";
    if (c == "COCAS" || c.Contains("COCA MACHUCADA") || n.StartsWith("COCA EL BRUJO") || n.StartsWith("COCA AMAIRE") || n.StartsWith("COCA BICO") || n.StartsWith("COCA MARACUYA") || n.StartsWith("COCA MEDUSA") || n.StartsWith("COCA RED") || n.StartsWith("COCA SANDIA") || n.StartsWith("COCA YOG")) return "Cocas";
    if (c.Contains("COMBO") || c.Contains("PROMO") || n.Contains("COMBO") || n.StartsWith("PROMO ")) return "Combos / Promos";
    if (c == "AGUA" || n.Contains("AGUA") || n.StartsWith("SANTE ")) return "Agua";
    if (c.Contains("ENERGIZANTE") || n.Contains("RED BULL") || n.Contains("CICLON") || n.Contains("POWER") || n == "BLACK") return "Energizantes";
    if (c.Contains("SODA") || n.StartsWith("SODA ") || n.Contains("SPRITE") || n.Contains("FANTA")) return "Sodas";
    if (c.Contains("CERVEZA") || c == "CERVEZAS" || n.StartsWith("CERVEZA ") || n.Contains("PACEÑA") || n.Contains("CONTI") || n.Contains("CORONA") || n.Contains("SKOL") || n.Contains("SKUL") || n.Contains("AMSTEL")) return "Cervezas";
    if (c.Contains("CIGARRO") || n.Contains("CIGARRO") || n.Contains("CAMEL") || n.Contains("BOHEM") || n.Contains("BOHEN") || n.Contains("HILLS")) return "Cigarros";
    if (c.Contains("SNACK") || c.Contains("PIQUEO") || n.Contains("NACHO") || n.Contains("PAPA") || n.Contains("PIZON") || n.Contains("PINZON") || n.Contains("PLATANITO") || n.Contains("TAKIS") || n.Contains("MIX NAX")) return "Snacks y piqueos";
    if (c.Contains("DULCE") || c.Contains("GOLOSINA") || n.Contains("CHICLE") || n.Contains("CLORETS") || n.Contains("BELDEN") || n.Contains("ARCOR") || n.Contains("HALLS") || n.Contains("MABEL") || n.Contains("GROSO") || n.Contains("MINT") || n.Contains("CHUPETE") || n.Contains("EUCALIPTO") || n.Contains("BICO SABORES")) return "Dulces y golosinas";
    if (c.Contains("TRAGO") || c.Contains("BOTELLA") || n.Contains("RON") || n.Contains("FERNET") || n.Contains("GIN") || n.Contains("TEQUILA") || n.Contains("WHIKY") || n.Contains("WHISK") || n.Contains("WISK") || n.Contains("VINO") || n.Contains("AMARULA") || n.Contains("FLOR DE CAÑA") || n.Contains("FOUR LOCO") || n.Contains("FLOW") || n.Contains("HAVANA") || n.Contains("HABANA") || n.Contains("ICE 51") || n.Contains("NOCHE ICE") || n.Contains("OLD")) return "Tragos / Botellas";
    return "Otros";
}

static async Task EnsureAppMeseraTables(MySqlConnection con)
{
    await using (var alter1 = new MySqlCommand("ALTER TABLE productos ADD COLUMN genera_comision TINYINT(1) NOT NULL DEFAULT 0;", con))
    {
        try { await alter1.ExecuteNonQueryAsync(); } catch { }
    }

    await using (var alter2 = new MySqlCommand("ALTER TABLE productos ADD COLUMN tipo_comision VARCHAR(30) NOT NULL DEFAULT 'NINGUNA';", con))
    {
        try { await alter2.ExecuteNonQueryAsync(); } catch { }
    }

    await using (var alter3 = new MySqlCommand("ALTER TABLE productos ADD COLUMN valor_comision DECIMAL(10,2) NOT NULL DEFAULT 0;", con))
    {
        try { await alter3.ExecuteNonQueryAsync(); } catch { }
    }

    await using (var alter4 = new MySqlCommand("ALTER TABLE productos ADD COLUMN sin_limite_stock TINYINT(1) NOT NULL DEFAULT 0;", con))
    {
        try { await alter4.ExecuteNonQueryAsync(); } catch { }
    }

    await using (var alter5 = new MySqlCommand("ALTER TABLE productos ADD COLUMN tipo_entrada VARCHAR(60) NOT NULL DEFAULT 'PAQUETE';", con))
    {
        try { await alter5.ExecuteNonQueryAsync(); } catch { }
    }

    await using (var alter6 = new MySqlCommand("ALTER TABLE productos ADD COLUMN unidades_por_entrada INT NOT NULL DEFAULT 1;", con))
    {
        try { await alter6.ExecuteNonQueryAsync(); } catch { }
    }

    await using (var alter7 = new MySqlCommand("ALTER TABLE productos ADD COLUMN precio_compra DECIMAL(10,2) NOT NULL DEFAULT 0;", con))
    {
        try { await alter7.ExecuteNonQueryAsync(); } catch { }
    }

    // Migración de nombres de categoría solicitados para el módulo Productos / Stock.
    await using (var catMig = new MySqlCommand("""
        UPDATE productos SET categoria = 'Cocas' WHERE categoria = 'Coca machucada';
        UPDATE productos SET categoria = 'Otros' WHERE categoria IN ('Varios', 'Otros / Extras');
        UPDATE productos SET categoria = 'Accesorios' WHERE categoria = 'Vasos/Accesorios';
        UPDATE productos SET categoria = 'Ceniceros'
        WHERE UPPER(nombre) LIKE '%CENICER%' OR UPPER(nombre) LIKE '%CINCERO%';
        UPDATE productos SET categoria = 'Accesorios'
        WHERE UPPER(nombre) IN ('ENCENDEDOR','COPAS DE VINO','VASO TEQUILERO','VASOS CERVECEROS','VASOS DE SODA','VASOS DE WISKI','VASOS DE WHISKY','VASO DE WISKI','VASO DE WISKIE','VASO DE WHISKY');
    """, con))
    {
        try { await catMig.ExecuteNonQueryAsync(); } catch { }
    }

    await using (var cmd = new MySqlCommand("""
        CREATE TABLE IF NOT EXISTS pedidos_movil (
            id BIGINT AUTO_INCREMENT PRIMARY KEY,
            sucursal_id INT NOT NULL,
            mesa_id INT NOT NULL,
            mesa VARCHAR(100) NOT NULL,
            mesera_usuario VARCHAR(100) NOT NULL,
            mesera_nombre VARCHAR(150) NOT NULL,
            cajero_usuario VARCHAR(100) NULL,
            fecha DATETIME NOT NULL,
            fecha_respuesta DATETIME NULL,
            estado VARCHAR(30) NOT NULL DEFAULT 'PENDIENTE',
            total DECIMAL(10,2) NOT NULL DEFAULT 0,
            observacion VARCHAR(250) NULL,
            sync_key VARCHAR(180) NOT NULL,
            UNIQUE KEY uk_pedidos_movil_sync (sync_key),
            INDEX idx_pedidos_movil_sucursal_estado (sucursal_id, estado),
            INDEX idx_pedidos_movil_mesera (mesera_usuario)
        );
    """, con))
    {
        await cmd.ExecuteNonQueryAsync();
    }

    await using (var cmd = new MySqlCommand("""
        CREATE TABLE IF NOT EXISTS detalle_pedidos_movil (
            id BIGINT AUTO_INCREMENT PRIMARY KEY,
            pedido_id BIGINT NOT NULL,
            producto_id BIGINT NOT NULL,
            presentacion_id BIGINT NOT NULL,
            producto VARCHAR(180) NOT NULL,
            presentacion VARCHAR(120) NOT NULL,
            cantidad DECIMAL(10,2) NOT NULL DEFAULT 0,
            precio_unitario DECIMAL(10,2) NOT NULL DEFAULT 0,
            subtotal DECIMAL(10,2) NOT NULL DEFAULT 0,
            genera_comision TINYINT(1) NOT NULL DEFAULT 0,
            tipo_comision VARCHAR(30) NOT NULL DEFAULT 'NINGUNA',
            valor_comision DECIMAL(10,2) NOT NULL DEFAULT 0,
            comision_calculada DECIMAL(10,2) NOT NULL DEFAULT 0,
            INDEX idx_detalle_pedidos_movil_pedido (pedido_id)
        );
    """, con))
    {
        await cmd.ExecuteNonQueryAsync();
    }

    await using (var cmd = new MySqlCommand("""
        CREATE TABLE IF NOT EXISTS comisiones_meseras (
            id BIGINT AUTO_INCREMENT PRIMARY KEY,
            pedido_id BIGINT NOT NULL,
            sucursal_id INT NOT NULL,
            mesa_id INT NOT NULL,
            mesera_usuario VARCHAR(100) NOT NULL,
            mesera_nombre VARCHAR(150) NOT NULL,
            fecha DATETIME NOT NULL,
            producto VARCHAR(180) NOT NULL,
            cantidad DECIMAL(10,2) NOT NULL DEFAULT 0,
            venta_total DECIMAL(10,2) NOT NULL DEFAULT 0,
            comision_total DECIMAL(10,2) NOT NULL DEFAULT 0,
            estado VARCHAR(30) NOT NULL DEFAULT 'PENDIENTE_PAGO',
            UNIQUE KEY uk_comision_pedido (pedido_id, producto),
            INDEX idx_comisiones_meseras_fecha (fecha),
            INDEX idx_comisiones_meseras_mesera (mesera_usuario)
        );
    """, con))
    {
        await cmd.ExecuteNonQueryAsync();
    }

    await using (var cmd = new MySqlCommand("""
        CREATE TABLE IF NOT EXISTS reportes_productos_movil (
            id BIGINT AUTO_INCREMENT PRIMARY KEY,
            sucursal_id INT NOT NULL,
            turno VARCHAR(30) NOT NULL DEFAULT 'MAÑANA',
            usuario VARCHAR(100) NOT NULL,
            nombre VARCHAR(150) NOT NULL,
            fecha DATETIME NOT NULL,
            producto_id BIGINT NULL,
            presentacion_id BIGINT NULL,
            producto VARCHAR(180) NOT NULL,
            presentacion VARCHAR(120) NULL,
            cantidad DECIMAL(10,2) NOT NULL DEFAULT 0,
            precio_unitario DECIMAL(10,2) NOT NULL DEFAULT 0,
            costo_perdido DECIMAL(10,2) NOT NULL DEFAULT 0,
            motivo VARCHAR(80) NOT NULL,
            observacion VARCHAR(250) NULL,
            sync_key VARCHAR(180) NOT NULL,
            UNIQUE KEY uk_reporte_producto_sync (sync_key),
            INDEX idx_reporte_producto_fecha (fecha),
            INDEX idx_reporte_producto_sucursal (sucursal_id)
        );
    """, con))
    {
        await cmd.ExecuteNonQueryAsync();
    }

    await using (var seed = new MySqlCommand("""
        INSERT INTO usuarios (usuario, clave, rol, sucursal_id, estado)
        SELECT 'ana_mesera', 'mesera123', 'MESERA', 1, 'ACTIVO'
        WHERE NOT EXISTS (SELECT 1 FROM usuarios WHERE usuario = 'ana_mesera');

        INSERT INTO usuarios (usuario, clave, rol, sucursal_id, estado)
        SELECT 'rosa_mesera', 'mesera123', 'MESERA', 2, 'ACTIVO'
        WHERE NOT EXISTS (SELECT 1 FROM usuarios WHERE usuario = 'rosa_mesera');
    """, con))
    {
        try { await seed.ExecuteNonQueryAsync(); } catch { }
    }
}


static async Task EnsureShiftCloseTables(MySqlConnection con)
{
    await using var cmd = new MySqlCommand("""
        CREATE TABLE IF NOT EXISTS cierres_turno (
            id BIGINT AUTO_INCREMENT PRIMARY KEY,
            sucursal_id INT NOT NULL,
            sucursal VARCHAR(120) NOT NULL,
            cajero_usuario VARCHAR(100) NOT NULL,
            cajero_nombre VARCHAR(150) NOT NULL,
            caja VARCHAR(100) NULL,
            turno VARCHAR(30) NOT NULL,
            inicio DATETIME NOT NULL,
            fin DATETIME NOT NULL,
            hora_entrada DATETIME NOT NULL,
            fecha_cierre DATETIME NOT NULL,
            transacciones_total INT NOT NULL DEFAULT 0,
            transacciones_efectivo INT NOT NULL DEFAULT 0,
            transacciones_qr INT NOT NULL DEFAULT 0,
            transacciones_tarjeta INT NOT NULL DEFAULT 0,
            transacciones_transferencia INT NOT NULL DEFAULT 0,
            efectivo DECIMAL(12,2) NOT NULL DEFAULT 0,
            qr DECIMAL(12,2) NOT NULL DEFAULT 0,
            tarjeta DECIMAL(12,2) NOT NULL DEFAULT 0,
            transferencia DECIMAL(12,2) NOT NULL DEFAULT 0,
            sin_metodo DECIMAL(12,2) NOT NULL DEFAULT 0,
            productos_total DECIMAL(12,2) NOT NULL DEFAULT 0,
            mesas_total DECIMAL(12,2) NOT NULL DEFAULT 0,
            minutos_jugados INT NOT NULL DEFAULT 0,
            propinas_total DECIMAL(12,2) NOT NULL DEFAULT 0,
            cortesias_valor DECIMAL(12,2) NOT NULL DEFAULT 0,
            comisiones_total DECIMAL(12,2) NOT NULL DEFAULT 0,
            gastos_total DECIMAL(12,2) NOT NULL DEFAULT 0,
            perdidas_total DECIMAL(12,2) NOT NULL DEFAULT 0,
            total_generado DECIMAL(12,2) NOT NULL DEFAULT 0,
            neto_turno DECIMAL(12,2) NOT NULL DEFAULT 0,
            observaciones TEXT NULL,
            detalle_json LONGTEXT NOT NULL,
            sync_key VARCHAR(220) NOT NULL,
            UNIQUE KEY uk_cierre_turno_sync (sync_key),
            INDEX idx_cierre_turno_fecha (fecha_cierre),
            INDEX idx_cierre_turno_sucursal (sucursal_id),
            INDEX idx_cierre_turno_cajero (cajero_usuario)
        );
    """, con);
    await cmd.ExecuteNonQueryAsync();
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

static class PasswordHasher
{
    const int Iterations = 100000;
    const int SaltSize = 16;
    const int KeySize = 32;
    const string Prefix = "PBKDF2$";

    public static bool IsHashed(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
    }

    public static string Hash(string password)
    {
        password ??= "";
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        byte[] key = pbkdf2.GetBytes(KeySize);
        return Prefix + Iterations + "$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(key);
    }

    public static bool Verify(string password, string stored)
    {
        password ??= "";
        stored ??= "";

        if (!IsHashed(stored))
            return stored == password;

        string[] parts = stored.Split('$');
        if (parts.Length != 4) return false;
        if (!int.TryParse(parts[1], out int iterations)) return false;

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch
        {
            return false;
        }

        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        byte[] actual = pbkdf2.GetBytes(expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
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

        List<List<object>> productosPerdidosMovil = new()
        {
            new() { "fecha", "hora", "sucursal", "turno", "producto", "presentacion", "cantidad", "precio_unitario", "costo_perdido", "registrado_por", "motivo", "observacion" }
        };
        productosPerdidosMovil.AddRange((await db.QueryAsync(con, """
            SELECT r.fecha,
                   CASE WHEN s.id = 2 THEN 'SEGUNDA SUCURSAL' ELSE 'PRIMERA SUCURSAL' END AS sucursal,
                   r.turno, r.producto, r.presentacion, r.cantidad, r.precio_unitario,
                   r.costo_perdido, r.nombre, r.motivo, r.observacion
            FROM reportes_productos_movil r
            LEFT JOIN sucursales s ON s.id = r.sucursal_id
            ORDER BY r.fecha DESC;
        """)).Select(r => new List<object>
        {
            DateOnlyText(r, "fecha"),
            DateTime.TryParse(Text(r, "fecha"), out var horaReporte) ? horaReporte.ToString("HH:mm:ss") : "",
            Text(r, "sucursal"),
            Text(r, "turno"),
            Text(r, "producto"),
            Text(r, "presentacion"),
            Val(r, "cantidad"),
            Val(r, "precio_unitario"),
            Val(r, "costo_perdido"),
            Text(r, "nombre"),
            Text(r, "motivo"),
            Text(r, "observacion")
        }));

        List<List<object>> gananciaNegocio = new()
        {
            new() { "fecha", "sucursal", "productos_vendidos", "uso_y_cobro_mesas", "total_ingreso", "ayuda_comida", "uso_interno", "perdidas", "neto_para_revisar" }
        };
        gananciaNegocio.AddRange(resumen.Skip(1).Select(r => new List<object> { r[0], r[1], r[2], r[3], r[4], 0, 0, 0, r[4] }));

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
        await ReplaceSheetAsync(service, "Productos_Perdidos_Danados", productosPerdidosMovil);
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


    private async Task InitSheetIfEmptyAsync(SheetsService service, string sheetName, List<object> headers)
    {
        string escaped = "'" + sheetName.Replace("'", "''") + "'!A1:Z2";
        var get = service.Spreadsheets.Values.Get(_sheetId, escaped);
        var existing = await get.ExecuteAsync();

        if (existing.Values != null && existing.Values.Count > 0 && existing.Values[0].Count > 0)
            return;

        List<IList<object>> values = new List<IList<object>>
        {
            headers,
            headers.Select(_ => (object)"").ToList()
        };

        var body = new Google.Apis.Sheets.v4.Data.ValueRange { Values = values };
        var update = service.Spreadsheets.Values.Update(body, _sheetId, "'" + sheetName.Replace("'", "''") + "'!A1");
        update.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
        await update.ExecuteAsync();
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

public record ProductReportRequest(
    int SucursalId,
    string Turno,
    string Usuario,
    string Nombre,
    long ProductoId,
    long PresentacionId,
    string Producto,
    string Presentacion,
    decimal Cantidad,
    decimal PrecioUnitario,
    string Motivo,
    string Observacion,
    string SyncKey
);

public record ProductCommissionRequest(
    int SucursalId,
    string Nombre,
    bool GeneraComision,
    string TipoComision,
    decimal ValorComision
);

public sealed record AdminProductPresentationRequest(
    long? PresentacionId,
    string Nombre,
    int CantidadBase,
    decimal PrecioVenta,
    string Estado
);

public sealed record AdminProductRequest(
    long? ProductoId,
    int SucursalId,
    string Nombre,
    string Categoria,
    string TipoEntrada,
    string UnidadBase,
    int UnidadesPorEntrada,
    decimal PrecioCompra,
    decimal StockActual,
    decimal StockMinimo,
    bool SinLimiteStock,
    bool GeneraComision,
    string TipoComision,
    decimal ValorComision,
    string Estado,
    List<AdminProductPresentationRequest>? Presentaciones
);


public sealed record ShiftCloseRequest(
    int SucursalId,
    string? Sucursal,
    string? CajeroUsuario,
    string? CajeroNombre,
    string? Caja,
    string? Turno,
    DateTime Inicio,
    DateTime Fin,
    DateTime HoraEntrada,
    DateTime FechaCierre,
    int TransaccionesTotal,
    int TransaccionesEfectivo,
    int TransaccionesQr,
    int TransaccionesTarjeta,
    int TransaccionesTransferencia,
    decimal Efectivo,
    decimal Qr,
    decimal Tarjeta,
    decimal Transferencia,
    decimal SinMetodo,
    decimal ProductosTotal,
    decimal MesasTotal,
    int MinutosJugados,
    decimal PropinasTotal,
    decimal CortesiasValor,
    decimal ComisionesTotal,
    decimal GastosTotal,
    decimal PerdidasTotal,
    decimal TotalGenerado,
    decimal NetoTurno,
    string? Observaciones,
    string? DetalleJson,
    string? SyncKey
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


public sealed record AppPedidoMovilRequest(
    int SucursalId,
    int MesaId,
    string Mesa,
    string MeseraUsuario,
    string MeseraNombre,
    long ProductoId,
    long PresentacionId,
    string Producto,
    string Presentacion,
    decimal Cantidad,
    decimal PrecioUnitario,
    bool GeneraComision,
    string TipoComision,
    decimal ValorComision,
    string? Observacion,
    string? SyncKey
);

public sealed record PedidoEstadoRequest(
    string Estado,
    string? CajeroUsuario
);


public record AdminUserRequest(
    string Usuario,
    string Clave,
    string NombreCompleto,
    string Rol,
    int SucursalId,
    string CajaNombre,
    string Turno,
    string Estado
);

public record UserEstadoRequest(string Estado);
