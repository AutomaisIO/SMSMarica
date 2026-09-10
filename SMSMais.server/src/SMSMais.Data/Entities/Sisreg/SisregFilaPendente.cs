namespace SMSMais.Data.Entities.Sisreg;

/// <summary>
/// Uma pessoa <b>esperando</b> no SISREG: pediu e ainda não foi agendada.
///
/// <para><b>Por que esta tabela existe.</b> O <c>expo_solicitacoes</c>, que alimenta
/// <see cref="Solicitacao"/>, só exporta MARCAÇÕES — quem já tem data. A espera que o sistema
/// publicava era a de quem já foi atendido; quem estava na fila há vinte meses não existia em
/// lugar nenhum da base. Esta tabela é o outro lado: o <c>gerenciador_solicitacao</c> na situação
/// 1 (Solicitação/Pendente/Regulação), que em Maricá é onde a fila mora de verdade — a situação 2,
/// "Fila de Espera", volta vazia em toda janela testada.</para>
///
/// <para><b>A fila é um ESTADO, não um histórico</b> (provado em 05/09/2026 pelo laboratório, com
/// interseção ZERO entre 15.502 pendentes e 6.765 agendados do mesmo período). Ao ser agendada, a
/// solicitação sai da situação 1 e some da leitura seguinte. Daí <see cref="UltimoVistoEm"/> e
/// <see cref="SaiuEm"/>: a linha nunca é apagada — some da fila, fica na história.</para>
///
/// <para><b>Consequência que engana quem lê rápido:</b> consultar uma janela antiga não devolve
/// "quem esperava naquela época", devolve <b>quem pediu naquela época e ainda espera hoje</b>.</para>
///
/// <para><b>Sem CPF.</b> A listagem traz CNS, nome, nome da mãe e nascimento — não CPF. O vínculo
/// com o paciente do hub é pelo CNS (ADR-0041 e a régua de dedup por CPF/CNS + origem).</para>
/// </summary>
public class SisregFilaPendente
{
    public Guid Id { get; set; }

    /// <summary>
    /// Número da solicitação no SISREG — a <b>mesma chave</b> de
    /// <see cref="Solicitacao.CodigoSolicitacao"/>. É o que dá idempotência de graça e o que
    /// permite descobrir, sem perguntar ao SISREG, se quem saiu da fila foi agendado.
    /// </summary>
    public string CodigoSolicitacao { get; set; } = string.Empty;

    /// <summary>Quando a pessoa pediu. É a partir daqui que se conta a espera de verdade.</summary>
    public DateOnly? DataSolicitacao { get; set; }

    /// <summary>
    /// Classificação de risco do SISREG: 0 = vermelho (mais urgente) … 3 = azul. Vem como imagem
    /// mais um <c>title</c> numérico na listagem; guardamos o número porque cor é apresentação.
    /// </summary>
    public int? Risco { get; set; }

    public string? PacienteNome { get; set; }

    /// <summary>Cartão SUS, do <c>title</c> da célula do paciente. Presente em 100% das linhas
    /// conferidas (15.502 de 15.502) — é o elo com <c>fhir.patient</c>.</summary>
    public string? Cns { get; set; }

    public string? NomeMae { get; set; }
    public DateOnly? DataNascimento { get; set; }

    /// <summary>Idade como o SISREG exibe, em anos. Redundante com o nascimento de propósito: é o
    /// que a tela ordena, e nem toda linha traz as duas coisas coerentes.</summary>
    public int? IdadeAnos { get; set; }

    /// <summary>Um ou mais números, como vieram. Descartar um é perder contato.</summary>
    public string? Telefone { get; set; }

    public string? Municipio { get; set; }

    /// <summary>
    /// Nome do procedimento. <b>O SISREG não manda o código nesta tela</b> — as 15.502 linhas
    /// conferidas trazem só o nome, com hífen fazendo parte dele. Por isso o casamento entre fila
    /// e oferta é pelo nome, que já é a régua da casa para o SISREG.
    /// </summary>
    public string? ProcedimentoNome { get; set; }

    /// <summary>Preenchido só se um dia o SISREG passar a mandar o código.</summary>
    public string? ProcedimentoCodigo { get; set; }

    public string? CidCodigo { get; set; }
    public string? UnidadeSolicitante { get; set; }

    /// <summary>Rótulo cru da situação ("SOL/PEN/REG"). Guardado para conferência: se um dia
    /// aparecer outro valor na situação 1, o dado denuncia sozinho.</summary>
    public string? Situacao { get; set; }

    /// <summary>Primeira leitura em que esta pessoa apareceu na fila.</summary>
    public DateTime PrimeiroVistoEm { get; set; }

    /// <summary>Última leitura em que ainda estava lá. É o que decide quem saiu.</summary>
    public DateTime UltimoVistoEm { get; set; }

    /// <summary>
    /// Quando percebemos que saiu da fila. Nulo = ainda esperando.
    ///
    /// <para>Não é apagada: uma linha que some sem deixar rastro impede responder "quanto tempo
    /// essa pessoa esperou até ser atendida", que é o indicador que a regulação precisa.</para>
    /// </summary>
    public DateTime? SaiuEm { get; set; }

    /// <summary>
    /// Para onde foi, quando saiu — descoberto <b>sem gastar requisição</b>: se o
    /// <see cref="CodigoSolicitacao"/> aparecer em <c>smsmarica.solicitacao</c> com data, foi
    /// agendada; se não aparecer, foi cancelada ou negada.
    /// </summary>
    public SaidaDaFilaSisreg? SaiuPara { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
}

/// <summary>Desfecho de quem saiu da fila do SISREG.</summary>
public enum SaidaDaFilaSisreg
{
    /// <summary>Sumiu da fila e apareceu agendada no nosso banco — o desfecho bom.</summary>
    Agendada = 1,

    /// <summary>
    /// Sumiu da fila e não apareceu agendada. Cancelamento, negativa ou devolução — o SISREG não
    /// diz qual, e chutar seria pior que registrar a ignorância.
    /// </summary>
    SaiuSemAgendar = 2,
}
