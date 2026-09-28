public class CasillaEvento : Casilla
{
    // El mazo se comparte entre todas las casillas de evento del tablero:
    // se arma una sola vez (con AgregarCarta) y se le asigna a cada CasillaEvento
    // cuando se construye el tablero.
    public ColaCartas Mazo { get; set; }

    public CasillaEvento(int id, string nombre) : base(id, nombre) { }

    public override void EjecutarEfecto(Jugador jugador)
    {
        if (Mazo == null)
        {
            Console.WriteLine($"{jugador.Nombre} cayó en una casilla de evento, pero el mazo de cartas aún no está asignado.");
            return;
        }

        CartaEvento carta = Mazo.SacarCarta();
        if (carta == null)
        {
            Console.WriteLine($"{jugador.Nombre} cayó en una casilla de evento, pero el mazo está vacío.");
            return;
        }

        Console.WriteLine($"{jugador.Nombre} tomó la carta: \"{carta.Descripcion}\"");

        switch (carta.Tipo)
        {
            case TipoCarta.RecibirDinero:
                jugador.RecibirDinero(carta.Valor);
                Console.WriteLine($"{jugador.Nombre} recibe {carta.Valor}.");
                break;

            case TipoCarta.PagarDinero:
                bool pagoExitoso = jugador.PagarDinero(carta.Valor);
                if (pagoExitoso)
                    Console.WriteLine($"{jugador.Nombre} paga {carta.Valor}.");
                else
                    Console.WriteLine($"{jugador.Nombre} no tiene suficiente dinero para pagar {carta.Valor}.");
                break;

            case TipoCarta.AvanzarPosiciones:
                jugador.Mover(carta.Valor); // ya existe en Jugador.cs, avanza usando Siguiente
                Console.WriteLine($"{jugador.Nombre} avanza {carta.Valor} casillas.");
                break;

            case TipoCarta.RetrocederPosiciones:
                // Jugador.Mover solo avanza, así que retrocedemos manualmente
                // usando el enlace Anterior del propio NodoTablero.
                for (int i = 0; i < carta.Valor; i++)
                {
                    jugador.PosicionActual = jugador.PosicionActual.Anterior;
                }
                Console.WriteLine($"{jugador.Nombre} retrocede {carta.Valor} casillas.");
                break;

            case TipoCarta.PerderTurno:
                jugador.DebePerderTurno = true; // ver nota: falta agregar esta propiedad a Jugador
                Console.WriteLine($"{jugador.Nombre} pierde su próximo turno.");
                break;

            case TipoCarta.IrACasilla:
                // Pendiente: para mover al jugador a una casilla específica por ID (carta.Valor)
                // esta clase necesitaría una referencia al Tablero completo para poder buscarla,
                // y hoy CasillaEvento solo conoce su propio ID/Nombre. Falta esa conexión.
                Console.WriteLine($"{jugador.Nombre} debería ir a la casilla {carta.Valor}, pero falta conectar el Tablero a esta casilla.");
                break;
        }
    }
}
