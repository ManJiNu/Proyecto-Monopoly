public class Jugador
{
    public int Id { get; set; }
    public string Nombre { get; set; }
    public int Saldo { get; private set; }
    public NodoTablero PosicionActual { get; set; }
    public bool Activo { get; private set; }
    public bool EstaEnCarcel { get; set; } // lo usa CasillaEspecial ("Ir a la Cárcel")
    public bool DebePerderTurno { get; set; } // lo usa CasillaEvento (carta TipoCarta.PerderTurno)
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
<<<<<<< HEAD

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
=======
}
>>>>>>> 41ad58188a6be1e81df3ab8e0c22626b9e8a70bb
