using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Regulacao.Solicitacoes;

/// <summary>
/// O que o envio automático precisa da solicitação (lido, nunca rastreado) — para o SER-RJ ou
/// para o SERNIT, que são a mesma aplicação em instâncias diferentes (ADR-0069).
/// </summary>
/// <param name="RecursoId">A linha do espelho do catálogo do sistema (<c>ser_catalogo_recurso</c>
/// ou <c>sernit_catalogo_recurso</c>) — a nossa numeração; o envio acha o recurso pelo NOME.</param>
/// <param name="PacienteId">O nosso cadastro — o SERNIT não consulta o CADSUS, e paciente que ele
/// não conhece é cadastrado na própria tela com os nossos dados.</param>
/// <param name="AmbulatorioEstadual">O ramo do SER-RJ; no SERNIT é sempre <c>false</c> (não há o combo).</param>
/// <param name="MedicoPendenteId">Médico pedido pela unidade e ainda não cadastrado no sistema — só vem
/// preenchido na prévia (e no cadastro pelo modal); o envio barra antes.</param>
public sealed record DadosEnvioSer(
    Guid Id,
    long NumeroLocal,
    Guid PacienteId,
    string PacienteNome,
    string? PacienteCpf,
    string? PacienteCns,
    Guid FormularioVersaoId,
    string FormularioJson,
    SistemaRegulacao Sistema,
    Guid RecursoId,
    bool EhExame,
    bool AmbulatorioEstadual,
    string RecursoRotulo,
    IReadOnlyList<ArquivoParaEnvio> Arquivos,
    Guid? MedicoPendenteId = null);

/// <summary>Um arquivo atual de uma exigência da solicitação.</summary>
public sealed record ArquivoParaEnvio(
    Guid Id, string Nome, string? Titulo, string ContentType, long Tamanho, string ChaveArmazenamento);

/// <summary>O que o sistema devolveu num envio que deu certo.</summary>
public sealed record ConclusaoEnvioSer(
    string NumeroExterno,
    string? OperadorLogin,
    bool Conferido,
    string? MensagemDoSer,
    IReadOnlyList<Guid> ArquivosEnviados);
