using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Monopoly.GUI
{
    // Ventana del modo "en red": se conecta como cliente TCP al servidor de
    // tu compañero (Servidor.cs, protocolo en Protocolo.cs) y dibuja el
    // mismo tablero visual que el modo local, pero el estado del juego
    // (saldos, posiciones, dueños de propiedades, de quién es el turno) lo
    // manda el servidor. Esta ventana nunca crea un Juego local: todo lo que
    // se ve aquí es una "foto" de los mensajes que llegan por la red.
    public partial class VentanaRed : Window
    {
        private readonly ClienteRed cliente = new ClienteRed();

        // Tablero construido localmente solo para dibujar (colores, precios,
        // nombres): es el mismo ListaTablero.ConstruirTableroPredeterminado()
        // que usa Program.cs para levantar el servidor, así que los ID de
        // casilla coinciden 1 a 1 con los que manda el servidor.
        private readonly ListaTablero tableroVisual = ListaTablero.ConstruirTableroPredeterminado();

        private readonly Border[] celdasPorIndice = new Border[24];
        private readonly StackPanel[] panelesFichasPorIndice = new StackPanel[24];
        private readonly TextBlock[] textosDuenoPorIndice = new TextBlock[24];
        private readonly int?[] propietarioPorCasilla = new int?[24];

        private readonly JugadorRed[] jugadores = new JugadorRed[4];
        private int cantidadJugadores;
        private int totalRegistradosServidor;

        private int? miId;
        private int? idJugadorActual;
        private int? propiedadPendienteId;
        private bool miTurnoDadosLanzados;
        private bool partidaTerminada;

        // Estado de los pagos/compras que esperan confirmación con tarjeta
        // RFID (el cobro lo decide el servidor; aquí solo reflejamos el
        // mensaje para deshabilitar botones y mostrar el estado correcto).
        private bool esperandoConfirmacionCompra;
        private int? idJugadorEnConfirmacionCompra;
        private int? idJugadorPagaAlquiler;

        // Lector RFID / dado físico de la Raspberry Pi: se conecta igual que
        // en el modo local (MainWindow), pero en vez de llamar directamente
        // a un Juego local, reenvía lo que lee como comandos de red
        // (TIRAR_DADOS_FORZADO / CONFIRMAR_TAG) hacia el servidor.
        private readonly ConectorRaspberry conector = new ConectorRaspberry(5050);

        // Buffer para armar el archivo de texto con el historial de
        // transacciones que pide el servidor (CONSULTAR_TRANSACCIONES).
        private StringBuilder bufferTransacciones;
        private bool exportandoTransacciones;

        private static readonly Brush[] coloresJugador =
        {
            new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)), // rojo
            new SolidColorBrush(Color.FromRgb(0x29, 0x80, 0xB9)), // azul
            new SolidColorBrush(Color.FromRgb(0xF1, 0xC4, 0x0F)), // amarillo
            new SolidColorBrush(Color.FromRgb(0x9B, 0x59, 0xB6))  // morado
        };

        public VentanaRed()
        {
            InitializeComponent();
            ConstruirCeldasDelTablero();

            cliente.LineaRecibida += linea => Dispatcher.Invoke(() => ProcesarLinea(linea));
            cliente.Desconectado += () => Dispatcher.Invoke(() =>
            {
                Log("Se perdió la conexión con el servidor.");
                TxtEstadoPartida.Text = "Desconectado.";
                DeshabilitarBotonesDeJuego();
            });

            // Igual que en el modo local: la Raspberry Pi manda "DADO:v1,v2"
            // o "RFID:tag" por la misma red WiFi, en el puerto 5050. Aquí no
            // se aplica nada localmente: solo se traduce a un comando de red
            // y el servidor es quien valida y decide qué pasa.
            conector.DadoRecibido += (valor1, valor2) =>
            {
                Dispatcher.Invoke(() =>
                {
                    _ = cliente.EnviarAsync($"TIRAR_DADOS_FORZADO|{valor1}|{valor2}");
                });
            };
            conector.TagRecibido += (tag) =>
            {
                Dispatcher.Invoke(() =>
                {
                    _ = cliente.EnviarAsync($"CONFIRMAR_TAG|{tag}");
                });
            };
            if (!conector.Iniciar())
            {
                Log("No se pudo abrir el puerto 5050 para la Raspberry Pi (probablemente ya hay otra ventana del juego abierta en esta laptop usandolo). Esta ventana funciona normal, solo que no va a recibir el dado fisico ni el lector RFID.");
            }
        }

        private void Log(string texto)
        {
            TxtLog.AppendText(texto + Environment.NewLine);
            TxtLog.ScrollToEnd();
        }

        // ---------------- Conexión ----------------

        private async void BtnConectar_Click(object sender, RoutedEventArgs e)
        {
            string nombre = TxtNombre.Text.Trim();
            if (string.IsNullOrEmpty(nombre))
            {
                MessageBox.Show("Escribe un nombre.");
                return;
            }
            if (!int.TryParse(TxtPuerto.Text.Trim(), out int puerto))
            {
                MessageBox.Show("El puerto debe ser un número.");
                return;
            }

            try
            {
                await cliente.ConectarAsync(TxtHost.Text.Trim(), puerto);
                await cliente.EnviarAsync($"CONECTAR|{nombre}");
                PanelConexion.IsEnabled = false;
                BtnActualizarEstado.IsEnabled = true;
                BtnExportarRed.IsEnabled = true;
                BtnIniciarPartidaRed.IsEnabled = true;
                TxtEstadoPartida.Text = "Conectado. Esperando a los demás jugadores...";
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo conectar: " + ex.Message);
            }
        }

        // ---------------- Acciones (mandan comandos del protocolo) ----------------

        private void BtnLanzarDados_Click(object sender, RoutedEventArgs e)
        {
            // Ya no se tira el dado "digital" al dar clic: el numero que mueve
            // al jugador siempre viene del dado fisico (Raspberry), que llega
            // por conector.DadoRecibido y manda TIRAR_DADOS_FORZADO al servidor.
            Log("Presiona el boton del dado fisico para tirar.");
        }
        private async void BtnComprar_Click(object sender, RoutedEventArgs e) => await cliente.EnviarAsync("COMPRAR_PROPIEDAD");
        private async void BtnNoComprar_Click(object sender, RoutedEventArgs e) => await cliente.EnviarAsync("NO_COMPRAR");
        private async void BtnTerminarTurno_Click(object sender, RoutedEventArgs e) => await cliente.EnviarAsync("TERMINAR_TURNO");
        private async void BtnActualizarEstado_Click(object sender, RoutedEventArgs e) => await cliente.EnviarAsync("CONSULTAR_ESTADO");
        private async void BtnIniciarPartidaRed_Click(object sender, RoutedEventArgs e) => await cliente.EnviarAsync("INICIAR_PARTIDA");

        private async void BtnExportarRed_Click(object sender, RoutedEventArgs e)
        {
            exportandoTransacciones = true;
            bufferTransacciones = new StringBuilder();
            await cliente.EnviarAsync("CONSULTAR_TRANSACCIONES");
        }

        // ---------------- Construcción visual del tablero (igual que en modo local) ----------------

        private (int fila, int columna) ObtenerPosicionEnGrid(int indice)
        {
            if (indice <= 6) return (6, 6 - indice);
            if (indice <= 12) return (6 - (indice - 6), 0);
            if (indice <= 18) return (0, indice - 12);
            return (indice - 18, 6);
        }

        private void ObtenerEstiloCasillaEspecial(string nombre, out Color fondo, out Color texto)
        {
            switch (nombre)
            {
                case "Salida":
                    fondo = Color.FromRgb(0x27, 0xAE, 0x60);
                    texto = Colors.White;
                    break;
                case "Cárcel (De visita)":
                    fondo = Color.FromRgb(0xD2, 0x8B, 0x4A);
                    texto = Colors.White;
                    break;
                case "Parqueo Gratis":
                    fondo = Color.FromRgb(0x34, 0x98, 0xDB);
                    texto = Colors.White;
                    break;
                case "Ir a la Cárcel":
                    fondo = Color.FromRgb(0xC0, 0x39, 0x2B);
                    texto = Colors.White;
                    break;
                default:
                    fondo = Colors.LightGray;
                    texto = Colors.Black;
                    break;
            }
        }

        private void ConstruirCeldasDelTablero()
        {
            NodoTablero actual = tableroVisual.CabezaNodo;
            int indice = 0;
            do
            {
                Casilla casilla = actual.CasillaActual;
                var (fila, columna) = ObtenerPosicionEnGrid(indice);

                Border celda = new Border
                {
                    BorderBrush = Brushes.DarkGray,
                    BorderThickness = new Thickness(0.75),
                    CornerRadius = new CornerRadius(3),
                    Margin = new Thickness(1.5),
                    Background = Brushes.White,
                    ClipToBounds = true
                };

                Grid contenedorCelda = new Grid();

                if (casilla is Propiedad propiedad)
                {
                    contenedorCelda.RowDefinitions.Add(new RowDefinition { Height = new GridLength(7) });
                    contenedorCelda.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                    Rectangle franja = new Rectangle
                    {
                        Fill = (Brush)new BrushConverter().ConvertFromString(propiedad.ColorGrupo)
                    };
                    Grid.SetRow(franja, 0);
                    contenedorCelda.Children.Add(franja);

                    StackPanel contenido = new StackPanel
                    {
                        Margin = new Thickness(3),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    contenido.Children.Add(new TextBlock
                    {
                        Text = casilla.Nombre,
                        FontWeight = FontWeights.SemiBold,
                        FontSize = 9.5,
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center,
                        Foreground = Brushes.Black
                    });
                    contenido.Children.Add(new TextBlock
                    {
                        Text = $"${propiedad.PrecioCompra}",
                        FontSize = 9,
                        TextAlignment = TextAlignment.Center,
                        Foreground = Brushes.DimGray
                    });

                    TextBlock txtDueno = new TextBlock
                    {
                        FontSize = 8,
                        FontWeight = FontWeights.Bold,
                        TextAlignment = TextAlignment.Center
                    };
                    contenido.Children.Add(txtDueno);
                    textosDuenoPorIndice[indice] = txtDueno;

                    StackPanel panelFichas = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    contenido.Children.Add(panelFichas);
                    panelesFichasPorIndice[indice] = panelFichas;

                    Grid.SetRow(contenido, 1);
                    contenedorCelda.Children.Add(contenido);
                }
                else
                {
                    Color fondo, textoColor;
                    if (casilla is CasillaEvento)
                    {
                        fondo = Color.FromRgb(0x8E, 0x44, 0xAD);
                        textoColor = Colors.White;
                    }
                    else
                    {
                        ObtenerEstiloCasillaEspecial(casilla.Nombre, out fondo, out textoColor);
                    }

                    celda.Background = new SolidColorBrush(fondo);

                    StackPanel contenido = new StackPanel
                    {
                        Margin = new Thickness(3),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    contenido.Children.Add(new TextBlock
                    {
                        Text = casilla is CasillaEvento ? "?" : casilla.Nombre,
                        FontWeight = FontWeights.Bold,
                        FontSize = casilla is CasillaEvento ? 16 : 9.5,
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center,
                        Foreground = new SolidColorBrush(textoColor)
                    });

                    StackPanel panelFichas = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    contenido.Children.Add(panelFichas);
                    panelesFichasPorIndice[indice] = panelFichas;

                    contenedorCelda.Children.Add(contenido);
                }

                celda.Child = contenedorCelda;
                Grid.SetRow(celda, fila);
                Grid.SetColumn(celda, columna);
                TableroGrid.Children.Add(celda);
                celdasPorIndice[indice] = celda;

                actual = actual.Siguiente;
                indice++;
            } while (actual != tableroVisual.CabezaNodo);
        }

        // ---------------- Estado de jugadores (armado a partir de la red) ----------------

        private JugadorRed BuscarOCrearJugador(int id)
        {
            for (int i = 0; i < cantidadJugadores; i++)
            {
                if (jugadores[i].Id == id) return jugadores[i];
            }

            JugadorRed nuevo = new JugadorRed { Id = id };
            jugadores[cantidadJugadores] = nuevo;
            cantidadJugadores++;
            return nuevo;
        }

        private string NombreDe(int id)
        {
            for (int i = 0; i < cantidadJugadores; i++)
            {
                if (jugadores[i].Id == id) return jugadores[i].Nombre;
            }
            return $"Jugador {id}";
        }

        private int IndiceDeColor(int id)
        {
            for (int i = 0; i < cantidadJugadores; i++)
            {
                if (jugadores[i].Id == id) return i;
            }
            return 0;
        }

        private void AgregarOActualizarJugador(int id, string nombre, int saldo, int casillaId, bool activo, bool enCarcel)
        {
            JugadorRed jugador = BuscarOCrearJugador(id);
            jugador.Nombre = nombre;
            jugador.Saldo = saldo;
            jugador.CasillaId = casillaId;
            jugador.Activo = activo;
            jugador.EnCarcel = enCarcel;
        }

        private void ActualizarSaldo(int id, int saldo) => BuscarOCrearJugador(id).Saldo = saldo;
        private void ActualizarActivo(int id, bool activo) => BuscarOCrearJugador(id).Activo = activo;
        private void ActualizarCarcel(int id, bool carcel) => BuscarOCrearJugador(id).EnCarcel = carcel;
        private void ActualizarPosicion(int id, int casillaId) => BuscarOCrearJugador(id).CasillaId = casillaId;

        private void ActualizarColorPropiedad(int casillaId)
        {
            if (casillaId < 0 || casillaId > 23) return;
            TextBlock txt = textosDuenoPorIndice[casillaId];
            if (txt == null) return;

            int? propietarioId = propietarioPorCasilla[casillaId];
            if (propietarioId == null)
            {
                txt.Text = "";
            }
            else
            {
                txt.Text = NombreDe(propietarioId.Value);
                txt.Foreground = coloresJugador[IndiceDeColor(propietarioId.Value) % coloresJugador.Length];
            }
        }

        // ---------------- Procesamiento de mensajes del servidor ----------------
        // Cada línea que manda el servidor viene con este formato:
        // TIPO|dato1|dato2|... (ver Protocolo.cs y PartidaServidor.cs).

        private void ProcesarLinea(string linea)
        {
            if (string.IsNullOrWhiteSpace(linea)) return;
            string[] partes = linea.Split('|');
            string tipo = partes[0];

            switch (tipo)
            {
                case "BIENVENIDO":
                    Log(linea);
                    break;

                case "CONECTADO":
                    miId = int.Parse(partes[1]);
                    AgregarOActualizarJugador(miId.Value, partes[2], int.Parse(partes[3]), 0, true, false);
                    Log($"Conectado como {partes[2]} (id {miId}), saldo {partes[3]}.");
                    break;

                case "ERROR":
                    Log($"Error del servidor: {partes[1]}" + (partes.Length > 2 ? " - " + partes[2] : ""));
                    break;

                case "JUGADOR_CONECTADO":
                {
                    int id = int.Parse(partes[1]);
                    totalRegistradosServidor = int.Parse(partes[3]);
                    if (id != miId)
                    {
                        AgregarOActualizarJugador(id, partes[2], 0, 0, true, false);
                    }
                    Log($"{partes[2]} se unió ({partes[3]}/{partes[4]}).");
                    break;
                }

                case "PARTIDA_INICIADA":
                    partidaTerminada = false;
                    BtnIniciarPartidaRed.IsEnabled = false;
                    Log($"¡La partida inició! Turno de {partes[3]}.");
                    _ = cliente.EnviarAsync("CONSULTAR_ESTADO");
                    break;

                case "TURNO_ACTUAL":
                    idJugadorActual = int.Parse(partes[2]);
                    miTurnoDadosLanzados = false;
                    Log($"Turno {partes[1]}: le toca a {partes[3]}.");
                    break;

                case "DADOS":
                {
                    int id = int.Parse(partes[1]);
                    if (id == idJugadorActual) miTurnoDadosLanzados = true;
                    Log($"{NombreDe(id)} tiró {partes[2]} y {partes[3]} ({partes[4]}).");
                    break;
                }

                case "MOVIMIENTO":
                case "MOVIMIENTO_EVENTO":
                {
                    int id = int.Parse(partes[1]);
                    ActualizarPosicion(id, int.Parse(partes[2]));
                    Log($"{NombreDe(id)} cae en \"{partes[3]}\".");
                    break;
                }

                case "PASO_SALIDA":
                {
                    int id = int.Parse(partes[1]);
                    ActualizarSaldo(id, int.Parse(partes[3]));
                    Log($"{NombreDe(id)} pasó por Salida y recibió {partes[2]}.");
                    break;
                }

                case "DECISION_COMPRA":
                    // Ahora es un broadcast (antes era privado): lo reciben todas las
                    // ventanas, pero Comprar/No comprar solo se habilitan en la ventana
                    // de quien tiene el turno (ver ActualizarInterfazRed, esMiTurno).
                    propiedadPendienteId = int.Parse(partes[2]);
                    Log($"¿Comprar {partes[3]} por {partes[4]}? (alquiler {partes[5]})");
                    break;

                case "ESPERANDO_TAG_COMPRA":
                {
                    int id = int.Parse(partes[1]);
                    esperandoConfirmacionCompra = true;
                    idJugadorEnConfirmacionCompra = id;
                    propiedadPendienteId = null;
                    Log($"Acerca la tarjeta RFID de {NombreDe(id)} para confirmar la compra de {partes[3]} por {partes[4]}.");
                    break;
                }

                case "ESPERANDO_TAG_ALQUILER":
                {
                    int idPaga = int.Parse(partes[1]);
                    int idDueno = int.Parse(partes[2]);
                    idJugadorPagaAlquiler = idPaga;
                    Log($"Acerca la tarjeta RFID de {NombreDe(idPaga)} para pagar {partes[4]} de alquiler a {NombreDe(idDueno)}.");
                    break;
                }

                case "TAG_VINCULADO":
                    Log($"Tarjeta vinculada a {partes[2]}.");
                    break;

                case "ESPERANDO_TAG_VINCULACION":
                    Log($"{partes[2]}: acerca tu tarjeta RFID al lector para vincularla.");
                    break;

                case "TAG_RECONOCIDO":
                    Log($"Tarjeta reconocida: {partes[2]} (saldo {partes[3]}).");
                    break;

                case "PROPIEDAD_COMPRADA":
                {
                    int id = int.Parse(partes[1]);
                    int propiedadId = int.Parse(partes[2]);
                    propietarioPorCasilla[propiedadId] = id;
                    ActualizarSaldo(id, int.Parse(partes[5]));
                    if (propiedadId == propiedadPendienteId) propiedadPendienteId = null;
                    esperandoConfirmacionCompra = false;
                    idJugadorEnConfirmacionCompra = null;
                    ActualizarColorPropiedad(propiedadId);
                    Log($"{NombreDe(id)} compró {partes[3]} por {partes[4]}.");
                    break;
                }

                case "DECISION_COMPRA_CERRADA":
                    propiedadPendienteId = null;
                    Log("Decidiste no comprar.");
                    break;

                case "PROPIEDAD_PROPIA":
                    Log($"{NombreDe(int.Parse(partes[1]))} cayó en su propia propiedad.");
                    break;

                case "PAGO_ALQUILER":
                {
                    int idPaga = int.Parse(partes[1]);
                    int idDueno = int.Parse(partes[2]);
                    ActualizarSaldo(idPaga, int.Parse(partes[5]));
                    idJugadorPagaAlquiler = null;
                    Log($"{NombreDe(idPaga)} pagó {partes[4]} de alquiler a {NombreDe(idDueno)}.");
                    break;
                }

                case "JUGADOR_ELIMINADO":
                {
                    int id = int.Parse(partes[1]);
                    ActualizarActivo(id, false);
                    Log($"{partes[2]} quedó eliminado ({partes[3]}).");
                    break;
                }

                case "CASILLA_ESPECIAL":
                {
                    int id = int.Parse(partes[1]);
                    bool carcel = partes[4].EndsWith("True");
                    ActualizarCarcel(id, carcel);
                    Log($"{NombreDe(id)} cae en {partes[3]}.");
                    break;
                }

                case "CARTA_EVENTO":
                    Log($"Carta: {partes[3]}");
                    break;

                case "EVENTO_DINERO":
                {
                    int id = int.Parse(partes[1]);
                    ActualizarSaldo(id, int.Parse(partes[4]));
                    Log($"{NombreDe(id)} {(partes[2] == "RECIBE" ? "recibió" : "pagó")} {partes[3]}.");
                    break;
                }

                case "PERDER_TURNO":
                    Log($"{NombreDe(int.Parse(partes[1]))} perderá el próximo turno.");
                    break;

                case "TURNO_PERDIDO":
                    Log($"{partes[2]} perdió este turno.");
                    break;

                case "TURNO_SALTADO_CARCEL":
                    Log($"{partes[2]} está en la cárcel y pierde este turno.");
                    break;

                case "PARTIDA_TERMINADA":
                    partidaTerminada = true;
                    Log("¡La partida terminó! " + string.Join(" ", partes, 1, partes.Length - 1));
                    break;

                case "JUGADOR_DESCONECTADO":
                    Log($"{partes[2]} se desconectó.");
                    break;

                case "JUGADOR_ESTADO":
                {
                    int id = int.Parse(partes[1]);
                    AgregarOActualizarJugador(id, partes[2], int.Parse(partes[3]), int.Parse(partes[4]), bool.Parse(partes[6]), bool.Parse(partes[9]));
                    if (partes[8] == "ACTUAL") idJugadorActual = id;
                    break;
                }

                case "PROPIEDAD_ESTADO":
                {
                    int casillaId = int.Parse(partes[1]);
                    int propietarioId = int.Parse(partes[5]);
                    propietarioPorCasilla[casillaId] = propietarioId == 0 ? (int?)null : propietarioId;
                    ActualizarColorPropiedad(casillaId);
                    break;
                }

                case "ESTADO_INICIO":
                case "ESTADO_FIN":
                    break;

                case "TRANSACCIONES_INICIO":
                    bufferTransacciones = new StringBuilder();
                    bufferTransacciones.AppendLine("=== Historial de Transacciones (Monopoly en red) ===");
                    break;

                case "TRANSACCION":
                {
                    // TRANSACCION|id|turno|tipo|idOrigen|nombreOrigen|idDestino|nombreDestino|monto|descripcion|fechaHora
                    if (bufferTransacciones == null) break;
                    bufferTransacciones.AppendLine();
                    bufferTransacciones.AppendLine($"N° Transacción: {partes[1]}");
                    bufferTransacciones.AppendLine($"Turno: {partes[2]}");
                    bufferTransacciones.AppendLine($"Tipo: {partes[3]}");
                    bufferTransacciones.AppendLine($"Origen: {partes[5]}");
                    bufferTransacciones.AppendLine($"Destino: {partes[7]}");
                    bufferTransacciones.AppendLine($"Monto: {partes[8]}");
                    bufferTransacciones.AppendLine($"Descripción: {partes[9]}");
                    bufferTransacciones.AppendLine("-----------------------------------");
                    break;
                }

                case "TRANSACCIONES_FIN":
                    if (exportandoTransacciones && bufferTransacciones != null)
                    {
                        GuardarTransaccionesEnArchivo(bufferTransacciones.ToString());
                        exportandoTransacciones = false;
                    }
                    else
                    {
                        Log("Historial de transacciones recibido.");
                    }
                    break;

                default:
                    Log(linea);
                    break;
            }

            ActualizarInterfazRed();
        }

        private void DeshabilitarBotonesDeJuego()
        {
            BtnLanzarDadosRed.IsEnabled = false;
            BtnComprarRed.IsEnabled = false;
            BtnNoComprarRed.IsEnabled = false;
            BtnTerminarTurnoRed.IsEnabled = false;
            BtnIniciarPartidaRed.IsEnabled = false;
        }

        private void GuardarTransaccionesEnArchivo(string contenido)
        {
            Microsoft.Win32.SaveFileDialog dialogo = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Guardar historial de transacciones",
                Filter = "Archivo de texto (*.txt)|*.txt",
                FileName = "transacciones_monopoly.txt"
            };

            if (dialogo.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(dialogo.FileName, contenido);
                    Log($"Transacciones exportadas a {dialogo.FileName}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("No se pudo guardar el archivo: " + ex.Message);
                }
            }
        }

        private void ActualizarInterfazRed()
        {
            foreach (StackPanel panel in panelesFichasPorIndice)
            {
                panel?.Children.Clear();
            }
            for (int i = 0; i < cantidadJugadores; i++)
            {
                JugadorRed jugador = jugadores[i];
                if (jugador.CasillaId >= 0 && jugador.CasillaId <= 23 && panelesFichasPorIndice[jugador.CasillaId] != null)
                {
                    panelesFichasPorIndice[jugador.CasillaId].Children.Add(new Ellipse
                    {
                        Width = 11,
                        Height = 11,
                        Fill = coloresJugador[i % coloresJugador.Length],
                        Stroke = Brushes.White,
                        StrokeThickness = 1,
                        Margin = new Thickness(1)
                    });
                }
            }

            ActualizarPanelJugadores();

            if (partidaTerminada)
            {
                DeshabilitarBotonesDeJuego();
            }
            else
            {
                bool esMiTurno = miId.HasValue && idJugadorActual == miId.Value;
                bool hayDecisionPendiente = propiedadPendienteId != null;
                bool hayPagoOCompraPendiente = esperandoConfirmacionCompra || idJugadorPagaAlquiler != null;

                BtnLanzarDadosRed.IsEnabled = esMiTurno && !miTurnoDadosLanzados && !hayDecisionPendiente && !hayPagoOCompraPendiente;
                BtnComprarRed.IsEnabled = esMiTurno && hayDecisionPendiente && !hayPagoOCompraPendiente;
                BtnNoComprarRed.IsEnabled = esMiTurno && hayDecisionPendiente && !hayPagoOCompraPendiente;
                BtnTerminarTurnoRed.IsEnabled = esMiTurno && miTurnoDadosLanzados && !hayDecisionPendiente && !hayPagoOCompraPendiente;
            }

            if (partidaTerminada)
            {
                TxtEstadoPartida.Text = "La partida terminó.";
            }
            else if (idJugadorActual == null)
            {
                int enEspera = Math.Max(cantidadJugadores, totalRegistradosServidor);
                TxtEstadoPartida.Text = enEspera == 0
                    ? "Sin conectar."
                    : $"Conectado. Esperando jugadores ({enEspera}/4)...";
            }
            else if (esperandoConfirmacionCompra && idJugadorEnConfirmacionCompra.HasValue)
            {
                TxtEstadoPartida.Text = $"Esperando la tarjeta RFID de {NombreDe(idJugadorEnConfirmacionCompra.Value)} para confirmar la compra...";
            }
            else if (idJugadorPagaAlquiler.HasValue)
            {
                TxtEstadoPartida.Text = $"Esperando la tarjeta RFID de {NombreDe(idJugadorPagaAlquiler.Value)} para pagar el alquiler...";
            }
            else
            {
                bool esMiTurno = miId.HasValue && idJugadorActual == miId.Value;
                TxtEstadoPartida.Text = esMiTurno ? "¡Es tu turno!" : $"Turno de {NombreDe(idJugadorActual.Value)}...";
            }
        }

        private void ActualizarPanelJugadores()
        {
            PanelJugadores.Children.Clear();
            for (int i = 0; i < cantidadJugadores; i++)
            {
                JugadorRed jugador = jugadores[i];
                string carcel = jugador.EnCarcel ? "  ·  En la cárcel" : "";
                string estado = jugador.Activo ? "" : "  ·  Eliminado";
                string yo = jugador.Id == miId ? "  (tú)" : "";

                Border tarjeta = new Border
                {
                    Background = Brushes.White,
                    BorderBrush = coloresJugador[i % coloresJugador.Length],
                    BorderThickness = new Thickness(0, 0, 0, 3),
                    Margin = new Thickness(0, 0, 0, 4),
                    Padding = new Thickness(8, 4, 8, 4),
                    CornerRadius = new CornerRadius(4)
                };

                StackPanel fila = new StackPanel { Orientation = Orientation.Horizontal };
                fila.Children.Add(new Ellipse
                {
                    Width = 12,
                    Height = 12,
                    Fill = coloresJugador[i % coloresJugador.Length],
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
                fila.Children.Add(new TextBlock
                {
                    Text = $"{jugador.Nombre}{yo}  —  ${jugador.Saldo}{carcel}{estado}",
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                });

                tarjeta.Child = fila;
                PanelJugadores.Children.Add(tarjeta);
            }
        }
    }
}
