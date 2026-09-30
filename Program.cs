int puerto = 5000;
if (args.Length > 0 && int.TryParse(args[0], out int puertoArgumento))
    puerto = puertoArgumento;

// Por ahora usamos el tablero de prueba para levantar el servidor.
ListaTablero tablero = FabricaTableroDemo.Crear();

Servidor servidor = new Servidor(
    puerto: puerto,
    saldoInicial: 15000,
    premioPorInicio: 2000,
    maximoTurnos: 100,
    tablero: tablero);

await servidor.IniciarAsync();
