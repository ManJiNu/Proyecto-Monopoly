public class Jugador
{
    public int Id { get; set; }
    public string Nombre { get; set; }
    public int Saldo { get; private set; }
    public NodoTablero PosicionActual { get; set; }
    public bool Activo { get; private set; }
    public bool EstaEnCarcel { get; set; } // lo usa CasillaEspecial ("Ir a la Cárcel")
    public bool DebePerderTurno { get; set; } // lo usa CasillaEvento (carta TipoCarta.PerderTurno)
    public string TagRFID { get; set; } // código de la tarjeta RFID que identifica a este jugador
    public ListaPropiedad Propiedades { get; set; }

    private Dado dado1;
    private Dado dado2;

    public Jugador(int id, string nombre, int saldoInicial)
    {
        Id = id;
        Nombre = nombre;
        Saldo = saldoInicial;
        PosicionActual = null;
        Activo = true;
        EstaEnCarcel = false;
        DebePerderTurno = false;
        Propiedades = new ListaPropiedad();
        dado1 = new Dado();
        dado2 = new Dado();
    }

    public void Mover(int pasos)
    {
        for (int i = 0; i < pasos; i++)
        {
            PosicionActual = PosicionActual.Siguiente;
        }
    }

    public int LanzarDados()
    {
        int total = dado1.Lanzar() + dado2.Lanzar();
        Mover(total);
        return total;
    }

    public void RecibirDinero(int monto)
    {
        Saldo += monto;
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

    public void Eliminar()
    {
        Activo = false;
    }
}