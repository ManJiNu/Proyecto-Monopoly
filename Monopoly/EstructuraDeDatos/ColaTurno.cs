//Cola(Queue)
class ColaTurno
{
    public NodoTurno CabezaNodo { get; private set; }
    public NodoTurno ColaNodo { get; private set; }
    public NodoTurno TurnoActual { get; private set; }
    public ColaTurno()
    {
        CabezaNodo = null; //head
        ColaNodo = null; //tail
        TurnoActual = CabezaNodo;
    }

    //Metodo Enqueue: agrega un nuevo jugador al final del turno
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