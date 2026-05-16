using System;
using System.Collections.Generic;


namespace NikSBO.models
{
    /// <summary>
    /// Clase base para los documentos de marketing de SAP B1 (pedidos, facturas,
    /// albaranes, ofertas, abonos, etc.). Contiene la cabecera común a todos ellos.
    /// Las subclases añaden el endpoint concreto vía <see cref="B1EntityAttribute"/>.
    /// Hereda de <see cref="B1Model"/> para soportar UDFs vía <c>GetUDF&lt;T&gt;</c>.
    /// <para>
    /// Casi todos los campos van como nullable (<c>?</c>) porque SAP puede no
    /// informarlos según el tipo de documento o su estado; si no se marca nullable,
    /// la deserialización peta al recibir <c>null</c>.
    /// </para>
    /// </summary>
    public class MarketingDocument : B1Model
    {
        /// <summary>Identificador interno único del documento (clave primaria).</summary>
        public int DocEntry { get; set; }

        /// <summary>Número visible del documento para el usuario.</summary>
        public int DocNum { get; set; }

        /// <summary>Código del socio de negocio asociado al documento.</summary>
        public string? CardCode { get; set; }

        /// <summary>Nombre del socio de negocio asociado al documento.</summary>
        public string? CardName { get; set; }

        /// <summary>Estado del documento: <c>bost_Open</c>, <c>bost_Close</c>, etc.</summary>
        public string? DocumentStatus { get; set; }

        /// <summary>Fecha de contabilización del documento.</summary>
        public DateTime? DocDate { get; set; }

        /// <summary>Fecha de vencimiento del documento.</summary>
        public DateTime? DocDueDate { get; set; }

        /// <summary>Fecha de impuestos del documento.</summary>
        public DateTime? TaxDate { get; set; }

        /// <summary>Número de referencia del socio (nº pedido del cliente, etc.).</summary>
        public string? NumAtCard { get; set; }

        /// <summary>Moneda del documento.</summary>
        public string? DocCurrency { get; set; }

        /// <summary>Comentarios del documento.</summary>
        public string? Comments { get; set; }

        /// <summary>Total del documento con impuestos.</summary>
        public double? DocTotal { get; set; }

        /// <summary>Total de IVA del documento.</summary>
        public double? VatSum { get; set; }

        /// <summary>Descuento global aplicado al documento en porcentaje.</summary>
        public double? DiscountPercent { get; set; }

        /// <summary>Código del vendedor asignado.</summary>
        public int? SalesPersonCode { get; set; }

        /// <summary>Código de la persona de contacto del socio.</summary>
        public int? ContactPersonCode { get; set; }

        /// <summary>Sucursal (BPL) asignada al documento en entornos multi-sucursal.</summary>
        public int? BPL_IDAssignedToInvoice { get; set; }

        /// <summary>Líneas de detalle del documento. <c>null</c> si no se han pedido.</summary>
        public List<DocumentLine>? DocumentLines { get; set; }
    }

    /// <summary>
    /// Línea de un <see cref="MarketingDocument"/>. Cada línea representa un artículo
    /// con su cantidad, precio y totales. Hereda de <see cref="B1Model"/> para soportar
    /// UDFs de línea (<c>U_*</c>) vía <c>GetUDF&lt;T&gt;</c>.
    /// Los campos numéricos van como nullable por el mismo motivo que la cabecera:
    /// SAP puede devolver <c>null</c> en algunos tipos de documento/estado y, si no son
    /// nullable, la deserialización peta.
    /// </summary>
    public class DocumentLine : B1Model
    {
        /// <summary>Número de línea dentro del documento (0-indexado por SAP).</summary>
        public int? LineNum { get; set; }

        /// <summary>Código del artículo.</summary>
        public string ItemCode { get; set; }

        /// <summary>Descripción del artículo en la línea (puede diferir del maestro).</summary>
        public string ItemDescription { get; set; }

        /// <summary>Cantidad del artículo.</summary>
        public double? Quantity { get; set; }

        /// <summary>Precio unitario sin impuestos.</summary>
        public double? Price { get; set; }

        /// <summary>Importe total de la línea (cantidad × precio, con descuentos).</summary>
        public double? LineTotal { get; set; }

        /// <summary>Moneda de la línea.</summary>
        public string Currency { get; set; }

        /// <summary>Código del almacén desde el que se mueve el stock.</summary>
        public string WarehouseCode { get; set; }

        /// <summary>Código de impuesto aplicado a la línea.</summary>
        public string TaxCode { get; set; }
    }

    /// <summary>Pedido de venta (<c>Orders</c>).</summary>
    [B1Entity("Orders")]
    public class SalesOrder : MarketingDocument { }

    /// <summary>Factura de venta (<c>Invoices</c>).</summary>
    [B1Entity("Invoices")]
    public class Invoice : MarketingDocument { }

    /// <summary>Oferta de venta (<c>Quotations</c>).</summary>
    [B1Entity("Quotations")]
    public class Quotation : MarketingDocument { }

    /// <summary>Albarán de venta (<c>DeliveryNotes</c>).</summary>
    [B1Entity("DeliveryNotes")]
    public class DeliveryNote : MarketingDocument { }

    /// <summary>Nota de abono de venta (<c>CreditNotes</c>).</summary>
    [B1Entity("CreditNotes")]
    public class CreditNote : MarketingDocument { }
    
    /// <summary>Devolución de venta (<c>Returns</c>).</summary>
    [B1Entity("Returns")]
    public class Return : MarketingDocument { }

    /// <summary>Solicitud de anticipo (<c>DownPaymentRequests</c>).</summary>
    [B1Entity("DownPaymentRequests")]
    public class DownPaymentRequest : MarketingDocument { }

    /// <summary>Borrador de documento (<c>Drafts</c>).</summary>
    [B1Entity("Drafts")]
    public class Draft : MarketingDocument { }
    
    /// <summary>Factura de anticipo (<c>DownPayments</c>).</summary>
    [B1Entity("DownPayments")]
    public class DownPayment : MarketingDocument { }

    /// <summary>Pedido de compra (<c>PurchaseOrders</c>).</summary>
    [B1Entity("PurchaseOrders")]
    public class PurchaseOrder : MarketingDocument { }

    /// <summary>Factura de compra (<c>PurchaseInvoices</c>).</summary>
    [B1Entity("PurchaseInvoices")]
    public class PurchaseInvoice : MarketingDocument { }
    
    /// <summary>Solicitud de compra (<c>PurchaseRequests</c>).</summary>
    [B1Entity("PurchaseRequests")]
    public class PurchaseRequest : MarketingDocument { }

    /// <summary>Entrada de mercancía (<c>PurchaseDeliveryNotes</c>).</summary>
    [B1Entity("PurchaseDeliveryNotes")]
    public class PurchaseDeliveryNote : MarketingDocument { }

    /// <summary>Devolución de mercancía (<c>PurchaseReturns</c>).</summary>
    [B1Entity("PurchaseReturns")]
    public class PurchaseReturn : MarketingDocument { }

    /// <summary>Abono de proveedor (<c>PurchaseCreditNotes</c>).</summary>
    [B1Entity("PurchaseCreditNotes")]
    public class PurchaseCreditNote : MarketingDocument { }

    /// <summary>Factura de anticipo de compra (<c>PurchaseDownPayments</c>).</summary>
    [B1Entity("PurchaseDownPayments")]
    public class PurchaseDownPayment : MarketingDocument { }
}
