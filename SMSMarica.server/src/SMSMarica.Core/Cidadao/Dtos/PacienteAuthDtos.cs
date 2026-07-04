namespace SMSMarica.Core.Cidadao.Dtos;

public sealed record SolicitarOtpRequest(string Cpf);

/// <summary>
/// Resultado de solicitar o código. No fluxo normal o código vai só pelo WhatsApp e
/// <see cref="CodigoTeste"/> é null. Ele só é preenchido como <b>fallback</b> quando o
/// WhatsApp não está configurado no servidor (ou <c>Tfd:Otp:ModoTeste=true</c>), para não
/// travar o login — aí o código é exibido na tela. <see cref="TelefoneMascarado"/> traz uma
/// dica do destino (ex.: <c>***-1234</c>) para o usuário conferir.
/// </summary>
public sealed record OtpEmitidoDto(
    bool Enviado, string Canal, string? CodigoTeste, int ValidadeSegundos, string? TelefoneMascarado = null);

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
/// <summary>Documento escaneado (DocumentoExame "Salvo") anexado a um exame, visível ao cidadão.</summary>
public sealed record AnexoResumoDto(Guid Id, string Nome, long TamanhoBytes, int? Paginas);

/// <summary>
/// Um exame realizado do paciente (SolicitacaoExame). Traz os documentos escaneados, a
/// disponibilidade de imagens no PACS (para gerar o PDF consolidado) e o laudo assinado, se houver.
/// </summary>
public sealed record ExameResumoDto(
    Guid Id,
    DateTime Data,
    string Nome,
    string Status,
    string? StudyInstanceUID,
    bool TemImagens,
    IReadOnlyList<AnexoResumoDto> Documentos,
    Guid? LaudoId,
    bool LaudoAssinado);

public sealed record LaudoResumoDto(Guid Id, DateTime Data, string Titulo, string Status);

/// <summary>Consulta ou exame agendado (futuro) do paciente, projetado para o app.
/// Exames importados do SISREG entram como SolicitacaoExame: <c>SolicitacaoExameId</c>
/// preenchido + <c>StatusConfirmacao</c> ("Pendente"|"Confirmada"|"Cancelada") habilitam os
/// botões Confirmar/Não poderei ir no card (<c>PodeResponder</c>).</summary>
public sealed record AgendamentoResumoDto(
    Guid Id,
    DateTime InicioEm,
    DateTime FimEm,
    string Tipo,
    string Titulo,
    string? Profissional,
    string? Unidade,
    string Status,
    Guid? SolicitacaoExameId = null,
    string? StatusConfirmacao = null,
    bool PodeResponder = false);
