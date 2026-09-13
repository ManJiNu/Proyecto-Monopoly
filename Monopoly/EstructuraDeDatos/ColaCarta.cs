public class ColaCartas
{
    public NodoCarta CabezaNodo { get; set; }
    public NodoCarta ColaNodo { get; set; }

    public ColaCartas()
    {
        CabezaNodo = null;
        ColaNodo = null;
    }

    // Agrega una carta al final (se usa al armar el mazo inicial)
    public void AgregarCarta(CartaEvento carta)
    {
        NodoCarta nuevoNodo = new NodoCarta(carta);
        if (CabezaNodo == null)
        {
            CabezaNodo = nuevoNodo;
            ColaNodo = nuevoNodo;
            ColaNodo.Siguiente = CabezaNodo; // circular: se apunta a sí mismo
        }
        else
        {
            ColaNodo.Siguiente = nuevoNodo;
            ColaNodo = nuevoNodo;
            ColaNodo.Siguiente = CabezaNodo; // cierra el círculo de nuevo
        }
    }

    // Toma la carta del frente, la mueve al final, y la devuelve para usarla
    public CartaEvento SacarCarta()
    {
        if (CabezaNodo == null)
        {
            return null; // no hay cartas
        }

        CartaEvento cartaTomada = CabezaNodo.CartaActual;

        if (CabezaNodo == ColaNodo)
        {
            // solo hay una carta en la cola, no hay nada que reordenar
            return cartaTomada;
        }

        NodoCarta viejaCabeza = CabezaNodo;
        CabezaNodo = CabezaNodo.Siguiente; // la segunda carta pasa a ser la nueva cabeza
        ColaNodo.Siguiente = viejaCabeza;  // la vieja cabeza se conecta al final
        ColaNodo = viejaCabeza;            // la vieja cabeza ahora es la cola
        ColaNodo.Siguiente = CabezaNodo;   // cierra el círculo de nuevo

        return cartaTomada;
    }
}