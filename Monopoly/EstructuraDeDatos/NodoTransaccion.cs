public class NodoTransaccion
{
    public Transaccion TransaccionActual { get; set; }
    public NodoTransaccion Anterior { get; set; }
    public NodoTransaccion Siguiente { get; set; }

    public NodoTransaccion(Transaccion transaccion)
    {
        TransaccionActual = transaccion;
        Anterior = null;
        Siguiente = null;
    }
}