// Este tablero se usa para probar el servidor mientras se integra el tablero definitivo del equipo.
public static class FabricaTableroDemo
{
    public static ListaTablero Crear()
    {
        ListaTablero tablero = new ListaTablero();
        ColaCartas mazo = CrearMazoDemo();

        tablero.AgregarCasilla(new CasillaEspecial(0, "Salida"));
        tablero.AgregarCasilla(new Propiedad(1, "Avenida Uno", 1200, 150, null));

        CasillaEvento evento1 = new CasillaEvento(2, "Evento");
        evento1.Mazo = mazo;
        tablero.AgregarCasilla(evento1);

        tablero.AgregarCasilla(new Propiedad(3, "Avenida Dos", 1600, 200, null));
        tablero.AgregarCasilla(new CasillaEspecial(4, "Parqueo Gratis"));
        tablero.AgregarCasilla(new Propiedad(5, "Avenida Tres", 2000, 250, null));

        CasillaEvento evento2 = new CasillaEvento(6, "Evento");
        evento2.Mazo = mazo;
        tablero.AgregarCasilla(evento2);

        tablero.AgregarCasilla(new CasillaEspecial(7, "Cárcel (De visita)"));
        tablero.AgregarCasilla(new Propiedad(8, "Avenida Cuatro", 2400, 300, null));
        tablero.AgregarCasilla(new CasillaEspecial(9, "Ir a la Cárcel"));

        return tablero;
    }

    private static ColaCartas CrearMazoDemo()
    {
        ColaCartas mazo = new ColaCartas();
        mazo.AgregarCarta(new CartaEvento(1, "Reciba 500 del Banco", TipoCarta.RecibirDinero, 500));
        mazo.AgregarCarta(new CartaEvento(2, "Pague 300 al Banco", TipoCarta.PagarDinero, 300));
        mazo.AgregarCarta(new CartaEvento(3, "Avance 2 casillas", TipoCarta.AvanzarPosiciones, 2));
        mazo.AgregarCarta(new CartaEvento(4, "Pierda el próximo turno", TipoCarta.PerderTurno, 0));
        return mazo;
    }
}
