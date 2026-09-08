namespace PharmacyIS.Domain.Reports;

/// <summary>Ред от справка „Дневен оборот“.</summary>
public class DailyTurnoverRow
{
    public DateTime Date { get; set; }
    public int SalesCount { get; set; }
    public int ItemsSold { get; set; }
    public decimal Turnover { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit => Math.Round(Turnover - Cost, 2, MidpointRounding.AwayFromZero);
}

/// <summary>Ред от справка „Най-продавани продукти“.</summary>
public class TopProductRow
{
    public string MedicineCode { get; set; } = string.Empty;
    public string MedicineName { get; set; } = string.Empty;
    public string IngredientName { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal Turnover { get; set; }
    public decimal Profit { get; set; }
}

/// <summary>Ред от справка „Продажби по служители“.</summary>
public class SalesByUserRow
{
    public string UserName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public int SalesCount { get; set; }
    public decimal Turnover { get; set; }
    public decimal AverageReceipt => SalesCount == 0
        ? 0m
        : Math.Round(Turnover / SalesCount, 2, MidpointRounding.AwayFromZero);
}

/// <summary>Ред от справка „Доставени количества по доставчици“.</summary>
public class DeliveriesBySupplierRow
{
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string MedicineCode { get; set; } = string.Empty;
    public string MedicineName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>Ред от справка „Складова наличност“.</summary>
public class StockRow
{
    public string MedicineCode { get; set; } = string.Empty;
    public string MedicineName { get; set; } = string.Empty;
    public string FormName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int MinStock { get; set; }
    public decimal RetailPrice { get; set; }
    public decimal StockValue { get; set; }
    public DateTime? NearestExpiry { get; set; }
}

/// <summary>Ред от справка „Изтичащи срокове на годност“.</summary>
public class ExpiryRow
{
    public string MedicineCode { get; set; } = string.Empty;
    public string MedicineName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public int Quantity { get; set; }
    public int DaysLeft { get; set; }
    public decimal Value { get; set; }
}

/// <summary>Ред от справка „Бракувани количества“.</summary>
public class WriteOffRow
{
    public DateTime Date { get; set; }
    public string DocNumber { get; set; } = string.Empty;
    public string MedicineCode { get; set; } = string.Empty;
    public string MedicineName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Value { get; set; }
    public string Reason { get; set; } = string.Empty;
}
