namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// De onde a Solicitação NASCEU — proveniência explícita, para rastrear divergências
/// (ex.: a extensão criou algo que o TXT depois não confirma). Antes disso a origem era só
/// inferida por <c>RawSisreg</c> vazio-ou-não. Nullable na entidade: o acervo anterior a este
/// campo fica <c>null</c> (legado), sem backfill.
/// </summary>
public enum FonteSolicitacao
{
    /// <summary>Cadastrada à mão (recepção), sem passar por importação.</summary>
    Manual = 1,

    /// <summary>Importação do SISREG (arquivo TXT / varredura da agenda).</summary>
    ImportacaoSisreg = 2,

    /// <summary>Observada pela extensão de navegador (marcação feita no SISREG, em tempo real).</summary>
    ExtensaoNavegador = 3,
}
