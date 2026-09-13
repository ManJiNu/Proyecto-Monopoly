// Lista cerrada de los tipos de efecto que puede tener una carta de evento.
public enum TipoCarta
{
    RecibirDinero,
    PagarDinero,
    AvanzarPosiciones,
    RetrocederPosiciones,
    PerderTurno,
    IrACasilla
}

public class CartaEvento
{
    public int Id { get; set; }
    public string Descripcion { get; set; }
    public TipoCarta Tipo { get; set; }
    public int Valor { get; set; } // monto a pagar/recibir, o cantidad de posiciones a mover, según el Tipo

    public CartaEvento(int id, string descripcion, TipoCarta tipo, int valor)
    {
        Id = id;
        Descripcion = descripcion;
        Tipo = tipo;
        Valor = valor;
    }
}