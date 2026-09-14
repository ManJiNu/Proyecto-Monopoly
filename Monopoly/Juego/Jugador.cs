using System.Security.Cryptography.X509Certificates;

public class Jugador
{
    public int Id { get; set; }
    public string Nombre { get; set; }
    public int Saldo { get; private set; }    
    public NodoTablero PosicionActual { get; set; }
    public bool Activo { get; private set; }
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
    public bool PagarDinero(int monto)
    {
        if (Saldo < monto)
        {
            return false;
        }
        Saldo -= monto;
        return true;
    }

    public bool RecibirDinero(int monto)
    {
        Saldo += monto;
    }
}