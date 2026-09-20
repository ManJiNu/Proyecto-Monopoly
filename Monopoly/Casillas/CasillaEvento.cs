public class CasillaEvento : Casilla
{
    private static readonly Random aleatorio = new Random();

    public CasillaEvento(int id, string nombre) : base(id, nombre) { }

    public override void EjecutarEfecto(Jugador jugador)
    {
        // Se "toma una carta" del mazo de eventos: un número aleatorio decide el efecto
        int carta = aleatorio.Next(1, 4); // genera 1, 2 o 3

        switch (carta)
        {
            case 1:
                int premio = 100;
                jugador.RecibirDinero(premio); // Saldo tiene "private set": se cambia con este método
                Console.WriteLine($"{jugador.Nombre} tomó una carta de evento: ¡gana {premio}!");
                break;

            case 2:
                int multa = 50;
                jugador.PagarDinero(multa); // ídem: no se puede hacer jugador.Saldo -= multa directo
                Console.WriteLine($"{jugador.Nombre} tomó una carta de evento: paga una multa de {multa}.");
                break;

            case 3:
                Console.WriteLine($"{jugador.Nombre} tomó una carta de evento: no pasa nada.");
                break;
        }
    }
}
