using System;

// Representa los dos dados que se lanzan en cada turno.
public class Dado
{
    private static readonly Random aleatorio = new Random();

    public int Dado1 { get; private set; }
    public int Dado2 { get; private set; }

    // Suma de ambos dados: es lo que se usa para mover al jugador
    public int Total => Dado1 + Dado2;

    // true si salieron dobles (mismo número en los dos dados)
    public bool SonDobles => Dado1 == Dado2;

    public int Lanzar()
    {
        Dado1 = aleatorio.Next(1, 7); // 1 a 6
        Dado2 = aleatorio.Next(1, 7);
        return Total;
    }

    // Usa un valor que vino de afuera (por ejemplo, el dado físico
    // conectado a la Raspberry Pi) en vez de generarlo al azar.
    public int Forzar(int valor1, int valor2)
    {
        Dado1 = valor1;
        Dado2 = valor2;
        return Total;
    }
}
