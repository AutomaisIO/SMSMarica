using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Regulacao;

/// <summary>
/// Uma versão congelada do formulário de um procedimento (ADR-0052).
///
/// <para><b>Por que versionar.</b> O formulário do Externo é a <b>união</b> do que o SER e o
/// SERNIT pedem, e essa união muda quando a SES recompila o catálogo. Sem versão, reabrir uma
/// solicitação de três meses atrás mostraria campos que não existiam quando ela foi preenchida —
/// e sumiria com respostas que o solicitante deu.</para>
///
/// <para>O <see cref="Hash"/> é a identidade: dois procedimentos que geram exatamente a mesma
/// união compartilham a linha, em vez de duplicá-la por recurso.</para>
/// </summary>
public sealed class RegulacaoFormularioVersao
{
    public Guid Id { get; set; }

    /// <summary>`sisreg.inclusao` ou `externo.uniao`.</summary>
    public string Esquema { get; set; } = string.Empty;

    public Guid ProcedimentoId { get; set; }
    public RegulacaoProcedimento? Procedimento { get; set; }

    /// <summary>
    /// `CampoFormulario[]` em JSON: <c>{ chave, rotulo, tipo, obrigatorio, opcoes?, origens, ordem }</c>.
    ///
    /// <para>Régua da união, medida no spike c: campo que existe de um lado só entra mesmo assim;
    /// obrigatoriedade é <b>OU</b> (obrigatório num dos sistemas ⇒ obrigatório aqui); conflito de
    /// tipo ou de opções não tem regra automática — vira divergência para a curadoria, porque nos
    /// 23 pares medidos não houve nenhum, e inventar regra sem caso seria adivinhação.</para>
    /// </summary>
    public string DefinicaoJson { get; set; } = "[]";

    /// <summary>SHA-256 da definição. Único: mesma união, mesma linha.</summary>
    public string Hash { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }

    public ICollection<RegulacaoFormularioCampoMapa> Mapa { get; set; } = [];
}

/// <summary>
/// De-para entre a chave canônica de um campo e o nome que ele tem em cada sistema.
///
/// <para>Existe porque o nome nativo é ilegível e instável (<c>form0:dinamico_id_3</c>), e porque
/// o mesmo campo canônico vira coisas diferentes em cada lado. Sem esta tabela, o envio teria de
/// carregar o de-para em código e mudaria a cada recompilação de catálogo.</para>
/// </summary>
public sealed class RegulacaoFormularioCampoMapa
{
    public Guid Id { get; set; }

    public Guid FormularioVersaoId { get; set; }
    public RegulacaoFormularioVersao? FormularioVersao { get; set; }

    public string ChaveCanonica { get; set; } = string.Empty;

    public SistemaRegulacao Sistema { get; set; }

    /// <summary>Nome do campo no sistema de destino: `form0:dinamico_id_3`, `cid10`…</summary>
    public string NomeNativo { get; set; } = string.Empty;

    /// <summary>
    /// Conversão a aplicar no envio: <c>null</c>, <c>data_ddMMyyyy</c>,
    /// <c>multiplo_quebra_linha</c> ou <c>opcao:{json de-para}</c> quando os dois sistemas usam
    /// códigos diferentes para a mesma opção.
    /// </summary>
    public string? Transformacao { get; set; }
}
