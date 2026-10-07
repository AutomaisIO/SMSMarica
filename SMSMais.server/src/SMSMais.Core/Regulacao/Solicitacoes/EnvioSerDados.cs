using SMSMais.Data.Entities.Ser;

namespace SMSMais.Core.Regulacao.Solicitacoes;

/// <summary>O que o envio automático ao SER precisa da solicitação (lido, nunca rastreado).</summary>
public sealed record DadosEnvioSer(
    Guid Id,
    long NumeroLocal,
    string PacienteNome,
    string? PacienteCpf,
    string? PacienteCns,
    Guid FormularioVersaoId,
    string FormularioJson,
    Guid SerRecursoId,
    TipoRecursoSer Tipo,
    bool AmbulatorioEstadual,
    string RecursoRotulo,
    IReadOnlyList<ArquivoParaEnvio> Arquivos);

/// <summary>Um arquivo atual de uma exigência da solicitação.</summary>
public sealed record ArquivoParaEnvio(
    Guid Id, string Nome, string? Titulo, string ContentType, long Tamanho, string ChaveArmazenamento);

/// <summary>O que o SER devolveu num envio que deu certo.</summary>
public sealed record ConclusaoEnvioSer(
    string NumeroExterno,
    string? OperadorLogin,
    bool Conferido,
    string? MensagemDoSer,
    IReadOnlyList<Guid> ArquivosEnviados);
