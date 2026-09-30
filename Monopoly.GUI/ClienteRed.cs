using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Monopoly.GUI
{
    // Cliente TCP que se conecta al servidor de Monopoly de tu compañero
    // (Servidor.cs / PartidaServidor.cs, en la raíz del repositorio) y habla
    // el mismo protocolo de texto por línea que él espera (ver Protocolo.cs).
    public class ClienteRed
    {
        private TcpClient cliente;
        private StreamReader lector;
        private StreamWriter escritor;

        public bool Conectado { get; private set; }

        // Se dispara por cada línea que manda el servidor: una respuesta
        // privada (por ejemplo "CONECTADO|...") o un mensaje de broadcast
        // (por ejemplo "DADOS|...").
        public event Action<string> LineaRecibida;

        // Se dispara si se pierde la conexión con el servidor.
        public event Action Desconectado;

        public async Task ConectarAsync(string host, int puerto)
        {
            cliente = new TcpClient();
            await cliente.ConnectAsync(host, puerto);

            NetworkStream flujo = cliente.GetStream();
            lector = new StreamReader(flujo, Encoding.UTF8);
            escritor = new StreamWriter(flujo, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = true
            };

            Conectado = true;
            _ = Task.Run(EscucharAsync);
        }

        private async Task EscucharAsync()
        {
            try
            {
                while (Conectado && lector != null)
                {
                    string linea = await lector.ReadLineAsync();
                    if (linea == null)
                    {
                        break;
                    }
                    LineaRecibida?.Invoke(linea);
                }
            }
            catch
            {
                // La conexión se cerró o hubo un error de red; se avisa abajo.
            }
            finally
            {
                Conectado = false;
                Desconectado?.Invoke();
            }
        }

        public async Task EnviarAsync(string mensaje)
        {
            if (!Conectado || escritor == null)
            {
                return;
            }

            try
            {
                await escritor.WriteLineAsync(mensaje);
            }
            catch
            {
                Conectado = false;
                Desconectado?.Invoke();
            }
        }

        public void Cerrar()
        {
            Conectado = false;
            try { lector?.Dispose(); } catch { }
            try { escritor?.Dispose(); } catch { }
            try { cliente?.Close(); } catch { }
        }
    }
}
