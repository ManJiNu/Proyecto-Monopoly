public class NodoCarta
{
    public CartaEvento CartaActual { get; set; }
    public NodoCarta Siguiente { get; set; }

    public NodoCarta(CartaEvento carta)
    {
        CartaActual = carta;
        Siguiente = null;
    }
}