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
        ResultadoAccionServidor resultado = ValidarTirada(jugador);
        if (resultado.RespuestaPrivada != null)
            return resultado;

        return EjecutarTirada(jugador, valor1, valor2, resultado);
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
            resultado.AgregarRespuesta(
                $"DECISION_COMPRA|{propiedad.ID}|{Protocolo.LimpiarTexto(propiedad.Nombre)}|{propiedad.PrecioCompra}|{propiedad.Alquiler}");
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
