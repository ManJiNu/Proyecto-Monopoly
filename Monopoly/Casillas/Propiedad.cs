public class Propiedad: Casilla
{
    public int PrecioCompra {get;set;}
    public int Alquiler {get;set;}
    public Jugador Propietario {get;set;}
    public Propiedad(int id, string nombre, int preciocompra, int alquiler, Jugador propietario)
        : base(id, nombre)
    {
        PrecioCompra = preciocompra;
        Alquiler = alquiler;
        Propietario = null;

    }
    public override void EjecutarEfecto(Jugador jugador)
    {
        // Aquí va la lógica: disponible / pagar alquiler / mismo dueño
    }

}