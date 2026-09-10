# BILLAR EL BRUJO API V39_CORTESIA_A_MESA

Usar esta versión con:
- Programa PC V100_CORTESIA_A_MESA
- App Mesera V15_CORTESIA_A_MESA

## Cortesía
La cortesía debe indicar una mesa en juego. La API valida la mesa y el producto,
usa el precio real del catálogo y registra Bs. 5 de comisión por unidad.
Caja acepta el pedido y el programa PC lo agrega a la cuenta de esa mesa.
El cobro se realiza al cerrar la mesa junto con tiempo y demás consumos.

Después del deploy, `/health` debe mostrar `V39_CORTESIA_A_MESA`.
