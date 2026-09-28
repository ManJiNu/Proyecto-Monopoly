public class Banco
{
    public ListaTransacciones Historial { get; private set; } //Lista del historial de transacciones
    private int contadorId; // para generar Id incremental de cada transacción

    public Banco()
    {
        Historial = new ListaTransacciones();
        contadorId = 1;
    }

    //Función que procesa el pago de alquiler: jugadorQuePaga hacia propietario
    public bool PagarAlquiler(Jugador jugadorQuePaga, Propiedad propiedad, int numeroTurno)
    {
        int monto = propiedad.Alquiler;
        Jugador propietario = propiedad.Propietario;

        bool pagoExitoso = jugadorQuePaga.PagarDinero(monto);
        if (!pagoExitoso)
        {
            return false; // el llamador decide qué hacer (posible eliminación)
        }

        propietario.RecibirDinero(monto);

        RegistrarTransaccion(numeroTurno, "PagoAlquiler", jugadorQuePaga, propietario, monto,
            $"{jugadorQuePaga.Nombre} pagó alquiler de {propiedad.Nombre} a {propietario.Nombre}");

        return true;
    }

    //Funcion que procesa la compra de una propiedad disponible
    public bool ComprarPropiedad(Jugador jugador, Propiedad propiedad, int numeroTurno)
    {
        if (propiedad.Propietario != null)
        {
            return false; // ya tiene dueño
        }

        bool pagoExitoso = jugador.PagarDinero(propiedad.PrecioCompra);
        if (!pagoExitoso)
        {
            return false; // no le alcanza el dinero
        }

        propiedad.Propietario = jugador; // antes: propiedad.AsignarPropietario(jugador), método que no existía
        jugador.Propiedades.AgregarPropiedad(propiedad);

        RegistrarTransaccion(numeroTurno, "CompraPropiedad", jugador, null, propiedad.PrecioCompra,
            $"{jugador.Nombre} compró {propiedad.Nombre}");

        return true;
    }

    //Funcion con pago genérico entre jugador y banco (o entre dos jugadores), usado por eventos
    public bool ProcesarPago(Jugador origen, Jugador destino, int monto, string tipo, int numeroTurno, string descripcion)
    {
        if (origen != null)
        {
            bool pagoExitoso = origen.PagarDinero(monto);
            if (!pagoExitoso)
            {
                return false;
            }
        }

        if (destino != null)
        {
            destino.RecibirDinero(monto);
        }

        RegistrarTransaccion(numeroTurno, tipo, origen, destino, monto, descripcion);
        return true;
    }

    // Metodo que registra la trasacciones en el historial
    private void RegistrarTransaccion(int numeroTurno, string tipo, Jugador origen, Jugador destino, int monto, string descripcion)
    {
        Transaccion t = new Transaccion(contadorId, numeroTurno, tipo, origen, destino, monto, descripcion);
        contadorId++;
        Historial.AgregarTransaccion(t);
    }
}
