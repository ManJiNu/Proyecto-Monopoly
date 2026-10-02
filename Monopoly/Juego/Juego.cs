using System;

// Coordina una partida completa: arma el tablero, registra jugadores,
// controla los turnos, tira los dados (a mano o desde la Raspberry Pi) y
// aplica lo que corresponda según la casilla donde caiga cada jugador.
public class Juego
{
    public const int SaldoInicial = 1000;
    public const int MaximoJugadores = 4;

    public ListaTablero Tablero { get; private set; }
    public ColaTurno Turnos { get; private set; }
    public Banco Banco { get; private set; }
    public Dado Dado { get; private set; }

    public bool PartidaIniciada { get; private set; }
    public int NumeroTurno { get; private set; }

    // Propiedad que el jugador actual acaba de encontrar disponible;
    // queda pendiente hasta que decida comprarla o no.
    public Propiedad PropiedadPendiente { get; private set; }

    // El jugador actual ya presionó "Comprar": falta que acerque su tarjeta
    // RFID para completar la compra (todavía no se le ha cobrado nada).
    public bool EsperandoConfirmacionCompra { get; private set; }

    // Propiedad de otro jugador en la que se cayó: el alquiler queda
    // pendiente hasta que el jugador que debe pagar acerque su tarjeta RFID.
    public Propiedad AlquilerPendiente { get; private set; }
    public Jugador JugadorQuePagaAlquiler { get; private set; }

    // Jugador que se acaba de registrar y todavía no ha vinculado su
    // tarjeta RFID (se le asigna la próxima tarjeta que se lea).
    private Jugador jugadorEsperandoTag;

    private int contadorId;

    // Se dispara cada vez que hay algo que mostrar en el log de la interfaz
    public event Action<string> Mensaje;

    public Juego()
    {
        Tablero = ListaTablero.ConstruirTableroPredeterminado();
        ArmarMazoDeEventos();
        Turnos = new ColaTurno();
        Banco = new Banco();
        Dado = new Dado();
        PartidaIniciada = false;
        NumeroTurno = 0;
        contadorId = 1;
    }

    private void Avisar(string texto)
    {
        Mensaje?.Invoke(texto);
    }

    // Arma un mazo básico y se lo asigna a todas las casillas de evento del tablero
    private void ArmarMazoDeEventos()
    {
        ColaCartas mazo = new ColaCartas();
        mazo.AgregarCarta(new CartaEvento(1, "Recibes 100 por un reembolso de impuestos", TipoCarta.RecibirDinero, 100));
        mazo.AgregarCarta(new CartaEvento(2, "Pagas una multa de 50", TipoCarta.PagarDinero, 50));
        mazo.AgregarCarta(new CartaEvento(3, "Avanzas 3 casillas", TipoCarta.AvanzarPosiciones, 3));
        mazo.AgregarCarta(new CartaEvento(4, "Retrocedes 2 casillas", TipoCarta.RetrocederPosiciones, 2));
        mazo.AgregarCarta(new CartaEvento(5, "Pierdes tu próximo turno", TipoCarta.PerderTurno, 0));
        mazo.AgregarCarta(new CartaEvento(6, "Recibes 200 por un premio", TipoCarta.RecibirDinero, 200));

        NodoTablero actual = Tablero.CabezaNodo;
        do
        {
            if (actual.CasillaActual is CasillaEvento casillaEvento)
            {
                casillaEvento.Mazo = mazo; // todas comparten el mismo mazo
            }
            actual = actual.Siguiente;
        } while (actual != Tablero.CabezaNodo);
    }

    public Jugador AgregarJugador(string nombre)
    {
        if (PartidaIniciada)
        {
            throw new InvalidOperationException("La partida ya empezó, no se pueden agregar más jugadores.");
        }
        if (CantidadJugadores() >= MaximoJugadores)
        {
            throw new InvalidOperationException($"Ya hay el máximo de {MaximoJugadores} jugadores.");
        }

        Jugador jugador = new Jugador(contadorId, nombre, SaldoInicial);
        contadorId++;
        jugador.PosicionActual = Tablero.CabezaNodo; // todos arrancan en "Salida"
        Turnos.AgregarJugador(jugador);
        jugadorEsperandoTag = jugador;

        Avisar($"{nombre} se unió a la partida. Escanea tu tarjeta RFID para identificarte.");
        return jugador;
    }

    public int CantidadJugadores()
    {
        int cantidad = 0;
        if (Turnos.CabezaNodo != null)
        {
            NodoTurno actual = Turnos.CabezaNodo;
            do
            {
                cantidad++;
                actual = actual.Siguiente;
            } while (actual != Turnos.CabezaNodo);
        }
        return cantidad;
    }

