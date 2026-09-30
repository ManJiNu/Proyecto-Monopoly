using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Monopoly.GUI
{
    public partial class MainWindow : Window
    {
        private readonly Juego juego = new Juego();
        private readonly Border[] celdasPorIndice = new Border[24];
        private readonly StackPanel[] panelesFichasPorIndice = new StackPanel[24];
        private bool dadosLanzadosEnTurno;
        private readonly ConectorRaspberry conector = new ConectorRaspberry(5050);

        private static readonly Brush[] coloresJugador =
        {
            new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)), // rojo
            new SolidColorBrush(Color.FromRgb(0x29, 0x80, 0xB9)), // azul
            new SolidColorBrush(Color.FromRgb(0xF1, 0xC4, 0x0F)), // amarillo
            new SolidColorBrush(Color.FromRgb(0x9B, 0x59, 0xB6))  // morado
        };

        public MainWindow()
        {
            InitializeComponent();
            juego.Mensaje += RegistrarMensaje;
            ConstruirCeldasDelTablero();
            ActualizarInterfaz();

            // Escucha lo que mande la Raspberry Pi (dado físico y lector RFID)
            // por la misma red WiFi, en el puerto 5050.
            conector.DadoRecibido += (valor1, valor2) =>
            {
                Dispatcher.Invoke(() =>
                {
                    try { juego.LanzarDadosConValor(valor1, valor2); dadosLanzadosEnTurno = true; }
                    catch (InvalidOperationException ex) { RegistrarMensaje(ex.Message); }
                    ActualizarInterfaz();
                });
            };
            conector.TagRecibido += (tag) =>
            {
                Dispatcher.Invoke(() =>
                {
                    juego.RegistrarTag(tag);
                    ActualizarInterfaz();
                });
            };
            if (!conector.Iniciar())
            {
                RegistrarMensaje("No se pudo abrir el puerto 5050 para la Raspberry Pi (probablemente ya hay otra ventana del juego abierta en esta laptop usandolo). Esta ventana funciona normal, solo que no va a recibir el dado fisico ni el lector RFID.");
            }
        }

        private void RegistrarMensaje(string texto)
        {
            TxtLog.AppendText(texto + Environment.NewLine);
            TxtLog.ScrollToEnd();
        }

        // Ubica cada una de las 24 casillas alrededor del anillo de 7x7
        // (con un hueco de 5x5 en el centro para el panel de controles).
        private (int fila, int columna) ObtenerPosicionEnGrid(int indice)
        {
            if (indice <= 6) return (6, 6 - indice);
            if (indice <= 12) return (6 - (indice - 6), 0);
            if (indice <= 18) return (0, indice - 12);
            return (indice - 18, 6);
        }

        // Colores de fondo/texto de cada tipo de casilla especial, para que
        // se vean como en un tablero de Monopoly real.
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
            NodoTablero actual = juego.Tablero.CabezaNodo;
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
            } while (actual != juego.Tablero.CabezaNodo);
        }

        private int ObtenerIndiceDeNodo(NodoTablero nodo)
        {
            NodoTablero actual = juego.Tablero.CabezaNodo;
            int indice = 0;
            do
            {
                if (actual == nodo) return indice;
                actual = actual.Siguiente;
                indice++;
            } while (actual != juego.Tablero.CabezaNodo);
            return -1;
        }

        private void BtnAgregarJugador_Click(object sender, RoutedEventArgs e)
        {
            string nombre = TxtNombreJugador.Text.Trim();
            if (string.IsNullOrEmpty(nombre))
            {
                MessageBox.Show("Escribe un nombre.");
                return;
            }
            try { juego.AgregarJugador(nombre); TxtNombreJugador.Clear(); }
            catch (InvalidOperationException ex) { MessageBox.Show(ex.Message); }
            ActualizarInterfaz();
        }

        private void BtnIniciarPartida_Click(object sender, RoutedEventArgs e)
        {
            try { juego.IniciarPartida(); dadosLanzadosEnTurno = false; }
            catch (InvalidOperationException ex) { MessageBox.Show(ex.Message); }
            ActualizarInterfaz();
        }

        private void BtnLanzarDados_Click(object sender, RoutedEventArgs e)
        {
            try { juego.LanzarDados(); dadosLanzadosEnTurno = true; }
            catch (InvalidOperationException ex) { MessageBox.Show(ex.Message); }
            ActualizarInterfaz();
        }

        private void BtnComprar_Click(object sender, RoutedEventArgs e)
        {
            try { juego.ComprarPropiedadActual(); }
            catch (InvalidOperationException ex) { MessageBox.Show(ex.Message); }
            ActualizarInterfaz();
        }

        private void BtnNoComprar_Click(object sender, RoutedEventArgs e)
        {
            juego.NoComprarPropiedadActual();
            ActualizarInterfaz();
        }

        private void BtnTerminarTurno_Click(object sender, RoutedEventArgs e)
        {
            try { juego.TerminarTurno(); dadosLanzadosEnTurno = false; }
            catch (InvalidOperationException ex) { MessageBox.Show(ex.Message); }
            ActualizarInterfaz();
        }

        private void BtnExportar_Click(object sender, RoutedEventArgs e)
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
                    juego.Banco.Historial.ExportarTXT(dialogo.FileName);
                    RegistrarMensaje($"Transacciones exportadas a {dialogo.FileName}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("No se pudo guardar el archivo: " + ex.Message);
                }
            }
        }

        private void ActualizarInterfaz()
        {
            foreach (StackPanel panel in panelesFichasPorIndice)
            {
                panel.Children.Clear();
            }

            if (juego.Turnos.CabezaNodo != null)
            {
                NodoTurno actual = juego.Turnos.CabezaNodo;
                int colorIndice = 0;
                do
                {
                    Jugador jugador = actual.JugadorDelTurno;
                    if (jugador.PosicionActual != null)
                    {
                        int indice = ObtenerIndiceDeNodo(jugador.PosicionActual);
                        if (indice >= 0)
                        {
                            panelesFichasPorIndice[indice].Children.Add(new Ellipse
                            {
                                Width = 11,
                                Height = 11,
                                Fill = coloresJugador[colorIndice % coloresJugador.Length],
                                Stroke = Brushes.White,
                                StrokeThickness = 1,
                                Margin = new Thickness(1)
                            });
                        }
                    }
                    colorIndice++;
                    actual = actual.Siguiente;
                } while (actual != juego.Turnos.CabezaNodo);
            }

            ActualizarPanelJugadores();

            bool registrando = !juego.PartidaIniciada;
            bool activa = juego.PartidaIniciada;
            bool hayPagoOCompraPendiente = juego.HayPagoOCompraPendiente();
            bool hayDecisionPendiente = juego.PropiedadPendiente != null && !juego.EsperandoConfirmacionCompra;

            PanelRegistro.IsEnabled = registrando;
            BtnAgregarJugador.IsEnabled = registrando && juego.CantidadJugadores() < Juego.MaximoJugadores;
            BtnIniciarPartida.IsEnabled = registrando && juego.CantidadJugadores() >= 2;

            BtnLanzarDados.IsEnabled = activa && !dadosLanzadosEnTurno && !hayDecisionPendiente && !hayPagoOCompraPendiente;
            BtnComprar.IsEnabled = activa && hayDecisionPendiente;
            BtnNoComprar.IsEnabled = activa && hayDecisionPendiente;
            BtnTerminarTurno.IsEnabled = activa && dadosLanzadosEnTurno && !hayDecisionPendiente && !hayPagoOCompraPendiente;

            TxtDados.Text = dadosLanzadosEnTurno
                ? $"{juego.Dado.Dado1}  +  {juego.Dado.Dado2}  =  {juego.Dado.Total}"
                : "Sin lanzar";

            if (registrando)
            {
                TxtEstadoPartida.Text = "Registrando jugadores (mínimo 2, máximo 4)...";
            }
            else if (juego.EsperandoConfirmacionCompra)
            {
                TxtEstadoPartida.Text = $"Acerca la tarjeta RFID de {juego.JugadorActual().Nombre} para confirmar la compra...";
            }
            else if (juego.AlquilerPendiente != null)
            {
                TxtEstadoPartida.Text = $"Acerca la tarjeta RFID de {juego.JugadorQuePagaAlquiler.Nombre} para pagar el alquiler...";
            }
            else
            {
                TxtEstadoPartida.Text = $"Turno {juego.NumeroTurno} — le toca a {juego.JugadorActual().Nombre}";
            }
        }

        private void ActualizarPanelJugadores()
        {
            PanelJugadores.Children.Clear();
            if (juego.Turnos.CabezaNodo == null) return;

            NodoTurno actual = juego.Turnos.CabezaNodo;
            int colorIndice = 0;
            do
            {
                Jugador jugador = actual.JugadorDelTurno;
                string carcel = jugador.EstaEnCarcel ? "  ·  En la cárcel" : "";
                string estado = jugador.Activo ? "" : "  ·  Eliminado";

                Border tarjeta = new Border
                {
                    Background = Brushes.White,
                    BorderBrush = coloresJugador[colorIndice % coloresJugador.Length],
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
                    Fill = coloresJugador[colorIndice % coloresJugador.Length],
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
                fila.Children.Add(new TextBlock
                {
                    Text = $"{jugador.Nombre}  —  ${jugador.Saldo}{carcel}{estado}",
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                });

                tarjeta.Child = fila;
                PanelJugadores.Children.Add(tarjeta);

                colorIndice++;
                actual = actual.Siguiente;
            } while (actual != juego.Turnos.CabezaNodo);
        }
    }
}
