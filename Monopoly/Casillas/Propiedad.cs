public class Propiedad : Casilla
{
    public int PrecioCompra { get; set; }
    public int Alquiler { get; set; }
    public Jugador Propietario { get; set; }

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
            // (la compra en sí se resuelve afuera, por ejemplo en la clase Juego)
            Console.WriteLine($"{Nombre} está disponible por {PrecioCompra}.");
        }
        else if (Propietario == jugador)
        {
            // El jugador cayó en su propia propiedad: no paga nada
            Console.WriteLine($"{jugador.Nombre} cayó en su propia propiedad ({Nombre}).");
        }
        else
        {
            // Es de otro jugador: debe pagar alquiler
            jugador.Saldo -= Alquiler;
            Propietario.Saldo += Alquiler;
            Console.WriteLine($"{jugador.Nombre} pagó {Alquiler} de alquiler a {Propietario.Nombre} por {Nombre}.");
        }
    }
}
