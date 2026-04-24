namespace SMSMarica.Data.Entities.Enums;

public enum TipoPeriodicidade
{
    Diaria = 1,
    IntervaloDias = 2,
    SemanaDiasFixos = 3,
    /// <summary>Datas lançadas manualmente, sem regra de expansão.</summary>
    Manual = 4,
}
