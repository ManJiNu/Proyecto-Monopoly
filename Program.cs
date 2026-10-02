int puerto = 5000;
if (args.Length > 0 && int.TryParse(args[0], out int puertoArgumento))
    puerto = puertoArgumento;

// Se usa el tablero definitivo del equipo (24 casillas con nombres de
// ciudades de Costa Rica), el mismo que arma la interfaz gráfica, en vez
// del tablero de prueba de FabricaTableroDemo.
ListaTablero tablero = ListaTablero.ConstruirTableroPredeterminado();

Servidor servidor = new Servidor(
    puerto: puerto,
    saldoInicial: 1000,   // mismo saldo inicial que usa Juego.cs y el tablero real (precios de 120 a 400)
    premioPorInicio: 200, // mismo premio por pasar Salida que usa Juego.cs
    maximoTurnos: 100,
    tablero: tablero);

await servidor.IniciarAsync();
