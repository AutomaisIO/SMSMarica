namespace SMSMarica.Core.Cidadao.Dtos;

public sealed record SolicitarOtpRequest(string Cpf);

/// <summary>
/// Resultado de solicitar o código. Em <b>modo de teste</b> (WhatsApp ainda não ativo),
/// <see cref="CodigoTeste"/> traz o código para ser exibido na tela; em produção será null
/// (o código vai só pelo WhatsApp).
/// </summary>
public sealed record OtpEmitidoDto(bool Enviado, string Canal, string? CodigoTeste, int ValidadeSegundos);

public sealed record ValidarOtpRequest(string Cpf, string Codigo);

public sealed record PacienteSessaoDto(Guid Id, string Nome, string Cpf);

public sealed record RespostaLoginPacienteDto(string Token, PacienteSessaoDto Paciente);

/// <summary>Perfil que o cidadão vê/edita de si mesmo no app (subconjunto seguro do paciente FHIR).</summary>
public sealed record PerfilCidadaoDto(
    Guid Id,
    string Nome,
    string? NomeSocial,
    string Cpf,
    string? Cns,
    DateOnly? DataNascimento,
    string? Email,
    string? TelefonePrincipal,
    string? TelefoneCelular,
    string? TelefoneResidencial,
    string? FotoBase64);

public sealed record AtualizarContatoCidadaoRequest(
    string? Email,
    string? TelefonePrincipal,
    string? TelefoneCelular,
    string? TelefoneResidencial);

public sealed record AtualizarFotoCidadaoRequest(string? FotoBase64);

// --- Resumos clínicos do app (shapes estáveis p/ a PWA). Translados/exames/laudos
// ainda são stub; atendimentos já vêm do hub FHIR (Encounter + Condition + documentos). ---
public sealed record TransladoResumoDto(Guid Id, string Data, string Destino, string Status);
public sealed record AtendimentoResumoDto(
    Guid Id,
    DateTime Data,
    string Estabelecimento,
    string Profissional,
    string Descricao,
    IReadOnlyList<DocumentoResumoDto> Documentos);
/// <summary>Documento clínico do atendimento (DocumentReference), com HTML já decodificado.</summary>
public sealed record DocumentoResumoDto(Guid Id, string Tipo, DateTime? Data, string ConteudoHtml);
public sealed record ExameResumoDto(Guid Id, DateTime Data, string Nome, string Status);
public sealed record LaudoResumoDto(Guid Id, DateTime Data, string Titulo, string Status);
