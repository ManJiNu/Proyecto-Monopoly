//Lista Enlazada Simple
public class ListaPropiedad
{
    public NodoPropiedad CabezaNodo {set;get;}
    public NodoPropiedad ColaNodo {set;get;}
    public ListaPropiedad()
    {
        CabezaNodo = null; //head
        ColaNodo = null; //tail
    }

    // Agrega Propiedad al final de la lista
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