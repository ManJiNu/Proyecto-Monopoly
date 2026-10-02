using System;
using System.Text;

public sealed class PartidaServidor
{
    private readonly Dado dado1 = new Dado();
    private readonly Dado dado2 = new Dado();
    private readonly int saldoInicial;
    private readonly int premioPorInicio;

    private int siguienteIdJugador = 1;
    private bool dadosLanzadosEnTurno;
    private Propiedad? propiedadPendienteCompra; // esperando decision SI/NO de comprar

    // Compra ya decidida (SI), pero todavia no se cobra: se espera la tarjeta
    // RFID del jugador actual para confirmar la operacion.
    private bool esperandoConfirmacionCompra;
    private Propiedad? propiedadEnConfirmacionCompra;
    private Jugador? jugadorEnConfirmacionCompra;

    // Alquiler pendiente de cobro: se espera la tarjeta RFID de quien debe
    // pagar antes de mover el dinero.
    private Propiedad? alquilerPendiente;
    private Jugador? jugadorQuePagaAlquiler;

    // Jugador recien registrado que todavia no tiene tarjeta RFID vinculada.
    // La proxima tarjeta que se lea se asocia a el.
    private Jugador? jugadorEsperandoTag;

    public Banco Banco { get; }
    public ListaTablero Tablero { get; }
    public ColaTurno Turnos { get; }
    public int NumeroTurno { get; private set; }
    public int MaximoTurnos { get; }
    public int JugadoresRegistrados { get; private set; }
    public bool Iniciada { get; private set; }
    public bool Terminada { get; private set; }

    // Numero minimo de jugadores para poder arrancar la partida a mano
    // (con IniciarPartida) sin esperar a que se conecten los 4.
    public const int MinimoJugadores = 2;

    public PartidaServidor(
        ListaTablero tablero,
        int saldoInicial,
        int premioPorInicio,
        int maximoTurnos)
    {
        Tablero = tablero;
        this.saldoInicial = saldoInicial;
        this.premioPorInicio = premioPorInicio;
        MaximoTurnos = maximoTurnos;

        Banco = new Banco();
        Turnos = new ColaTurno();
        NumeroTurno = 1;
        JugadoresRegistrados = 0;
        Iniciada = false;
        Terminada = false;
        dadosLanzadosEnTurno = false;
        propiedadPendienteCompra = null;

        ArmarMazoDeEventos();
    }

