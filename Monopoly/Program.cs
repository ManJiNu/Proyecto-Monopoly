// Crear jugadores de prueba
Jugador jugador1 = new Jugador(1, "Ana", 15000);
Jugador jugador2 = new Jugador(2, "Luis", 15000);

// Crear el banco
Banco banco = new Banco();

// Simular un pago entre jugador y banco (ganancia por evento)
banco.ProcesarPago(null, jugador1, 5000, "GananciaEvento", 1, "Ana ganó una carta de evento");

// Simular un pago de Luis al banco
banco.ProcesarPago(jugador2, null, 2000, "PagoAlBanco", 2, "Luis pagó una multa");

// Exportar el historial a un archivo TXT
banco.Historial.ExportarTXT("transacciones_prueba.txt");
Console.WriteLine("Archivo generado con éxito.");
