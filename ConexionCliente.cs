using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public sealed class ConexionCliente : IDisposable
{
    private readonly SemaphoreSlim candadoEnvio = new SemaphoreSlim(1, 1);
    private bool cerrada;

    public TcpClient Cliente { get; }
    public NetworkStream Flujo { get; }
    public StreamReader Lector { get; }
    public StreamWriter Escritor { get; }
    public Jugador? Jugador { get; set; }
    public bool Activa { get; private set; }
    public ConexionCliente? Siguiente { get; set; }

    public ConexionCliente(TcpClient cliente)
    {
        Cliente = cliente;
        Flujo = cliente.GetStream();
        Lector = new StreamReader(Flujo, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        Escritor = new StreamWriter(Flujo, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true)
        {
            AutoFlush = true
        };

        Jugador = null;
        Activa = true;
        Siguiente = null;
        cerrada = false;
    }

    public async Task EnviarAsync(string mensaje)
    {
        if (!Activa)
            return;

        await candadoEnvio.WaitAsync();
        try
        {
            if (Activa)
                await Escritor.WriteLineAsync(mensaje);
        }
        catch (IOException)
        {
            Activa = false;
        }
        catch (SocketException)
        {
            Activa = false;
        }
        catch (ObjectDisposedException)
        {
            Activa = false;
        }
        finally
        {
            candadoEnvio.Release();
        }
    }

    public void Cerrar()
    {
        if (cerrada)
            return;

        cerrada = true;
        Activa = false;

        try { Escritor.Dispose(); } catch { }
        try { Lector.Dispose(); } catch { }
        try { Flujo.Dispose(); } catch { }
        try { Cliente.Close(); } catch { }
    }

    public void Dispose()
    {
        Cerrar();
        candadoEnvio.Dispose();
    }
}