    // Igual que Juego.cs en modo local: arma un mazo basico y se lo asigna a
    // TODAS las casillas de evento del tablero (comparten el mismo mazo).
    // Sin esto, cada CasillaEvento.Mazo queda null y el servidor contesta
    // EVENTO_SIN_MAZO en vez de aplicar una carta real.
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
                casillaEvento.Mazo = mazo;
            }
            actual = actual.Siguiente;
        } while (actual != Tablero.CabezaNodo);
    }

    public ResultadoAccionServidor RegistrarJugador(string nombre)
    {
        ResultadoAccionServidor resultado = new ResultadoAccionServidor();

        if (Iniciada || Terminada)
        {
            resultado.RespuestaPrivada = Protocolo.Error("PARTIDA_EN_CURSO", "La partida ya inició o terminó");
            return resultado;
        }

        if (JugadoresRegistrados >= 4)
        {
            resultado.RespuestaPrivada = Protocolo.Error("PARTIDA_LLENA", "Ya hay cuatro jugadores registrados");
            return resultado;
        }

        Jugador jugador = new Jugador(siguienteIdJugador, nombre.Trim(), saldoInicial);
        siguienteIdJugador++;

        if (Tablero.CabezaNodo != null)
            jugador.PosicionActual = Tablero.CabezaNodo;

        Turnos.AgregarJugador(jugador);
        JugadoresRegistrados++;
        jugadorEsperandoTag = jugador;

        resultado.JugadorRegistrado = jugador;
        resultado.RespuestaPrivada = $"CONECTADO|{jugador.Id}|{Protocolo.LimpiarTexto(jugador.Nombre)}|{jugador.Saldo}";
        resultado.AgregarBroadcast($"JUGADOR_CONECTADO|{jugador.Id}|{Protocolo.LimpiarTexto(jugador.Nombre)}|{JugadoresRegistrados}|4");
        resultado.AgregarBroadcast($"ESPERANDO_TAG_VINCULACION|{jugador.Id}|{Protocolo.LimpiarTexto(jugador.Nombre)}");

        if (JugadoresRegistrados == 4)
        {
            if (Tablero.CabezaNodo == null)
            {
                resultado.AgregarRespuesta(Protocolo.Error(
                    "TABLERO_NO_CONFIGURADO",
                    "Hay cuatro jugadores, pero no existe un tablero cargado"));
            }
            else
            {
                Iniciada = true;
                dadosLanzadosEnTurno = false;
                propiedadPendienteCompra = null;

                Jugador? actual = JugadorActual();
                if (actual != null)
                {
                    resultado.AgregarBroadcast(
                        $"PARTIDA_INICIADA|{NumeroTurno}|{actual.Id}|{Protocolo.LimpiarTexto(actual.Nombre)}");
                    resultado.AgregarBroadcast(MensajeTurnoActual());
                }
            }
        }

        return resultado;
    }

    // Arranca la partida "a mano" con los jugadores que ya se conectaron
    // (minimo 2), en vez de esperar siempre a que se conecten los 4. Sirve
    // para pruebas o para partidas mas cortas; cualquier jugador ya
    // registrado puede pedirlo.
    public ResultadoAccionServidor IniciarPartida(Jugador jugador)
    {
        ResultadoAccionServidor resultado = new ResultadoAccionServidor();

        if (Iniciada || Terminada)
        {
            resultado.RespuestaPrivada = Protocolo.Error("PARTIDA_EN_CURSO", "La partida ya inició o terminó");
            return resultado;
        }

        if (JugadoresRegistrados < MinimoJugadores)
        {
            resultado.RespuestaPrivada = Protocolo.Error(
                "FALTAN_JUGADORES",
                $"Se necesitan al menos {MinimoJugadores} jugadores para iniciar (hay {JugadoresRegistrados})");
            return resultado;
        }

        if (Tablero.CabezaNodo == null)
        {
            resultado.RespuestaPrivada = Protocolo.Error("TABLERO_NO_CONFIGURADO", "No existe un tablero cargado");
            return resultado;
        }

        Iniciada = true;
        dadosLanzadosEnTurno = false;
        propiedadPendienteCompra = null;

        Jugador? actual = JugadorActual();
        if (actual != null)
        {
            resultado.AgregarBroadcast(
                $"PARTIDA_INICIADA|{NumeroTurno}|{actual.Id}|{Protocolo.LimpiarTexto(actual.Nombre)}");
            resultado.AgregarBroadcast(MensajeTurnoActual());
        }

        return resultado;
    }

    public ResultadoAccionServidor TirarDados(Jugador jugador)
    {
        ResultadoAccionServidor resultado = ValidarTirada(jugador);
        if (resultado.RespuestaPrivada != null)
            return resultado;

        int valor1 = dado1.Lanzar();
        int valor2 = dado2.Lanzar();
        return EjecutarTirada(jugador, valor1, valor2, resultado);
    }

    // Tirada forzada: la usa el dado fisico controlado por la Raspberry Pi
    // (llega desde ConectorRaspberry en el cliente y se reenvia por red con
    // TIRAR_DADOS_FORZADO). El servidor sigue siendo quien decide si la
    // tirada es valida; solo cambia de donde vienen los valores de los dados.
    public ResultadoAccionServidor TirarDadosForzado(Jugador jugador, int valor1, int valor2)
    {
        // El dado fisico es UN SOLO objeto compartido junto al tablero (conectado
        // a una sola laptop), pero cualquiera lo puede presionar sin importar de
        // quien sea esa laptop. Por eso esta tirada no se valida contra "jugador"
        // (quien esta conectado en la laptop de la Raspberry), sino que siempre
        // tira por el jugador al que le toca el turno en ese momento.
        Jugador? jugadorDelTurno = JugadorActual();
        if (jugadorDelTurno == null)
        {
            ResultadoAccionServidor sinTurno = new ResultadoAccionServidor();
            sinTurno.RespuestaPrivada = Protocolo.Error("PARTIDA_NO_INICIADA", "La partida todavía no ha iniciado");
            return sinTurno;
        }

        ResultadoAccionServidor resultado = ValidarTirada(jugadorDelTurno);
        if (resultado.RespuestaPrivada != null)
            return resultado;

        return EjecutarTirada(jugadorDelTurno, valor1, valor2, resultado);
    }

    private ResultadoAccionServidor ValidarTirada(Jugador jugador)
    {
        ResultadoAccionServidor resultado = ValidarAccionDeTurno(jugador);
        if (resultado.RespuestaPrivada != null)
            return resultado;

        if (dadosLanzadosEnTurno)
        {
            resultado.RespuestaPrivada = Protocolo.Error("DADOS_YA_LANZADOS", "Ya lanzó durante este turno");
            return resultado;
        }

        if (propiedadPendienteCompra != null || esperandoConfirmacionCompra || alquilerPendiente != null)
        {
            resultado.RespuestaPrivada = Protocolo.Error("DECISION_PENDIENTE", "Debe resolver la compra o el pago pendiente (con la tarjeta RFID) antes de lanzar los dados");
            return resultado;
        }

        if (jugador.PosicionActual == null)
        {
            resultado.RespuestaPrivada = Protocolo.Error("TABLERO_NO_CONFIGURADO", "El jugador no tiene una posición válida");
            return resultado;
        }

        return resultado;
    }

    private ResultadoAccionServidor EjecutarTirada(Jugador jugador, int valor1, int valor2, ResultadoAccionServidor resultado)
    {
        int suma = valor1 + valor2;

        dadosLanzadosEnTurno = true;
        resultado.AgregarBroadcast($"DADOS|{jugador.Id}|{valor1}|{valor2}|{suma}");

        if (!MoverJugador(jugador, suma, resultado))
        {
            resultado.RespuestaPrivada = Protocolo.Error("TABLERO_INVALIDO", "No se pudo completar el movimiento");
            return resultado;
        }

        Casilla? casilla = jugador.PosicionActual?.CasillaActual;
        if (casilla == null)
        {
            resultado.RespuestaPrivada = Protocolo.Error("TABLERO_INVALIDO", "La posición actual no contiene una casilla");
            return resultado;
        }

        resultado.AgregarBroadcast(
            $"MOVIMIENTO|{jugador.Id}|{casilla.ID}|{Protocolo.LimpiarTexto(casilla.Nombre)}");

        ResolverCasilla(jugador, casilla, resultado);

        // Si el jugador quedó eliminado, avanzamos el turno para no dejar la partida detenida.
        if (!jugador.Activo && propiedadPendienteCompra == null && !esperandoConfirmacionCompra && alquilerPendiente == null)
            AvanzarTurno(resultado);

        return resultado;
    }

    // Ya NO compra de una vez: el jugador dijo que SI quiere comprar, pero el
    // cobro se completa hasta que se confirme con la tarjeta RFID (ver
    // ConfirmarTag/ConfirmarCompraConTag mas abajo).
    public ResultadoAccionServidor ComprarPropiedad(Jugador jugador)
    {
        ResultadoAccionServidor resultado = ValidarAccionDeTurno(jugador);
        if (resultado.RespuestaPrivada != null)
            return resultado;

        if (!dadosLanzadosEnTurno)
        {
            resultado.RespuestaPrivada = Protocolo.Error("DADOS_NO_LANZADOS", "Debe lanzar los dados antes de comprar");
            return resultado;
        }

        Propiedad? propiedad = propiedadPendienteCompra;
        if (propiedad == null)
        {
            resultado.RespuestaPrivada = Protocolo.Error("SIN_COMPRA_PENDIENTE", "No hay una propiedad pendiente de decisión");
            return resultado;
        }

        if (propiedad.Propietario != null)
        {
            propiedadPendienteCompra = null;
            resultado.RespuestaPrivada = Protocolo.Error("PROPIEDAD_OCUPADA", "La propiedad ya tiene dueño");
            return resultado;
        }

        if (jugador.Saldo < propiedad.PrecioCompra)
        {
            resultado.RespuestaPrivada = Protocolo.Error("SALDO_INSUFICIENTE", "No tiene suficiente dinero para comprar la propiedad");
            return resultado;
        }

        propiedadPendienteCompra = null;
        esperandoConfirmacionCompra = true;
        propiedadEnConfirmacionCompra = propiedad;
        jugadorEnConfirmacionCompra = jugador;

        resultado.AgregarBroadcast(
            $"ESPERANDO_TAG_COMPRA|{jugador.Id}|{propiedad.ID}|{Protocolo.LimpiarTexto(propiedad.Nombre)}|{propiedad.PrecioCompra}");

        return resultado;
    }

    public ResultadoAccionServidor NoComprar(Jugador jugador)
    {
        ResultadoAccionServidor resultado = ValidarAccionDeTurno(jugador);
        if (resultado.RespuestaPrivada != null)
            return resultado;

        if (propiedadPendienteCompra == null)
        {
            resultado.RespuestaPrivada = Protocolo.Error("SIN_COMPRA_PENDIENTE", "No hay una propiedad pendiente de decisión");
            return resultado;
        }

        int propiedadId = propiedadPendienteCompra.ID;
        propiedadPendienteCompra = null;
        resultado.RespuestaPrivada = $"DECISION_COMPRA_CERRADA|{propiedadId}|NO_COMPRAR";
        return resultado;
    }

    public ResultadoAccionServidor TerminarTurno(Jugador jugador)
    {
        ResultadoAccionServidor resultado = ValidarAccionDeTurno(jugador);
        if (resultado.RespuestaPrivada != null)
            return resultado;

        if (!dadosLanzadosEnTurno)
        {
            resultado.RespuestaPrivada = Protocolo.Error("DADOS_NO_LANZADOS", "Debe lanzar los dados antes de terminar el turno");
            return resultado;
        }

        if (propiedadPendienteCompra != null)
        {
            resultado.RespuestaPrivada = Protocolo.Error("DECISION_PENDIENTE", "Debe comprar o rechazar la propiedad antes de terminar el turno");
            return resultado;
        }

        if (esperandoConfirmacionCompra || alquilerPendiente != null)
        {
            resultado.RespuestaPrivada = Protocolo.Error("PAGO_PENDIENTE", "Debe confirmar la compra o el pago del alquiler con la tarjeta RFID antes de terminar el turno");
            return resultado;
        }

        AvanzarTurno(resultado);
        return resultado;
    }

    public string CrearBloqueEstado()
    {
        StringBuilder texto = new StringBuilder();
        Jugador? actual = JugadorActual();
        int idActual = actual?.Id ?? 0;

        texto.AppendLine($"ESTADO_INICIO|{NumeroTurno}|{MaximoTurnos}|{idActual}");

        NodoTurno? nodoTurno = Turnos.CabezaNodo;
        if (nodoTurno != null)
        {
            NodoTurno? inicio = nodoTurno;
            do
            {
                Jugador jugador = nodoTurno.JugadorDelTurno;
                NodoTablero? posicion = jugador.PosicionActual;
                int casillaId = posicion?.CasillaActual?.ID ?? -1;
                string casillaNombre = posicion?.CasillaActual?.Nombre ?? "SIN_POSICION";
                int patrimonio = CalcularPatrimonio(jugador);
                string estadoTurno = ObtenerEstadoTurno(jugador, actual);

                texto.AppendLine(
                    $"JUGADOR_ESTADO|{jugador.Id}|{Protocolo.LimpiarTexto(jugador.Nombre)}|{jugador.Saldo}|{casillaId}|{Protocolo.LimpiarTexto(casillaNombre)}|{jugador.Activo}|{patrimonio}|{estadoTurno}|{jugador.EstaEnCarcel}|{jugador.DebePerderTurno}");

                nodoTurno = nodoTurno.Siguiente;
            }
            while (nodoTurno != null && !ReferenceEquals(nodoTurno, inicio));
        }

        NodoTablero? nodoTablero = Tablero.CabezaNodo;
        if (nodoTablero != null)
        {
            NodoTablero? inicio = nodoTablero;
            do
            {
                if (nodoTablero.CasillaActual is Propiedad propiedad)
                {
                    int propietarioId = propiedad.Propietario?.Id ?? 0;
                    texto.AppendLine(
                        $"PROPIEDAD_ESTADO|{propiedad.ID}|{Protocolo.LimpiarTexto(propiedad.Nombre)}|{propiedad.PrecioCompra}|{propiedad.Alquiler}|{propietarioId}");
                }

                nodoTablero = nodoTablero.Siguiente;
            }
            while (nodoTablero != null && !ReferenceEquals(nodoTablero, inicio));
        }

        texto.Append("ESTADO_FIN");
        return texto.ToString();
    }

    public string CrearBloqueTransacciones()
    {
        StringBuilder texto = new StringBuilder();
        texto.AppendLine("TRANSACCIONES_INICIO");

        NodoTransaccion? nodo = Banco.Historial.CabezaNodo;
        while (nodo != null)
        {
            Transaccion t = nodo.TransaccionActual;
            string idOrigen = t.JugadorOrigen == null ? "BANCO" : t.JugadorOrigen.Id.ToString();
            string nombreOrigen = t.JugadorOrigen == null ? "BANCO" : Protocolo.LimpiarTexto(t.JugadorOrigen.Nombre);
            string idDestino = t.JugadorDestino == null ? "BANCO" : t.JugadorDestino.Id.ToString();
            string nombreDestino = t.JugadorDestino == null ? "BANCO" : Protocolo.LimpiarTexto(t.JugadorDestino.Nombre);

            texto.AppendLine(
                $"TRANSACCION|{t.Id}|{t.NumeroTurno}|{Protocolo.LimpiarTexto(t.Tipo)}|{idOrigen}|{nombreOrigen}|{idDestino}|{nombreDestino}|{t.Monto}|{Protocolo.LimpiarTexto(t.Descripcion)}|{t.FechaHora:O}");

            nodo = nodo.Siguiente;
        }

        texto.Append("TRANSACCIONES_FIN");
        return texto.ToString();
    }

    public void ExportarHistorial(string rutaArchivo)
    {
        Banco.Historial.ExportarTXT(rutaArchivo);
    }

    // Punto de entrada unico para cualquier lectura del lector RFID que llega
    // por red (CONFIRMAR_TAG). El orden de prioridad importa: primero
    // vincular tarjetas nuevas, luego confirmar pagos pendientes.
    public ResultadoAccionServidor ConfirmarTag(string tag)
    {
        ResultadoAccionServidor resultado = new ResultadoAccionServidor();

        if (string.IsNullOrWhiteSpace(tag))
        {
            resultado.RespuestaPrivada = Protocolo.Error("FORMATO_INVALIDO", "La tarjeta no tiene un código válido");
            return resultado;
        }

        string tagLimpio = tag.Trim();

        // 1) Vincular la tarjeta a un jugador recien registrado que aun no tiene una.
        if (jugadorEsperandoTag != null)
        {
            Jugador vinculado = jugadorEsperandoTag;
            vinculado.TagRFID = tagLimpio;
            jugadorEsperandoTag = null;
            resultado.AgregarBroadcast($"TAG_VINCULADO|{vinculado.Id}|{Protocolo.LimpiarTexto(vinculado.Nombre)}");
            return resultado;
        }

        // 2) Confirmar el pago de un alquiler pendiente.
        if (alquilerPendiente != null && jugadorQuePagaAlquiler != null)
        {
            ConfirmarPagoDeAlquiler(tagLimpio, resultado);
            return resultado;
        }

        // 3) Confirmar una compra pendiente.
        if (esperandoConfirmacionCompra && propiedadEnConfirmacionCompra != null && jugadorEnConfirmacionCompra != null)
        {
            ConfirmarCompraConTag(tagLimpio, resultado);
            return resultado;
        }

        // 4) No hay nada pendiente: solo identifica de quien es la tarjeta.
        Jugador? dueno = BuscarJugadorPorTag(tagLimpio);
        if (dueno != null)
        {
            resultado.RespuestaPrivada = $"TAG_RECONOCIDO|{dueno.Id}|{Protocolo.LimpiarTexto(dueno.Nombre)}|{dueno.Saldo}";
        }
        else
        {
            resultado.RespuestaPrivada = Protocolo.Error("TAG_DESCONOCIDO", "Tarjeta no reconocida");
        }

        return resultado;
    }

    private Jugador? BuscarJugadorPorTag(string tag)
    {
        NodoTurno? nodo = Turnos.CabezaNodo;
        if (nodo == null)
            return null;

        NodoTurno? inicio = nodo;
        do
        {
            Jugador jugadorDelNodo = nodo.JugadorDelTurno;
            if (!string.IsNullOrEmpty(jugadorDelNodo.TagRFID) && jugadorDelNodo.TagRFID == tag)
                return jugadorDelNodo;

            nodo = nodo.Siguiente;
        }
        while (nodo != null && !ReferenceEquals(nodo, inicio));

        return null;
    }

    // Si se acerca la tarjeta equivocada, se rechaza y se queda esperando la
    // correcta (no se cobra nada ni se cancela el pago pendiente).
    private void ConfirmarPagoDeAlquiler(string tag, ResultadoAccionServidor resultado)
    {
        Jugador jugador = jugadorQuePagaAlquiler!;
        Propiedad propiedad = alquilerPendiente!;

        if (jugador.TagRFID != tag)
        {
            resultado.RespuestaPrivada = MensajeTarjetaEquivocada(jugador, tag);
            return;
        }

        Jugador? propietario = propiedad.Propietario;
        bool pagado = Banco.PagarAlquiler(jugador, propiedad, NumeroTurno);

        alquilerPendiente = null;
        jugadorQuePagaAlquiler = null;

        if (pagado)
        {
            resultado.AgregarBroadcast(
                $"PAGO_ALQUILER|{jugador.Id}|{propietario?.Id ?? 0}|{propiedad.ID}|{propiedad.Alquiler}|{jugador.Saldo}");
            resultado.AgregarBroadcast(
                $"TRANSACCION_GENERADA|PagoAlquiler|{jugador.Id}|{propietario?.Id ?? 0}|{propiedad.Alquiler}");
        }
        else
        {
            jugador.Eliminar();
            resultado.AgregarBroadcast(
                $"JUGADOR_ELIMINADO|{jugador.Id}|{Protocolo.LimpiarTexto(jugador.Nombre)}|SALDO_INSUFICIENTE_ALQUILER");
        }

        if (!jugador.Activo && propiedadPendienteCompra == null && !esperandoConfirmacionCompra && alquilerPendiente == null)
            AvanzarTurno(resultado);
    }

    private void ConfirmarCompraConTag(string tag, ResultadoAccionServidor resultado)
    {
        Jugador jugador = jugadorEnConfirmacionCompra!;
        Propiedad propiedad = propiedadEnConfirmacionCompra!;

        if (jugador.TagRFID != tag)
        {
            resultado.RespuestaPrivada = MensajeTarjetaEquivocada(jugador, tag);
            return;
        }

        esperandoConfirmacionCompra = false;
        propiedadEnConfirmacionCompra = null;
        jugadorEnConfirmacionCompra = null;

        if (propiedad.Propietario != null)
        {
            resultado.RespuestaPrivada = Protocolo.Error("PROPIEDAD_OCUPADA", "La propiedad ya tiene dueño");
            return;
        }

        bool comprada = Banco.ComprarPropiedad(jugador, propiedad, NumeroTurno);
        if (comprada)
        {
            resultado.AgregarBroadcast(
                $"PROPIEDAD_COMPRADA|{jugador.Id}|{propiedad.ID}|{Protocolo.LimpiarTexto(propiedad.Nombre)}|{propiedad.PrecioCompra}|{jugador.Saldo}");
            resultado.AgregarBroadcast(
                $"TRANSACCION_GENERADA|CompraPropiedad|{jugador.Id}|BANCO|{propiedad.PrecioCompra}");
        }
        else
        {
            resultado.RespuestaPrivada = Protocolo.Error("SALDO_INSUFICIENTE", "No tiene suficiente dinero para comprar la propiedad");
        }
    }

    private string MensajeTarjetaEquivocada(Jugador jugadorEsperado, string tagRecibido)
    {
        Jugador? quienEscaneo = BuscarJugadorPorTag(tagRecibido);
        string nombreEscaneo = quienEscaneo != null ? quienEscaneo.Nombre : "una tarjeta no registrada";
        return Protocolo.Error("TARJETA_INCORRECTA", $"Esa tarjeta no es de {jugadorEsperado.Nombre}. Se detectó {nombreEscaneo}. Acerca la tarjeta correcta.");
    }

    private ResultadoAccionServidor ValidarAccionDeTurno(Jugador jugador)
    {
        ResultadoAccionServidor resultado = new ResultadoAccionServidor();

        if (!Iniciada)
        {
            resultado.RespuestaPrivada = Protocolo.Error("PARTIDA_NO_INICIADA", "La partida todavía no ha iniciado");
            return resultado;
        }

        if (Terminada)
        {
            resultado.RespuestaPrivada = Protocolo.Error("PARTIDA_TERMINADA", "La partida ya terminó");
            return resultado;
        }

        if (!jugador.Activo)
        {
            resultado.RespuestaPrivada = Protocolo.Error("JUGADOR_ELIMINADO", "El jugador ya no está activo");
            return resultado;
        }

        Jugador? actual = JugadorActual();
        if (actual == null || !ReferenceEquals(actual, jugador))
        {
            resultado.RespuestaPrivada = Protocolo.Error("FUERA_DE_TURNO", "No es su turno");
            return resultado;
        }

        return resultado;
    }

    private Jugador? JugadorActual()
    {
        return Turnos.TurnoActual?.JugadorDelTurno;
    }

    private string MensajeTurnoActual()
    {
        Jugador? actual = JugadorActual();
        if (actual == null)
            return $"TURNO_ACTUAL|{NumeroTurno}|0|SIN_JUGADOR";

        return $"TURNO_ACTUAL|{NumeroTurno}|{actual.Id}|{Protocolo.LimpiarTexto(actual.Nombre)}";
    }

    private bool MoverJugador(Jugador jugador, int pasos, ResultadoAccionServidor resultado)
    {
        for (int i = 0; i < pasos; i++)
        {
            NodoTablero? siguiente = jugador.PosicionActual?.Siguiente;
            if (siguiente == null)
                return false;

            jugador.PosicionActual = siguiente;

            if (ReferenceEquals(jugador.PosicionActual, Tablero.CabezaNodo) && premioPorInicio > 0)
            {
                Banco.ProcesarPago(
                    null,
                    jugador,
                    premioPorInicio,
                    "PremioSalida",
                    NumeroTurno,
                    $"{jugador.Nombre} recibió premio por completar una vuelta");

                resultado.AgregarBroadcast($"PASO_SALIDA|{jugador.Id}|{premioPorInicio}|{jugador.Saldo}");
                resultado.AgregarBroadcast($"TRANSACCION_GENERADA|PremioSalida|BANCO|{jugador.Id}|{premioPorInicio}");
            }
        }

        return true;
    }

    private void ResolverCasilla(Jugador jugador, Casilla casilla, ResultadoAccionServidor resultado)
    {
        if (casilla is Propiedad propiedad)
        {
            ResolverPropiedad(jugador, propiedad, resultado);
            return;
        }

        if (casilla is CasillaEvento evento)
        {
            ResolverEvento(jugador, evento, resultado);
            return;
        }

        if (casilla is CasillaEspecial especial)
        {
            especial.EjecutarEfecto(jugador);
            resultado.AgregarBroadcast(
                $"CASILLA_ESPECIAL|{jugador.Id}|{especial.ID}|{Protocolo.LimpiarTexto(especial.Nombre)}|CARCEL={jugador.EstaEnCarcel}");
            return;
        }

        casilla.EjecutarEfecto(jugador);
    }

    // Ya NO cobra de una vez: se arma la espera de la tarjeta RFID de quien
    // debe pagar. El cobro se completa en ConfirmarTag/ConfirmarPagoDeAlquiler
    // cuando llega el tag correcto.
    private void ResolverPropiedad(Jugador jugador, Propiedad propiedad, ResultadoAccionServidor resultado)
    {
        Jugador? propietario = propiedad.Propietario;

        if (propietario == null)
        {
            propiedadPendienteCompra = propiedad;
            // Antes era un mensaje PRIVADO (solo volvia a quien mando la tirada).
            // Eso fallaba cuando el dado fisico lo tira una laptop distinta a la
            // del jugador al que le toca decidir (el dado es un objeto compartido).
            // Por eso ahora es un BROADCAST con el id del jugador incluido: todas
            // las ventanas lo reciben, pero los botones Comprar/No comprar solo se
            // habilitan en la ventana de quien tiene el turno (ActualizarInterfazRed).
            resultado.AgregarBroadcast(
                $"DECISION_COMPRA|{jugador.Id}|{propiedad.ID}|{Protocolo.LimpiarTexto(propiedad.Nombre)}|{propiedad.PrecioCompra}|{propiedad.Alquiler}");
            return;
        }

        if (ReferenceEquals(propietario, jugador))
        {
            resultado.AgregarBroadcast($"PROPIEDAD_PROPIA|{jugador.Id}|{propiedad.ID}");
            return;
        }

        alquilerPendiente = propiedad;
        jugadorQuePagaAlquiler = jugador;
        resultado.AgregarBroadcast(
            $"ESPERANDO_TAG_ALQUILER|{jugador.Id}|{propietario.Id}|{propiedad.ID}|{propiedad.Alquiler}");
    }

    private void ResolverEvento(Jugador jugador, CasillaEvento evento, ResultadoAccionServidor resultado)
    {
        if (evento.Mazo == null)
        {
            resultado.AgregarBroadcast($"EVENTO_SIN_MAZO|{jugador.Id}|{evento.ID}");
            return;
        }

        CartaEvento? carta = evento.Mazo.SacarCarta();
        if (carta == null)
        {
            resultado.AgregarBroadcast($"EVENTO_MAZO_VACIO|{jugador.Id}|{evento.ID}");
            return;
        }

        resultado.AgregarBroadcast(
            $"CARTA_EVENTO|{jugador.Id}|{carta.Id}|{Protocolo.LimpiarTexto(carta.Descripcion)}|{carta.Tipo}|{carta.Valor}");

        switch (carta.Tipo)
        {
            case TipoCarta.RecibirDinero:
                Banco.ProcesarPago(null, jugador, carta.Valor, "EventoRecibir", NumeroTurno, carta.Descripcion);
                resultado.AgregarBroadcast($"EVENTO_DINERO|{jugador.Id}|RECIBE|{carta.Valor}|{jugador.Saldo}");
                resultado.AgregarBroadcast($"TRANSACCION_GENERADA|EventoRecibir|BANCO|{jugador.Id}|{carta.Valor}");
                break;

            case TipoCarta.PagarDinero:
                if (Banco.ProcesarPago(jugador, null, carta.Valor, "EventoPagar", NumeroTurno, carta.Descripcion))
                {
                    resultado.AgregarBroadcast($"EVENTO_DINERO|{jugador.Id}|PAGA|{carta.Valor}|{jugador.Saldo}");
                    resultado.AgregarBroadcast($"TRANSACCION_GENERADA|EventoPagar|{jugador.Id}|BANCO|{carta.Valor}");
                }
                else
                {
                    jugador.Eliminar();
                    resultado.AgregarBroadcast(
                        $"JUGADOR_ELIMINADO|{jugador.Id}|{Protocolo.LimpiarTexto(jugador.Nombre)}|SALDO_INSUFICIENTE_EVENTO");
                }
                break;

            case TipoCarta.AvanzarPosiciones:
                if (MoverJugador(jugador, carta.Valor, resultado) && jugador.PosicionActual?.CasillaActual != null)
                {
                    Casilla destino = jugador.PosicionActual.CasillaActual;
                    resultado.AgregarBroadcast(
                        $"MOVIMIENTO_EVENTO|{jugador.Id}|{destino.ID}|{Protocolo.LimpiarTexto(destino.Nombre)}");
                }
                break;

            case TipoCarta.RetrocederPosiciones:
                RetrocederJugador(jugador, carta.Valor);
                if (jugador.PosicionActual?.CasillaActual != null)
                {
                    Casilla destino = jugador.PosicionActual.CasillaActual;
                    resultado.AgregarBroadcast(
                        $"MOVIMIENTO_EVENTO|{jugador.Id}|{destino.ID}|{Protocolo.LimpiarTexto(destino.Nombre)}");
                }
                break;

            case TipoCarta.PerderTurno:
                jugador.DebePerderTurno = true;
                resultado.AgregarBroadcast($"PERDER_TURNO|{jugador.Id}|PROXIMO_TURNO");
                break;

            case TipoCarta.IrACasilla:
                NodoTablero? destinoNodo = BuscarCasillaPorId(carta.Valor);
                if (destinoNodo == null)
                {
                    resultado.AgregarBroadcast($"EVENTO_DESTINO_INVALIDO|{jugador.Id}|{carta.Valor}");
                }
                else
                {
                    jugador.PosicionActual = destinoNodo;
                    resultado.AgregarBroadcast(
                        $"MOVIMIENTO_EVENTO|{jugador.Id}|{destinoNodo.CasillaActual.ID}|{Protocolo.LimpiarTexto(destinoNodo.CasillaActual.Nombre)}");
                }
                break;
        }
    }

    private void RetrocederJugador(Jugador jugador, int pasos)
    {
        for (int i = 0; i < pasos; i++)
        {
            NodoTablero? anterior = jugador.PosicionActual?.Anterior;
            if (anterior == null)
                return;

            jugador.PosicionActual = anterior;
        }
    }

    private NodoTablero? BuscarCasillaPorId(int id)
    {
        NodoTablero? nodo = Tablero.CabezaNodo;
        if (nodo == null)
            return null;

        NodoTablero? inicio = nodo;
        do
        {
            if (nodo.CasillaActual.ID == id)
                return nodo;

            nodo = nodo.Siguiente;
        }
        while (nodo != null && !ReferenceEquals(nodo, inicio));

        return null;
    }

    private void AvanzarTurno(ResultadoAccionServidor resultado)
    {
        propiedadPendienteCompra = null;
        esperandoConfirmacionCompra = false;
        propiedadEnConfirmacionCompra = null;
        jugadorEnConfirmacionCompra = null;
        alquilerPendiente = null;
        jugadorQuePagaAlquiler = null;
        dadosLanzadosEnTurno = false;

        if (NumeroTurno >= MaximoTurnos)
        {
            FinalizarPorMaximoTurnos(resultado);
            return;
        }

        int maximoRevisiones = Math.Max(1, JugadoresRegistrados * 2);
        int revisiones = 0;

        while (revisiones < maximoRevisiones)
        {
            Turnos.AvanzarTurno();
            NumeroTurno++;
            revisiones++;

            if (NumeroTurno > MaximoTurnos)
            {
                FinalizarPorMaximoTurnos(resultado);
                return;
            }

            Jugador? actual = JugadorActual();
            if (actual == null)
            {
                Terminada = true;
                Iniciada = false;
                resultado.AgregarBroadcast("PARTIDA_TERMINADA|SIN_JUGADOR_ACTUAL");
                return;
            }

            if (!actual.Activo)
                continue;

            if (actual.DebePerderTurno)
            {
                actual.DebePerderTurno = false;
                resultado.AgregarBroadcast(
                    $"TURNO_PERDIDO|{NumeroTurno}|{actual.Id}|{Protocolo.LimpiarTexto(actual.Nombre)}");
                continue;
            }

            if (actual.EstaEnCarcel)
            {
                actual.EstaEnCarcel = false;
                resultado.AgregarBroadcast(
                    $"TURNO_SALTADO_CARCEL|{NumeroTurno}|{actual.Id}|{Protocolo.LimpiarTexto(actual.Nombre)}");
                continue;
            }

            resultado.AgregarBroadcast(MensajeTurnoActual());
            return;
        }

        Terminada = true;
        Iniciada = false;
        resultado.AgregarBroadcast("PARTIDA_TERMINADA|SIN_JUGADORES_ACTIVOS");
    }

    private void FinalizarPorMaximoTurnos(ResultadoAccionServidor resultado)
    {
        Terminada = true;
        Iniciada = false;
        resultado.AgregarBroadcast($"PARTIDA_TERMINADA|MAXIMO_TURNOS|{MaximoTurnos}");
    }

    // Se llama desde Servidor.cs cuando se cae la conexion de un jugador
    // (cerro la ventana, se le fue el WiFi, etc.). Sin esto, si se desconecta
    // justo en su propio turno, el juego queda trabado para siempre esperando
    // una jugada que ya nadie puede hacer. Se trata igual que una eliminacion
    // por insolvencia: se marca inactivo y, si era su turno, se avanza solo.
    public ResultadoAccionServidor ManejarDesconexion(Jugador jugador)
    {
        ResultadoAccionServidor resultado = new ResultadoAccionServidor();

        if (!jugador.Activo || Terminada || !Iniciada)
            return resultado;

        bool eraSuTurno = ReferenceEquals(JugadorActual(), jugador);

        if (ReferenceEquals(jugadorQuePagaAlquiler, jugador))
        {
            alquilerPendiente = null;
            jugadorQuePagaAlquiler = null;
        }

        if (ReferenceEquals(jugadorEnConfirmacionCompra, jugador))
        {
            esperandoConfirmacionCompra = false;
            propiedadEnConfirmacionCompra = null;
            jugadorEnConfirmacionCompra = null;
        }

        if (ReferenceEquals(jugadorEsperandoTag, jugador))
        {
            jugadorEsperandoTag = null;
        }

        if (eraSuTurno)
        {
            propiedadPendienteCompra = null;
        }

        jugador.Eliminar();
        resultado.AgregarBroadcast(
            $"JUGADOR_ELIMINADO|{jugador.Id}|{Protocolo.LimpiarTexto(jugador.Nombre)}|DESCONECTADO");

        if (eraSuTurno)
            AvanzarTurno(resultado);

        return resultado;
    }

    private int CalcularPatrimonio(Jugador jugador)
    {
        int patrimonio = jugador.Saldo;
        NodoPropiedad? nodo = jugador.Propiedades.CabezaNodo;

        while (nodo != null)
        {
            patrimonio += nodo.PropiedadActual.PrecioCompra;
            nodo = nodo.Siguiente;
        }

        return patrimonio;
    }

    private string ObtenerEstadoTurno(Jugador jugador, Jugador? actual)
    {
        if (!jugador.Activo)
            return "ELIMINADO";

        if (ReferenceEquals(jugador, actual))
            return "ACTUAL";

        if (jugador.DebePerderTurno)
            return "PERDER_TURNO";

        if (jugador.EstaEnCarcel)
            return "CARCEL";

        return "ESPERA";
    }
}
