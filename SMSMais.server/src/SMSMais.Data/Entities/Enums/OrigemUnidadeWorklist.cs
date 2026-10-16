namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// De qual unidade do pedido sai o nome que o equipamento recebe no campo "unidade/clínica" da
/// worklist (ADR-0074, adendo de 16/10/2026). Persistido como int — valores estáveis.
/// </summary>
public enum OrigemUnidadeWorklist
{
    /// <summary>A unidade onde o exame é feito — a "clínica" do CardioNet.</summary>
    Executante = 0,

    /// <summary>A unidade que pediu o exame.</summary>
    Solicitante = 1,
}
