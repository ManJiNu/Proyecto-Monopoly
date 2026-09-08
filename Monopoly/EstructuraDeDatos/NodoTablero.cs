public class NodoTablero
{
    public Casilla CasillaActual { get; set; }
    public NodoTablero Anterior { get; set; }
    public NodoTablero Siguiente { get; set; }
    public NodoTablero(Casilla casillaactual)
    {
        CasillaActual = casillaactual;
        Anterior = null;
        Siguiente = null;
    }
}