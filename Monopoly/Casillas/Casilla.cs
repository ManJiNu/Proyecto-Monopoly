public abstract class Casilla
{
    public int ID {get; set;}
    public string Nombre {get; set;}
    public Casilla(int id, string nombre)
    {
        ID = id;
        Nombre = nombre;
    }

    // Cada tipo de casilla decide qué pasa cuando un jugador cae aquí
    public abstract void EjecutarEfecto(Jugador jugador);
}
