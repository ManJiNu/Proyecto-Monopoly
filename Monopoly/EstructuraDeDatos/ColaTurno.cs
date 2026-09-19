//Lista Enlazada Simple Circular
class ColaTurno
{
    public NodoTurno CabezaNodo {get;set;}
    public NodoTurno ColaNodo {get;set;}
    public NodoTurno TurnoActual {get;set;}
    public ColaTurno()
    {
        CabezaNodo = null; //head
        ColaNodo = null; //cola
        TurnoActual = CabezaNodo;
    }

    // Agrega un nuevo jugador al final del turno
    public void AgregarJugador(Jugador jugador)
    {
        NodoTurno JugadorNuevo = new NodoTurno(jugador);
        if(CabezaNodo == null)
        {
            CabezaNodo = JugadorNuevo;
            ColaNodo = JugadorNuevo;
            ColaNodo.Siguiente = CabezaNodo;
            TurnoActual = JugadorNuevo;
        }
        else
        {
            ColaNodo.Siguiente = JugadorNuevo;
            ColaNodo = JugadorNuevo;
            ColaNodo.Siguiente = CabezaNodo;
        }
    }
    public void AvanzarTurno()
    {
        TurnoActual = TurnoActual.Siguiente;
    }
}