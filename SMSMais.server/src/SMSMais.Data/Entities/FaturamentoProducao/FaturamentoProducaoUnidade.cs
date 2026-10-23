namespace SMSMais.Data.Entities.FaturamentoProducao;

/// <summary>
/// Configuração do módulo Faturamento de Produção: marca uma unidade como HABILITADA para o usuário
/// final solicitar/gerar o BPA. Uma linha por unidade configurada.
///
/// <para>Isto é configuração de NEGÓCIO (quais unidades aparecem), ortogonal ao RBAC
/// (<c>ModuloPermissao.FaturamentoProducao</c>, que diz QUEM entra). Habilitar/desabilitar exige a
/// ação Edição; gerar o BPA exige Consulta — e só das unidades habilitadas ∩ escopo.</para>
/// </summary>
public class FaturamentoProducaoUnidade
{
    /// <summary>Unidade (PK e FK para <c>smsmarica.unidade</c>).</summary>
    public Guid UnidadeId { get; set; }
    public Unidade? Unidade { get; set; }

    /// <summary>A unidade está habilitada para gerar o BPA de produção?</summary>
    public bool Habilitada { get; set; }

    public DateTime AtualizadoEm { get; set; }

    /// <summary>Usuário que mudou a flag por último (auditoria).</summary>
    public Guid? AtualizadoPor { get; set; }
}
