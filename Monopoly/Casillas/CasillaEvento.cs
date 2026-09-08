public class CasillaEvento : Casilla
{
    public CasillaEvento(int id, string nombre) : base(id, nombre) { }

    public override void EjecutarEfecto(Jugador jugador)
    {
        // Aquí se toma una carta del mazo de eventos
    }
}