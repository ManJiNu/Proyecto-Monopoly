public class Jugador
{
    public int Id { get; set; }
    public string Nombre { get; set; }
    public int Saldo { get; set; }
    public NodoTablero PosicionActual { get; set; }
    public bool Activo { get; set; }
    public ListaPropiedad Propiedades { get; set; }

    public Jugador(int id, string nombre, int saldoInicial)
    {
        Id = id;
        Nombre = nombre;
        Saldo = saldoInicial;
        PosicionActual = null; // se asigna cuando entra al tablero
        Activo = true;
        Propiedades = new ListaPropiedad();
    }

    public void Mover(int pasos)
    {
        for (int i = 0; i < pasos; i++)
        {
            PosicionActual = PosicionActual.Siguiente;
        }
    }
}