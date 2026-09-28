//Lista Enlazada Simple
public class ListaPropiedad
{
    public NodoPropiedad CabezaNodo { get; private set; }
    public NodoPropiedad ColaNodo { get; private set; }
    public ListaPropiedad()
    {
        CabezaNodo = null; //head
        ColaNodo = null; //tail
    }

    // Metodo que agrega propiedad al final de la lista
    public void AgregarPropiedad(Propiedad propiedad)
    {
        NodoPropiedad NuevaPropiedad = new NodoPropiedad(propiedad);
        if(CabezaNodo == null)
        {
            CabezaNodo = NuevaPropiedad;
            ColaNodo = NuevaPropiedad;
        }
        else
        {
            ColaNodo.Siguiente = NuevaPropiedad;
            ColaNodo = NuevaPropiedad;
        }

    }
}