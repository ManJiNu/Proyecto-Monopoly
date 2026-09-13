public class NodoTurno
{
    public Jugador JugadorDelTurno {get; set;}
    public NodoTurno Siguiente {get; set;}
    public NodoTurno(Jugador turno)
    {
        JugadorDelTurno = turno;
        Siguiente = null;
    }
}