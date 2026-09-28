using System.Security.Cryptography.X509Certificates;

public class Jugador
{
    public int Id { get; set; }
    public string Nombre { get; set; }
    public int Saldo { get; private set; }    
    public NodoTablero PosicionActual { get; set; }
    public bool Activo { get; private set; }
    public ListaPropiedad Propiedades { get; set; }
    private Dado dado1;
    private Dado dado2;

    public Jugador(int id, string nombre, int saldoInicial)
    {
        Id = id;
        Nombre = nombre;
        Saldo = saldoInicial;
        PosicionActual = null; // se asigna cuando entra al tablero
        Activo = true;
        Propiedades = new ListaPropiedad();
        dado1 = new Dado();
        dado2 = new Dado();

    }

    //Metodo para moverse dentro del tablero
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

    public void RecibirDinero(int monto)
    {
        Saldo += monto;
    }

    public int LanzarDados()
    {
        int valor1 = dado1.Lanzar();
        int valor2 = dado2.Lanzar();
        int total = valor1 + valor2;
        Mover(total);
        return total;
    }

    //Metodo para cuando el jugador quede eliminado en la partida
    public void Eliminar()
    {
    Activo = false;
    }

}