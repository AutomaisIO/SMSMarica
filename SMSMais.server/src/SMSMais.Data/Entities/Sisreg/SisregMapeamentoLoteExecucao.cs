using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Sisreg;

/// <summary>
/// Uma execução do "sincroniza tudo" do mapeamento — a rede inteira, não uma unidade.
///
/// <para><b>Por que persistir:</b> o progresso do lote sempre viveu só em memória
/// (<c>MapeamentoLoteEstadoVivo</c>), e memória some quando a execução termina. Quem clicava no
/// botão e saía da tela nunca descobria o que tinha entrado: quantas unidades o SISREG tem,
/// quantas foram criadas aqui, quantos médicos e procedimentos vieram de cada uma. Este rastreio
/// responde isso depois do fato, no mesmo espírito de <see cref="SisregVarreduraExecucao"/>.</para>
///
/// <para>Não tem FK para unidade porque não é de uma unidade — o detalhe por unidade está em
/// <see cref="SisregMapeamentoLoteExecucaoItem"/>.</para>
/// </summary>
public class SisregMapeamentoLoteExecucao
{
    public Guid Id { get; set; }

    public DisparoSincronizacao Disparo { get; set; } = DisparoSincronizacao.Manual;

    /// <summary>Reaproveita o status da varredura: os estados são os mesmos, inclusive o
    /// <c>Parcial</c> — que aqui significa "parou no orçamento ou no CAPTCHA, faltou unidade".</summary>
    public StatusVarredura Status { get; set; } = StatusVarredura.Pendente;

    // ---- descoberta (o passo que o lote não tinha) ----

    /// <summary>Unidades que a credencial enxerga no SISREG (select <c>ups</c> do
    /// <c>cons_agendas</c>). Zero quando a descoberta falhou — aí o lote roda só com o que já
    /// existia aqui.</summary>
    public int UnidadesNoSisreg { get; set; }

    /// <summary>Unidades criadas no nosso cadastro nesta execução (existiam no SISREG e não aqui).</summary>
    public int UnidadesCriadas { get; set; }

    /// <summary>Unidades que já existiam e ganharam o CNES por casamento de nome (backfill).</summary>
    public int UnidadesComCnesPreenchido { get; set; }

    // ---- mapeamento ----

    /// <summary>Unidades elegíveis nesta execução — o denominador do progresso.</summary>
    public int UnidadesTotal { get; set; }

    /// <summary>Unidades efetivamente mapeadas (foram ao SISREG).</summary>
    public int UnidadesMapeadas { get; set; }

    /// <summary>Unidades puladas por estarem dentro do TTL (mapeamento ainda recente). Não é falha:
    /// é a economia de requisição que permite apontar o botão para a rede inteira.</summary>
    public int UnidadesPuladas { get; set; }

    public int UnidadesComErro { get; set; }

    public int ProfissionaisEncontrados { get; set; }
    public int ProfissionaisNovos { get; set; }
    public int ProcedimentosEncontrados { get; set; }
    public int ProcedimentosNovos { get; set; }

    public int PractitionersCriados { get; set; }
    public int PractitionersVinculados { get; set; }

    /// <summary>Requisições HTTP gastas no SISREG — o custo real da execução, para comparar com o
    /// orçamento anti-robô.</summary>
    public int Requisicoes { get; set; }

    public string? MensagemErro { get; set; }

    public DateTime IniciadoEm { get; set; }
    public DateTime? FinalizadoEm { get; set; }
    public int? DuracaoSegundos { get; set; }

    /// <summary>Quem disparou. NULL no agendado — a autoria ali é o <see cref="Disparo"/>.</summary>
    public Guid? CriadoPor { get; set; }
}
