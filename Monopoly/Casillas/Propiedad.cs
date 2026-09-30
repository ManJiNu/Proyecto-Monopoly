public class Propiedad : Casilla
{
    public int PrecioCompra { get; set; }
    public int Alquiler { get; set; }
    public Jugador Propietario { get; set; }
    public string ColorGrupo { get; set; } = "#BDC3C7"; // color del grupo, lo usa la interfaz gráfica

    public Propiedad(int id, string nombre, int preciocompra, int alquiler, Jugador propietario)
        : base(id, nombre)
    {
        PrecioCompra = preciocompra;
        Alquiler = alquiler;
        Propietario = null; // toda propiedad nace sin dueño, sin importar lo que se pase aquí
    }

    public override void EjecutarEfecto(Jugador jugador)
    {
        if (Propietario == null)
        {
            // Nadie la ha comprado todavía: se le ofrece al jugador
            // (la compra en sí se resuelve afuera, en la clase Juego)
            System.Console.WriteLine($"{Nombre} está disponible por {PrecioCompra}.");
        }
        else if (Propietario == jugador)
        {
            // El jugador cayó en su propia propiedad: no paga nada
            System.Console.WriteLine($"{jugador.Nombre} cayó en su propia propiedad ({Nombre}).");
        }
        else
        {
            // Es de otro jugador: debe pagar alquiler, pero el cobro ya NO se
            // hace aquí. Se queda pendiente hasta que el jugador acerque su
            // tarjeta RFID (ver Juego.ResolverCasillaActual / RegistrarTag).
            System.Console.WriteLine($"{jugador.Nombre} debe pagar {Alquiler} de alquiler a {Propietario.Nombre} por {Nombre}. Esperando tarjeta RFID...");
        }
    }
}
