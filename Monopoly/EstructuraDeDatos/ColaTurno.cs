<<<<<<< HEAD
//Cola(Queue)
class ColaTurno
=======
//Lista Enlazada Simple Circular
public class ColaTurno
>>>>>>> 41ad58188a6be1e81df3ab8e0c22626b9e8a70bb
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
