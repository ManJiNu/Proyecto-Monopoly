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
    private Propiedad? propiedadPendienteCompra;

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

        resultado.JugadorRegistrado = jugador;
        resultado.RespuestaPrivada = $"CONECTADO|{jugador.Id}|{Protocolo.LimpiarTexto(jugador.Nombre)}|{jugador.Saldo}";
        resultado.AgregarBroadcast($"JUGADOR_CONECTADO|{jugador.Id}|{Protocolo.LimpiarTexto(jugador.Nombre)}|{JugadoresRegistrados}|4");

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
        ResultadoAccionServidor resultado = ValidarAccionDeTurno(jugador);
        if (resultado.RespuestaPrivada != null)
            return resultado;

        if (dadosLanzadosEnTurno)
        {
            resultado.RespuestaPrivada = Protocolo.Error("DADOS_YA_LANZADOS", "Ya lanzó durante este turno");
            return resultado;
        }

        if (propiedadPendienteCompra != null)
        {
            resultado.RespuestaPrivada = Protocolo.Error("DECISION_PENDIENTE", "Debe resolver la compra pendiente");
            return resultado;
        }

        if (jugador.PosicionActual == null)
        {
            resultado.RespuestaPrivada = Protocolo.Error("TABLERO_NO_CONFIGURADO", "El jugador no tiene una posición válida");
            return resultado;
        }

        int valor1 = dado1.Lanzar();
        int valor2 = dado2.Lanzar();
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
        if (!jugador.Activo && propiedadPendienteCompra == null)
            AvanzarTurno(resultado);

        return resultado;
    }

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

        bool comprada = Banco.ComprarPropiedad(jugador, propiedad, NumeroTurno);
        if (!comprada)
        {
            resultado.RespuestaPrivada = Protocolo.Error("COMPRA_RECHAZADA", "Banco rechazó la compra al revalidar la operación");
            return resultado;
        }

        propiedadPendienteCompra = null;
        resultado.AgregarBroadcast(
            $"PROPIEDAD_COMPRADA|{jugador.Id}|{propiedad.ID}|{Protocolo.LimpiarTexto(propiedad.Nombre)}|{propiedad.PrecioCompra}|{jugador.Saldo}");
        resultado.AgregarBroadcast(
            $"TRANSACCION_GENERADA|CompraPropiedad|{jugador.Id}|BANCO|{propiedad.PrecioCompra}");

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

        bool pagado = Banco.PagarAlquiler(jugador, propiedad, NumeroTurno);
        if (pagado)
        {
            resultado.AgregarBroadcast(
                $"PAGO_ALQUILER|{jugador.Id}|{propietario.Id}|{propiedad.ID}|{propiedad.Alquiler}|{jugador.Saldo}");
            resultado.AgregarBroadcast(
                $"TRANSACCION_GENERADA|PagoAlquiler|{jugador.Id}|{propietario.Id}|{propiedad.Alquiler}");
        }
        else
        {
            jugador.Eliminar();
            resultado.AgregarBroadcast(
                $"JUGADOR_ELIMINADO|{jugador.Id}|{Protocolo.LimpiarTexto(jugador.Nombre)}|SALDO_INSUFICIENTE_ALQUILER");
        }
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
