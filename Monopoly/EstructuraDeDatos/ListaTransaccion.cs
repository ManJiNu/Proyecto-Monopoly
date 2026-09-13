//Lista Doblemente Enlazada
//Esta lista esta consiste en el historial de Transacciones
public class ListaTransacciones
{
    public NodoTransaccion CabezaNodo { get; set; }
    public NodoTransaccion ColaNodo { get; set; }

    public ListaTransacciones()
    {
        CabezaNodo = null;
        ColaNodo = null;
    }

    // Agregar una transacción nueva al final
    public void AgregarTransaccion(Transaccion transaccion)
    {
        NodoTransaccion nuevoNodo = new NodoTransaccion(transaccion);
        if (CabezaNodo == null)
        {
            CabezaNodo = nuevoNodo;
            ColaNodo = nuevoNodo;
        }
        else
        {
            nuevoNodo.Anterior = ColaNodo;
            ColaNodo.Siguiente = nuevoNodo;
            ColaNodo = nuevoNodo;
        }
    }

    // Imprime recorriendo desde la transacción más antigua hacia la más reciente
    public void ImprimirDesdeAntigua()
    {
        NodoTransaccion actual = CabezaNodo;
        while (actual != null)
        {
            Console.WriteLine(actual.TransaccionActual.Descripcion);
            actual = actual.Siguiente;
        }
    }

    // Imprime recorriendo desde la transacción más reciente hacia la más antigua
    public void ImprimirDesdeReciente()
    {
        NodoTransaccion actual = ColaNodo;
        while (actual != null)
        {
            Console.WriteLine(actual.TransaccionActual.Descripcion);
            actual = actual.Anterior;
        }
    }

    // Busca todas las transacciones donde el jugador participó (como origen o destino)
    public ListaTransacciones BuscarPorJugador(Jugador jugador)
    {
        ListaTransacciones resultado = new ListaTransacciones();
        NodoTransaccion actual = CabezaNodo;
        while (actual != null)
        {
            if (actual.TransaccionActual.JugadorOrigen == jugador || actual.TransaccionActual.JugadorDestino == jugador)
            {
                resultado.AgregarTransaccion(actual.TransaccionActual);
            }
            actual = actual.Siguiente;
        }
        return resultado;
}

    // Busca todas las transacciones de un tipo específico
    public ListaTransacciones BuscarPorTipo(string tipo)
    {
        ListaTransacciones resultado = new ListaTransacciones();
        NodoTransaccion actual = CabezaNodo;
        while (actual != null)
        {
            if (actual.TransaccionActual.Tipo == tipo)
            {
                resultado.AgregarTransaccion(actual.TransaccionActual);
            }
            actual = actual.Siguiente;
        }
        return resultado;
}
}