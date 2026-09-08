// Lista Circular Doblemente Enlazada
public class ListaTablero
{
    public NodoTablero CabezaNodo { get; set; }
    public NodoTablero ColaNodo { get; set; }
    public ListaTablero(){
        CabezaNodo = null; //head
        ColaNodo = null; //tail
    }

    // Agrega Propiedad al final de la lista
    public void AgregarCasilla(Casilla casilla)
    {
        NodoTablero NuevaCasilla = new NodoTablero(casilla);
        if(CabezaNodo == null)
        {
            CabezaNodo = NuevaCasilla;
            ColaNodo = NuevaCasilla;
            CabezaNodo.Siguiente = CabezaNodo;
            ColaNodo.Anterior = CabezaNodo;
        }
        else
        {
            NuevaCasilla.Anterior = ColaNodo;
            ColaNodo.Siguiente = NuevaCasilla;
            ColaNodo = NuevaCasilla;
            ColaNodo.Siguiente = CabezaNodo;
            CabezaNodo.Anterior = ColaNodo;
        }
    }
}
