using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Monopoly.GUI
{
    // Escucha conexiones de la Raspberry Pi por TCP (misma red WiFi) y
    // traduce cada línea que llega ("DADO:3,5" o "RFID:04A3B2C1") en un
    // evento que la ventana principal puede escuchar.
    public class ConectorRaspberry
    {
        private readonly TcpListener servidor;

        public event Action<int, int> DadoRecibido;
        public event Action<string> TagRecibido;

        public ConectorRaspberry(int puerto)
        {
            servidor = new TcpListener(IPAddress.Any, puerto);
        }

        public void Iniciar()
        {
            servidor.Start();
            _ = Task.Run(EscucharConexiones);
        }

        private async Task EscucharConexiones()
        {
            while (true)
            {
                try
                {
                    TcpClient cliente = await servidor.AcceptTcpClientAsync();
                    _ = Task.Run(() => AtenderCliente(cliente));
                }
                catch (SocketException)
                {
                    break; // el servidor se cerró
                }
            }
        }

        private async Task AtenderCliente(TcpClient cliente)
        {
            using (cliente)
            using (StreamReader lector = new StreamReader(cliente.GetStream()))
            {
                string linea;
                while ((linea = await lector.ReadLineAsync()) != null)
                {
                    ProcesarLinea(linea.Trim());
                }
            }
        }

        // Interpreta una línea recibida. Formato esperado:
        //   DADO:valor1,valor2   -> se lanzó el dado físico
        //   RFID:codigoDeTarjeta -> el lector detectó una tarjeta
        private void ProcesarLinea(string linea)
        {
            if (linea.StartsWith("DADO:", StringComparison.OrdinalIgnoreCase))
            {
                string[] partes = linea.Substring(5).Split(',');
                if (partes.Length == 2
                    && int.TryParse(partes[0], out int valor1)
                    && int.TryParse(partes[1], out int valor2))
                {
                    DadoRecibido?.Invoke(valor1, valor2);
                }
            }
            else if (linea.StartsWith("RFID:", StringComparison.OrdinalIgnoreCase))
            {
                string tag = linea.Substring(5).Trim();
                if (!string.IsNullOrEmpty(tag))
                {
                    TagRecibido?.Invoke(tag);
                }
            }
        }
    }
}
