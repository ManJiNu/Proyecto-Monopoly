using System;

public class Transaccion
{
    public int Id { get; set; }
    public DateTime FechaHora { get; set; }
    public int NumeroTurno { get; set; }
    public string Tipo { get; set; }
    public Jugador JugadorOrigen { get; set; }   // null = banco
    public Jugador JugadorDestino { get; set; }  // null = banco
    public int Monto { get; set; }
    public string Descripcion { get; set; }

    public Transaccion(int id, int numeroTurno, string tipo, Jugador jugadorOrigen,
                        Jugador jugadorDestino, int monto, string descripcion)
    {
        Id = id;
        FechaHora = DateTime.Now;
        NumeroTurno = numeroTurno;
        Tipo = tipo;
        JugadorOrigen = jugadorOrigen;
        JugadorDestino = jugadorDestino;
        Monto = monto;
        Descripcion = descripcion;
    }
}