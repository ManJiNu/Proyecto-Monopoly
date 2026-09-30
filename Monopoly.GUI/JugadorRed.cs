namespace Monopoly.GUI
{
    // Estado de un jugador tal como lo ve el cliente de red: se arma y se
    // va actualizando a partir de los mensajes que manda el servidor.
    // No es el mismo Jugador que usa Juego.cs en modo local (ese vive en el
    // servidor, dentro de PartidaServidor); esta es solo la "foto" que la
    // interfaz necesita para dibujar el tablero.
    public class JugadorRed
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public int Saldo { get; set; }
        public int CasillaId { get; set; }
        public bool Activo { get; set; } = true;
        public bool EnCarcel { get; set; }
    }
}
