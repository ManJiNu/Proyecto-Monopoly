// Lista Circular Doblemente Enlazada
public class ListaTablero
{
    public NodoTablero CabezaNodo { get; private set; }
    public NodoTablero ColaNodo { get; private set; }
    public ListaTablero(){
        CabezaNodo = null; //head
        ColaNodo = null; //tail
    }

    //Metodo que agrega la Propiedad al final de la lista
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
