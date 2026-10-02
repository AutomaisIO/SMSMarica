using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Regulacao;

/// <summary>
/// Médico solicitante guardado SÓ por nós, para sistemas em que o médico é texto digitado na
/// solicitação e não um cadastro do próprio sistema — o SISREG (`cpfprofsol`/`nomeprofsol`).
/// Pedido do Bernardo, 02/10/2026: aproveitar quem já passou pelo SISREG em vez de redigitar.
///
/// <para><b>Um registro por médico, sem duplicação.</b> As fichas do SISREG trazem o mesmo médico
/// de vários jeitos ("OTAVIO FRANCISCO SANTOS", "OTAVIO F SANTOS", "OTAVIO FRANCICO SANTOS"): o
/// nome principal é a forma mais completa e as outras ficam em <see cref="GrafiasJson"/>, que a
/// busca também procura. O CPF, quando há, é a identidade; sem ele, o nome normalizado.</para>
///
/// <para>Nada daqui é escrito no sistema: no SISREG não existe cadastro de médico para criar.</para>
/// </summary>
public sealed class RegulacaoMedicoLocal
{
    public Guid Id { get; set; }

    public SistemaRegulacao Sistema { get; set; }

    /// <summary>A forma mais completa do nome, em MAIÚSCULAS.</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Sem acento, sem pontuação, sem partículas — a chave contra duplicação.</summary>
    public string NomeNormalizado { get; set; } = string.Empty;

    /// <summary>Só os 11 dígitos. Nulo quando ninguém informou ainda.</summary>
    public string? Cpf { get; set; }

    /// <summary>"CRM", "COREN"… como veio.</summary>
    public string? Conselho { get; set; }

    public string? NumeroConselho { get; set; }

    public string? UfConselho { get; set; }

    /// <summary>As outras formas do nome vistas (lista JSON) — abreviações, erros de digitação.</summary>
    public string? GrafiasJson { get; set; }

    public OrigemMedicoLocal Origem { get; set; }

    /// <summary>Quantos pedidos com este médico as fichas trouxeram — ordena a busca.</summary>
    public int Ocorrencias { get; set; }

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
}