    // Busca al jugador dueño de una tarjeta RFID, o null si ninguno la tiene
    private Jugador BuscarJugadorPorTag(string tag)
    {
        if (Turnos.CabezaNodo == null)
        {
            return null;
        }

        NodoTurno actual = Turnos.CabezaNodo;
        do
        {
            if (actual.JugadorDelTurno.TagRFID == tag)
            {
                return actual.JugadorDelTurno;
            }
            actual = actual.Siguiente;
        } while (actual != Turnos.CabezaNodo);

        return null;
    }

    // Hay una compra o un alquiler esperando que se confirme con una tarjeta RFID.
    public bool HayPagoOCompraPendiente()
    {
        return EsperandoConfirmacionCompra || AlquilerPendiente != null;
    }

    // Se llama cuando el lector RFID de la Raspberry Pi detecta una tarjeta.
    // El orden importa: primero se vincula una tarjeta nueva, luego se
    // confirman los pagos que estén pendientes, y si no hay nada de eso
    // pendiente, solo se informa de quién es la tarjeta.
    public void RegistrarTag(string tag)
    {
        if (jugadorEsperandoTag != null)
        {
            jugadorEsperandoTag.TagRFID = tag;
            Avisar($"Tarjeta vinculada a {jugadorEsperandoTag.Nombre}.");
            jugadorEsperandoTag = null;
            return;
        }

        if (AlquilerPendiente != null)
        {
            ConfirmarPagoDeAlquiler(tag);
            return;
        }

        if (EsperandoConfirmacionCompra)
        {
            ConfirmarCompra(tag);
            return;
        }

        Jugador dueño = BuscarJugadorPorTag(tag);
        Avisar(dueño != null
            ? $"Tarjeta detectada: {dueño.Nombre}."
            : "Se detectó una tarjeta que no pertenece a ningún jugador registrado.");
    }

    // Si la tarjeta no es la del jugador que debe pagar, se rechaza y se
    // sigue esperando la correcta (no se cancela el cobro pendiente).
    private void ConfirmarPagoDeAlquiler(string tag)
    {
        if (JugadorQuePagaAlquiler.TagRFID != tag)
        {
            Avisar(MensajeTarjetaEquivocada(JugadorQuePagaAlquiler, tag));
            return;
        }

        bool pagoExitoso = Banco.PagarAlquiler(JugadorQuePagaAlquiler, AlquilerPendiente, NumeroTurno);
        Avisar(pagoExitoso
            ? $"{JugadorQuePagaAlquiler.Nombre} pagó {AlquilerPendiente.Alquiler} a {AlquilerPendiente.Propietario.Nombre} por {AlquilerPendiente.Nombre}."
            : $"{JugadorQuePagaAlquiler.Nombre} no tiene suficiente dinero para pagar el alquiler de {AlquilerPendiente.Nombre}.");

        AlquilerPendiente = null;
        JugadorQuePagaAlquiler = null;
    }

    private void ConfirmarCompra(string tag)
    {
        Jugador jugador = JugadorActual();
        if (jugador.TagRFID != tag)
        {
            Avisar(MensajeTarjetaEquivocada(jugador, tag));
            return;
        }

        bool compraExitosa = Banco.ComprarPropiedad(jugador, PropiedadPendiente, NumeroTurno);
        Avisar(compraExitosa
            ? $"{jugador.Nombre} compró {PropiedadPendiente.Nombre}."
            : $"{jugador.Nombre} no tiene suficiente dinero para comprar {PropiedadPendiente.Nombre}.");

        PropiedadPendiente = null;
        EsperandoConfirmacionCompra = false;
    }

    private string MensajeTarjetaEquivocada(Jugador jugadorEsperado, string tagRecibido)
    {
        Jugador quienEscaneo = BuscarJugadorPorTag(tagRecibido);
        string nombreEscaneo = quienEscaneo != null ? quienEscaneo.Nombre : "una tarjeta no registrada";
        return $"Esa tarjeta no es de {jugadorEsperado.Nombre}. Se detectó {nombreEscaneo}. Acerca la tarjeta correcta.";
    }

    public void IniciarPartida()
    {
        if (PartidaIniciada)
        {
            throw new InvalidOperationException("La partida ya está en curso.");
        }
        if (CantidadJugadores() < 2)
        {
            throw new InvalidOperationException("Se necesitan al menos 2 jugadores.");
        }

        PartidaIniciada = true;
        NumeroTurno = 1;
        Avisar($"¡La partida comenzó! Le toca a {Turnos.TurnoActual.JugadorDelTurno.Nombre}.");
    }

    public Jugador JugadorActual()
    {
        return Turnos.TurnoActual?.JugadorDelTurno;
    }

