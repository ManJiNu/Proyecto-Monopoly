using System;

public class Dado
{
    private Random generador;

    public int UltimoValor { get; private set; }

    public Dado()
    {
        generador = new Random();
    }

    public int Lanzar()
    {
        UltimoValor = generador.Next(1, 7);
        return UltimoValor;
    }
}