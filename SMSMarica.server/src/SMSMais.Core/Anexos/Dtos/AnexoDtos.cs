using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Anexos.Dtos;

/// <summary>Documento de exame anexado (metadados + URL para baixar o PDF).</summary>
public sealed record AnexoExameDto(
    Guid Id,
    string Nome,
    string? Descricao,
    string MimeType,
    long TamanhoBytes,
    StatusDocumentoExame Status,
    string? Origem,
    int? Paginas,
    DateTime CriadoEm,
    string UrlConteudo);

/// <summary>Paciente (resumo) embutido na resposta de criação do token.</summary>
public sealed record AnexoTokenPacienteDto(Guid Id, string Nome);

/// <summary>Resposta da criação do token de upload (alimenta o QR code no front do médico).</summary>
public sealed record CriarTokenRespostaDto(
    string Token,
    string Url,
    DateTime ExpiraEm,
    Guid SolicitacaoExameId,
    AnexoTokenPacienteDto Paciente);

/// <summary>Paciente exibido no PWA ao validar a sessão.</summary>
public sealed record SessaoPacienteDto(string Nome);

/// <summary>Solicitação exibida no PWA ao validar a sessão.</summary>
public sealed record SessaoSolicitacaoDto(Guid Id, string? Resumo);

/// <summary>Resposta da validação do token (lado PWA, anônimo).</summary>
public sealed record ValidarTokenRespostaDto(
    bool Valido,
    DateTime ExpiraEm,
    SessaoPacienteDto Paciente,
    SessaoSolicitacaoDto Solicitacao);

/// <summary>Payload de confirmação/edição (Pendente → Salvo) de um documento.</summary>
public sealed record SalvarAnexoDto(string? Nome, string? Descricao);

/// <summary>Resposta do upload (lado PWA).</summary>
public sealed record AnexoUploadRespostaDto(Guid Id, string Nome);

/// <summary>Conteúdo binário de um documento para streaming (uso interno do controller).</summary>
public sealed record AnexoConteudo(byte[] Conteudo, string MimeType, string NomeArquivo);
