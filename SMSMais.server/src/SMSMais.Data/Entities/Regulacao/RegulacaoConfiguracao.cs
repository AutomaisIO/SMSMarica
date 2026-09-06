using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Regulacao;

/// <summary>
/// Configuração singleton do módulo Regulação → Solicitações (ADR-0052). Linha única com Id fixo,
/// criada pelo service no primeiro acesso — <b>nunca por seed em migration</b>: migration é
/// imutável e roda igual em toda instância nova (CLAUDE.md, regra 9).
///
/// <para>O que mora aqui é o que muda sem deploy: o que a ponta pode escolher, os cortes da busca
/// semântica, os limites de anexo e o rótulo da fila. O que é regra de negócio dura fica em
/// código.</para>
/// </summary>
public sealed class RegulacaoConfiguracao
{
    /// <summary>Id fixo da linha singleton (`c052` = ADR-0052, mesmo idioma de TicketConfiguracao).</summary>
    public static readonly Guid IdSingleton = new("00000000-0000-0000-0000-00000000c052");

    public Guid Id { get; set; } = IdSingleton;

    // ---- fluxo ----

    /// <summary>
    /// Deixa a ponta mandar para fora mesmo havendo oferta interna. Nasce <c>false</c>: com
    /// oferta em Maricá, o normal é resolver dentro do município.
    /// </summary>
    public bool PermitirExternoComInterno { get; set; }

    /// <summary>Reservado — a escolha de unidade executante ainda não é da ponta.</summary>
    public bool PontaPodeEscolherUnidade { get; set; }

    /// <summary>Se o solicitante enxerga a fila de todas as unidades ou só a das suas.</summary>
    public bool PontaPodeVerTodasUnidades { get; set; }

    /// <summary>
    /// CPF obrigatório para a solicitação sair do rascunho. O SERNIT não grava sem CPF, e o
    /// ADR-0041 admite paciente sem CPF só no cadastro — a exigência é na transição para a fila.
    /// </summary>
    public bool ExigirCpf { get; set; } = true;

    /// <summary>Nome da fila na tela. "Pré-regulação" é provisório (D-6).</summary>
    public string RotuloFila { get; set; } = "Pré-regulação";

    // ---- SISREG ----

    /// <summary>
    /// Janela em que o agente ainda edita no SISREG uma solicitação já incluída. O valor real só
    /// se confirma no spike b; 7 dias é a estimativa da transcrição.
    /// </summary>
    public int SisregPrazoEdicaoDias { get; set; } = 7;

    // ---- busca ----

    /// <summary>Distância de cosseno acima da qual o resultado semântico é descartado.</summary>
    public decimal BuscaCorteDistancia { get; set; } = 0.45m;

    /// <summary>Semelhança mínima para propor que duas origens são o mesmo procedimento.</summary>
    public decimal BuscaScoreSugestaoPareamento { get; set; } = 0.85m;

    // ---- anexos (armazenamento em Spaces) ----

    public int AnexoLimiteMb { get; set; } = 15;

    /// <summary>
    /// Tipos aceitos. Entram imagens além de PDF porque o caminho real é foto de celular tirada
    /// no balcão, não documento digitalizado.
    /// </summary>
    public string[] AnexoTiposPermitidos { get; set; } =
        ["image/jpeg", "image/png", "image/webp", "application/pdf"];

    // ---- follow-up (consumido no incremento 6) ----

    /// <summary>
    /// Regras de classificação de follow-up, versionadas fora do código (plano 09). Semente
    /// medida no spike d: <c>SMSMais.Regulacao/revisoes/spike-d-regras-followup.json</c>.
    /// Vazio = classificador desligado, tudo cai em "Outro".
    /// </summary>
    public string RegrasFollowupJson { get; set; } = "[]";

    /// <summary>
    /// Destino padrão do "não sei" nas regras de elegibilidade. A coluna nasce aqui, e não numa
    /// migration do incremento 4, para o motor de regras não exigir uma segunda migration.
    /// </summary>
    public NaoSeiViraRegulacao NaoSeiPadrao { get; set; } = NaoSeiViraRegulacao.Ressalva;

    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }

    public uint RowVersion { get; set; }
}
