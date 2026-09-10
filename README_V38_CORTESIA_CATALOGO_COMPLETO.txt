BILLAR EL BRUJO API V38 - CORTESÍA CATÁLOGO COMPLETO

CAMBIOS
- /api/app-mesera/productos continúa devolviendo todos los productos ACTIVOS de la sucursal y sus presentaciones activas.
- CORTESÍA ya no está limitada a categorías de tragos/botellas.
- Se permite cualquier producto ACTIVO que exista realmente en el catálogo de Railway.
- El precio de cortesía SIEMPRE se obtiene del catálogo en el servidor; la app no puede inventar el precio.
- Si el producto no existe, está inactivo o no tiene precio de venta mayor a 0, la API rechaza el pedido.
- Comisión de cortesía: Bs. 5 por unidad.

USAR CON APP MESERA V14. Compatible con programa V99.
/health debe mostrar V38_CORTESIA_CATALOGO_COMPLETO.