    // Lanza los dados generándolos al azar (botón "Lanzar dados" de la GUI).
    public void LanzarDados()
    {
        RequierePartidaEnCurso();
        RequiereNadaPendiente();
        Dado.Lanzar();
        ProcesarTirada();
    }

    // Usa un valor que ya vino de afuera (el dado físico conectado por la
    // Raspberry Pi) en vez de generarlo al azar.
    public void LanzarDadosConValor(int valor1, int valor2)
    {
        RequierePartidaEnCurso();
        RequiereNadaPendiente();
        Dado.Forzar(valor1, valor2);
        ProcesarTirada();
    }

    // Lógica común a ambas formas de lanzar: asume que Dado.Dado1/Dado2
    // ya tienen el valor correcto (por azar o forzado desde la Raspberry).
    private void ProcesarTirada()
    {
        Jugador jugador = JugadorActual();
        int tirada = Dado.Total;
        Avisar($"{jugador.Nombre} tiró {Dado.Dado1} y {Dado.Dado2} ({tirada}).");

        if (jugador.EstaEnCarcel)
        {
            if (Dado.SonDobles)
            {
                jugador.EstaEnCarcel = false;
                Avisar($"{jugador.Nombre} sacó dobles y sale de la Cárcel.");
            }
            else
            {
                Avisar($"{jugador.Nombre} sigue en la Cárcel (no sacó dobles).");
                return;
            }
        }

        NodoTablero posicionAnterior = jugador.PosicionActual;
        jugador.Mover(tirada);

        if (DioVueltaCompleta(posicionAnterior, tirada))
        {
            jugador.RecibirDinero(200);
            Avisar($"{jugador.Nombre} pasó por Salida y recibe 200.");
        }

        Avisar($"{jugador.Nombre} cae en \"{jugador.PosicionActual.CasillaActual.Nombre}\".");
        ResolverCasillaActual(jugador);
    }

    private bool DioVueltaCompleta(NodoTablero desde, int pasos)
    {
        NodoTablero actual = desde;
        for (int i = 0; i < pasos; i++)
        {
            actual = actual.Siguiente;
            if (actual.CasillaActual.ID == 0)
            {
                return true;
            }
        }
        return false;
    }

    private void ResolverCasillaActual(Jugador jugador)
    {
        Casilla casilla = jugador.PosicionActual.CasillaActual;

        casilla.EjecutarEfecto(jugador);

        PropiedadPendiente = null;
        AlquilerPendiente = null;
        JugadorQuePagaAlquiler = null;

        if (casilla is Propiedad propiedad)
        {
            if (propiedad.Propietario == null)
            {
                PropiedadPendiente = propiedad; // la GUI debe preguntar si la compra
            }
            else if (propiedad.Propietario != jugador)
            {
                AlquilerPendiente = propiedad; // se espera la tarjeta RFID de "jugador" para cobrar
                JugadorQuePagaAlquiler = jugador;
            }
        }
    }

    // Ya no compra de una vez: arma la espera de la tarjeta RFID del jugador
    // actual. La compra se completa en ConfirmarCompra cuando llega el tag
    // correcto.
    public void ComprarPropiedadActual()
    {
        RequierePartidaEnCurso();
        if (PropiedadPendiente == null)
        {
            throw new InvalidOperationException("No hay ninguna propiedad disponible para comprar ahora.");
        }

        EsperandoConfirmacionCompra = true;
        Avisar($"Acerca la tarjeta RFID de {JugadorActual().Nombre} para confirmar la compra de {PropiedadPendiente.Nombre}.");
    }

    public void NoComprarPropiedadActual()
    {
        RequierePartidaEnCurso();
        PropiedadPendiente = null;
        EsperandoConfirmacionCompra = false;
    }

    public void TerminarTurno()
    {
        RequierePartidaEnCurso();
        if (PropiedadPendiente != null)
        {
            throw new InvalidOperationException("Primero decide si compras la propiedad pendiente.");
        }
        RequiereNadaPendiente();

        do
        {
            Turnos.AvanzarTurno();
            Jugador siguiente = JugadorActual();

            if (!siguiente.Activo)
            {
                continue;
            }
            if (siguiente.DebePerderTurno)
            {
                siguiente.DebePerderTurno = false;
                Avisar($"{siguiente.Nombre} pierde este turno.");
                continue;
            }
            break;
        } while (true);

        NumeroTurno++;
        Avisar($"Le toca a {JugadorActual().Nombre}.");
    }

    private void RequierePartidaEnCurso()
    {
        if (!PartidaIniciada)
        {
            throw new InvalidOperationException("La partida no ha comenzado.");
        }
    }

    private void RequiereNadaPendiente()
    {
        if (HayPagoOCompraPendiente())
        {
            throw new InvalidOperationException("Falta confirmar una compra o un pago con la tarjeta RFID.");
        }
    }
}
