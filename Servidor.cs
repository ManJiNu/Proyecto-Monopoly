using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public sealed class Servidor
{
    private readonly SemaphoreSlim candadoJuego = new SemaphoreSlim(1, 1);
    private readonly object candadoConexiones = new object();
    private readonly PartidaServidor partida;

    private TcpListener? listener;
    private ConexionCliente? primeraConexion;
    private ConexionCliente? ultimaConexion;
    private bool activo;

    public int Puerto { get; }

    public Servidor(
        int puerto,
        int saldoInicial,
        int premioPorInicio,
        int maximoTurnos,
        ListaTablero tablero)
    {
        Puerto = puerto;
        partida = new PartidaServidor(tablero, saldoInicial, premioPorInicio, maximoTurnos);
    }

    public async Task IniciarAsync(CancellationToken cancellationToken = default)
    {
        listener = new TcpListener(IPAddress.Any, Puerto);
        listener.Start();
        activo = true;

        Console.WriteLine($"Servidor Monopoly escuchando en 0.0.0.0:{Puerto}");
        Console.WriteLine("Esperando conexiones TCP...");

        try
        {
            while (activo && !cancellationToken.IsCancellationRequested)
            {
                // Esperamos una nueva conexión sin detener el resto del servidor.
                TcpClient cliente = await listener.AcceptTcpClientAsync(cancellationToken);
                ConexionCliente conexion = new ConexionCliente(cliente);
                AgregarConexion(conexion);

                // Cada cliente se atiende por separado para poder seguir aceptando conexiones.
                _ = AtenderClienteAsync(conexion);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (SocketException ex) when (!activo)
        {
            Console.WriteLine($"Listener detenido: {ex.SocketErrorCode}");
        }
        finally
        {
            Detener();
        }
    }

    public void Detener()
    {
        activo = false;

        try { listener?.Stop(); } catch { }

        ConexionCliente? actual = primeraConexion;
        while (actual != null)
        {
            actual.Cerrar();
            actual = actual.Siguiente;
        }
    }

    private void AgregarConexion(ConexionCliente conexion)
    {
        lock (candadoConexiones)
        {
            if (primeraConexion == null)
            {
                primeraConexion = conexion;
                ultimaConexion = conexion;
            }
            else
            {
                if (ultimaConexion != null)
                    ultimaConexion.Siguiente = conexion;

                ultimaConexion = conexion;
            }
        }
    }

    private async Task AtenderClienteAsync(ConexionCliente conexion)
    {
        try
        {
            await conexion.EnviarAsync("BIENVENIDO|Use CONECTAR|Nombre para registrarse");

            while (conexion.Activa)
            {
                string? mensaje = await conexion.Lector.ReadLineAsync();
                if (mensaje == null)
                    break;

                if (mensaje.Length > Protocolo.LongitudMaximaMensaje)
                {
                    await conexion.EnviarAsync(
                        Protocolo.Error("MENSAJE_DEMASIADO_LARGO", $"Máximo {Protocolo.LongitudMaximaMensaje} caracteres"));
                    continue;
                }

                await ProcesarMensajeAsync(conexion, mensaje);
            }
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Cliente desconectado por error de E/S: {ex.Message}");
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"Cliente desconectado por socket: {ex.SocketErrorCode}");
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            // Los errores internos se muestran solo en la consola del servidor.
            Console.WriteLine($"Error inesperado atendiendo cliente: {ex}");
        }
        finally
        {
            Jugador? jugador = conexion.Jugador;
            conexion.Cerrar();

            if (jugador != null)
            {
                Console.WriteLine($"Se desconectó {jugador.Nombre} (Id {jugador.Id}).");
                await BroadcastAsync(
                    $"JUGADOR_DESCONECTADO|{jugador.Id}|{Protocolo.LimpiarTexto(jugador.Nombre)}");

                // Si estaba activo, se marca como eliminado y, si era su turno,
                // se avanza solo para que la partida no quede trabada esperando
                // una jugada que ya nadie puede hacer. Todo envuelto en try/catch
                // porque esto corre dentro de un "finally": si algo de adentro
                // lanzara una excepcion sin atraparla aqui, se perderia en
                // silencio (ni siquiera saldria en la consola del servidor).
                try
                {
                    ResultadoAccionServidor resultadoDesconexion;
                    await candadoJuego.WaitAsync();
                    try { resultadoDesconexion = partida.ManejarDesconexion(jugador); }
                    finally { candadoJuego.Release(); }

                    Console.WriteLine(
                        $"ManejarDesconexion({jugador.Nombre}) -> MensajeBroadcast=\"{resultadoDesconexion.MensajeBroadcast}\"");

                    if (!string.IsNullOrEmpty(resultadoDesconexion.MensajeBroadcast))
                        await BroadcastAsync(resultadoDesconexion.MensajeBroadcast);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error avanzando el turno tras la desconexion de {jugador.Nombre}: {ex}");
                }
            }
        }
    }

    private async Task ProcesarMensajeAsync(ConexionCliente conexion, string mensaje)
    {
        if (string.IsNullOrWhiteSpace(mensaje))
        {
            await conexion.EnviarAsync(Protocolo.Error("FORMATO_INVALIDO", "Mensaje vacío"));
            return;
        }

        string[] partes = Protocolo.Separar(mensaje);
        if (partes.Length == 0 || string.IsNullOrWhiteSpace(partes[0]))
        {
            await conexion.EnviarAsync(Protocolo.Error("FORMATO_INVALIDO", "No se indicó una acción"));
            return;
        }

        string accion = partes[0].Trim().ToUpperInvariant();

        if (accion == Protocolo.Conectar)
        {
            await ProcesarConexionAsync(conexion, partes);
            return;
        }

        if (!EsComandoConocido(accion))
        {
            await conexion.EnviarAsync(Protocolo.Error("ACCION_DESCONOCIDA", accion));
            return;
        }

        // TIRAR_DADOS_FORZADO y CONFIRMAR_TAG llevan parametros (el valor que
        // leyo la Raspberry, o la tarjeta escaneada); el resto no recibe nada.
        bool llevaParametros = accion == Protocolo.TirarDadosForzado || accion == Protocolo.ConfirmarTag;
        if (!llevaParametros && partes.Length != 1)
        {
            await conexion.EnviarAsync(Protocolo.Error("FORMATO_INVALIDO", $"{accion} no recibe parámetros"));
            return;
        }

        Jugador? jugador = conexion.Jugador;
        if (jugador == null)
        {
            await conexion.EnviarAsync(Protocolo.Error("NO_REGISTRADO", "Debe conectarse primero"));
            return;
        }

        ResultadoAccionServidor resultado;

        if (accion == Protocolo.TirarDadosForzado)
        {
            if (partes.Length != 3 || !int.TryParse(partes[1], out int valor1) || !int.TryParse(partes[2], out int valor2))
            {
                await conexion.EnviarAsync(Protocolo.Error("FORMATO_INVALIDO", "Use TIRAR_DADOS_FORZADO|valor1|valor2"));
                return;
            }

            await candadoJuego.WaitAsync();
            try { resultado = partida.TirarDadosForzado(jugador, valor1, valor2); }
            finally { candadoJuego.Release(); }

            await EnviarResultadoAsync(conexion, resultado);
            return;
        }

        if (accion == Protocolo.ConfirmarTag)
        {
            if (partes.Length != 2 || string.IsNullOrWhiteSpace(partes[1]))
            {
                await conexion.EnviarAsync(Protocolo.Error("FORMATO_INVALIDO", "Use CONFIRMAR_TAG|tag"));
                return;
            }

            await candadoJuego.WaitAsync();
            try { resultado = partida.ConfirmarTag(partes[1].Trim()); }
            finally { candadoJuego.Release(); }

            await EnviarResultadoAsync(conexion, resultado);
            return;
        }

        await candadoJuego.WaitAsync();
        try
        {
            resultado = accion switch
            {
                Protocolo.TirarDados => partida.TirarDados(jugador),
                Protocolo.ComprarPropiedad => partida.ComprarPropiedad(jugador),
                Protocolo.NoComprar => partida.NoComprar(jugador),
                Protocolo.TerminarTurno => partida.TerminarTurno(jugador),
                Protocolo.IniciarPartida => partida.IniciarPartida(jugador),
                Protocolo.ConsultarEstado => CrearResultadoPrivado(partida.CrearBloqueEstado()),
                Protocolo.ConsultarTransacciones => CrearResultadoPrivado(partida.CrearBloqueTransacciones()),
                _ => CrearResultadoPrivado(Protocolo.Error("ACCION_DESCONOCIDA", accion))
            };
        }
        finally
        {
            candadoJuego.Release();
        }

        await EnviarResultadoAsync(conexion, resultado);
    }

    private async Task ProcesarConexionAsync(ConexionCliente conexion, string[] partes)
    {
        if (partes.Length != 2 || !Protocolo.NombreValido(partes[1]))
        {
            await conexion.EnviarAsync(
                Protocolo.Error("FORMATO_INVALIDO", "Use CONECTAR|Nombre con un nombre válido"));
            return;
        }

        if (conexion.Jugador != null)
        {
            await conexion.EnviarAsync(Protocolo.Error("YA_REGISTRADO", "Esta conexión ya tiene un jugador asociado"));
            return;
        }

        ResultadoAccionServidor resultado;

        await candadoJuego.WaitAsync();
        try
        {
            resultado = partida.RegistrarJugador(partes[1]);
            if (resultado.JugadorRegistrado != null)
                conexion.Jugador = resultado.JugadorRegistrado;
        }
        finally
        {
            candadoJuego.Release();
        }

        await EnviarResultadoAsync(conexion, resultado);
    }

    private static ResultadoAccionServidor CrearResultadoPrivado(string mensaje)
    {
        return new ResultadoAccionServidor { RespuestaPrivada = mensaje };
    }

    private static bool EsComandoConocido(string accion)
    {
        return accion == Protocolo.TirarDados
            || accion == Protocolo.TirarDadosForzado
            || accion == Protocolo.ComprarPropiedad
            || accion == Protocolo.NoComprar
            || accion == Protocolo.TerminarTurno
            || accion == Protocolo.IniciarPartida
            || accion == Protocolo.ConsultarEstado
            || accion == Protocolo.ConsultarTransacciones
            || accion == Protocolo.ConfirmarTag;
    }

    private async Task EnviarResultadoAsync(ConexionCliente conexion, ResultadoAccionServidor resultado)
    {
        // Enviamos los mensajes después de liberar el candado del juego.
        if (!string.IsNullOrEmpty(resultado.RespuestaPrivada))
            await conexion.EnviarAsync(resultado.RespuestaPrivada);

        if (!string.IsNullOrEmpty(resultado.MensajeBroadcast))
            await BroadcastAsync(resultado.MensajeBroadcast);
    }

    private async Task BroadcastAsync(string mensaje)
    {
        // Las conexiones inactivas se quedan en la lista, pero se ignoran al hacer broadcast.
        ConexionCliente? actual = primeraConexion;
        while (actual != null)
        {
            if (actual.Activa && actual.Jugador != null)
                await actual.EnviarAsync(mensaje);

            actual = actual.Siguiente;
        }
    }
}
