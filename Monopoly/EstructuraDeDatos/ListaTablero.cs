// Lista Circular Doblemente Enlazada
public class ListaTablero
{
    public NodoTablero CabezaNodo { get; set; }
    public NodoTablero ColaNodo { get; set; }
    public ListaTablero(){
        CabezaNodo = null; //head
        ColaNodo = null; //tail
    }

    // Agrega la Propiedad al final de la lista
    public void AgregarCasilla(Casilla casilla)
    {
        NodoTablero NuevaCasilla = new NodoTablero(casilla);
        if(CabezaNodo == null)
        {
            CabezaNodo = NuevaCasilla;
            ColaNodo = NuevaCasilla;
            CabezaNodo.Siguiente = CabezaNodo;
            ColaNodo.Anterior = CabezaNodo;
        }
        else
        {
            NuevaCasilla.Anterior = ColaNodo;
            ColaNodo.Siguiente = NuevaCasilla;
            ColaNodo = NuevaCasilla;
            ColaNodo.Siguiente = CabezaNodo;
            CabezaNodo.Anterior = ColaNodo;
        }
    }

    // Arma el tablero de 24 casillas que usa la interfaz gráfica, con ciudades
    // de Costa Rica agrupadas de a pares por color (como en el Monopoly real).
    // Las esquinas (0, 6, 12, 18) son casillas especiales.
    public static ListaTablero ConstruirTableroPredeterminado()
    {
        ListaTablero tablero = new ListaTablero();

        tablero.AgregarCasilla(new CasillaEspecial(0, "Salida"));
        tablero.AgregarCasilla(new Propiedad(1, "San José", 120, 25, null) { ColorGrupo = "#8B5E3C" });
        tablero.AgregarCasilla(new Propiedad(2, "Cartago", 120, 25, null) { ColorGrupo = "#8B5E3C" });
        tablero.AgregarCasilla(new CasillaEvento(3, "Evento"));
        tablero.AgregarCasilla(new Propiedad(4, "Heredia", 160, 30, null) { ColorGrupo = "#8EC9E0" });
        tablero.AgregarCasilla(new Propiedad(5, "Alajuela", 160, 30, null) { ColorGrupo = "#8EC9E0" });

        tablero.AgregarCasilla(new CasillaEspecial(6, "Cárcel (De visita)"));
        tablero.AgregarCasilla(new Propiedad(7, "Escazú", 200, 38, null) { ColorGrupo = "#D9509A" });
        tablero.AgregarCasilla(new Propiedad(8, "Santa Ana", 200, 38, null) { ColorGrupo = "#D9509A" });
        tablero.AgregarCasilla(new CasillaEvento(9, "Evento"));
        tablero.AgregarCasilla(new Propiedad(10, "Liberia", 240, 45, null) { ColorGrupo = "#F2A03D" });
        tablero.AgregarCasilla(new Propiedad(11, "Guanacaste", 240, 45, null) { ColorGrupo = "#F2A03D" });

        tablero.AgregarCasilla(new CasillaEspecial(12, "Parqueo Gratis"));
        tablero.AgregarCasilla(new Propiedad(13, "Puntarenas", 280, 50, null) { ColorGrupo = "#E15241" });
        tablero.AgregarCasilla(new Propiedad(14, "Jacó", 280, 50, null) { ColorGrupo = "#E15241" });
        tablero.AgregarCasilla(new CasillaEvento(15, "Evento"));
        tablero.AgregarCasilla(new Propiedad(16, "Tamarindo", 320, 55, null) { ColorGrupo = "#F2E23D" });
        tablero.AgregarCasilla(new Propiedad(17, "Monteverde", 320, 55, null) { ColorGrupo = "#F2E23D" });

        tablero.AgregarCasilla(new CasillaEspecial(18, "Ir a la Cárcel"));
        tablero.AgregarCasilla(new Propiedad(19, "Manuel Antonio", 360, 60, null) { ColorGrupo = "#3FA34D" });
        tablero.AgregarCasilla(new Propiedad(20, "Turrialba", 360, 60, null) { ColorGrupo = "#3FA34D" });
        tablero.AgregarCasilla(new CasillaEvento(21, "Evento"));
        tablero.AgregarCasilla(new Propiedad(22, "La Fortuna", 400, 65, null) { ColorGrupo = "#2E5EAA" });
        tablero.AgregarCasilla(new Propiedad(23, "Limón", 400, 65, null) { ColorGrupo = "#2E5EAA" });

        return tablero;
    }
}
